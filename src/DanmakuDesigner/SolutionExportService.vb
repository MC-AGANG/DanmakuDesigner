Imports System.IO
Imports System.IO.Compression
Imports System.Linq
Imports System.Text
Imports System.Text.RegularExpressions
Imports DanmakuDesigner.Core

''' <summary>把内置 AgSTG 引擎模板解压出来，替换关卡和资源，生成完整可编译的解决方案。</summary>
Public Module SolutionExportService

    Private Const UiPackPrefix As String = "pack://application:,,,/GameResources;component/UI/"
    Private Const ResourcePackPrefix As String = "pack://application:,,,/GameResources;component/Resources/"

    Private Class ShellVisuals
        Public Property StartupUri As String = UiPackPrefix & "loadpage.png"
        Public Property TitleUri As String = UiPackPrefix & "title00.png"
        Public Property GameUri As String = UiPackPrefix & "game_background.png"
        Public Property TitleBgmUri As String = UiPackPrefix & "title_bgm.mp3"
    End Class

    Public Function ExportSolution(game As GameProject, folder As String, gameCodeName As String,
                                   templateZip As String, sourceDirectory As String) As ExportResult
        If game Is Nothing Then Throw New ArgumentNullException(NameOf(game))
        If String.IsNullOrWhiteSpace(folder) Then Throw New ArgumentException("导出目录不能为空。", NameOf(folder))
        If Not File.Exists(templateZip) Then Throw New FileNotFoundException("找不到内置引擎模板。", templateZip)

        Dim result As New ExportResult With {.Folder = folder}
        Dim code = SafeIdentifier(gameCodeName, "GameExport")
        Directory.CreateDirectory(folder)
        ZipFile.ExtractToDirectory(templateZip, folder, True)

        ' 保留原 TH07 的按钮贴图和默认主题图，复制到生成资源包的 UI 目录。
        Dim uiStage = Path.Combine(folder, ".dd-ui")
        CopyUiAssets(Path.Combine(folder, "ResourcePack.TH07", "Resources"), uiStage)

        ' 模板中的旧关卡与 TH07 资源包会被生成内容替代。
        For i = 1 To 32
            DeleteDirectory(Path.Combine(folder, "Stage" & i.ToString()))
        Next
        DeleteDirectory(Path.Combine(folder, "ResourcePack.TH07"))
        DeleteDirectory(Path.Combine(folder, "Stages"))
        DeleteDirectory(Path.Combine(folder, "GameResources"))
        DeleteFile(Path.Combine(folder, "AgSTG.sln"))

        Dim clone = ProjectJson.Clone(game)
        clone.Normalize()
        For i = 0 To clone.Stages.Count - 1
            clone.Stages(i).Stage.CodeName = "Stage" & (i + 1).ToString()
        Next

        ' 复用现有导出流程生成关卡工程、资源包和图片资源。
        Dim generatedResult = ExportService.ExportGame(clone, folder, code, folder, sourceDirectory)
        result.Warnings.AddRange(generatedResult.Warnings)

        ' 将原 TH07 UI 贴图放进生成的 GameResources，保证按钮贴图不丢失。
        InstallUiAssets(uiStage, Path.Combine(folder, "GameResources"))
        DeleteDirectory(uiStage)
        PatchGameResourcesProject(Path.Combine(folder, "GameResources", "GameResources.vbproj"))

        ' 旧导出流程还会写自己的解决方案和说明，稍后由完整解决方案替代。
        DeleteFile(Path.Combine(folder, code & ".slnx"))
        DeleteFile(Path.Combine(folder, "README-游戏集成说明.txt"))
        DeleteFile(Path.Combine(folder, code & ".dmkproj"))

        Dim oldShell = Path.Combine(folder, "TH07")
        Dim shellFolder = Path.Combine(folder, code)
        If Not String.Equals(oldShell, shellFolder, StringComparison.OrdinalIgnoreCase) Then
            If Directory.Exists(shellFolder) Then Directory.Delete(shellFolder, True)
        End If
        If Not Directory.Exists(oldShell) Then Throw New InvalidOperationException("引擎模板中缺少 TH07 游戏外壳工程。")
        If Not String.Equals(oldShell, shellFolder, StringComparison.OrdinalIgnoreCase) Then
            Directory.Move(oldShell, shellFolder)
        End If
        Dim oldProject = Path.Combine(shellFolder, "TH07.vbproj")
        Dim shellProject = Path.Combine(shellFolder, code & ".vbproj")
        If File.Exists(oldProject) AndAlso Not String.Equals(oldProject, shellProject, StringComparison.OrdinalIgnoreCase) Then
            File.Move(oldProject, shellProject)
        End If
        If Not File.Exists(shellProject) Then Throw New InvalidOperationException("引擎模板中缺少 TH07.vbproj。")

        For i = 0 To clone.Stages.Count - 1
            PatchStageProject(Path.Combine(folder, "Stages", "Stage" & (i + 1).ToString(),
                                           "Stage" & (i + 1).ToString() & ".vbproj"))
        Next

        PatchShellProject(shellProject, code, clone.Stages.Count)
        Dim visuals = CreateVisuals(clone)
        PatchShellSources(shellFolder, code, clone.Stages.Count, clone.Name, visuals)

        Dim solutionPath = Path.Combine(folder, code & ".slnx")
        File.WriteAllText(solutionPath, BuildSolution(code, clone.Stages.Count), New UTF8Encoding(False))
        result.Files.Add(solutionPath)

        Dim readme = BuildReadme(clone, code)
        Dim readmePath = Path.Combine(folder, "README-解决方案说明.txt")
        File.WriteAllText(readmePath, readme, New UTF8Encoding(False))
        result.Files.Add(readmePath)
        result.Integration = readme
        Return result
    End Function

    Private Sub PatchStageProject(path As String)
        If Not File.Exists(path) Then Return
        Dim text = File.ReadAllText(path)
        text = text.Replace("$(AgSTGRepo)\AgSTG.Core\", "..\..\AgSTG.Core\")
        text = text.Replace("$(AgSTGRepo)\AgSTG\", "..\..\AgSTG\")
        text = text.Replace("$(AgSTGRepo)\ResourcePack\", "..\..\ResourcePack\")
        text = text.Replace("$(AgSTGRepo)\ResourcePack.TH07\", "..\..\ResourcePack.TH07\")
        text = text.Replace("$(AgSTGRepo)\dll\", "..\..\dll\")
        text = Regex.Replace(text, "\s*<ProjectReference Include=""\.\.\\\.\.\\ResourcePack\.TH07\\.*?/>", "")
        text = Regex.Replace(text, "(?s)\s*<!--.*?AgSTGRepo.*?-->\s*<AgSTGRepo.*?</AgSTGRepo>", "")
        File.WriteAllText(path, text, New UTF8Encoding(False))
    End Sub

    Private Sub PatchShellProject(path As String, code As String, stageCount As Integer)
        Dim text = File.ReadAllText(path)
        text = Regex.Replace(text, "<RootNamespace>.*?</RootNamespace>", "<RootNamespace>" & code & "</RootNamespace>")
        If Not text.Contains("<AssemblyName>") Then
            text = text.Replace("<UseWPF>true</UseWPF>", "<UseWPF>true</UseWPF>" & Environment.NewLine & "    <AssemblyName>" & code & "</AssemblyName>")
        Else
            text = Regex.Replace(text, "<AssemblyName>.*?</AssemblyName>", "<AssemblyName>" & code & "</AssemblyName>")
        End If
        text = Regex.Replace(text, "\s*<ProjectReference Include=""\.\.\\Stage\d+\\Stage\d+\.vbproj""\s*/>", "")
        text = Regex.Replace(text, "\s*<ProjectReference Include=""\.\.\\ResourcePack\.TH07\\ResourcePack\.TH07\.vbproj""\s*/>", "")

        Dim refs As New StringBuilder()
        refs.AppendLine("    <ProjectReference Include=""..\GameResources\GameResources.vbproj"" />")
        For i = 1 To stageCount
            refs.AppendLine("    <ProjectReference Include=""..\Stages\Stage" & i.ToString() & "\Stage" & i.ToString() & ".vbproj"" />")
        Next
        text = text.Replace("    <ProjectReference Include=""..\ResourcePack\ResourcePack.vbproj"" />",
                            "    <ProjectReference Include=""..\ResourcePack\ResourcePack.vbproj"" />" & Environment.NewLine & refs.ToString().TrimEnd())
        File.WriteAllText(path, text, New UTF8Encoding(False))
    End Sub

    Private Sub PatchShellSources(folder As String, code As String, stageCount As Integer, gameName As String, visuals As ShellVisuals)
        Dim application = Path.Combine(folder, "Application.xaml.vb")
        If File.Exists(application) Then
            Dim lines = File.ReadAllLines(application).Where(Function(line) Not line.Contains("ResourcePack.TH07.")).ToArray()
            File.WriteAllLines(application, lines, New UTF8Encoding(False))
        End If

        Dim title = Path.Combine(folder, "TitlePage.vb")
        If File.Exists(title) Then
            Dim text = File.ReadAllText(title)
            text = Regex.Replace(text, "(?s)((?:Public|Private) Sub Page_ActivatedChanged.*?If Activated Then\s*)",
                                 "$1" & Environment.NewLine &
                                 "            If STG.CurrentMusic <> 0 Then ManagedBass.Bass.ChannelStop(STG.CurrentMusic)" & Environment.NewLine &
                                 "            STG.CurrentMusic = 0" & Environment.NewLine &
                                 "            MW.StartTitleMusic()" & Environment.NewLine)
            text = Regex.Replace(text, "(?s)((?:Public|Private) Sub Page_ActivatedChanged.*?Else\s*)",
                                 "$1" & Environment.NewLine & "            MW.StopTitleMusic()" & Environment.NewLine)
            text = text.Replace("        STG.ReplayMode = False" & Environment.NewLine & "        Page.Activated = False",
                                "        STG.ReplayMode = False" & Environment.NewLine & "        MW.StopTitleMusic()" & Environment.NewLine & "        Page.Activated = False")
            File.WriteAllText(title, text, New UTF8Encoding(False))
        End If

        Dim main = Path.Combine(folder, "MainWindow.xaml.vb")
        If File.Exists(main) Then
            Dim text = File.ReadAllText(main)
            text = Regex.Replace(text, "\s*Imports ResourcePack\.TH07", "")
            Dim registrations As New StringBuilder()
            For i = 1 To stageCount
                registrations.AppendLine("        STG.Stages.Add(New Stage" & i.ToString() & ".Stage" & i.ToString() & "(2))")
            Next
            text = Regex.Replace(text, "(?m)^\s*STG\.Stages\.Add\(New Stage\d+\.Stage\d+\(2\)\)\s*$", "").TrimEnd() & Environment.NewLine
            text = text.Replace("        TP = New TitlePage", registrations.ToString() & "        TP = New TitlePage")
            text = text.Replace("GP.SetBackground(Textures.game_background)", "GP.SetBackground(" & BrushExpression(visuals.GameUri) & ")")
            text = text.Replace("        TP.Page.Activated = True", "        TP.Page.Activated = True" & Environment.NewLine & "        StartTitleMusic()")
            text = text.Replace("    Public Ticks As Long", "    Public Ticks As Long" & Environment.NewLine & Environment.NewLine & TitleBgmCode(visuals.TitleBgmUri))
            File.WriteAllText(main, text, New UTF8Encoding(False))
        End If

        For Each name In {"ClearPage.vb", "FailPage.vb", "PausePage.vb"}
            PatchPage(Path.Combine(folder, name), nothing)
        Next
        PatchPage(title, visuals.TitleUri)

        For Each name In {"Application.xaml", "Launcher.xaml", "MainWindow.xaml"}
            Dim filePath = Path.Combine(folder, name)
            If Not File.Exists(filePath) Then Continue For
            Dim text = File.ReadAllText(filePath)
            text = text.Replace("clr-namespace:TH07", "clr-namespace:" & code)
            If name = "MainWindow.xaml" Then
                text = text.Replace("Title=""AgSTG v3""", "Title=""" & XmlEscape(gameName) & """")
                text = text.Replace("ImageSource=""/loadpage.png""", "ImageSource=""" & visuals.StartupUri & """")
            End If
            File.WriteAllText(filePath, text, New UTF8Encoding(False))
        Next
    End Sub

    Private Sub PatchPage(path As String, titleBackgroundUri As String)
        If Not File.Exists(path) Then Return
        Dim output As New List(Of String)
        For Each line In File.ReadAllLines(path)
            If line.Contains("Imports ResourcePack.TH07") Then Continue For
            If line.Contains("Textures.") Then
                Dim match = Regex.Match(line, "Textures\.(bt_[A-Za-z0-9_]+)\((\d+)\)")
                If match.Success Then
                    Dim file = match.Groups(1).Value & If(match.Groups(2).Value = "1", "_b", "") & ".png"
                    output.Add(line.Replace(match.Value, BrushExpression(UiPackPrefix & file)))
                ElseIf line.Contains("Page.Background") AndAlso Not String.IsNullOrWhiteSpace(titleBackgroundUri) Then
                    output.Add(Regex.Replace(line, "= Textures\..*$", "= " & BrushExpression(titleBackgroundUri)))
                ElseIf line.Contains(".ForeLayer.Fill") Then
                    output.Add(Regex.Replace(line, "= Textures\..*$", "= New SolidColorBrush(Color.FromRgb(0, 122, 204))"))
                ElseIf line.Contains(".BackLayer.Fill") Then
                    output.Add(Regex.Replace(line, "= Textures\..*$", "= New SolidColorBrush(Color.FromRgb(45, 45, 48))"))
                ElseIf line.Contains("Page.Background") Then
                    output.Add(Regex.Replace(line, "= Textures\..*$", "= New SolidColorBrush(Color.FromRgb(12, 12, 20))"))
                End If
                Continue For
            End If
            If line.Contains("Sounds.mu") Then Continue For
            output.Add(line)
        Next
        File.WriteAllLines(path, output, New UTF8Encoding(False))
    End Sub

    Private Function BrushExpression(uri As String) As String
        Return "New ImageBrush(New BitmapImage(New Uri(""" & uri & """, UriKind.Absolute)))"
    End Function

    Private Function TitleBgmCode(uri As String) As String
        Dim nl = Environment.NewLine
        Dim sb As New StringBuilder()
        sb.AppendLine("    Private TitleBgm As Integer = 0")
        sb.AppendLine("    Public Sub StartTitleMusic()")
        sb.AppendLine("        If TitleBgm <> 0 Then Return")
        sb.AppendLine("        Try")
        sb.AppendLine("            Dim info = System.Windows.Application.GetResourceStream(New Uri(""" & uri & """, UriKind.Absolute))")
        sb.AppendLine("            If info Is Nothing Then Return")
        sb.AppendLine("            Using ms As New System.IO.MemoryStream()")
        sb.AppendLine("                info.Stream.CopyTo(ms)")
        sb.AppendLine("                TitleBgm = ManagedBass.Bass.CreateStream(ms.ToArray(), 0, ms.Length, ManagedBass.BassFlags.Loop)")
        sb.AppendLine("                ManagedBass.Bass.ChannelPlay(TitleBgm, True)")
        sb.AppendLine("            End Using")
        sb.AppendLine("        Catch")
        sb.AppendLine("        End Try")
        sb.AppendLine("    End Sub")
        sb.AppendLine("    Public Sub StopTitleMusic()")
        sb.AppendLine("        If TitleBgm = 0 Then Return")
        sb.AppendLine("        Try")
        sb.AppendLine("            ManagedBass.Bass.ChannelStop(TitleBgm)")
        sb.AppendLine("        Catch")
        sb.AppendLine("        End Try")
        sb.AppendLine("        TitleBgm = 0")
        sb.AppendLine("    End Sub")
        Return sb.ToString().TrimEnd()
    End Function

    Private Sub CopyUiAssets(source As String, target As String)
        If Not Directory.Exists(source) Then Return
        Directory.CreateDirectory(target)
        For Each name In {"loadpage.png", "title00.png", "game_background.png"}
            CopyFile(Path.Combine(source, name), Path.Combine(target, name))
        Next
        For Each file In Directory.GetFiles(source, "bt_*.png")
            CopyFile(file, Path.Combine(target, Path.GetFileName(file)))
        Next
        CopyFile(Path.Combine(source, "th07_01.mp3"), Path.Combine(target, "title_bgm.mp3"))
    End Sub

    Private Sub InstallUiAssets(stage As String, gameResources As String)
        If Not Directory.Exists(stage) Then Return
        Dim target = Path.Combine(gameResources, "UI")
        If Directory.Exists(target) Then Directory.Delete(target, True)
        Directory.Move(stage, target)
    End Sub

    Private Sub PatchGameResourcesProject(path As String)
        If Not File.Exists(path) Then Return
        Dim text = File.ReadAllText(path)
        If Not text.Contains("UI\*.png") Then
            Dim index = text.IndexOf("  </ItemGroup>", StringComparison.Ordinal)
            If index >= 0 Then
                text = text.Insert(index, "    <Resource Include=""UI\*.png"" />" & Environment.NewLine &
                                            "    <Resource Include=""UI\*.mp3"" />" & Environment.NewLine)
            End If
        End If
        File.WriteAllText(path, text, New UTF8Encoding(False))
    End Sub

    Private Function CreateVisuals(game As GameProject) As ShellVisuals
        Return New ShellVisuals With {
            .StartupUri = ResourceUri(game, game.StartupBackgroundId, GameResourceKind.Image, UiPackPrefix & "loadpage.png"),
            .TitleUri = ResourceUri(game, game.TitleBackgroundId, GameResourceKind.Image, UiPackPrefix & "title00.png"),
            .GameUri = ResourceUri(game, game.GameBackgroundId, GameResourceKind.Image, UiPackPrefix & "game_background.png"),
            .TitleBgmUri = ResourceUri(game, game.TitleBgmId, GameResourceKind.Audio, UiPackPrefix & "title_bgm.mp3")
        }
    End Function

    Private Function ResourceUri(game As GameProject, id As String, kind As GameResourceKind, fallback As String) As String
        If String.IsNullOrWhiteSpace(id) Then Return fallback
        Dim resource = game.Resources.FirstOrDefault(Function(r) String.Equals(r.Id, id, StringComparison.Ordinal))
        If resource Is Nothing OrElse resource.Kind <> kind OrElse String.IsNullOrWhiteSpace(resource.RelativePath) Then Return fallback
        Dim extension = Path.GetExtension(resource.RelativePath)
        If String.IsNullOrWhiteSpace(extension) Then Return fallback
        Return ResourcePackPrefix & SafeIdentifier(resource.Id, "resource") & extension
    End Function

    Private Sub CopyFile(source As String, target As String)
        If File.Exists(source) Then File.Copy(source, target, True)
    End Sub

    Private Function BuildSolution(code As String, stageCount As Integer) As String
        Dim sb As New StringBuilder()
        sb.AppendLine("<Solution>")
        sb.AppendLine("  <Project Path=""AgSTG\AgSTG.vbproj"" />")
        sb.AppendLine("  <Project Path=""AgSTG.Controls\AgSTG.Controls.vbproj"" />")
        sb.AppendLine("  <Project Path=""AgSTG.Core\AgSTG.Core.vbproj"" />")
        sb.AppendLine("  <Project Path=""ResourcePack\ResourcePack.vbproj"" />")
        sb.AppendLine("  <Project Path=""GameResources\GameResources.vbproj"" />")
        sb.AppendLine("  <Project Path=""" & code & "\" & code & ".vbproj"" />")
        For i = 1 To stageCount
            sb.AppendLine("  <Project Path=""Stages\Stage" & i.ToString() & "\Stage" & i.ToString() & ".vbproj"" />")
        Next
        sb.AppendLine("</Solution>")
        Return sb.ToString()
    End Function

    Private Function BuildReadme(game As GameProject, code As String) As String
        Dim sb As New StringBuilder()
        sb.AppendLine("DanmakuDesigner 导出的完整 AgSTG 解决方案")
        sb.AppendLine("==========================================")
        sb.AppendLine()
        sb.AppendLine("游戏：" & game.Name)
        sb.AppendLine("解决方案：" & code & ".slnx")
        sb.AppendLine("关卡数：" & game.Stages.Count)
        sb.AppendLine()
        sb.AppendLine("包含：")
        sb.AppendLine("  · AgSTG / AgSTG.Controls / AgSTG.Core / ResourcePack（引擎本体，未修改）")
        sb.AppendLine("  · " & code & "（由原 TH07 外壳改写而成的游戏启动项目）")
        sb.AppendLine("  · GameResources（本工程导入的图片和音频）")
        sb.AppendLine("  · Stages\Stage1..StageN（DanmakuDesigner 生成的关卡）")
        sb.AppendLine()
        sb.AppendLine("使用：")
        sb.AppendLine("  1. 打开 " & code & ".slnx")
        sb.AppendLine("  2. 还原 NuGet 包并生成解决方案")
        sb.AppendLine("  3. 启动项目 " & code)
        sb.AppendLine()
        sb.AppendLine("说明：模板中的旧 Stage1..Stage6 和 ResourcePack.TH07 已移除；")
        sb.AppendLine("      TH07 外壳中的专属贴图、音乐和关卡注册代码已改为通用资源/生成关卡。")
        Return sb.ToString()
    End Function

    Private Sub DeleteDirectory(path As String)
        If Directory.Exists(path) Then Directory.Delete(path, True)
    End Sub

    Private Sub DeleteFile(path As String)
        If File.Exists(path) Then File.Delete(path)
    End Sub

    Private Function SafeIdentifier(text As String, fallback As String) As String
        If String.IsNullOrWhiteSpace(text) Then Return fallback
        Dim chars = text.Trim().Select(Function(c) If((Char.IsLetterOrDigit(c) AndAlso AscW(c) < 128) OrElse c = "_"c, c, "_"c)).ToArray()
        Dim value = New String(chars)
        If String.IsNullOrWhiteSpace(value.Replace("_", "")) OrElse Char.IsDigit(value(0)) Then value = fallback
        Return value
    End Function

    Private Function XmlEscape(value As String) As String
        Return If(value, "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("""", "&quot;")
    End Function
End Module
