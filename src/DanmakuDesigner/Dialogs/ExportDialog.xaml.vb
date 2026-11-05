Imports System.IO
Imports System.Text
Imports System.Windows
Imports DanmakuDesigner.Core

Public Class ExportResult
    Public Property Folder As String = ""
    Public Property Files As New List(Of String)
    Public Property Integration As String = ""
    Public Property Warnings As New List(Of String)
End Class

Public Module ExportService

    Public Function Export(project As StageProject, folder As String, className As String, enginePath As String) As ExportResult
        Dim clone = ProjectJson.Clone(project)
        clone.Stage.CodeName = className
        clone.Normalize()
        Dim generated = VBCodeGenerator.Generate(clone, enginePath)
        Directory.CreateDirectory(folder)
        Dim result As New ExportResult With {.Folder = folder, .Integration = generated.Integration}
        result.Warnings.AddRange(generated.Warnings)

        Dim baseName = generated.FileBaseName
        Dim files As New Dictionary(Of String, String) From {
            {baseName & ".vb", generated.Code},
            {baseName & "bg.xaml", generated.BackgroundXaml},
            {baseName & "bg.xaml.vb", generated.BackgroundCode},
            {baseName & ".vbproj", generated.ProjectFile},
            {"README-集成说明.txt", BuildReadme(clone, generated)},
            {baseName & ".dmkproj", ""}
        }
        For Each pair In files
            Dim outputPath As String = System.IO.Path.Combine(folder, pair.Key)
            If pair.Key.EndsWith(".dmkproj", StringComparison.OrdinalIgnoreCase) Then
                ProjectJson.Save(outputPath, clone)
            Else
                File.WriteAllText(outputPath, pair.Value, New UTF8Encoding(True))
            End If
            result.Files.Add(outputPath)
        Next
        Return result
    End Function

    ''' <summary>导出整个游戏：每个关卡一个 VB 工程，另加一个独立资源包工程和解决方案。</summary>
    Public Function ExportGame(game As GameProject, folder As String, gameCodeName As String, enginePath As String,
                               Optional sourceDirectory As String = "") As ExportResult
        Dim clone = ProjectJson.Clone(game)
        clone.Normalize()
        Dim code = SafeIdentifier(gameCodeName, "GameExport")
        Directory.CreateDirectory(folder)
        Dim result As New ExportResult With {.Folder = folder}
        Dim usedStageNames As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        Dim stageEntries As New List(Of ExportedStageInfo)

        Dim resourceUris As New Dictionary(Of String, String)(StringComparer.Ordinal)
        Dim audioNames As New Dictionary(Of String, String)(StringComparer.Ordinal)
        For Each resource In clone.Resources
            If resource Is Nothing Then Continue For
            If resource.Kind = GameResourceKind.Audio Then audioNames(resource.Id) = resource.Name
            If String.IsNullOrWhiteSpace(sourceDirectory) OrElse String.IsNullOrWhiteSpace(resource.RelativePath) Then Continue For
            Dim resourceSource = Path.Combine(sourceDirectory, resource.RelativePath.Replace("/"c, "\"c))
            If Not File.Exists(resourceSource) Then Continue For
            Dim resourceTarget = SafeIdentifier(resource.Id, "resource") & Path.GetExtension(resourceSource)
            resourceUris(resource.Id) = "pack://application:,,,/GameResources;component/Resources/" & resourceTarget
        Next

        Dim index = 1
        For Each stageProject In clone.Stages
            Dim stageCode = SafeIdentifier(stageProject.Stage.CodeName, "Stage" & index.ToString())
            Dim unique = stageCode
            Dim suffix = 2
            While usedStageNames.Contains(unique)
                unique = stageCode & suffix.ToString()
                suffix += 1
            End While
            usedStageNames.Add(unique)
            stageProject.Stage.CodeName = unique
            If String.IsNullOrWhiteSpace(stageProject.Name) Then stageProject.Name = stageProject.Stage.Name

            Dim generated = VBCodeGenerator.Generate(stageProject, enginePath, resourceUris, audioNames)
            result.Warnings.AddRange(generated.Warnings)
            Dim stageDir = Path.Combine(folder, "Stages", unique)
            Directory.CreateDirectory(stageDir)
            Dim projectText = AddResourceProjectReference(generated.ProjectFile)
            WriteExportFile(stageDir, unique & ".vb", generated.Code, result)
            WriteExportFile(stageDir, unique & "bg.xaml", generated.BackgroundXaml, result)
            WriteExportFile(stageDir, unique & "bg.xaml.vb", generated.BackgroundCode, result)
            WriteExportFile(stageDir, unique & ".vbproj", projectText, result)
            stageEntries.Add(New ExportedStageInfo With {
                .Stage = stageProject,
                .Generated = generated,
                .Folder = Path.Combine("Stages", unique)
            })
            index += 1
        Next

        ' ---------------- 资源包工程 ----------------
        Dim resourceDir = Path.Combine(folder, "GameResources")
        Dim copied As New List(Of ExportedResourceInfo)
        Directory.CreateDirectory(Path.Combine(resourceDir, "Resources"))
        For Each resource In clone.Resources
            Dim sourcePath = ""
            If Not String.IsNullOrWhiteSpace(sourceDirectory) AndAlso Not String.IsNullOrWhiteSpace(resource.RelativePath) Then
                sourcePath = Path.Combine(sourceDirectory, resource.RelativePath.Replace("/"c, "\"c))
            End If
            If String.IsNullOrWhiteSpace(sourcePath) OrElse Not File.Exists(sourcePath) Then
                result.Warnings.Add("资源缺失，未导出：" & resource.Name & " (" & resource.RelativePath & ")")
                Continue For
            End If
            Dim extension = Path.GetExtension(sourcePath)
            Dim targetName = SafeIdentifier(resource.Id, "resource") & extension
            Dim targetPath = Path.Combine(resourceDir, "Resources", targetName)
            File.Copy(sourcePath, targetPath, True)
            resource.RelativePath = "GameResources/Resources/" & targetName
            copied.Add(New ExportedResourceInfo With {
                .Resource = resource,
                .FileName = targetName,
                .Extension = extension
            })
        Next
        WriteExportFile(resourceDir, "GameResources.vbproj", BuildResourceProject(copied), result)
        WriteExportFile(resourceDir, "GameResources.vb", BuildResourceCode(copied), result)

        ' ---------------- 游戏工程 / 解决方案 / README ----------------
        WriteExportFile(folder, code & ".slnx", BuildGameSolution(stageEntries), result)
        Dim gameProjectPath = Path.Combine(folder, code & ".dmkproj")
        ProjectJson.SaveGame(gameProjectPath, clone)
        result.Files.Add(gameProjectPath)
        Dim readme = BuildGameReadme(clone, stageEntries, copied, code)
        WriteExportFile(folder, "README-游戏集成说明.txt", readme, result)
        result.Integration = readme
        Return result
    End Function

    Private Class ExportedStageInfo
        Public Property Stage As StageProject
        Public Property Generated As GeneratedStage
        Public Property Folder As String = ""
    End Class

    Private Class ExportedResourceInfo
        Public Property Resource As GameResource
        Public Property FileName As String = ""
        Public Property Extension As String = ""
    End Class

    Private Sub WriteExportFile(folder As String, fileName As String, content As String, result As ExportResult)
        Directory.CreateDirectory(folder)
        Dim path = IO.Path.Combine(folder, fileName)
        File.WriteAllText(path, content, New UTF8Encoding(True))
        result.Files.Add(path)
    End Sub

    Private Function AddResourceProjectReference(projectText As String) As String
        Dim block = "  <ItemGroup>" & Environment.NewLine &
                    "    <ProjectReference Include=""..\..\GameResources\GameResources.vbproj"" />" & Environment.NewLine &
                    "  </ItemGroup>" & Environment.NewLine & "</Project>"
        Return projectText.Replace("</Project>", block)
    End Function

    Private Function BuildResourceProject(resources As List(Of ExportedResourceInfo)) As String
        Dim sb As New StringBuilder()
        sb.AppendLine("<Project Sdk=""Microsoft.NET.Sdk"">")
        sb.AppendLine("  <PropertyGroup>")
        sb.AppendLine("    <TargetFramework>net10.0-windows</TargetFramework>")
        sb.AppendLine("    <RootNamespace>GameResources</RootNamespace>")
        sb.AppendLine("    <AssemblyName>GameResources</AssemblyName>")
        sb.AppendLine("    <UseWPF>true</UseWPF>")
        sb.AppendLine("    <OptionStrict>Off</OptionStrict>")
        sb.AppendLine("  </PropertyGroup>")
        sb.AppendLine("  <ItemGroup>")
        For Each resource In resources
            sb.AppendLine("    <Resource Include=""Resources\" & XmlEscape(resource.FileName) & """ />")
        Next
        sb.AppendLine("  </ItemGroup>")
        sb.AppendLine("</Project>")
        Return sb.ToString()
    End Function

    Private Function BuildResourceCode(resources As List(Of ExportedResourceInfo)) As String
        Dim sb As New StringBuilder()
        sb.AppendLine("Option Strict Off")
        sb.AppendLine("Imports System")
        sb.AppendLine("Imports System.Windows.Media")
        sb.AppendLine("Imports System.Windows.Media.Imaging")
        sb.AppendLine()
        sb.AppendLine("Namespace GameResources")
        sb.AppendLine("    Public NotInheritable Class GameAssetInfo")
        sb.AppendLine("        Public ReadOnly Property Id As String")
        sb.AppendLine("        Public ReadOnly Property Name As String")
        sb.AppendLine("        Public ReadOnly Property Kind As String")
        sb.AppendLine("        Public ReadOnly Property FileName As String")
        sb.AppendLine("        Public Sub New(id As String, name As String, kind As String, fileName As String)")
        sb.AppendLine("            Me.Id = id : Me.Name = name : Me.Kind = kind : Me.FileName = fileName")
        sb.AppendLine("        End Sub")
        sb.AppendLine("    End Class")
        sb.AppendLine()
        sb.AppendLine("    Public Module GameAssetCatalog")
        sb.AppendLine("        Public ReadOnly Assets As GameAssetInfo() = {")
        For i = 0 To resources.Count - 1
            Dim r = resources(i)
            Dim comma = If(i = resources.Count - 1, "", ",")
            sb.AppendLine("            New GameAssetInfo(" & VbQuote(r.Resource.Id) & ", " & VbQuote(r.Resource.Name) & ", " &
                          VbQuote(r.Resource.Kind.ToString()) & ", " & VbQuote("Resources/" & r.FileName) & ")" & comma)
        Next
        sb.AppendLine("        }")
        sb.AppendLine()
        sb.AppendLine("        Public Function Image(id As String) As ImageSource")
        For Each r In resources
            If r.Resource.Kind = GameResourceKind.Image Then
                sb.AppendLine("            If id = " & VbQuote(r.Resource.Id) & " Then Return LoadImage(" &
                              VbQuote("Resources/" & r.FileName) & ")")
            End If
        Next
        sb.AppendLine("            Return Nothing")
        sb.AppendLine("        End Function")
        sb.AppendLine()
        sb.AppendLine("        Public Function FileName(id As String) As String")
        For Each r In resources
            sb.AppendLine("            If id = " & VbQuote(r.Resource.Id) & " Then Return " & VbQuote("Resources/" & r.FileName))
        Next
        sb.AppendLine("            Return """"")
        sb.AppendLine("        End Function")
        sb.AppendLine()
        sb.AppendLine("        Private Function LoadImage(relativePath As String) As ImageSource")
        sb.AppendLine("            Dim image As New BitmapImage()")
        sb.AppendLine("            image.BeginInit()")
        sb.AppendLine("            image.CacheOption = BitmapCacheOption.OnLoad")
        sb.AppendLine("            image.UriSource = New Uri(""pack://application:,,,/GameResources;component/"" & relativePath, UriKind.Absolute)")
        sb.AppendLine("            image.EndInit()")
        sb.AppendLine("            image.Freeze()")
        sb.AppendLine("            Return image")
        sb.AppendLine("        End Function")
        sb.AppendLine("    End Module")
        sb.AppendLine("End Namespace")
        Return sb.ToString()
    End Function

    Private Function BuildGameSolution(stages As List(Of ExportedStageInfo)) As String
        Dim sb As New StringBuilder()
        sb.AppendLine("<Solution>")
        For Each stage In stages
            sb.AppendLine("  <Project Path=""" & stage.Folder.Replace("\"c, "/"c) & "/" & stage.Generated.FileBaseName & ".vbproj"" />")
        Next
        sb.AppendLine("  <Project Path=""GameResources/GameResources.vbproj"" />")
        sb.AppendLine("</Solution>")
        Return sb.ToString()
    End Function

    Private Function BuildGameReadme(game As GameProject, stages As List(Of ExportedStageInfo),
                                     resources As List(Of ExportedResourceInfo), code As String) As String
        Dim sb As New StringBuilder()
        sb.AppendLine("DanmakuDesigner 导出的 AgSTG 游戏工程")
        sb.AppendLine("=====================================")
        sb.AppendLine()
        sb.AppendLine("游戏名称：" & game.Name)
        sb.AppendLine("游戏代码名：" & code)
        sb.AppendLine("关卡数量：" & game.Stages.Count)
        sb.AppendLine("共享资源：" & resources.Count & " 个")
        sb.AppendLine("生成时间：" & DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
        sb.AppendLine()
        sb.AppendLine("目录结构：")
        For Each stage In stages
            sb.AppendLine("  " & stage.Folder & "\" & stage.Generated.FileBaseName & ".vbproj")
        Next
        sb.AppendLine("  GameResources\GameResources.vbproj")
        sb.AppendLine("  " & code & ".slnx")
        sb.AppendLine("  " & code & ".dmkproj")
        sb.AppendLine()
        sb.AppendLine("关卡注册：")
        For Each stage In stages
            sb.AppendLine("  STG.Stages.Add(New " & stage.Generated.StageClassName & "." & stage.Generated.StageClassName &
                          "(Difficulty." & stage.Stage.Stage.Difficulty.ToString() & "))")
        Next
        sb.AppendLine()
        sb.AppendLine("接入步骤：")
        sb.AppendLine("1. 把整个导出目录放到 AgSTG 解决方案根目录，并把各关卡工程与 GameResources 工程加入 AgSTG.sln。")
        sb.AppendLine("2. 为 TH07 项目添加对每个关卡工程的项目引用。")
        sb.AppendLine("3. 在 TH07\MainWindow.xaml.vb 的 Start() 中注册上面的关卡。")
        sb.AppendLine("4. 若移动引擎目录，可用 -p:AgSTGRepo=... 覆盖关卡工程中的引擎路径。")
        sb.AppendLine()
        sb.AppendLine("注意：")
        sb.AppendLine("  · 生成代码会覆盖导出目录中的同名文件，请修改 .dmkproj 源工程而不是生成结果。")
        sb.AppendLine("  · 资源包工程只包含工程登记的共享资源，不修改 AgSTG 引擎本体。")
        Return sb.ToString()
    End Function

    Private Function SafeIdentifier(text As String, fallback As String) As String
        Dim raw = If(String.IsNullOrWhiteSpace(text), fallback, text.Trim())
        Dim chars = raw.Select(Function(c) If((Char.IsLetterOrDigit(c) AndAlso AscW(c) < 128) OrElse c = "_"c, c, "_"c)).ToArray()
        Dim result = New String(chars)
        If String.IsNullOrWhiteSpace(result.Replace("_", "")) Then result = fallback
        If Char.IsDigit(result(0)) Then result = "_" & result
        Return result
    End Function

    Private Function XmlEscape(text As String) As String
        Return If(text, "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("""", "&quot;")
    End Function

    Private Function VbQuote(text As String) As String
        Return """" & If(text, "").Replace("""", """""") & """"
    End Function

    Private Function BuildReadme(project As StageProject, generated As GeneratedStage) As String
        Dim sb As New StringBuilder()
        sb.AppendLine("DanmakuDesigner 导出的 AgSTG 关卡")
        sb.AppendLine("=====================================")
        sb.AppendLine()
        sb.AppendLine("关卡名称：" & project.Stage.Name)
        sb.AppendLine("代码类名：" & generated.StageClassName)
        sb.AppendLine("长度：" & project.Stage.LengthTicks & " 帧（" & (project.Stage.LengthTicks / 60.0).ToString("0.0") & " 秒）")
        sb.AppendLine("生成时间：" & DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
        sb.AppendLine()
        sb.AppendLine("文件清单：")
        sb.AppendLine("  " & generated.StageClassName & ".vb            关卡时间轴与积木行为的生成代码")
        sb.AppendLine("  " & generated.StageClassName & "bg.xaml        关卡背景 UserControl")
        sb.AppendLine("  " & generated.StageClassName & "bg.xaml.vb     背景图层与动画逻辑")
        sb.AppendLine("  " & generated.StageClassName & ".vbproj        可直接加入 AgSTG.sln 的工程文件")
        sb.AppendLine("  " & generated.StageClassName & ".dmkproj        可再次用 DanmakuDesigner 打开的工程")
        sb.AppendLine()
        sb.AppendLine(generated.Integration)
        sb.AppendLine()
        sb.AppendLine("注意：")
        sb.AppendLine("  · 生成代码会覆盖本文件夹中的同名文件，请勿直接修改生成结果。")
        sb.AppendLine("  · 如果需要换引擎路径，请编辑 .vbproj 中的 ProjectReference。")
        sb.AppendLine("  · 本工程不会修改 AgSTG 引擎本体，只引用引擎项目。")
        Return sb.ToString()
    End Function

End Module

Public Class ExportDialog

    Public Property ResultFolder As String = ""
    Private _game As GameProject

    Public Sub New()
        InitializeComponent()
        _game = App.Session.Project
        If _game Is Nothing Then Return
        ClassBox.Text = If(String.IsNullOrWhiteSpace(_game.Name), "GameExport", SafeName(_game.Name))
        If String.IsNullOrWhiteSpace(ClassBox.Text.Replace("_", "")) Then ClassBox.Text = "GameExport"
        EngineBox.Text = Path.Combine(AppContext.BaseDirectory, "EngineTemplate.zip")
        Dim defaultFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "DanmakuDesigner", ClassBox.Text)
        FolderBox.Text = defaultFolder
        Dim errors = App.Session.ErrorCount()
        Dim warnings = App.Session.WarningCount()
        IssuesText.Text = If(errors = 0,
                             "当前关卡验证通过：" & warnings & " 个警告。",
                             "当前关卡有 " & errors & " 个错误、" & warnings & " 个警告，导出的代码可能无法编译或运行。建议先修复。")
        UpdateFilesText()
        AddHandler ClassBox.TextChanged, Sub() UpdateFilesText()
        AddHandler Loaded, Sub()
                              ClassBox.Focus()
                              ClassBox.SelectAll()
                          End Sub
    End Sub

    Private Sub UpdateFilesText()
        If _game Is Nothing Then Return
        Dim name = If(String.IsNullOrWhiteSpace(ClassBox.Text), "GameExport", ClassBox.Text.Trim())
        Dim lines As New List(Of String)
        lines.Add("AgSTG / AgSTG.Controls / AgSTG.Core / ResourcePack（引擎原样）")
        lines.Add(name & "\" & name & ".vbproj（游戏外壳）")
        lines.Add("GameResources\GameResources.vbproj")
        For i = 1 To _game.Stages.Count
            lines.Add("Stages\Stage" & i.ToString() & "\Stage" & i.ToString() & ".vbproj")
        Next
        lines.Add(name & ".slnx")
        lines.Add("README-解决方案说明.txt")
        FilesText.Text = String.Join(Environment.NewLine, lines)
    End Sub

    Private Function SafeName(text As String) As String
        If String.IsNullOrWhiteSpace(text) Then Return "GameExport"
        Dim chars = text.ToCharArray().Select(Function(c) If((Char.IsLetterOrDigit(c) AndAlso AscW(c) < 128) OrElse c = "_"c, c, "_"c)).ToArray()
        Return New String(chars)
    End Function

    Private Sub Browse_Click(sender As Object, e As RoutedEventArgs)
        Dim dialog As New Microsoft.Win32.OpenFolderDialog With {
            .Title = "选择导出目录",
            .Multiselect = False
        }
        If Directory.Exists(FolderBox.Text) Then dialog.InitialDirectory = FolderBox.Text
        If dialog.ShowDialog() = True Then FolderBox.Text = dialog.FolderName
    End Sub

    Private Sub BrowseEngine_Click(sender As Object, e As RoutedEventArgs)
        Dim dialog As New Microsoft.Win32.OpenFileDialog With {
            .Title = "选择 AgSTG 引擎模板压缩包",
            .Filter = "引擎模板 (*.zip)|*.zip|所有文件 (*.*)|*.*"
        }
        If File.Exists(EngineBox.Text) Then dialog.InitialDirectory = Path.GetDirectoryName(EngineBox.Text)
        If dialog.ShowDialog() = True Then EngineBox.Text = dialog.FileName
    End Sub

    Private Sub Cancel_Click(sender As Object, e As RoutedEventArgs)
        DialogResult = False
    End Sub

    Private Sub Export_Click(sender As Object, e As RoutedEventArgs)
        Dim className = ClassBox.Text.Trim()
        If Not Text.RegularExpressions.Regex.IsMatch(className, "^[A-Za-z_][A-Za-z0-9_]*$") Then
            ShowError("游戏代码名必须是 ASCII 标识符：字母/数字/下划线，且不能以数字开头。")
            Return
        End If
        Dim folder = FolderBox.Text.Trim()
        If String.IsNullOrWhiteSpace(folder) Then
            ShowError("请选择导出目录。")
            Return
        End If
        Dim templatePath = EngineBox.Text.Trim()
        If Not File.Exists(templatePath) Then
            ShowError("找不到内置引擎模板：" & templatePath)
            Return
        End If
        Try
            If File.Exists(Path.Combine(folder, className & ".slnx")) Then
                If MessageBox.Show(Me, "目标目录中已存在 " & className & ".slnx，是否覆盖？", "导出解决方案", MessageBoxButton.YesNo, MessageBoxImage.Question) <> MessageBoxResult.Yes Then Return
            End If
            Mouse.OverrideCursor = Cursors.Wait
            Dim result = SolutionExportService.ExportSolution(App.Session.Project, folder, className, templatePath, App.Session.ProjectDirectory)
            ResultFolder = result.Folder
            Dim solutionPath = Path.Combine(result.Folder, className & ".slnx")
            BuildAndRunService.LastSolutionPath = solutionPath
            App.Session.AddLog(IssueSeverity.Info, "已导出完整解决方案：" & result.Folder)
            If result.Warnings.Count > 0 Then
                App.Session.AddLog(IssueSeverity.Warning, String.Join("；", result.Warnings))
            End If

            Dim revealedExe = False
            If MessageBox.Show(Me, "解决方案已导出。" & Environment.NewLine & "是否立即编译？",
                               "编译解决方案", MessageBoxButton.YesNo, MessageBoxImage.Question) = MessageBoxResult.Yes Then
                If BuildAndRunService.EnsureDotNet10Sdk(Me) Then
                    Dim build = BuildAndRunService.CompileSolution(solutionPath, Me)
                    If build.Success Then
                        BuildAndRunService.RevealFile(build.ExePath)
                        revealedExe = True
                        App.Session.AddLog(IssueSeverity.Info, "解决方案编译成功，已打开 exe 所在目录。")
                    Else
                        BuildAndRunService.ShowBuildFailure(Me, build)
                    End If
                End If
            End If

            If Not revealedExe AndAlso OpenFolderCheck.IsChecked = True Then
                Try
                    Process.Start(New ProcessStartInfo("explorer.exe", result.Folder) With {.UseShellExecute = True})
                Catch
                End Try
            End If
            DialogResult = True
        Catch ex As Exception
            ShowError("导出失败：" & ex.Message)
        Finally
            Mouse.OverrideCursor = Nothing
        End Try
    End Sub

    Private Sub ShowError(message As String)
        ErrorText.Text = message
        ErrorText.Visibility = Visibility.Visible
    End Sub

End Class
