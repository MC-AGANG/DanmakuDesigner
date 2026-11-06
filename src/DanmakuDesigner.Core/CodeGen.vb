Option Strict On
Option Infer On

Imports System.Globalization
Imports System.Collections.Generic
Imports System.Linq
Imports System.Text
Imports System.Text.RegularExpressions

Namespace Global.DanmakuDesigner.Core

    Public Class GeneratedStage
        Public Property Code As String = ""
        Public Property StageClassName As String = ""
        Public Property FileBaseName As String = ""
        Public Property BackgroundXaml As String = ""
        Public Property BackgroundCode As String = ""
        Public Property ProjectFile As String = ""
        Public Property Integration As String = ""
        Public Property Warnings As New List(Of String)
    End Class

    ''' <summary>把工程编译成可直接加入 AgSTG 解决方案的 VB 关卡源码。</summary>
    Public Module VBCodeGenerator

        Public Function Generate(project As StageProject, Optional engineRepoPath As String = "",
                                 Optional resourceUris As IDictionary(Of String, String) = Nothing,
                                 Optional audioNames As IDictionary(Of String, String) = Nothing) As GeneratedStage
            If project Is Nothing Then Throw New ArgumentNullException(NameOf(project))
            project.Normalize()
            Dim name = SanitizeIdentifier(project.Stage.CodeName, "StageCustom")
            Dim generator As New StageGenerator(project, name, engineRepoPath, resourceUris, audioNames)
            Return generator.Build()
        End Function

        Friend Function SanitizeIdentifier(text As String, fallback As String) As String
            Dim cleaned = Regex.Replace(If(text, "").Trim(), "[^A-Za-z0-9_]", "_")
            If String.IsNullOrWhiteSpace(cleaned) Then cleaned = fallback
            If Char.IsDigit(cleaned(0)) Then cleaned = "_" & cleaned
            Return cleaned
        End Function

        Friend Function Quote(text As String) As String
            Return """" & If(text, "").Replace("""", """""") & """"
        End Function

        Friend Function Num(value As Double) As String
            Return ValueExpression.FormatNumber(value)
        End Function

        Friend Function NormalizeColor(text As String) As String
            Dim value = If(text, "").Trim()
            If value.StartsWith("#", StringComparison.Ordinal) Then
                Dim hex = value.Substring(1)
                If hex.Length = 6 Then Return "#FF" & hex.ToUpperInvariant()
                If hex.Length = 8 Then Return "#" & hex.ToUpperInvariant()
            End If
            Return "#FF101820"
        End Function

    End Module

    Friend Class CodeWriter
        Private ReadOnly _sb As New StringBuilder()
        Public Property Indent As Integer = 0

        Public Sub Line(Optional text As String = "")
            If text.Length > 0 Then
                _sb.Append(New String(" "c, Indent * 4)).Append(text).AppendLine()
            Else
                _sb.AppendLine()
            End If
        End Sub

        Public Sub Open(text As String)
            Line(text)
            Indent += 1
        End Sub

        Public Sub Close(text As String)
            Indent = Math.Max(0, Indent - 1)
            Line(text)
        End Sub

        Public Overrides Function ToString() As String
            Return _sb.ToString()
        End Function
    End Class

    Friend Class BehaviorInfo
        Public Property OwnerId As String
        Public Property OwnerName As String
        Public Property ClassName As String
        Public Property Scope As ProgramScope
        Public Property Program As Program
        Public Property HasBehavior As Boolean
        Public Property BossTemplate As EnemyTemplate
        Public Property ResourceUris As IDictionary(Of String, String)
        Public Property AudioNames As IDictionary(Of String, String)
        Public Property SpellPrograms As New List(Of SpellBehaviorRef)
    End Class

    Friend Class SpellBehaviorRef
        Public Property SpellIndex As Integer
        Public Property SpellId As String = ""
        Public Property Program As Program
    End Class

    Friend Class StageGenerator
        Private ReadOnly _project As StageProject
        Private ReadOnly _stage As StageDocument
        Private ReadOnly _className As String
        Private ReadOnly _enginePath As String
        Private ReadOnly _resourceUris As IDictionary(Of String, String)
        Private ReadOnly _audioNames As IDictionary(Of String, String)
        Private ReadOnly _enemyBehaviors As New Dictionary(Of String, BehaviorInfo)(StringComparer.Ordinal)
        Private ReadOnly _bulletBehaviors As New Dictionary(Of String, BehaviorInfo)(StringComparer.Ordinal)
        Private ReadOnly _result As New GeneratedStage()

        Public Sub New(project As StageProject, className As String, enginePath As String,
                       Optional resourceUris As IDictionary(Of String, String) = Nothing,
                       Optional audioNames As IDictionary(Of String, String) = Nothing)
            _project = project
            _stage = project.Stage
            _className = className
            _enginePath = If(enginePath, "")
            _resourceUris = If(resourceUris, New Dictionary(Of String, String)())
            _audioNames = If(audioNames, New Dictionary(Of String, String)())
        End Sub

        Public Function Build() As GeneratedStage
            PrepareBehaviors()
            Dim code As New CodeWriter()
            EmitHeader(code)
            code.Open("Public Class " & _className)
            code.Line("Inherits Stage")
            code.Line("Private BG As " & _className & "bg")
            code.Line("Private _cleared As Boolean")
            code.Line("Private _boss As Enemy.Boss")
            code.Line()
            EmitConstructor(code)
            EmitReset(code)
            EmitAction(code)
            EmitSpawnTimeline(code)
            EmitCues(code)
            EmitSpawnMethods(code)
            EmitDialogueMethods(code)
            code.Close("End Class")
            code.Line()
            EmitBehaviorClasses(code)

            _result.Code = code.ToString()
            _result.StageClassName = _className
            _result.FileBaseName = _className
            _result.BackgroundXaml = BuildBackgroundXaml()
            _result.BackgroundCode = BuildBackgroundCode()
            _result.ProjectFile = BuildProjectFile()
            _result.Integration = BuildIntegration()
            Return _result
        End Function

        Private Sub PrepareBehaviors()
            Dim used As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
            For Each template In _stage.EnemyTemplates
                Dim cls = UniqueName(_className & "_Beh_" & VBCodeGenerator.SanitizeIdentifier(template.Id, "E"), used)
                _enemyBehaviors(template.Id) = New BehaviorInfo With {
                    .OwnerId = template.Id,
                    .OwnerName = template.Name,
                    .ClassName = cls,
                    .Scope = ProgramScope.Enemy,
                    .Program = template.Behavior,
                    .BossTemplate = If(template.IsBoss, template, Nothing),
                    .ResourceUris = _resourceUris,
                    .AudioNames = _audioNames,
                    .HasBehavior = template.IsBoss OrElse (template.Behavior IsNot Nothing AndAlso template.Behavior.Scripts.Count > 0)
                }
                If template.IsBoss Then
                    Dim bossInfo = _enemyBehaviors(template.Id)
                    Dim bossIndex = 0
                    For i = 0 To template.SpellCardIds.Count - 1
                        Dim spell = _stage.FindSpell(template.SpellCardIds(i))
                        If spell Is Nothing OrElse spell.Behavior Is Nothing Then Continue For
                        bossInfo.SpellPrograms.Add(New SpellBehaviorRef With {
                            .SpellIndex = bossIndex,
                            .SpellId = spell.Id,
                            .Program = spell.Behavior
                        })
                        bossIndex += 1
                    Next
                End If
            Next
            For Each pattern In _stage.BulletPatterns
                Dim cls = UniqueName(_className & "_Beh_" & VBCodeGenerator.SanitizeIdentifier(pattern.Id, "B"), used)
                _bulletBehaviors(pattern.Id) = New BehaviorInfo With {
                    .OwnerId = pattern.Id,
                    .OwnerName = pattern.Name,
                    .ClassName = cls,
                    .Scope = ProgramScope.Bullet,
                    .Program = pattern.Behavior,
                    .HasBehavior = pattern.Behavior IsNot Nothing AndAlso pattern.Behavior.Scripts.Count > 0
                }
            Next
        End Sub

        Private Function UniqueName(baseName As String, used As HashSet(Of String)) As String
            Dim candidate = baseName
            Dim index = 2
            While Not used.Add(candidate)
                candidate = baseName & index.ToString(CultureInfo.InvariantCulture)
                index += 1
            End While
            Return candidate
        End Function

        Private Sub EmitHeader(w As CodeWriter)
            w.Line("' ============================================================================")
            w.Line("' 由 DanmakuDesigner 生成 · " & _stage.Name)
            w.Line("' 生成时间: " & DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))
            w.Line("' 引擎: AgSTG · 目标框架 net10.0-windows")
            w.Line("' 重新导出会覆盖本文件，请勿直接修改生成结果。")
            w.Line("' ============================================================================")
            w.Line("Option Strict Off")
            w.Line()
            w.Line("Imports System")
            w.Line("Imports System.Math")
            w.Line("Imports System.Collections.Generic")
            w.Line("Imports System.Windows")
            w.Line("Imports AgSTG")
            w.Line("Imports ResourcePack")
            w.Line()
        End Sub

        Private Sub EmitConstructor(w As CodeWriter)
            w.Open("Public Sub New(Difficulty As Difficulty)")
            w.Line("MyBase.New(Difficulty)")
            w.Close("End Sub")
            w.Line()
        End Sub

        Private Sub EmitReset(w As CodeWriter)
            w.Open("Public Overrides Sub Initialize()")
            w.Line("Reset()")
            w.Close("End Sub")
            w.Line()
            w.Open("Public Overrides Sub Reset()")
            w.Line("MyBase.Reset()")
            w.Line("Background = New " & _className & "bg()")
            w.Line("BG = DirectCast(Background, " & _className & "bg)")
            w.Line("_cleared = False")
            w.Line("_boss = Nothing")
            w.Close("End Sub")
            w.Line()
        End Sub

        Private Sub EmitAction(w As CodeWriter)
            w.Open("Public Overrides Sub Action()")
            w.Line("If BG IsNot Nothing Then BG.Render()")
            w.Line("SpawnTimeline()")
            w.Line("RunCues()")
            w.Close("End Sub")
            w.Line()
        End Sub

        Private Sub EmitSpawnTimeline(w As CodeWriter)
            w.Open("Private Sub SpawnTimeline()")
            w.Line("If _cleared Then Return")
            Dim trackIndex = 0
            For Each track In _stage.Tracks
                If track.Kind = TrackKind.Enemy Then
                    Dim template = _stage.FindTemplate(track.TemplateId)
                    If template IsNot Nothing AndAlso Not track.IsMuted Then
                        Dim clipIndex = 0
                        For Each clip In track.Clips
                            EmitSpawnCondition(w, clip, trackIndex, clipIndex)
                            clipIndex += 1
                        Next
                    End If
                    trackIndex += 1
                End If
            Next
            w.Close("End Sub")
            w.Line()
        End Sub

        Private Sub EmitSpawnCondition(w As CodeWriter, clip As TimelineClip, trackIndex As Integer, clipIndex As Integer)
            Dim methodName = SpawnMethodName(trackIndex, clipIndex)
            Dim start = Math.Max(0, clip.StartTick)
            Dim count = Math.Max(1, clip.Count)
            If count = 1 Then
                w.Open("If Ticks = " & start & " Then")
                w.Line(methodName & "(0)")
                w.Close("End If")
                Return
            End If
            Dim interval = Math.Max(0, clip.IntervalTicks)
            If interval = 0 Then
                Dim loopVar = "i_" & trackIndex.ToString(CultureInfo.InvariantCulture) & "_" & clipIndex.ToString(CultureInfo.InvariantCulture)
                w.Open("If Ticks = " & start & " Then")
                w.Open("For " & loopVar & " As Integer = 0 To " & (count - 1).ToString(CultureInfo.InvariantCulture))
                w.Line(methodName & "(" & loopVar & ")")
                w.Close("Next")
                w.Close("End If")
                Return
            End If
            Dim endTick = start + (count - 1) * interval
            w.Open("If Ticks >= " & start & " AndAlso Ticks <= " & endTick & " AndAlso (Ticks - " & start & ") Mod " & interval & " = 0 Then")
            w.Line(methodName & "(CInt((Ticks - " & start & ") \ " & interval & "))")
            w.Close("End If")
        End Sub

        Private Function SpawnMethodName(trackIndex As Integer, clipIndex As Integer) As String
            Return "Spawn_" & trackIndex.ToString(CultureInfo.InvariantCulture) & "_" & clipIndex.ToString(CultureInfo.InvariantCulture)
        End Function

        Private Sub EmitCues(w As CodeWriter)
            w.Open("Private Sub RunCues()")
            For Each track In _stage.Tracks
                If track.Kind <> TrackKind.Cue OrElse track.IsMuted Then Continue For
                For Each clip In track.Clips
                    Dim tick = Math.Max(0, clip.StartTick)
                    Select Case clip.Cue
                        Case CueKind.Music
                            w.Line("If Ticks = " & tick & " Then Stage.Showmusic(" & VBCodeGenerator.Quote(clip.CueText) & ")")
                        Case CueKind.ClearBullets
                            w.Line("If Ticks = " & tick & " Then STG.ClearBullet()")
                        Case CueKind.Shake
                            w.Line("If Ticks = " & tick & " Then STG.ShakeFrame = " & Math.Max(6, clip.CueValue).ToString(CultureInfo.InvariantCulture))
                        Case CueKind.EndStage
                            w.Open("If Ticks = " & tick & " AndAlso Not _cleared Then")
                            w.Line("_cleared = True")
                            w.Line("Me.Clear()")
                            w.Line("Return")
                            w.Close("End If")
                    End Select
                Next
            Next
            w.Close("End Sub")
            w.Line()
        End Sub

        Private Sub EmitSpawnMethods(w As CodeWriter)
            Dim trackIndex = 0
            For Each track In _stage.Tracks
                If track.Kind = TrackKind.Enemy Then
                    Dim template = _stage.FindTemplate(track.TemplateId)
                    If template IsNot Nothing AndAlso Not track.IsMuted Then
                        Dim clipIndex = 0
                        For Each clip In track.Clips
                            EmitSpawnMethod(w, template, clip, trackIndex, clipIndex)
                            clipIndex += 1
                        Next
                    End If
                    trackIndex += 1
                End If
            Next
        End Sub

        Private Sub EmitSpawnMethod(w As CodeWriter, template As EnemyTemplate, clip As TimelineClip, trackIndex As Integer, clipIndex As Integer)
            w.Open("Private Sub " & SpawnMethodName(trackIndex, clipIndex) & "(i As Integer)")
            w.Line("Dim px As Double = " & VBCodeGenerator.Num(clip.X))
            w.Line("Dim py As Double = " & VBCodeGenerator.Num(clip.Y))
            Dim count = Math.Max(1, clip.Count)
            Dim spacing = Math.Max(0, clip.Spacing)
            Select Case clip.Formation
                Case FormationKind.LineHorizontal
                    Dim center = (count - 1) / 2.0
                    w.Line("px += (i - " & VBCodeGenerator.Num(center) & ") * " & VBCodeGenerator.Num(spacing))
                Case FormationKind.LineVertical
                    w.Line("py += i * " & VBCodeGenerator.Num(spacing))
                Case FormationKind.Ring
                    Dim stepAngle = If(count > 0, clip.FormationAngle / count, 0)
                    w.Line("Dim ang As Double = (" & VBCodeGenerator.Num(clip.StartAngle) & " + i * " & VBCodeGenerator.Num(stepAngle) & ") / 180.0 * Math.PI")
                    w.Line("px += " & VBCodeGenerator.Num(spacing) & " * Math.Sin(ang)")
                    w.Line("py -= " & VBCodeGenerator.Num(spacing) & " * Math.Cos(ang)")
                Case FormationKind.Arc
                    Dim stepAngle = If(count > 1, clip.FormationAngle / (count - 1), 0)
                    w.Line("Dim ang As Double = (" & VBCodeGenerator.Num(clip.StartAngle) & " + i * " & VBCodeGenerator.Num(stepAngle) & ") / 180.0 * Math.PI")
                    w.Line("px += " & VBCodeGenerator.Num(spacing) & " * Math.Sin(ang)")
                    w.Line("py -= " & VBCodeGenerator.Num(spacing) & " * Math.Cos(ang)")
                Case FormationKind.RandomBox
                    w.Line("px += (Rnd() * 2.0 - 1.0) * " & VBCodeGenerator.Num(spacing))
                    w.Line("py += (Rnd() * 2.0 - 1.0) * " & VBCodeGenerator.Num(spacing))
            End Select
            If clip.JitterX > 0 Then
                w.Line("px += (Rnd() * 2.0 - 1.0) * " & VBCodeGenerator.Num(clip.JitterX))
            End If
            If clip.JitterY > 0 Then
                w.Line("py += (Rnd() * 2.0 - 1.0) * " & VBCodeGenerator.Num(clip.JitterY))
            End If
            Dim typeInfo = EngineCatalog.EnemyType(template.EnemyType)
            If template.IsBoss Then
                w.Line("Dim e As New Enemy.Boss(CByte(" & template.BossId.ToString(CultureInfo.InvariantCulture) & "), px, py)")
            Else
                w.Line("Dim e As New Enemy(EnemyType." & typeInfo.Name & ", CByte(" & template.Color & "), px, py, " &
                       VBCodeGenerator.Num(template.HP) & ", " & VBCodeGenerator.Quote(If(template.DropItems, "")) & ", " &
                       template.Life.ToString(CultureInfo.InvariantCulture) & ", " & VBCodeGenerator.Num(template.Speed) & ", " &
                       VBCodeGenerator.Num(template.Direction) & ", " & VBCodeGenerator.Num(template.Defence) & ")")
            End If
            Dim info As BehaviorInfo = Nothing
            If _enemyBehaviors.TryGetValue(template.Id, info) AndAlso info.HasBehavior Then
                w.Line("Dim bh As New " & info.ClassName & "(e, " & VBCodeGenerator.Num(template.HP) & ")")
                If template.IsBoss Then w.Line("e.Init = AddressOf bh.Init")
                w.Line("e.Act = AddressOf bh.Tick")
            End If
            If template.IsBoss Then w.Line("_boss = e")
            w.Line("STG.Add(e)")
            w.Close("End Sub")
            w.Line()
        End Sub

        Private Sub EmitDialogueMethods(w As CodeWriter)
            For Each dialogue In _stage.Dialogues
                If dialogue Is Nothing Then Continue For
                Dim methodName = DialogueMethodName(dialogue.Id)
                Dim finishedName = DialogueFinishedMethodName(dialogue.Id)
                w.Open("Public Sub " & methodName & "()")
                w.Line("If STG.DialogArea Is Nothing Then Return")
                Dim illustrationMap = DialogueCsv.BuildIllustrationMap(dialogue)
                w.Line("Dim csv As String = " & VBCodeGenerator.Quote(DialogueCsv.ToCsv(dialogue, illustrationMap)))
                Dim maxIllustration = Math.Max(Math.Max(0, illustrationMap.PlayerIds.Count - 1),
                                               Math.Max(0, illustrationMap.EnemyIds.Count - 1))
                For Each line In dialogue.Lines
                    If line IsNot Nothing Then maxIllustration = Math.Max(maxIllustration, Math.Max(0, line.Illustration))
                Next
                If maxIllustration >= 0 Then
                    w.Line("Dim playerImages(" & maxIllustration & ") As System.Windows.Media.ImageBrush")
                    w.Line("Dim enemyImages(" & maxIllustration & ") As System.Windows.Media.ImageBrush")
                    For i = 0 To maxIllustration
                        Dim playerImage = If(i < illustrationMap.PlayerIds.Count, DialogueImageExpression(illustrationMap.PlayerIds(i)), "New System.Windows.Media.ImageBrush()")
                        Dim enemyImage = If(i < illustrationMap.EnemyIds.Count, DialogueImageExpression(illustrationMap.EnemyIds(i)), "New System.Windows.Media.ImageBrush()")
                        w.Line("playerImages(" & i & ") = " & playerImage)
                        w.Line("enemyImages(" & i & ") = " & enemyImage)
                    Next
                Else
                    w.Line("Dim playerImages As System.Windows.Media.ImageBrush() = New System.Windows.Media.ImageBrush() {}")
                    w.Line("Dim enemyImages As System.Windows.Media.ImageBrush() = New System.Windows.Media.ImageBrush() {}")
                End If
                w.Line("STG.DialogArea.LoadPlayer(playerImages)")
                w.Line("STG.DialogArea.LoadEnemy(enemyImages)")
                w.Line("Dim reader As New System.IO.StreamReader(New System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(csv)))")
                w.Line("reader.ReadLine()")
                w.Line("reader.ReadLine()")
                w.Line("STG.DialogArea.LoadDialog(reader)")
                w.Line("AddHandler STG.DialogArea.Changed, AddressOf " & finishedName)
                w.Line("STG.DialogArea.Show()")
                w.Close("End Sub")
                w.Line()

                w.Open("Private Sub " & finishedName & "(position As Integer, speaker As Speakers)")
                w.Line("If position <> -1 Then Return")
                w.Line("RemoveHandler STG.DialogArea.Changed, AddressOf " & finishedName)
                If dialogue.AfterAction = DialogueAfterAction.NextStage Then
                    w.Line("If STG.CurrentStage >= 0 AndAlso STG.CurrentStage < STG.Stages.Count Then STG.Stages(STG.CurrentStage).Clear()")
                ElseIf dialogue.AfterAction = DialogueAfterAction.StartBoss Then
                    w.Line("If _boss IsNot Nothing AndAlso _boss.CurrentSpell < 0 AndAlso _boss.SpellCards.Count > 0 Then")
                    w.Line("    _boss.IsEnabled = True")
                    w.Line("    _boss.NextSpell()")
                    w.Line("End If")
                End If
                w.Close("End Sub")
                w.Line()
            Next
        End Sub

        Private Function DialogueImageExpression(resourceId As String) As String
            If String.IsNullOrWhiteSpace(resourceId) OrElse _resourceUris Is Nothing Then
                Return "New System.Windows.Media.ImageBrush()"
            End If
            Dim uri As String = Nothing
            If Not _resourceUris.TryGetValue(resourceId, uri) OrElse String.IsNullOrWhiteSpace(uri) Then
                Return "New System.Windows.Media.ImageBrush()"
            End If
            Return "New System.Windows.Media.ImageBrush(New System.Windows.Media.Imaging.BitmapImage(New System.Uri(" &
                   VBCodeGenerator.Quote(uri) & ", System.UriKind.Absolute)))"
        End Function

        Private Shared Function DialogueMethodName(id As String) As String
            Return "PlayDialogue_" & VBCodeGenerator.SanitizeIdentifier(id, "dialog")
        End Function

        Private Shared Function DialogueFinishedMethodName(id As String) As String
            Return "DialogueFinished_" & VBCodeGenerator.SanitizeIdentifier(id, "dialog")
        End Function

        Private Sub EmitBehaviorClasses(w As CodeWriter)
            For Each info In _enemyBehaviors.Values
                If info.HasBehavior Then
                    Dim gen As New ProgramEmitter(_stage, info, _bulletBehaviors)
                    gen.Emit(w)
                End If
            Next
            For Each info In _bulletBehaviors.Values
                If info.HasBehavior Then
                    Dim gen As New ProgramEmitter(_stage, info, _bulletBehaviors)
                    gen.Emit(w)
                End If
            Next
        End Sub

        Private Function BuildBackgroundXaml() As String
            If _stage.BackgroundLayers.Count > 0 Then
                Return New BackgroundSceneGenerator(_stage, _className, _resourceUris).BuildXaml()
            End If
            Dim sb As New StringBuilder()
            sb.AppendLine("<UserControl x:Class=""" & _className & "bg""")
            sb.AppendLine("             xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""")
            sb.AppendLine("             xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""")
            sb.AppendLine("             xmlns:mc=""http://schemas.openxmlformats.org/markup-compatibility/2006""")
            sb.AppendLine("             xmlns:d=""http://schemas.microsoft.com/expression/blend/2008""")
            sb.AppendLine("             mc:Ignorable=""d"" d:DesignHeight=""448"" d:DesignWidth=""384"">")
            sb.AppendLine("    <Canvas x:Name=""Root"" ClipToBounds=""True"" Width=""384"" Height=""448""/>")
            sb.AppendLine("</UserControl>")
            Return sb.ToString()
        End Function

        Private Function BuildBackgroundCode() As String
            If _stage.BackgroundLayers.Count > 0 Then
                Return New BackgroundSceneGenerator(_stage, _className, _resourceUris).BuildCode()
            End If
            Dim w As New CodeWriter()
            w.Line("Imports System")
            w.Line("Imports System.Collections.Generic")
            w.Line("Imports System.Windows")
            w.Line("Imports System.Windows.Controls")
            w.Line("Imports System.Windows.Media")
            w.Line("Imports System.Windows.Shapes")
            w.Line()
            w.Open("Public Class " & _className & "bg")
            w.Line("Inherits UserControl")
            w.Line()
            w.Open("Public Sub New()")
            w.Line("InitializeComponent()")
            w.Close("End Sub")
            w.Line()
            w.Open("Public Sub Render()")
            w.Close("End Sub")
            w.Close("End Class")
            Return w.ToString()
        End Function

        Private Function BuildProjectFile() As String
            Dim repo = If(String.IsNullOrWhiteSpace(_enginePath), "..\..\AgSTG", _enginePath)
            repo = repo.TrimEnd("\"c, "/"c)
            Dim sb As New StringBuilder()
            sb.AppendLine("<Project Sdk=""Microsoft.NET.Sdk"">")
            sb.AppendLine()
            sb.AppendLine("  <PropertyGroup>")
            sb.AppendLine("    <TargetFramework>net10.0-windows</TargetFramework>")
            sb.AppendLine("    <RootNamespace>" & _className & "</RootNamespace>")
            sb.AppendLine("    <AssemblyName>" & _className & "</AssemblyName>")
            sb.AppendLine("    <UseWPF>true</UseWPF>")
            sb.AppendLine("    <OptionStrict>Off</OptionStrict>")
            sb.AppendLine("    <!-- 可用 -p:AgSTGRepo=... 覆盖引擎仓库路径 -->")
            sb.AppendLine("    <AgSTGRepo Condition=""'$(AgSTGRepo)' == ''"">" & XmlPath(repo) & "</AgSTGRepo>")
            sb.AppendLine("  </PropertyGroup>")
            sb.AppendLine()
            sb.AppendLine("  <ItemGroup>")
            sb.AppendLine("    <Import Include=""System.Windows"" />")
            sb.AppendLine("    <Import Include=""System.Windows.Controls"" />")
            sb.AppendLine("    <Import Include=""System.Windows.Data"" />")
            sb.AppendLine("    <Import Include=""System.Windows.Documents"" />")
            sb.AppendLine("    <Import Include=""System.Windows.Input"" />")
            sb.AppendLine("    <Import Include=""System.Windows.Media"" />")
            sb.AppendLine("    <Import Include=""System.Windows.Media.Imaging"" />")
            sb.AppendLine("    <Import Include=""System.Windows.Navigation"" />")
            sb.AppendLine("    <Import Include=""System.Windows.Shapes"" />")
            sb.AppendLine("  </ItemGroup>")
            sb.AppendLine()
            sb.AppendLine("  <ItemGroup>")
            sb.AppendLine("    <ProjectReference Include=""$(AgSTGRepo)\AgSTG.Core\AgSTG.Core.vbproj"" />")
            sb.AppendLine("    <ProjectReference Include=""$(AgSTGRepo)\AgSTG\AgSTG.vbproj"" />")
            sb.AppendLine("    <ProjectReference Include=""$(AgSTGRepo)\ResourcePack\ResourcePack.vbproj"" />")
            sb.AppendLine("  </ItemGroup>")
            sb.AppendLine()
            sb.AppendLine("  <ItemGroup>")
            sb.AppendLine("    <Reference Include=""ManagedBass"">")
            sb.AppendLine("      <HintPath>$(AgSTGRepo)\dll\Bass\ManagedBass.dll</HintPath>")
            sb.AppendLine("    </Reference>")
            sb.AppendLine("  </ItemGroup>")
            sb.AppendLine()
            sb.AppendLine("</Project>")
            Return sb.ToString()
        End Function

        Private Function XmlPath(path As String) As String
            Return path.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
        End Function

        Private Function BuildIntegration() As String
            Dim sb As New StringBuilder()
            sb.AppendLine("【在 TH07 中加载本关卡】")
            sb.AppendLine("1. 把 " & _className & " 文件夹放到 AgSTG 解决方案根目录下，并把它加入 AgSTG.sln。")
            sb.AppendLine("2. 为 TH07 项目添加对 " & _className & ".vbproj 的项目引用。")
            sb.AppendLine("3. 在 TH07\MainWindow.xaml.vb 的 Start() 中加入：")
            sb.AppendLine()
            sb.AppendLine("    STG.Stages.Add(New " & _className & "." & _className & "(Difficulty." & _stage.Difficulty.ToString() & "))")
            sb.AppendLine()
            sb.AppendLine("关卡长度：" & _stage.LengthTicks & " 帧（" & (_stage.LengthTicks / 60.0).ToString("0.0", CultureInfo.InvariantCulture) & " 秒）")
            Return sb.ToString()
        End Function
    End Class

    ''' <summary>把一段积木程序翻译成一个带 Tick 方法的 VB 类。</summary>
    Friend Class ProgramEmitter
        Private ReadOnly _stage As StageDocument
        Private ReadOnly _info As BehaviorInfo
        Private ReadOnly _bulletBehaviors As Dictionary(Of String, BehaviorInfo)
        Private ReadOnly _varMap As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
        Private _localIndex As Integer = 0
        Private _needsTimer As Boolean = False

        Public Sub New(stage As StageDocument, info As BehaviorInfo, bulletBehaviors As Dictionary(Of String, BehaviorInfo))
            _stage = stage
            _info = info
            _bulletBehaviors = bulletBehaviors
        End Sub

        Public Sub Emit(w As CodeWriter)
            CollectVariables()
            For Each script In _info.Program.Scripts
                If ContainsWait(script.Children) Then _needsTimer = True
            Next
            For Each spellProgram In _info.SpellPrograms
                For Each script In spellProgram.Program.Scripts
                    If ContainsWait(script.Children) Then _needsTimer = True
                Next
            Next

            Dim hostType = If(_info.Scope = ProgramScope.Enemy, "Enemy", "Bullet")
            w.Open("Friend Class " & _info.ClassName)
            w.Line("Private ReadOnly Host As " & hostType)
            If _info.BossTemplate IsNot Nothing Then w.Line("Private ReadOnly Boss As Enemy.Boss")
            If _info.Scope = ProgramScope.Enemy Then w.Line("Private ReadOnly MaxHP As Double")
            w.Line("Private Halted As Boolean")
            For i = 0 To _info.Program.Scripts.Count - 1
                Dim script = _info.Program.Scripts(i)
                If UsesOnce(script) Then w.Line("Private _once" & i & " As Boolean")
                If script.Definition IsNot Nothing AndAlso script.Definition.HatKind = HatKind.SpellStart Then
                    w.Line("Private _spellStart" & i & " As Boolean")
                End If
            Next
            For Each spellProgram In _info.SpellPrograms
                For j = 0 To spellProgram.Program.Scripts.Count - 1
                    w.Line("Private _spOnce" & spellProgram.SpellIndex & "_" & j & " As Boolean")
                Next
            Next
            For Each kv In _varMap
                w.Line("Private " & kv.Value & " As Double")
            Next
            If _needsTimer Then w.Line("Private ReadOnly _timers As New List(Of Timer)")
            w.Line()

            w.Open("Public Sub New(host As " & hostType & If(_info.Scope = ProgramScope.Enemy, ", maxHp As Double", "") & ")")
            w.Line("Me.Host = host")
            If _info.BossTemplate IsNot Nothing Then w.Line("Me.Boss = DirectCast(host, Enemy.Boss)")
            If _info.Scope = ProgramScope.Enemy Then w.Line("Me.MaxHP = maxHp")
            w.Close("End Sub")
            w.Line()

            If _info.BossTemplate IsNot Nothing Then EmitBossInit(w)
            EmitTick(w)
            If _needsTimer Then EmitTimerHelpers(w)
            For i = 0 To _info.Program.Scripts.Count - 1
                EmitScriptMethod(w, _info.Program.Scripts(i), i)
            Next
            EmitSpellScriptMethods(w)
            w.Close("End Class")
            w.Line()
            If _info.BossTemplate IsNot Nothing Then EmitBossSpellClass(w)
        End Sub

        Private Function UsesOnce(script As BlockNode) As Boolean
            If script Is Nothing Then Return False
            Dim def = script.Definition
            If def Is Nothing Then Return False
            Return def.HatKind = HatKind.Spawn OrElse def.HatKind = HatKind.AtTime OrElse def.HatKind = HatKind.HpBelow OrElse def.HatKind = HatKind.Killed
        End Function

        Private Sub CollectVariables()
            For Each script In _info.Program.Scripts
                CollectVariablesFromBlock(script)
            Next
            For Each spellProgram In _info.SpellPrograms
                For Each script In spellProgram.Program.Scripts
                    CollectVariablesFromBlock(script)
                Next
            Next
        End Sub

        Private Sub CollectVariablesFromBlock(block As BlockNode)
            If block Is Nothing Then Return
            If block.TypeId = "set_var" OrElse block.TypeId = "add_var" Then
                AddVariable(block.GetField("name"))
            End If
            If block.TypeId = "if" AndAlso block.GetField("subj") = "var" Then
                AddVariable(block.GetField("varName"))
            End If
            For Each f In block.Fields
                If ValueExpression.IsReporter(f.Value) AndAlso ValueExpression.ReporterKind(f.Value) = "var" Then
                    AddVariable(ValueExpression.ReporterArg(f.Value))
                End If
            Next
            For Each c In block.Children
                CollectVariablesFromBlock(c)
            Next
            For Each c In block.ElseChildren
                CollectVariablesFromBlock(c)
            Next
        End Sub

        Private Sub AddVariable(name As String)
            Dim key = If(String.IsNullOrWhiteSpace(name), "n", name.Trim())
            If _varMap.ContainsKey(key) Then Return
            Dim suffix = VBCodeGenerator.SanitizeIdentifier(key, "var")
            Dim candidate = "V_" & suffix
            Dim i = 2
            While _varMap.Values.Contains(candidate, StringComparer.OrdinalIgnoreCase)
                candidate = "V_" & suffix & i.ToString(CultureInfo.InvariantCulture)
                i += 1
            End While
            _varMap(key) = candidate
        End Sub

        Private Function VarField(name As String) As String
            Dim key = If(String.IsNullOrWhiteSpace(name), "n", name.Trim())
            If Not _varMap.ContainsKey(key) Then AddVariable(key)
            Return _varMap(key)
        End Function

        Private Sub EmitTick(w As CodeWriter)
            w.Open("Public Sub Tick()")
            w.Line("If Halted Then Return")
            If _needsTimer Then w.Line("RunTimers()")
            For i = 0 To _info.Program.Scripts.Count - 1
                Dim script = _info.Program.Scripts(i)
                Dim def = script.Definition
                If def Is Nothing OrElse Not def.IsHat Then Continue For
                If _info.BossTemplate IsNot Nothing Then
                    If def.HatKind = HatKind.BossInit Then Continue For
                    If def.HatKind = HatKind.SpellStart Then
                        EmitBossSpellHat(w, script, i, True)
                        Continue For
                    End If
                    If def.HatKind = HatKind.SpellEveryFrame Then
                        EmitBossSpellHat(w, script, i, False)
                        Continue For
                    End If
                ElseIf def.HatKind = HatKind.BossInit OrElse def.HatKind = HatKind.SpellStart OrElse def.HatKind = HatKind.SpellEveryFrame Then
                    Continue For
                End If
                EmitHatCall(w, script, i, def)
            Next
            EmitSpellProgramTicks(w)
            w.Close("End Sub")
            w.Line()
        End Sub

        Private Sub EmitSpellProgramTicks(w As CodeWriter)
            For Each spellProgram In _info.SpellPrograms
                Dim spellIndex = spellProgram.SpellIndex
                Dim baseCondition = "Boss.CurrentSpell = " & spellIndex & " AndAlso Boss.SpellCards.Count > " & spellIndex &
                                    " AndAlso Boss.SpellCards(" & spellIndex & ").IsEnabled"
                Dim spellTicks = "Boss.SpellCards(" & spellIndex & ").Ticks"
                For j = 0 To spellProgram.Program.Scripts.Count - 1
                    Dim script = spellProgram.Program.Scripts(j)
                    If script Is Nothing OrElse script.Definition Is Nothing Then Continue For
                    Dim def = script.Definition
                    If Not def.IsHat Then Continue For
                    Dim method = "SP" & spellIndex & "_" & j
                    Select Case def.HatKind
                        Case HatKind.SpellUsualStart
                            w.Open("If " & baseCondition & SpellPhaseCondition(spellIndex, "usual") & " AndAlso Not _spOnce" & spellIndex & "_" & j & " Then")
                            w.Line("_spOnce" & spellIndex & "_" & j & " = True")
                            w.Line(method & "()")
                            w.Close("End If")
                        Case HatKind.SpellFormalStart
                            w.Open("If " & baseCondition & SpellPhaseCondition(spellIndex, "spell") & " AndAlso Not _spOnce" & spellIndex & "_" & j & " Then")
                            w.Line("_spOnce" & spellIndex & "_" & j & " = True")
                            w.Line(method & "()")
                            w.Close("End If")
                        Case HatKind.SpellUsualEveryFrame
                            w.Line("If " & baseCondition & SpellPhaseCondition(spellIndex, "usual") & " Then " & method & "()")
                        Case HatKind.SpellFormalEveryFrame
                            w.Line("If " & baseCondition & SpellPhaseCondition(spellIndex, "spell") & " Then " & method & "()")
                        Case HatKind.Spawn, HatKind.SpellStart
                            w.Open("If " & baseCondition & " AndAlso Not _spOnce" & spellIndex & "_" & j & " Then")
                            w.Line("_spOnce" & spellIndex & "_" & j & " = True")
                            w.Line(method & "()")
                            w.Close("End If")
                        Case HatKind.EveryFrame, HatKind.SpellEveryFrame
                            w.Line("If " & baseCondition & " Then " & method & "()")
                        Case HatKind.EveryN
                            Dim n = Math.Max(1, CInt(script.GetDouble("n", 10)))
                            w.Line("If " & baseCondition & " AndAlso " & spellTicks & " Mod " & n & " = 0 Then " & method & "()")
                        Case HatKind.AtTime
                            Dim t = CInt(script.GetDouble("t", 0))
                            w.Line("If " & baseCondition & " AndAlso " & spellTicks & " = " & t & " Then " & method & "()")
                        Case HatKind.TimeBetween
                            Dim a = CInt(script.GetDouble("a", 0))
                            Dim b = CInt(script.GetDouble("b", 60))
                            If a > b Then
                                Dim tmp = a
                                a = b
                                b = tmp
                            End If
                            w.Line("If " & baseCondition & " AndAlso " & spellTicks & " >= " & a & " AndAlso " & spellTicks & " <= " & b & " Then " & method & "()")
                        Case HatKind.HpBelow
                            Dim pct = script.GetDouble("pct", 50)
                            w.Line("If " & baseCondition & " AndAlso Boss.HP <= Boss.SpellCards(" & spellIndex & ").SpellHP * " &
                                   VBCodeGenerator.Num(pct / 100.0) & " Then " & method & "()")
                        Case HatKind.PlayerNear
                            Dim dist = script.GetDouble("dist", 120)
                            w.Line("If " & baseCondition & " AndAlso Math.Sqrt((Boss.X - STG.Player.X) * (Boss.X - STG.Player.X) + (Boss.Y - STG.Player.Y) * (Boss.Y - STG.Player.Y)) < " &
                                   VBCodeGenerator.Num(dist) & " Then " & method & "()")
                        Case HatKind.Killed
                            w.Line("If " & baseCondition & " AndAlso Boss.HP <= 0 Then " & method & "()")
                    End Select
                Next
            Next
        End Sub

        Private Sub EmitSpellScriptMethods(w As CodeWriter)
            For Each spellProgram In _info.SpellPrograms
                For j = 0 To spellProgram.Program.Scripts.Count - 1
                    Dim script = spellProgram.Program.Scripts(j)
                    w.Open("Private Sub SP" & spellProgram.SpellIndex & "_" & j & "()")
                    EmitStack(w, script.Children)
                    w.Close("End Sub")
                    w.Line()
                Next
            Next
        End Sub

        Private Function SpellPhaseCondition(spellIndex As Integer, phase As String) As String
            Dim card = "Boss.SpellCards(" & spellIndex.ToString(CultureInfo.InvariantCulture) & ")"
            Select Case If(phase, "").Trim().ToLowerInvariant()
                Case "usual" : Return " AndAlso " & card & ".HaveUsual AndAlso Not " & card & ".AtSpell"
                Case "spell" : Return " AndAlso " & card & ".AtSpell"
                Case Else : Return ""
            End Select
        End Function

        Private Sub EmitBossSpellHat(w As CodeWriter, script As BlockNode, index As Integer, startOnly As Boolean)
            Dim spell = Math.Max(1, CInt(script.GetDouble("spell", 1))) - 1
            Dim method = "S" & index.ToString(CultureInfo.InvariantCulture)
            Dim condition = "Boss.CurrentSpell = " & spell & " AndAlso Boss.SpellCards.Count > " & spell &
                            " AndAlso Boss.SpellCards(" & spell & ").IsEnabled" &
                            SpellPhaseCondition(spell, script.GetField("phase"))
            If startOnly Then
                w.Open("If " & condition & " AndAlso Not _spellStart" & index & " Then")
                w.Line("_spellStart" & index & " = True")
                w.Line(method & "()")
                w.Close("End If")
            Else
                w.Line("If " & condition & " Then " & method & "()")
            End If
        End Sub

        Private Sub EmitBossInit(w As CodeWriter)
            Dim template = _info.BossTemplate
            Dim spells = ResolveBossSpells(template)
            w.Open("Public Sub Init()")
            If template.UseResourceBossTextures Then
                EmitBossTextureList(w, "Boss.NormalTextures", template.BossNormalTextureIds)
            End If
            If template.UseResourceBossTextures Then
                EmitBossTextureList(w, "Boss.MoveTextures", template.BossMoveTextureIds)
            End If
            For i = 0 To spells.Count - 1
                Dim spell = spells(i)
                Dim varName = "sp" & i.ToString(CultureInfo.InvariantCulture)
                w.Line("Dim " & varName & " As New " & _info.ClassName & "_Spell(Boss)")
                w.Line(varName & ".Name = " & VBCodeGenerator.Quote(spell.Name))
                w.Line(varName & ".SpellName = " & VBCodeGenerator.Quote(spell.Name))
                w.Line(varName & ".Type = SpellType." & spell.Type.ToString())
                w.Line(varName & ".HaveUsual = " & If(spell.HaveUsual, "True", "False"))
                w.Line(varName & ".UsualHP = " & VBCodeGenerator.Num(spell.UsualHP))
                w.Line(varName & ".UsualTime = " & spell.UsualTime.ToString(CultureInfo.InvariantCulture))
                w.Line(varName & ".SpellHP = " & VBCodeGenerator.Num(spell.SpellHP))
                w.Line(varName & ".SpellTime = " & spell.SpellTime.ToString(CultureInfo.InvariantCulture))
                w.Line(varName & ".Score = " & spell.Score.ToString(CultureInfo.InvariantCulture))
                w.Line(varName & ".Items = " & VBCodeGenerator.Quote(If(spell.Items, "")))
                w.Line(varName & ".StageCount = CByte(" & spell.StageCount.ToString(CultureInfo.InvariantCulture) & ")")
                w.Line("Boss.SpellCards.Add(" & varName & ")")
            Next
            w.Line("Boss.Layer3.Width = 96")
            w.Line("Boss.Layer3.Height = 96")
            w.Line("Canvas.SetLeft(Boss.Layer3, 16)")
            w.Line("Canvas.SetTop(Boss.Layer3, 16)")
            If template.UseResourceBossTextures Then
                w.Line("If Boss.NormalTextures.Count > 0 Then Boss.Layer3.Fill = Boss.NormalTextures(0)")
            End If
            w.Line("STG.NameArea.Initialize(" & VBCodeGenerator.Quote(If(String.IsNullOrWhiteSpace(template.BossName), template.Name, template.BossName)) & ", " & spells.Count & ")")
            For i = 0 To _info.Program.Scripts.Count - 1
                Dim script = _info.Program.Scripts(i)
                If script.Definition IsNot Nothing AndAlso script.Definition.HatKind = HatKind.BossInit Then
                    w.Line("S" & i.ToString(CultureInfo.InvariantCulture) & "()")
                End If
            Next
            w.Close("End Sub")
            w.Line()
        End Sub

        Private Sub EmitBossTextureList(w As CodeWriter, target As String, ids As List(Of String))
            If ids Is Nothing Then Return
            For Each id In ids
                Dim uri As String = Nothing
                If _info.ResourceUris Is Nothing OrElse Not _info.ResourceUris.TryGetValue(id, uri) OrElse String.IsNullOrWhiteSpace(uri) Then
                    Continue For
                End If
                w.Line(target & ".Add(New System.Windows.Media.ImageBrush(New System.Windows.Media.Imaging.BitmapImage(New System.Uri(" &
                       VBCodeGenerator.Quote(uri) & ", System.UriKind.Absolute))))")
            Next
        End Sub

        Private Function ResolveBossSpells(template As EnemyTemplate) As List(Of BossSpellCard)
            Dim result As New List(Of BossSpellCard)
            If template Is Nothing Then Return result
            For Each id In template.SpellCardIds
                Dim spell = _stage.FindSpell(id)
                If spell IsNot Nothing Then result.Add(spell)
            Next
            If result.Count = 0 AndAlso template.SpellCards IsNot Nothing Then
                For Each spell In template.SpellCards
                    If spell IsNot Nothing Then result.Add(spell)
                Next
            End If
            Return result
        End Function

        Private Sub EmitBossSpellClass(w As CodeWriter)
            w.Open("Friend Class " & _info.ClassName & "_Spell")
            w.Line("Inherits SpellCard")
            w.Open("Public Sub New(owner As Enemy.Boss)")
            w.Line("MyBase.New(owner)")
            w.Close("End Sub")
            w.Close("End Class")
            w.Line()
        End Sub

        Private Sub EmitHatCall(w As CodeWriter, script As BlockNode, index As Integer, def As BlockDefinition)
            Dim method = "S" & index.ToString(CultureInfo.InvariantCulture)
            Select Case def.HatKind
                Case HatKind.Spawn
                    If _info.Scope = ProgramScope.Bullet Then
                        w.Line("If Host.Ticks = 11 Then " & method & "()")
                    Else
                        w.Line("If Host.Ticks = 0 Then " & method & "()")
                    End If
                Case HatKind.EveryFrame
                    w.Line(method & "()")
                Case HatKind.EveryN
                    Dim n = Math.Max(1, CInt(script.GetDouble("n", 10)))
                    w.Line("If Host.Ticks Mod " & n & " = 0 Then " & method & "()")
                Case HatKind.AtTime
                    Dim t = CInt(script.GetDouble("t", 0))
                    w.Open("If Host.Ticks = " & t & " AndAlso Not _once" & index & " Then")
                    w.Line("_once" & index & " = True")
                    w.Line(method & "()")
                    w.Close("End If")
                Case HatKind.TimeBetween
                    Dim a = script.GetDouble("a", 0)
                    Dim b = script.GetDouble("b", 60)
                    If a > b Then
                        Dim tmp = a
                        a = b
                        b = tmp
                    End If
                    w.Line("If Host.Ticks >= " & VBCodeGenerator.Num(a) & " AndAlso Host.Ticks <= " & VBCodeGenerator.Num(b) & " Then " & method & "()")
                Case HatKind.HpBelow
                    Dim pct = script.GetDouble("pct", 50)
                    w.Open("If Not _once" & index & " AndAlso Host.HP <= MaxHP * " & VBCodeGenerator.Num(pct / 100.0) & " Then")
                    w.Line("_once" & index & " = True")
                    w.Line(method & "()")
                    w.Close("End If")
                Case HatKind.Killed
                    w.Open("If Not _once" & index & " AndAlso Host.HP <= 0 Then")
                    w.Line("_once" & index & " = True")
                    w.Line(method & "()")
                    w.Close("End If")
                Case HatKind.PlayerNear
                    w.Line("If Host.GetDistance(STG.Player) < " & VBCodeGenerator.Num(script.GetDouble("dist", 120)) & " Then " & method & "()")
            End Select
        End Sub

        Private Sub EmitTimerHelpers(w As CodeWriter)
            w.Open("Private Sub RunTimers()")
            w.Line("If _timers.Count = 0 Then Return")
            w.Line("Dim due As New List(Of Timer)")
            w.Open("For i As Integer = _timers.Count - 1 To 0 Step -1")
            w.Line("_timers(i).Frames -= 1")
            w.Open("If _timers(i).Frames <= 0 Then")
            w.Line("due.Add(_timers(i))")
            w.Line("_timers.RemoveAt(i)")
            w.Close("End If")
            w.Close("Next")
            w.Open("For Each t In due")
            w.Line("t.Action.Invoke()")
            w.Close("Next")
            w.Close("End Sub")
            w.Line()
            w.Open("Private Sub After(frames As Integer, action As Action)")
            w.Line("_timers.Add(New Timer(Math.Max(0, frames), action))")
            w.Close("End Sub")
            w.Line()
            w.Open("Private Class Timer")
            w.Line("Public Frames As Integer")
            w.Line("Public Action As Action")
            w.Open("Public Sub New(frames As Integer, action As Action)")
            w.Line("Me.Frames = frames")
            w.Line("Me.Action = action")
            w.Close("End Sub")
            w.Close("End Class")
            w.Line()
        End Sub

        Private Sub EmitScriptMethod(w As CodeWriter, script As BlockNode, index As Integer)
            w.Open("Private Sub S" & index.ToString(CultureInfo.InvariantCulture) & "()")
            EmitStack(w, script.Children)
            w.Close("End Sub")
            w.Line()
        End Sub

        ''' <summary>生成一段积木栈；遇到『等待』时把余下积木包进延迟委托。</summary>
        Private Sub EmitStack(w As CodeWriter, blocks As List(Of BlockNode))
            If blocks Is Nothing Then Return
            Dim i = 0
            While i < blocks.Count
                Dim block = blocks(i)
                If block Is Nothing Then
                    i += 1
                    Continue While
                End If
                If block.TypeId = "wait" Then
                    Dim frames = EmitValue(block.GetField("frames"))
                    Dim rest = blocks.Skip(i + 1).ToList()
                    w.Line("Me.After(CInt(Math.Max(0, " & frames & ")), Sub()")
                    w.Indent += 1
                    EmitStack(w, rest)
                    w.Indent -= 1
                    w.Line("End Sub)")
                    Return
                End If
                EmitStatement(w, block)
                i += 1
            End While
        End Sub

        Private Sub EmitStatement(w As CodeWriter, block As BlockNode)
            Dim def = block.Definition
            If def Is Nothing Then Return
            Select Case block.TypeId
                Case "repeat"
                    Dim local = NextLocal("rep")
                    w.Line("Dim " & local & " As Integer = CInt(Math.Max(0, Math.Min(1000, CDbl(" & EmitValue(block.GetField("times")) & "))))")
                    Dim loopVar = NextLocal("i")
                    w.Open("For " & loopVar & " As Integer = 1 To " & local)
                    EmitStack(w, block.Children)
                    w.Close("Next")
                Case "if"
                    Dim cond = EmitCondition(block)
                    w.Open("If " & cond & " Then")
                    EmitStack(w, block.Children)
                    If block.ElseChildren IsNot Nothing AndAlso block.ElseChildren.Count > 0 Then
                        w.Close("Else")
                        w.Indent += 1
                        EmitStack(w, block.ElseChildren)
                        w.Close("End If")
                    Else
                        w.Close("End If")
                    End If
                Case "stop_script"
                    w.Line("Me.Halted = True")
                    w.Line("Return")
                Case "boss_next_spell"
                    If _info.BossTemplate IsNot Nothing Then w.Line("Boss.NextSpell()")
                Case "next_stage"
                    w.Line("If STG.CurrentStage >= 0 AndAlso STG.CurrentStage < STG.Stages.Count Then STG.Stages(STG.CurrentStage).Clear()")
                Case "play_dialogue"
                    Dim dialogueId = block.GetField("dialogue")
                    If Not String.IsNullOrWhiteSpace(dialogueId) AndAlso _stage.FindDialogue(dialogueId) IsNot Nothing Then
                        Dim stageClass = VBCodeGenerator.SanitizeIdentifier(_stage.CodeName, "StageCustom")
                        w.Line("If STG.CurrentStage >= 0 AndAlso STG.CurrentStage < STG.Stages.Count Then")
                        w.Line("    Dim __stage = TryCast(STG.Stages(STG.CurrentStage), " & stageClass & ")")
                        w.Line("    If __stage IsNot Nothing Then __stage.PlayDialogue_" & VBCodeGenerator.SanitizeIdentifier(dialogueId, "dialog") & "()")
                        w.Line("End If")
                    End If
                Case "play_music"
                    Dim musicTitle = If(block.GetField("title"), "").Trim()
                    If String.IsNullOrWhiteSpace(musicTitle) Then
                        Dim musicId = block.GetField("music")
                        Dim resourceName As String = Nothing
                        If _info.AudioNames IsNot Nothing AndAlso _info.AudioNames.TryGetValue(musicId, resourceName) Then musicTitle = resourceName
                    End If
                    If Not String.IsNullOrWhiteSpace(musicTitle) Then
                        w.Line("Stage.Showmusic(" & VBCodeGenerator.Quote(musicTitle) & ")")
                    End If
                Case "set_var"
                    w.Line(VarField(block.GetField("name")) & " = " & EmitValue(block.GetField("value")))
                Case "add_var"
                    w.Line(VarField(block.GetField("name")) & " += " & EmitValue(block.GetField("value")))
                Case "set_speed"
                    w.Line("Host.Speed = " & EmitValue(block.GetField("speed")))
                Case "add_speed"
                    w.Line("Host.Speed += " & EmitValue(block.GetField("delta")))
                Case "set_direction"
                    w.Line("Host.Direction = " & EmitValue(block.GetField("direction")))
                Case "add_direction"
                    w.Line("Host.Direction += " & EmitValue(block.GetField("delta")))
                Case "random_direction"
                    w.Line("Host.Direction = " & EmitValue(block.GetField("min")) & " + Rnd() * (" & EmitValue(block.GetField("max")) & " - " & EmitValue(block.GetField("min")) & ")")
                Case "aim_player"
                    w.Line("Host.Direction = System.Windows.Vector.AngleBetween(New System.Windows.Vector(0, -1), New System.Windows.Vector(STG.Player.X - Host.X, STG.Player.Y - Host.Y)) + " & EmitValue(block.GetField("offset")))
                Case "move_to"
                    Dim dx = NextLocal("dx")
                    Dim dy = NextLocal("dy")
                    w.Line("Dim " & dx & " As Double = " & EmitValue(block.GetField("x")) & " - Host.X")
                    w.Line("Dim " & dy & " As Double = " & EmitValue(block.GetField("y")) & " - Host.Y")
                    w.Line("Host.Direction = System.Windows.Vector.AngleBetween(New System.Windows.Vector(0, -1), New System.Windows.Vector(" & dx & ", " & dy & "))")
                    w.Line("Host.Speed = Math.Sqrt(" & dx & " * " & dx & " + " & dy & " * " & dy & ") / Math.Max(1.0, CDbl(" & EmitValue(block.GetField("frames")) & "))")
                Case "move_center"
                    Dim dx = NextLocal("dx")
                    Dim dy = NextLocal("dy")
                    w.Line("Dim " & dx & " As Double = 192.0 - Host.X")
                    w.Line("Dim " & dy & " As Double = 128.0 - Host.Y")
                    w.Line("Host.Direction = System.Windows.Vector.AngleBetween(New System.Windows.Vector(0, -1), New System.Windows.Vector(" & dx & ", " & dy & "))")
                    w.Line("Host.Speed = Math.Sqrt(" & dx & " * " & dx & " + " & dy & " * " & dy & ") / Math.Max(1.0, CDbl(" & EmitValue(block.GetField("frames")) & "))")
                Case "set_position"
                    w.Line("Host.X = " & EmitValue(block.GetField("x")))
                    w.Line("Host.Y = " & EmitValue(block.GetField("y")))
                Case "stop_move"
                    w.Line("Host.Speed = 0")
                Case "fire_ring"
                    EmitFireRing(w, block)
                Case "fire_aimed"
                    EmitFireAimed(w, block)
                Case "fire_single"
                    EmitFireSingle(w, block)
                Case "fire_laser"
                    EmitFireLaser(w, block)
                Case "clear_bullets"
                    w.Line("STG.ClearBullet()")
                Case "play_sound"
                    w.Line("ResourcePack.Sounds.PlaySound(ResourcePack.Sounds." & SoundName(block.GetField("sound")) & ", 0.4)")
                Case "shake_screen"
                    w.Line("STG.ShakeFrame = CInt(" & EmitValue(block.GetField("frames")) & ")")
            End Select
        End Sub

        Private Sub EmitFireRing(w As CodeWriter, block As BlockNode)
            Dim pattern = _stage.FindPattern(block.GetField("pattern"))
            If pattern Is Nothing Then
                w.Line("' 未选择子弹样式，跳过环形弹幕")
                Return
            End If
            Dim info = GetBulletBehavior(pattern.Id)
            Dim count = NextLocal("n")
            Dim speed = NextLocal("spd")
            Dim start = NextLocal("start")
            Dim arc = NextLocal("arc")
            Dim stepVar = NextLocal("step")
            Dim k = NextLocal("k")
            w.Line("Dim " & count & " As Integer = CInt(Math.Max(1, Math.Min(200, CDbl(" & EmitValue(block.GetField("count")) & "))))")
            w.Line("Dim " & speed & " As Double = " & EmitValue(block.GetField("speed")))
            w.Line("Dim " & start & " As Double = " & EmitValue(block.GetField("start")))
            w.Line("Dim " & arc & " As Double = " & EmitValue(block.GetField("arc")))
            w.Line("Dim " & stepVar & " As Double = If(Math.Abs(" & arc & ") >= 359.9, " & arc & " / " & count & ", If(" & count & " > 1, " & arc & " / (" & count & " - 1), 0.0))")
            w.Open("For " & k & " As Integer = 0 To " & count & " - 1")
            EmitBulletCreation(w, pattern, info, speed, start & " + " & stepVar & " * " & k)
            w.Close("Next")
        End Sub

        Private Sub EmitFireAimed(w As CodeWriter, block As BlockNode)
            Dim pattern = _stage.FindPattern(block.GetField("pattern"))
            If pattern Is Nothing Then
                w.Line("' 未选择子弹样式，跳过自机狙")
                Return
            End If
            Dim info = GetBulletBehavior(pattern.Id)
            Dim count = NextLocal("n")
            Dim speed = NextLocal("spd")
            Dim baseVar = NextLocal("base")
            Dim spread = NextLocal("spread")
            Dim stepVar = NextLocal("step")
            Dim k = NextLocal("k")
            Dim off = NextLocal("off")
            w.Line("Dim " & count & " As Integer = CInt(Math.Max(1, Math.Min(200, CDbl(" & EmitValue(block.GetField("count")) & "))))")
            w.Line("Dim " & speed & " As Double = " & EmitValue(block.GetField("speed")))
            w.Line("Dim " & baseVar & " As Double = System.Windows.Vector.AngleBetween(New System.Windows.Vector(0, -1), New System.Windows.Vector(STG.Player.X - Host.X, STG.Player.Y - Host.Y))")
            w.Line("Dim " & spread & " As Double = " & EmitValue(block.GetField("spread")))
            w.Line("Dim " & stepVar & " As Double = If(" & count & " > 1, " & spread & " / (" & count & " - 1), 0.0)")
            w.Open("For " & k & " As Integer = 0 To " & count & " - 1")
            w.Line("Dim " & off & " As Double = (" & k & " - (" & count & " - 1) / 2.0) * " & stepVar)
            EmitBulletCreation(w, pattern, info, speed, baseVar & " + " & off)
            w.Close("Next")
        End Sub

        Private Sub EmitFireSingle(w As CodeWriter, block As BlockNode)
            Dim pattern = _stage.FindPattern(block.GetField("pattern"))
            If pattern Is Nothing Then
                w.Line("' 未选择子弹样式，跳过发射")
                Return
            End If
            Dim info = GetBulletBehavior(pattern.Id)
            Dim speed = NextLocal("spd")
            w.Line("Dim " & speed & " As Double = " & EmitValue(block.GetField("speed")))
            EmitBulletCreation(w, pattern, info, speed, EmitValue(block.GetField("direction")))
        End Sub

        Private Sub EmitBulletCreation(w As CodeWriter, pattern As BulletPattern, info As BehaviorInfo, speedExpr As String, angleExpr As String)
            Dim b = NextLocal("b")
            Dim bh = NextLocal("bh")
            Dim typeInfo = EngineCatalog.BulletType(pattern.BulletType)
            w.Line("Dim " & b & " As New Bullet(BulletType." & typeInfo.Name & ", BulletColor." & EngineCatalog.BulletColorName(pattern.Color) & ", Host.X, Host.Y, " & speedExpr & ", " & angleExpr & ", 0.0)")
            If info IsNot Nothing AndAlso info.HasBehavior Then
                w.Line("Dim " & bh & " As New " & info.ClassName & "(" & b & ")")
                w.Line(b & ".Act = AddressOf " & bh & ".Tick")
            End If
            w.Line("STG.Add(" & b & ")")
        End Sub

        Private Sub EmitFireLaser(w As CodeWriter, block As BlockNode)
            Dim colorIndex = CInt(block.GetDouble("color", 2))
            If colorIndex < 0 OrElse colorIndex >= EngineCatalog.BulletColors.Count Then colorIndex = 2
            Dim laserVar = NextLocal("laser")
            w.Line("Dim " & laserVar & " As New Bullet.Laser(BulletColor." & EngineCatalog.BulletColorName(colorIndex) & ", Host.X, Host.Y, " &
                   EmitValue(block.GetField("direction")) & ", " & EmitValue(block.GetField("width")) & ", " &
                   EmitValue(block.GetField("length")) & ", CInt(" & EmitValue(block.GetField("life")) & "))")
            w.Line("STG.Add(" & laserVar & ")")
        End Sub

        Private Function GetBulletBehavior(patternId As String) As BehaviorInfo
            Dim info As BehaviorInfo = Nothing
            If _bulletBehaviors.TryGetValue(patternId, info) Then Return info
            Return Nothing
        End Function

        Private Function EmitCondition(block As BlockNode) As String
            Dim subj = block.GetField("subj")
            If subj = "atspell" Then
                If _info.BossTemplate Is Nothing Then Return "False"
                Dim card = "Boss.SpellCards(Boss.CurrentSpell)"
                Return "Boss.CurrentSpell >= 0 AndAlso Boss.CurrentSpell < Boss.SpellCards.Count AndAlso " & card & ".AtSpell"
            End If
            Dim left As String
            Select Case subj
                Case "ticks" : left = "Host.Ticks"
                Case "hp" : left = If(_info.Scope = ProgramScope.Enemy, "(Host.HP / MaxHP * 100.0)", "0.0")
                Case "dist" : left = "Host.GetDistance(STG.Player)"
                Case "px" : left = "STG.Player.X"
                Case "py" : left = "STG.Player.Y"
                Case "x" : left = "Host.X"
                Case "y" : left = "Host.Y"
                Case "var" : left = VarField(block.GetField("varName"))
                Case "random" : left = "Rnd()"
                Case Else : left = "0.0"
            End Select
            Dim right = EmitValue(block.GetField("value"))
            Select Case block.GetField("op")
                Case ">" : Return left & " > " & right
                Case "<" : Return left & " < " & right
                Case ">=" : Return left & " >= " & right
                Case "<=" : Return left & " <= " & right
                Case "==" : Return "Math.Abs(" & left & " - (" & right & ")) < 0.000001"
                Case "!=" : Return "Math.Abs(" & left & " - (" & right & ")) >= 0.000001"
                Case Else : Return "False"
            End Select
        End Function

        ''' <summary>把字段值翻译成 VB 表达式。</summary>
        Private Function EmitValue(text As String) As String
            If String.IsNullOrWhiteSpace(text) Then Return "0.0"
            If Not ValueExpression.IsReporter(text) Then
                Return VBCodeGenerator.Num(ValueExpression.ParseNumber(text, 0))
            End If
            Dim kind = ValueExpression.ReporterKind(text)
            Dim arg = ValueExpression.ReporterArg(text)
            Select Case kind
                Case "ticks" : Return "CDbl(Host.Ticks)"
                Case "hp" : Return If(_info.Scope = ProgramScope.Enemy, "(Host.HP / MaxHP * 100.0)", "0.0")
                Case "px" : Return "STG.Player.X"
                Case "py" : Return "STG.Player.Y"
                Case "dist" : Return "Host.GetDistance(STG.Player)"
                Case "x" : Return "Host.X"
                Case "y" : Return "Host.Y"
                Case "rand"
                    Dim a = 0.0
                    Dim b = 1.0
                    Dim parts = If(arg, "").Split(","c)
                    If parts.Length >= 2 Then
                        Double.TryParse(parts(0), NumberStyles.Float, CultureInfo.InvariantCulture, a)
                        Double.TryParse(parts(1), NumberStyles.Float, CultureInfo.InvariantCulture, b)
                    End If
                    Return "(" & VBCodeGenerator.Num(a) & " + Rnd() * " & VBCodeGenerator.Num(b - a) & ")"
                Case "var" : Return VarField(arg)
                Case "bullets" : Return "STG.SearchBullet().Count"
                Case "enemies" : Return "STG.SearchEnemy().Count"
                Case Else : Return "0.0"
            End Select
        End Function

        Private Function NextLocal(prefix As String) As String
            _localIndex += 1
            Return "__" & prefix & _localIndex.ToString(CultureInfo.InvariantCulture)
        End Function

        Private Function SoundName(name As String) As String
            Dim match = EngineCatalog.Sounds.FirstOrDefault(Function(s) String.Equals(s.Value, name, StringComparison.OrdinalIgnoreCase))
            If match IsNot Nothing Then Return match.Value
            Return "tan00"
        End Function

        Private Shared Function ContainsWait(blocks As List(Of BlockNode)) As Boolean
            If blocks Is Nothing Then Return False
            For Each b In blocks
                If b Is Nothing Then Continue For
                If b.TypeId = "wait" Then Return True
                If ContainsWait(b.Children) Then Return True
                If ContainsWait(b.ElseChildren) Then Return True
            Next
            Return False
        End Function
    End Class

End Namespace
