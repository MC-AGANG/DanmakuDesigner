Imports System.Collections.ObjectModel
Imports System.IO
Imports DanmakuDesigner.Core

''' <summary>编辑器的共享状态：工程、选择、积木上下文、预览世界与撤销栈。</summary>
Public Class EditorSession

    Public Property Project As GameProject
    Public Property ActiveStageProject As StageProject
    Public Property FilePath As String
    Public Property IsDirty As Boolean
    Public Property Selection As Object
    Public Property BlockContext As BlockContext
    Public Property ActiveView As String = "timeline"
    Public Property World As PreviewWorld
    Public Property Issues As New List(Of ValidationIssue)
    Public Property Logs As New List(Of ValidationIssue)
    Public Property EngineRepo As String = ""
    Public Property IsPlaying As Boolean

    Public ReadOnly UndoStack As New List(Of String)
    Public ReadOnly RedoStack As New List(Of String)
    Private _pendingUndo As String = Nothing
    Private _suppressEvents As Boolean = False

    Public Event ProjectReplaced()
    Public Event ProjectChanged()
    Public Event SelectionChanged()
    Public Event BlockContextChanged()
    Public Event ViewChanged()
    Public Event StatusMessage(message As String)
    Public Event IssuesChanged()
    Public Event PreviewReset()
    Public Event PreviewTick(tick As Long)
    Public Event PlaybackChanged(playing As Boolean)
    Public Event TitleChanged()

    Public ReadOnly Property Stage As StageDocument
        Get
            Return If(ActiveStageProject Is Nothing, Nothing, ActiveStageProject.Stage)
        End Get
    End Property

    Public ReadOnly Property HasProject As Boolean
        Get
            Return Project IsNot Nothing
        End Get
    End Property

    Public ReadOnly Property ProjectDirectory As String
        Get
            If String.IsNullOrWhiteSpace(FilePath) Then Return ""
            Return Path.GetDirectoryName(FilePath)
        End Get
    End Property

    Public ReadOnly Property SelectionName As String
        Get
            Select Case True
                Case TypeOf Selection Is StageDocument : Return "关卡"
                Case TypeOf Selection Is GameProject : Return "游戏"
                Case TypeOf Selection Is TimelineTrack : Return "轨道"
                Case TypeOf Selection Is TimelineClip : Return "片段"
                Case TypeOf Selection Is EnemyTemplate : Return "敌机模板"
                Case TypeOf Selection Is BulletPattern : Return "子弹样式"
                Case TypeOf Selection Is BossSpellCard : Return "符卡"
                Case TypeOf Selection Is DialogueDefinition : Return "对话"
                Case TypeOf Selection Is BlockNode : Return "积木"
                Case Else : Return "未选择"
            End Select
        End Get
    End Property

    Public Sub Initialize(openPath As String)
        EngineRepo = FindEngineRepo()
        App.LogStartup("EditorSession.Initialize openPath=[" & openPath & "] engine=[" & EngineRepo & "]")

        ' 正常启动不自动恢复上次工程，也不自动载入示例；
        ' 只有显式传入 --open 时才打开工程。
        If Not String.IsNullOrWhiteSpace(openPath) Then
            If File.Exists(openPath) Then
                Try
                    Dim loaded = ProjectJson.LoadGame(openPath)
                    SetProject(loaded, openPath, False)
                    App.LogStartup("opened project path; stage=" & If(Stage Is Nothing, "NOTHING", Stage.Name))
                    AddLog(IssueSeverity.Info, "已打开工程：" & Path.GetFileName(openPath))
                    Return
                Catch ex As Exception
                    App.LogStartup("open project failed: " & ex.ToString())
                    ResetProjectState()
                    AddLog(IssueSeverity.[Error], "打开工程失败：" & ex.Message)
                    Return
                End Try
            Else
                ResetProjectState()
                AddLog(IssueSeverity.[Error], "打开工程失败：文件不存在 " & openPath)
                Return
            End If
        End If

        ResetProjectState()
        AddLog(IssueSeverity.Info, "未打开工程。使用 文件 → 新建工程 或 打开工程。")
    End Sub

    Public Shared Function FindSamplePath() As String
        Dim candidates As New List(Of String)
        candidates.Add(Path.Combine(AppContext.BaseDirectory, "Samples", "DemoStage.dmkproj"))
        Dim dir As New DirectoryInfo(AppContext.BaseDirectory)
        For i = 0 To 8
            If dir Is Nothing Then Exit For
            candidates.Add(Path.Combine(dir.FullName, "samples", "DemoStage.dmkproj"))
            dir = dir.Parent
        Next
        For Each path In candidates
            If File.Exists(path) Then Return path
        Next
        Return Nothing
    End Function

    Public Shared Function FindEngineRepo() As String
        Dim dir As New DirectoryInfo(AppContext.BaseDirectory)
        For i = 0 To 9
            If dir Is Nothing Then Exit For
            Dim probe = Path.Combine(dir.FullName, "AgSTG", "AgSTG", "AgSTG.vbproj")
            If File.Exists(probe) Then Return Path.Combine(dir.FullName, "AgSTG")
            dir = dir.Parent
        Next
        For Each candidate In {"F:\code\AgSTG", "C:\code\AgSTG"}
            If File.Exists(Path.Combine(candidate, "AgSTG", "AgSTG.vbproj")) Then Return candidate
        Next
        Return ""
    End Function

    ''' <summary>清空当前工程，回到“未打开工程”的空白工作区。</summary>
    Public Sub CloseProject()
        If Project Is Nothing Then Return
        ResetProjectState()
        Logs.Clear()
        AddLog(IssueSeverity.Info, "已关闭工程。")
    End Sub

    Private Sub ResetProjectState()
        _suppressEvents = True
        Project = Nothing
        ActiveStageProject = Nothing
        FilePath = Nothing
        IsDirty = False
        Selection = Nothing
        BlockContext = Nothing
        UndoStack.Clear()
        RedoStack.Clear()
        _pendingUndo = Nothing
        IsPlaying = False
        World = Nothing
        Issues = New List(Of ValidationIssue)
        _suppressEvents = False
        RaiseEvent ProjectReplaced()
        RaiseEvent SelectionChanged()
        RaiseEvent BlockContextChanged()
        RaiseEvent TitleChanged()
        RaiseEvent PlaybackChanged(False)
        RaiseEvent PreviewReset()
        RaiseEvent IssuesChanged()
    End Sub

    Public Sub SetProject(newProject As GameProject, path As String, dirty As Boolean)
        If newProject Is Nothing Then
            ResetProjectState()
            Return
        End If
        _suppressEvents = True
        Project = newProject
        Project.Normalize()
        ActiveStageProject = Project.Stages(0)
        FilePath = path
        IsDirty = dirty
        Selection = ActiveStageProject.Stage
        BlockContext = Nothing
        UndoStack.Clear()
        RedoStack.Clear()
        _pendingUndo = Nothing
        IsPlaying = False
        RebuildWorld()
        _suppressEvents = False
        RaiseEvent ProjectReplaced()
        RaiseEvent SelectionChanged()
        RaiseEvent BlockContextChanged()
        RaiseEvent TitleChanged()
        RaiseEvent PlaybackChanged(False)
        Validate()
        AddLog(IssueSeverity.Info, "游戏工程：" & Project.Name & " · " & Project.Stages.Count & " 个关卡 · 当前 " & ActiveStageProject.Stage.Name)
    End Sub

    Public Sub NewProject()
        SetProject(ProjectFactory.CreateBlankGame("未命名游戏"), Nothing, True)
        AddLog(IssueSeverity.Info, "新建空白游戏工程。")
    End Sub

    Public Function OpenProject(path As String) As Boolean
        Try
            Dim loaded = ProjectJson.LoadGame(path)
            SetProject(loaded, path, False)
            AddLog(IssueSeverity.Info, "已打开游戏工程：" & path)
            Return True
        Catch ex As Exception
            AddLog(IssueSeverity.[Error], "打开工程失败：" & ex.Message)
            Return False
        End Try
    End Function

    Public ReadOnly Property ActiveStageIndex As Integer
        Get
            If Project Is Nothing OrElse ActiveStageProject Is Nothing Then Return -1
            Return Project.Stages.IndexOf(ActiveStageProject)
        End Get
    End Property

    ''' <summary>切换当前编辑的关卡，不修改工程内容。</summary>
    Public Sub ActivateStage(stage As StageProject)
        If Project Is Nothing OrElse stage Is Nothing OrElse Not Project.Stages.Contains(stage) Then Return
        If ActiveStageProject Is stage Then
            Selection = stage.Stage
            RaiseEvent SelectionChanged()
            Return
        End If
        ActiveStageProject = stage
        BlockContext = Nothing
        Selection = stage.Stage
        IsPlaying = False
        RebuildWorld()
        RaiseEvent ProjectReplaced()
        RaiseEvent SelectionChanged()
        RaiseEvent BlockContextChanged()
        RaiseEvent TitleChanged()
    End Sub

    Public Function AddStage(Optional name As String = "", Optional codeName As String = "") As StageProject
        If Project Is Nothing Then Return Nothing
        BeginEdit()
        Dim stage = ProjectFactory.CreateBlankProject(If(String.IsNullOrWhiteSpace(name), "新关卡", name))
        stage.Stage.CodeName = UniqueStageCodeName(If(String.IsNullOrWhiteSpace(codeName), stage.Stage.CodeName, codeName))
        Project.Stages.Add(stage)
        ActiveStageProject = stage
        BlockContext = Nothing
        Selection = stage.Stage
        RebuildWorld()
        CommitEdit()
        RaiseEvent ProjectReplaced()
        Return stage
    End Function

    Public Function DuplicateStage(source As StageProject) As StageProject
        If Project Is Nothing OrElse source Is Nothing Then Return Nothing
        BeginEdit()
        Dim copy = ProjectJson.Clone(source)
        copy.Id = Ids.NewId("stage")
        copy.Name = source.Name & " 副本"
        copy.Stage.Name = copy.Name
        copy.Stage.CodeName = UniqueStageCodeName(source.Stage.CodeName & "Copy")
        Project.Stages.Insert(Project.Stages.IndexOf(source) + 1, copy)
        ActiveStageProject = copy
        BlockContext = Nothing
        Selection = copy.Stage
        RebuildWorld()
        CommitEdit()
        RaiseEvent ProjectReplaced()
        Return copy
    End Function

    Public Sub DeleteStage(stage As StageProject)
        If Project Is Nothing OrElse stage Is Nothing OrElse Project.Stages.Count <= 1 Then Return
        BeginEdit()
        Dim index = Project.Stages.IndexOf(stage)
        If index < 0 Then Return
        Project.Stages.RemoveAt(index)
        If ActiveStageProject Is stage Then
            ActiveStageProject = Project.Stages(Math.Max(0, Math.Min(index, Project.Stages.Count - 1)))
            BlockContext = Nothing
            Selection = ActiveStageProject.Stage
            RebuildWorld()
        End If
        CommitEdit()
        RaiseEvent ProjectReplaced()
    End Sub

    Public Function UniqueStageCodeName(baseName As String) As String
        Dim root = If(String.IsNullOrWhiteSpace(baseName), "StageCustom", baseName.Trim())
        Dim candidate = root
        Dim n = 2
        While Project IsNot Nothing AndAlso Project.Stages.Any(Function(s) String.Equals(s.Stage.CodeName, candidate, StringComparison.OrdinalIgnoreCase))
            candidate = root & n.ToString()
            n += 1
        End While
        Return candidate
    End Function

    Public Function ResourceFullPath(resource As GameResource) As String
        If resource Is Nothing OrElse String.IsNullOrWhiteSpace(resource.RelativePath) OrElse String.IsNullOrWhiteSpace(ProjectDirectory) Then Return ""
        Return Path.Combine(ProjectDirectory, resource.RelativePath.Replace("/"c, "\"c))
    End Function

    ''' <summary>把外部文件复制到工程的 Resources 目录，并登记为游戏共享资源。</summary>
    Public Function AddResource(sourcePath As String, Optional displayName As String = "") As GameResource
        If Project Is Nothing OrElse String.IsNullOrWhiteSpace(sourcePath) OrElse Not File.Exists(sourcePath) Then Return Nothing
        If String.IsNullOrWhiteSpace(ProjectDirectory) Then Throw New InvalidOperationException("请先保存工程，再导入资源。")
        Dim resourceDir = Path.Combine(ProjectDirectory, "Resources")
        Directory.CreateDirectory(resourceDir)
        Dim baseName = Path.GetFileNameWithoutExtension(sourcePath)
        Dim extension = Path.GetExtension(sourcePath)
        Dim targetName = SafeFileName(If(String.IsNullOrWhiteSpace(displayName), baseName, displayName)) & extension
        Dim targetPath = Path.Combine(resourceDir, targetName)
        Dim n = 2
        While File.Exists(targetPath) AndAlso Not String.Equals(Path.GetFullPath(targetPath), Path.GetFullPath(sourcePath), StringComparison.OrdinalIgnoreCase)
            targetName = SafeFileName(If(String.IsNullOrWhiteSpace(displayName), baseName, displayName)) & n.ToString() & extension
            targetPath = Path.Combine(resourceDir, targetName)
            n += 1
        End While
        If Not String.Equals(Path.GetFullPath(targetPath), Path.GetFullPath(sourcePath), StringComparison.OrdinalIgnoreCase) Then
            File.Copy(sourcePath, targetPath, True)
        End If

        BeginEdit()
        Dim resource As New GameResource With {
            .Name = If(String.IsNullOrWhiteSpace(displayName), baseName, displayName),
            .Kind = ResourceKindForFile(targetPath),
            .RelativePath = "Resources/" & targetName
        }
        Project.Resources.Add(resource)
        CommitEdit()
        Return resource
    End Function

    Public Sub RemoveResource(resource As GameResource)
        If Project Is Nothing OrElse resource Is Nothing Then Return
        BeginEdit()
        Project.Resources.Remove(resource)
        CommitEdit()
    End Sub

    Public Shared Function ResourceKindForFile(path As String) As GameResourceKind
        Select Case IO.Path.GetExtension(path).ToLowerInvariant()
            Case ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tif", ".tiff", ".webp"
                Return GameResourceKind.Image
            Case ".wav", ".mp3", ".ogg", ".flac", ".m4a"
                Return GameResourceKind.Audio
            Case Else
                Return GameResourceKind.Other
        End Select
    End Function

    Public Sub SaveProject()
        If Project Is Nothing Then Return
        If String.IsNullOrWhiteSpace(FilePath) Then
            Dim dialog As New Microsoft.Win32.SaveFileDialog With {
                .Title = "保存游戏工程",
                .Filter = "Danmaku Designer 游戏工程 (*.dmkproj)|*.dmkproj|所有文件 (*.*)|*.*",
                .FileName = SafeFileName(Project.Name) & ".dmkproj"
            }
            If dialog.ShowDialog() <> True Then Return
            FilePath = dialog.FileName
        End If
        SaveTo(FilePath)
    End Sub

    Public Sub SaveProjectAs()
        If Project Is Nothing Then Return
        Dim dialog As New Microsoft.Win32.SaveFileDialog With {
            .Title = "游戏工程另存为",
            .Filter = "Danmaku Designer 游戏工程 (*.dmkproj)|*.dmkproj|所有文件 (*.*)|*.*",
            .FileName = SafeFileName(Project.Name) & ".dmkproj"
        }
        If String.IsNullOrWhiteSpace(FilePath) = False Then
            dialog.InitialDirectory = Path.GetDirectoryName(FilePath)
        End If
        If dialog.ShowDialog() <> True Then Return
        FilePath = dialog.FileName
        SaveTo(FilePath)
    End Sub

    Public Sub SaveTo(path As String)
        If Project Is Nothing Then Return
        Try
            ProjectJson.SaveGame(path, Project)
            FilePath = path
            IsDirty = False
            RaiseEvent TitleChanged()
            AddLog(IssueSeverity.Info, "已保存游戏工程：" & path)
        Catch ex As Exception
            AddLog(IssueSeverity.[Error], "保存失败：" & ex.Message)
        End Try
    End Sub

    Private Function SafeFileName(name As String) As String
        Dim invalid = Path.GetInvalidFileNameChars()
        Dim chars = If(name, "stage").ToCharArray().Select(Function(c) If(invalid.Contains(c), "_"c, c)).ToArray()
        Dim result As New String(chars)
        If String.IsNullOrWhiteSpace(result) Then result = "stage"
        Return result
    End Function

    ''' <summary>开始一次可撤销编辑（用于拖拽等连续操作）。</summary>
    Public Sub BeginEdit()
        If Project Is Nothing Then Return
        If _pendingUndo Is Nothing Then
            _pendingUndo = ProjectJson.ToGameJson(Project)
        End If
    End Sub

    Public Sub CommitEdit()
        If Project Is Nothing Then Return
        If _pendingUndo Is Nothing Then Return
        Dim current = ProjectJson.ToGameJson(Project)
        If current <> _pendingUndo Then
            UndoStack.Add(_pendingUndo)
            If UndoStack.Count > 80 Then UndoStack.RemoveAt(0)
            RedoStack.Clear()
            IsDirty = True
            RaiseEvent TitleChanged()
        End If
        _pendingUndo = Nothing
        MarkEdited()
    End Sub

    Public Sub CancelEdit()
        _pendingUndo = Nothing
    End Sub

    Public Sub MarkEdited()
        If Project Is Nothing Then Return
        IsDirty = True
        Project.Normalize()
        If BlockContext IsNot Nothing AndAlso Stage IsNot Nothing Then
            Dim ownerExists = Stage.FindTemplate(BlockContext.OwnerId) IsNot Nothing OrElse Stage.FindPattern(BlockContext.OwnerId) IsNot Nothing OrElse Stage.FindSpell(BlockContext.OwnerId) IsNot Nothing
            If Not ownerExists Then
                BlockContext = Nothing
                RaiseEvent BlockContextChanged()
            End If
        End If
        RaiseEvent TitleChanged()
        RaiseEvent ProjectChanged()
        If Not IsPlaying Then RebuildWorld()
        Validate()
    End Sub

    Public Sub Undo()
        If Project Is Nothing OrElse UndoStack.Count = 0 Then Return
        Dim current = ProjectJson.ToGameJson(Project)
        Dim previous = UndoStack(UndoStack.Count - 1)
        UndoStack.RemoveAt(UndoStack.Count - 1)
        RedoStack.Add(current)
        ApplySnapshot(previous)
        AddLog(IssueSeverity.Info, "撤销")
    End Sub

    Public Sub Redo()
        If Project Is Nothing OrElse RedoStack.Count = 0 Then Return
        Dim current = ProjectJson.ToGameJson(Project)
        Dim nextJson = RedoStack(RedoStack.Count - 1)
        RedoStack.RemoveAt(RedoStack.Count - 1)
        UndoStack.Add(current)
        ApplySnapshot(nextJson)
        AddLog(IssueSeverity.Info, "重做")
    End Sub

    Private Sub ApplySnapshot(json As String)
        Dim previousSelectionId = GetSelectionId()
        Dim previousKind = GetSelectionKind()
        Dim previousStageId = If(ActiveStageProject Is Nothing, "", ActiveStageProject.Id)
        Project = ProjectJson.FromGameJson(json)
        ActiveStageProject = Project.FindStage(previousStageId)
        If ActiveStageProject Is Nothing Then ActiveStageProject = Project.Stages(0)
        IsDirty = True
        BlockContext = Nothing
        Selection = ResolveSelection(previousKind, previousSelectionId)
        RebuildWorld()
        RaiseEvent ProjectReplaced()
        RaiseEvent BlockContextChanged()
        RaiseEvent SelectionChanged()
        RaiseEvent TitleChanged()
        RaiseEvent ProjectChanged()
        Validate()
    End Sub

    Public ReadOnly Property CanUndo As Boolean
        Get
            Return UndoStack.Count > 0
        End Get
    End Property

    Public ReadOnly Property CanRedo As Boolean
        Get
            Return RedoStack.Count > 0
        End Get
    End Property

    Private Function GetSelectionId() As String
        If TypeOf Selection Is EnemyTemplate Then Return DirectCast(Selection, EnemyTemplate).Id
        If TypeOf Selection Is BulletPattern Then Return DirectCast(Selection, BulletPattern).Id
        If TypeOf Selection Is BossSpellCard Then Return DirectCast(Selection, BossSpellCard).Id
        If TypeOf Selection Is DialogueDefinition Then Return DirectCast(Selection, DialogueDefinition).Id
        If TypeOf Selection Is TimelineTrack Then Return DirectCast(Selection, TimelineTrack).Id
        If TypeOf Selection Is TimelineClip Then Return DirectCast(Selection, TimelineClip).Id
        Return ""
    End Function

    Private Function GetSelectionKind() As String
        If TypeOf Selection Is EnemyTemplate Then Return "template"
        If TypeOf Selection Is BulletPattern Then Return "pattern"
        If TypeOf Selection Is BossSpellCard Then Return "spell"
        If TypeOf Selection Is DialogueDefinition Then Return "dialogue"
        If TypeOf Selection Is TimelineTrack Then Return "track"
        If TypeOf Selection Is TimelineClip Then Return "clip"
        If TypeOf Selection Is StageDocument Then Return "stage"
        If TypeOf Selection Is GameProject Then Return "game"
        Return ""
    End Function

    Private Function ResolveSelection(kind As String, id As String) As Object
        If Stage Is Nothing Then Return Nothing
        Select Case kind
            Case "template" : Return Stage.FindTemplate(id)
            Case "pattern" : Return Stage.FindPattern(id)
            Case "spell" : Return Stage.FindSpell(id)
            Case "dialogue" : Return Stage.FindDialogue(id)
            Case "track" : Return Stage.FindTrack(id)
            Case "clip" : Return Stage.FindClip(id)
            Case "game" : Return Project
            Case Else : Return Stage
        End Select
    End Function

    Public Sub SelectItem(item As Object)
        Selection = item
        RaiseEvent SelectionChanged()
        If TypeOf item Is EnemyTemplate OrElse TypeOf item Is BulletPattern Then
            ' 这里只选择，不自动进入积木视图，避免打断时间轴操作。
        End If
    End Sub

    Public Sub SetView(view As String)
        ActiveView = view
        RaiseEvent ViewChanged()
    End Sub

    Public Sub OpenBlockEditor(program As Program, scope As ProgramScope, displayName As String, ownerId As String, Optional isSpell As Boolean = False)
        If program Is Nothing Then Return
        BlockContext = New BlockContext With {
            .Program = program,
            .Scope = scope,
            .DisplayName = displayName,
            .OwnerId = ownerId,
            .IsSpell = isSpell
        }
        ActiveView = "blocks"
        RaiseEvent BlockContextChanged()
        RaiseEvent ViewChanged()
    End Sub

    Public Sub EditTemplateBehavior(template As EnemyTemplate)
        If template Is Nothing OrElse template.Behavior Is Nothing Then Return
        SelectItem(template)
        OpenBlockEditor(template.Behavior, ProgramScope.Enemy, template.Name, template.Id)
    End Sub

    Public Sub EditPatternBehavior(pattern As BulletPattern)
        If pattern Is Nothing OrElse pattern.Behavior Is Nothing Then Return
        SelectItem(pattern)
        OpenBlockEditor(pattern.Behavior, ProgramScope.Bullet, pattern.Name, pattern.Id)
    End Sub

    Public Sub EditSpellBehavior(spell As BossSpellCard)
        If spell Is Nothing OrElse spell.Behavior Is Nothing Then Return
        SelectItem(spell)
        OpenBlockEditor(spell.Behavior, ProgramScope.Enemy, spell.Name, spell.Id, True)
    End Sub

    Public Sub RebuildWorld()
        If Project Is Nothing OrElse ActiveStageProject Is Nothing Then
            World = Nothing
            IsPlaying = False
            RaiseEvent PreviewReset()
            RaiseEvent PreviewTick(0)
            RaiseEvent PlaybackChanged(False)
            Return
        End If
        Dim god = If(World Is Nothing, False, World.GodMode)
        Dim previousTick = If(World Is Nothing, 0L, World.Tick)
        World = New PreviewWorld(ActiveStageProject)
        World.GodMode = god
        IsPlaying = False
        RaiseEvent PreviewReset()
        RaiseEvent PreviewTick(0)
        RaiseEvent PlaybackChanged(False)
    End Sub

    Public Sub SeekPreview(tick As Long)
        If World Is Nothing Then Return
        If IsPlaying Then Return
        World.Seek(tick)
        RaiseEvent PreviewTick(World.Tick)
    End Sub

    Public Sub SetPlaying(playing As Boolean)
        If Project Is Nothing Then playing = False
        IsPlaying = playing
        RaiseEvent PlaybackChanged(playing)
    End Sub

    Public Sub Validate()
        If Project Is Nothing OrElse ActiveStageProject Is Nothing Then
            Issues = New List(Of ValidationIssue)
            RaiseEvent IssuesChanged()
            Return
        End If
        Try
            Issues = Validator.Validate(ActiveStageProject)
        Catch ex As Exception
            Issues = New List(Of ValidationIssue)
            AddLog(IssueSeverity.[Error], "验证器错误：" & ex.Message)
        End Try
        RaiseEvent IssuesChanged()
    End Sub

    Public Sub AddLog(severity As IssueSeverity, message As String, Optional targetKind As String = "", Optional targetId As String = "")
        Logs.Add(New ValidationIssue With {
            .Severity = severity,
            .Message = message,
            .TargetKind = targetKind,
            .TargetId = targetId,
            .ObjectName = DateTime.Now.ToString("HH:mm:ss")
        })
        While Logs.Count > 200
            Logs.RemoveAt(0)
        End While
        RaiseEvent IssuesChanged()
    End Sub

    Public Function ErrorCount() As Integer
        Return Issues.Where(Function(i) i.Severity = IssueSeverity.[Error]).Count()
    End Function

    Public Function WarningCount() As Integer
        Return Issues.Where(Function(i) i.Severity = IssueSeverity.Warning).Count()
    End Function

    Public Function TotalIssueCount() As Integer
        Return Issues.Count
    End Function

    Public Sub SelectById(kind As String, id As String)
        Dim item = ResolveSelection(kind, id)
        If item IsNot Nothing Then SelectItem(item)
    End Sub

End Class

Public Class BlockContext
    Public Property Program As Program
    Public Property Scope As ProgramScope
    Public Property DisplayName As String
    Public Property OwnerId As String
    Public Property IsSpell As Boolean
End Class
