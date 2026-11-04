Imports System.IO
Imports System.Runtime.InteropServices
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Input
Imports System.Windows.Interop
Imports System.Windows.Media
Imports System.Windows.Media.Animation
Imports System.Windows.Media.Imaging
Imports System.Windows.Shell
Imports System.Windows.Threading
Imports DanmakuDesigner.Core

Public Class MainWindow

    Private Enum AccentState
        Startup
        Project
        Running
        Inactive
    End Enum

    Private Shared ReadOnly AccentStartupColor As Color = Color.FromRgb(104, 33, 122)
    Private Shared ReadOnly AccentProjectColor As Color = Color.FromRgb(1, 121, 203)
    Private Shared ReadOnly AccentRunningColor As Color = Color.FromRgb(202, 81, 0)
    Private Shared ReadOnly AccentInactiveColor As Color = Color.FromRgb(67, 67, 70)

    Private ReadOnly _session As EditorSession = App.Session
    Private _switchingTabs As Boolean
    Private _loaded As Boolean
    Private _capturePath As String = ""
    Private _captureDelay As Integer = 900
    Private _startupTab As String = ""
    Private _startupTemplate As String = ""
    Private _startupPattern As String = ""
    Private _startupTick As Long = -1
    Private _startupPlay As Boolean
    Private _captureScheduled As Boolean
    Private _uiTest As Boolean
    Private _captureExit As Boolean
    Private _accentState As AccentState = AccentState.Startup
    Private _accentInitialized As Boolean
    Private _isWindowActive As Boolean = True
    Private _startupAccent As Boolean = True
    Private _startupAccentTimer As DispatcherTimer

    Public Sub New()
        InitializeComponent()
        AddHandler _session.ProjectReplaced, AddressOf Session_ProjectReplaced
        AddHandler _session.ProjectChanged, AddressOf Session_ProjectChanged
        AddHandler _session.SelectionChanged, AddressOf Session_SelectionChanged
        AddHandler _session.BlockContextChanged, AddressOf Session_BlockContextChanged
        AddHandler _session.ViewChanged, AddressOf Session_ViewChanged
        AddHandler _session.IssuesChanged, AddressOf Session_IssuesChanged
        AddHandler _session.PreviewReset, AddressOf Session_PreviewReset
        AddHandler _session.PreviewTick, AddressOf Session_PreviewTick
        AddHandler _session.PlaybackChanged, AddressOf Session_PlaybackChanged
        AddHandler _session.TitleChanged, AddressOf Session_TitleChanged
        AddHandler _session.StatusMessage, AddressOf Session_StatusMessage
        AddHandler PreviewPanelControl.TickChanged, AddressOf Preview_TickChanged
        AddHandler Loaded, AddressOf Window_Loaded
        AddHandler PreviewKeyDown, AddressOf Window_PreviewKeyDown
        PaletteView.Editor = BlockEditorViewControl
        ' 启动阶段先显示紫色；项目载入完成后再过渡到蓝色。
        ApplyAccentState(False)
        UpdateTitle()
    End Sub

    ' ============================================================
    ' 启动与截图
    ' ============================================================

    Public Sub ApplyStartupOptions(tab As String, templateId As String, patternId As String, previewTick As Long, autoPlay As Boolean, capturePath As String, captureDelay As Integer, Optional uiTest As Boolean = False)
        _startupTab = If(tab, "")
        _startupTemplate = If(templateId, "")
        _startupPattern = If(patternId, "")
        _startupTick = previewTick
        _startupPlay = autoPlay
        _capturePath = If(capturePath, "")
        _captureDelay = Math.Max(200, captureDelay)
        _uiTest = uiTest
        If _loaded Then ApplyStartupNow()
    End Sub

    Private Sub Window_Loaded(sender As Object, e As RoutedEventArgs)
        App.LogStartup("Window_Loaded begin; stage=" & If(_session.Stage Is Nothing, "NOTHING", _session.Stage.Name))
        _loaded = True
        If Width > SystemParameters.WorkArea.Width Then Width = Math.Max(MinWidth, SystemParameters.WorkArea.Width - 30)
        If Height > SystemParameters.WorkArea.Height Then Height = Math.Max(MinHeight, SystemParameters.WorkArea.Height - 30)
        'EnginePathText.Text = If(String.IsNullOrWhiteSpace(_session.EngineRepo), "未找到 AgSTG 引擎", "引擎 " & Path.GetFileName(_session.EngineRepo))
        _isWindowActive = IsActive
        RefreshAll()
        ApplyStartupNow()
        ApplyAccentState(False)
        StartStartupAccentTransition()
        TimelineEditorView.Focus()
        App.LogStartup("Window_Loaded end")
    End Sub

    ' ============================================================
    ' 窗口强调色 / 外发光
    ' ============================================================

    Private Sub StartStartupAccentTransition()
        If Not _startupAccent Then Return
        _startupAccentTimer = New DispatcherTimer With {.Interval = TimeSpan.FromMilliseconds(700)}
        AddHandler _startupAccentTimer.Tick, Sub(sender As Object, e As EventArgs)
                                                 _startupAccentTimer.Stop()
                                                 If _startupAccent Then
                                                     _startupAccent = False
                                                     ApplyAccentState(True)
                                                 End If
                                             End Sub
        _startupAccentTimer.Start()
    End Sub

    Private Sub ApplyAccentState(animate As Boolean)
        If AccentGlow Is Nothing OrElse StatusBar Is Nothing Then Return
        Dim state As AccentState
        If Not _isWindowActive Then
            state = AccentState.Inactive
        ElseIf _session.IsPlaying Then
            state = AccentState.Running
        ElseIf _startupAccent Then
            state = AccentState.Startup
        ElseIf _session.Project IsNot Nothing Then
            state = AccentState.Project
        Else
            state = AccentState.Startup
        End If
        If _accentInitialized AndAlso state = _accentState Then Return
        _accentState = state
        _accentInitialized = True
        ApplyAccentColor(AccentColorFor(state), animate)
    End Sub

    Private Shared Function AccentColorFor(state As AccentState) As Color
        Select Case state
            Case AccentState.Project : Return AccentProjectColor
            Case AccentState.Running : Return AccentRunningColor
            Case AccentState.Inactive : Return AccentInactiveColor
            Case Else : Return AccentStartupColor
        End Select
    End Function

    Private Sub ApplyAccentColor(color As Color, animate As Boolean)
        Dim glowStart = AccentGlow.AccentColor
        AccentGlow.BeginAnimation(AccentGlowDecorator.AccentColorProperty, Nothing)
        AccentGlow.AccentColor = color
        If animate AndAlso glowStart <> color Then
            AccentGlow.BeginAnimation(AccentGlowDecorator.AccentColorProperty,
                New ColorAnimation(glowStart, color, TimeSpan.FromMilliseconds(240)) With {
                    .EasingFunction = New QuadraticEase With {.EasingMode = EasingMode.EaseInOut},
                    .FillBehavior = FillBehavior.Stop
                })
        End If

        Dim brush = TryCast(StatusBar.Background, SolidColorBrush)
        If brush Is Nothing OrElse brush.IsFrozen Then
            StatusBar.Background = New SolidColorBrush(color)
            Return
        End If
        Dim barStart = brush.Color
        brush.BeginAnimation(SolidColorBrush.ColorProperty, Nothing)
        brush.Color = color
        If animate AndAlso barStart <> color Then
            brush.BeginAnimation(SolidColorBrush.ColorProperty,
                New ColorAnimation(barStart, color, TimeSpan.FromMilliseconds(240)) With {
                    .EasingFunction = New QuadraticEase With {.EasingMode = EasingMode.EaseInOut},
                    .FillBehavior = FillBehavior.Stop
                })
        End If
    End Sub

    Private Sub SetChromeButtonOpacity(value As Double)
        If TitleIconImage IsNot Nothing Then TitleIconImage.Opacity = value
        If MinButton IsNot Nothing Then MinButton.Opacity = value
        If MaxButton IsNot Nothing Then MaxButton.Opacity = value
        If CloseButton IsNot Nothing Then CloseButton.Opacity = value
    End Sub

    Private Sub Window_Activated(sender As Object, e As EventArgs)
        _isWindowActive = True
        SetChromeButtonOpacity(1.0)
        ApplyAccentState(True)
    End Sub

    Private Sub Window_Deactivated(sender As Object, e As EventArgs)
        _isWindowActive = False
        SetChromeButtonOpacity(0.55)
        ApplyAccentState(True)
    End Sub

    Private Sub ApplyStartupNow()
        If Not _loaded Then Return
        App.LogStartup("ApplyStartupNow tab=" & _startupTab & " capture=" & _capturePath)
        If _session.Project IsNot Nothing Then
            If Not String.IsNullOrEmpty(_startupTemplate) Then
                Dim tmpl = _session.Stage.FindTemplate(_startupTemplate)
                If tmpl IsNot Nothing Then _session.EditTemplateBehavior(tmpl)
            End If
            If Not String.IsNullOrEmpty(_startupPattern) Then
                Dim pattern = _session.Stage.FindPattern(_startupPattern)
                If pattern IsNot Nothing Then _session.EditPatternBehavior(pattern)
            End If
            Dim view = _startupTab
            If String.IsNullOrEmpty(view) AndAlso _session.BlockContext IsNot Nothing Then view = "blocks"
            If String.IsNullOrEmpty(view) AndAlso _startupTick >= 0 Then view = "preview"
            If String.IsNullOrEmpty(view) Then view = "timeline"
            If view = "blocks" AndAlso _session.BlockContext Is Nothing AndAlso _session.Stage.EnemyTemplates.Count > 0 Then
                _session.EditTemplateBehavior(_session.Stage.EnemyTemplates(0))
            End If
            _session.SetView(view)
            If _startupTick >= 0 Then
                _session.SeekPreview(_startupTick)
                PreviewPanelControl.RefreshFromSession()
            End If
            If _startupPlay Then
                _session.SetView("preview")
                Dispatcher.BeginInvoke(Sub() PreviewPanelControl.Play(), DispatcherPriority.Background)
            End If
        End If
        If _uiTest Then
            Dispatcher.BeginInvoke(Sub() RunUiSelfTest(), DispatcherPriority.ApplicationIdle)
        End If
        If Not String.IsNullOrEmpty(_capturePath) AndAlso Not _captureScheduled Then
            _captureScheduled = True
            ScheduleCapture()
        End If
    End Sub

    Private Sub RunUiSelfTest()
        Dim report As New List(Of String)
        Try
            ' UI 自检需要一个工程；正常启动是空白工作区，这里只给测试加载示例。
            If _session.Project Is Nothing Then
                Dim sample = EditorSession.FindSamplePath()
                If sample IsNot Nothing Then
                    _session.OpenProject(sample)
                Else
                    _session.NewProject()
                End If
            End If
            If _session.Project Is Nothing Then
                report.Add("SKIP: no project")
            Else
                Dim tmpl = _session.Stage.EnemyTemplates.FirstOrDefault()
                If tmpl Is Nothing Then
                    report.Add("SKIP: no templates")
                Else
                    _session.EditTemplateBehavior(tmpl)
                    _session.SetView("blocks")
                    BlockEditorViewControl.Refresh()
                    Dim originalScripts = _session.BlockContext.Program.Scripts.Count
                    Dim originalBlocks = _session.BlockContext.Program.BlockCount
                    report.Add("initial scripts=" & originalScripts & " blocks=" & originalBlocks)

                    BlockEditorViewControl.PaletteClicked(BlockRegistry.Find("every_frame"))
                    BlockEditorViewControl.PaletteClicked(BlockRegistry.Find("fire_ring"))
                    BlockEditorViewControl.PaletteClicked(BlockRegistry.Find("repeat"))
                    BlockEditorViewControl.PaletteClicked(BlockRegistry.Find("if"))
                    report.Add("after add scripts=" & _session.BlockContext.Program.Scripts.Count & " blocks=" & _session.BlockContext.Program.BlockCount)

                    For i = 1 To 4
                        _session.Undo()
                    Next
                    ' 撤销后积木上下文会被清空，重新打开再验证。
                    Dim fresh = _session.Stage.FindTemplate(tmpl.Id)
                    If fresh IsNot Nothing Then _session.EditTemplateBehavior(fresh)
                    BlockEditorViewControl.Refresh()
                    report.Add("after undo scripts=" & _session.BlockContext.Program.Scripts.Count & " blocks=" & _session.BlockContext.Program.BlockCount)
                    For i = 1 To 4
                        _session.Redo()
                    Next
                    fresh = _session.Stage.FindTemplate(tmpl.Id)
                    If fresh IsNot Nothing Then _session.EditTemplateBehavior(fresh)
                    BlockEditorViewControl.Refresh()
                    BlockEditorViewControl.ScrollToEnd()
                    report.Add("after redo scripts=" & _session.BlockContext.Program.Scripts.Count & " blocks=" & _session.BlockContext.Program.BlockCount)
                    Dim lastScript = _session.BlockContext.Program.Scripts.Last()
                    Dim dropData As New BlockDragData With {
                    .TypeId = "add_direction",
                    .FromPalette = True,
                    .IsHat = False
                }
                    If dropData.Effect <> DragDropEffects.Copy Then Throw New InvalidOperationException("积木库拖拽效果应为 Copy")
                    Dim movedData As New BlockDragData With {
                    .TypeId = "add_direction",
                    .FromPalette = False,
                    .IsHat = False
                }
                    If movedData.Effect <> DragDropEffects.Move Then Throw New InvalidOperationException("画布内拖拽效果应为 Move")
                    report.Add("drag effects palette=Copy internal=Move")
                    BlockEditorViewControl.PerformDrop(dropData, lastScript.Children, 0)
                    report.Add("after drop blocks=" & _session.BlockContext.Program.BlockCount)
                    BlockEditorViewControl.ScrollToEnd()
                    report.Add("validation errors=" & _session.ErrorCount() & " warnings=" & _session.WarningCount())
                    Dim initialStage = _session.ActiveStageProject
                    Dim stageCount = _session.Project.Stages.Count
                    Dim addedStage = _session.AddStage("UI 测试关卡")
                    If addedStage Is Nothing OrElse _session.Project.Stages.Count <> stageCount + 1 Then Throw New InvalidOperationException("添加关卡失败")
                    Dim duplicateStage = _session.DuplicateStage(addedStage)
                    If duplicateStage Is Nothing OrElse _session.Project.Stages.Count <> stageCount + 2 Then Throw New InvalidOperationException("复制关卡失败")
                    _session.DeleteStage(duplicateStage)
                    _session.DeleteStage(addedStage)
                    If _session.Project.Stages.Count <> stageCount Then Throw New InvalidOperationException("删除关卡失败")
                    _session.ActivateStage(initialStage)
                    report.Add("stage operations add/duplicate/delete ok")
                    report.Add("PASS")
                End If
            End If
        Catch ex As Exception
            report.Add("FAIL: " & ex.ToString())
        End Try
        Try
            Dim logPath = IO.Path.Combine(IO.Path.GetTempPath(), "DanmakuDesigner-uitest.log")
            File.WriteAllText(logPath, String.Join(Environment.NewLine, report))
        Catch
        End Try
    End Sub

    Private Sub ScheduleCapture()
        Dim timer As New DispatcherTimer With {.Interval = TimeSpan.FromMilliseconds(_captureDelay)}
        AddHandler timer.Tick, Sub(sender As Object, e As EventArgs)
                                   timer.Stop()
                                   CaptureWindow()
                               End Sub
        timer.Start()
    End Sub

    Private Sub CaptureWindow()
        App.LogStartup("CaptureWindow begin path=" & _capturePath)
        Try
            UpdateLayout()
            Dim dpi = VisualTreeHelper.GetDpi(Me)
            Dim pixelWidth = Math.Max(1, CInt(ActualWidth * dpi.DpiScaleX))
            Dim pixelHeight = Math.Max(1, CInt(ActualHeight * dpi.DpiScaleY))
            Dim bitmap As New RenderTargetBitmap(pixelWidth, pixelHeight, 96 * dpi.DpiScaleX, 96 * dpi.DpiScaleY, PixelFormats.Pbgra32)
            bitmap.Render(Me)
            ' 截图时在透明外发光外围补一层深色底，便于文档和聊天窗口展示。
            Dim backdrop As New DrawingVisual()
            Using dc = backdrop.RenderOpen()
                dc.DrawRectangle(New SolidColorBrush(Color.FromRgb(20, 20, 23)), Nothing, New Rect(0, 0, ActualWidth, ActualHeight))
                dc.DrawImage(bitmap, New Rect(0, 0, ActualWidth, ActualHeight))
            End Using
            Dim composed As New RenderTargetBitmap(pixelWidth, pixelHeight, 96 * dpi.DpiScaleX, 96 * dpi.DpiScaleY, PixelFormats.Pbgra32)
            composed.Render(backdrop)
            Dim encoder As New PngBitmapEncoder()
            encoder.Frames.Add(BitmapFrame.Create(composed))
            Dim dir = Path.GetDirectoryName(_capturePath)
            If Not String.IsNullOrEmpty(dir) Then Directory.CreateDirectory(dir)
            Using stream = File.Create(_capturePath)
                encoder.Save(stream)
            End Using
        Catch ex As Exception
            Try
                File.WriteAllText(_capturePath & ".error.txt", ex.ToString())
            Catch
            End Try
        Finally
            _captureExit = True
            Application.Current.Shutdown(0)
        End Try
    End Sub

    ' ============================================================
    ' 会话事件
    ' ============================================================

    Private Sub RefreshAll()
        UpdateWorkspaceVisibility()
        If _session.Project Is Nothing Then
            UpdateTitle()
            UpdateValidation()
            UpdateStatus()
            PreviewPanelControl.RefreshFromSession()
            Return
        End If
        UpdateTitle()
        Explorer.Refresh()
        TimelineEditorView.Refresh()
        PropertyView.Refresh()
        OutputView.Refresh()
        PaletteView.RefreshPalette()
        BlockEditorViewControl.Refresh()
        ResourceManagerView.Refresh()
        BackgroundEditorView.Refresh()
        If _session.ActiveView = "code" Then CodePreviewControl.Refresh()
        PreviewPanelControl.RefreshFromSession()
        If _session.World IsNot Nothing Then TimelineEditorView.SetPlayhead(Math.Max(0, _session.World.Tick))
        UpdateValidation()
        UpdateStatus()
        UpdateCommandStates()
    End Sub

    Private Sub UpdateWorkspaceVisibility()
        Dim hasProject = _session.Project IsNot Nothing
        If WorkspaceGrid IsNot Nothing Then WorkspaceGrid.Visibility = If(hasProject, Visibility.Visible, Visibility.Collapsed)
        If EmptyWorkspace IsNot Nothing Then EmptyWorkspace.Visibility = If(hasProject, Visibility.Collapsed, Visibility.Visible)
    End Sub

    Private Sub UpdateCommandStates()
        Dim hasProject = _session.Project IsNot Nothing
        If SaveMenuItem IsNot Nothing Then SaveMenuItem.IsEnabled = hasProject
        If SaveAsMenuItem IsNot Nothing Then SaveAsMenuItem.IsEnabled = hasProject
        If ExportMenuItem IsNot Nothing Then ExportMenuItem.IsEnabled = hasProject
        If CloseProjectMenuItem IsNot Nothing Then CloseProjectMenuItem.IsEnabled = hasProject
        If SaveButton IsNot Nothing Then SaveButton.IsEnabled = hasProject
        If SaveAsButton IsNot Nothing Then SaveAsButton.IsEnabled = hasProject
        If PlayButton IsNot Nothing Then PlayButton.IsEnabled = hasProject
        If StopButton IsNot Nothing Then StopButton.IsEnabled = hasProject
        If AddTrackButton IsNot Nothing Then AddTrackButton.IsEnabled = hasProject
        If ValidateButton IsNot Nothing Then ValidateButton.IsEnabled = hasProject
        If ExportButton IsNot Nothing Then ExportButton.IsEnabled = hasProject
        If AccentExportButton IsNot Nothing Then AccentExportButton.IsEnabled = hasProject
        If UndoButton IsNot Nothing Then UndoButton.IsEnabled = hasProject AndAlso _session.CanUndo
        If RedoButton IsNot Nothing Then RedoButton.IsEnabled = hasProject AndAlso _session.CanRedo
        If UndoMenu IsNot Nothing Then UndoMenu.IsEnabled = hasProject AndAlso _session.CanUndo
        If RedoMenu IsNot Nothing Then RedoMenu.IsEnabled = hasProject AndAlso _session.CanRedo
    End Sub

    Private Sub Session_ProjectReplaced()
        If Not _loaded Then Return
        _startupAccent = False
        If _startupAccentTimer IsNot Nothing Then _startupAccentTimer.Stop()
        RefreshAll()
        ApplyAccentState(True)
    End Sub

    Private Sub Session_ProjectChanged()
        If Not _loaded Then Return
        If _session.Project Is Nothing Then Return
        UpdateTitle()
        TimelineEditorView.Refresh()
        Explorer.Refresh()
        PropertyView.Refresh()
        OutputView.Refresh()
        PaletteView.RefreshPalette()
        ResourceManagerView.Refresh()
        BackgroundEditorView.Refresh()
        If _session.ActiveView = "code" Then CodePreviewControl.Refresh()
        UpdateValidation()
        UpdateStatus()
    End Sub

    Private Sub Session_SelectionChanged()
        If Not _loaded Then Return
        If _session.Project Is Nothing Then
            UpdateTitle()
            UpdateStatus()
            Return
        End If
        Explorer.Refresh()
        PropertyView.Refresh()
        TimelineEditorView.UpdateSelection()
        UpdateStatus()
    End Sub

    Private Sub Session_BlockContextChanged()
        If Not _loaded Then Return
        If _session.Project Is Nothing Then Return
        BlockEditorViewControl.Refresh()
        PaletteView.RefreshPalette()
        If _session.ActiveView = "blocks" Then UpdateStatus()
    End Sub

    Private Sub Session_ViewChanged()
        ApplyView(_session.ActiveView)
    End Sub

    Private Sub Session_IssuesChanged()
        If Not _loaded Then Return
        UpdateValidation()
        If _session.Project IsNot Nothing Then OutputView.Refresh()
    End Sub

    Private Sub Session_PreviewReset()
        If Not _loaded Then Return
        If _session.Project Is Nothing OrElse _session.World Is Nothing Then
            UpdateStatus()
            Return
        End If
        PreviewPanelControl.RefreshFromSession()
        TimelineEditorView.SetPlayhead(Math.Max(0, _session.World.Tick))
        TickStatusText.Text = "Tick 0 / " & _session.Stage.LengthTicks
    End Sub

    Private Sub Session_PreviewTick(tick As Long)
        If Not _loaded OrElse _session.Project Is Nothing Then Return
        TimelineEditorView.SetPlayhead(tick)
        UpdateTickStatus(tick)
        PreviewPanelControl.NotifyExternalSeek()
    End Sub

    Private Sub Session_PlaybackChanged(playing As Boolean)
        If _loaded Then UpdateStatus()
        ApplyAccentState(True)
    End Sub

    Private Sub Session_TitleChanged()
        UpdateTitle()
    End Sub

    Private Sub Session_StatusMessage(message As String)
        If StatusLeftText IsNot Nothing Then StatusLeftText.Text = If(message, "就绪")
    End Sub

    Private Sub Preview_TickChanged(tick As Long)
        If Not _loaded OrElse _session.Project Is Nothing Then Return
        TimelineEditorView.SetPlayhead(tick)
        UpdateTickStatus(tick)
        If BackgroundEditorView.Visibility = Visibility.Visible Then BackgroundEditorView.SetPreviewTick(tick)
    End Sub

    ' ============================================================
    ' 状态展示
    ' ============================================================

    Private Sub UpdateTitle()
        If DocumentText Is Nothing Then Return
        If _session.Project Is Nothing Then
            BD_Title.Visibility = Visibility.Collapsed
            DocumentText.Text = "未打开项目"
            DirtyText.Visibility = Visibility.Collapsed
            DocumentText.ToolTip = ""
            Title = "弹幕设计器 — DanmakuDesigner"
            UpdateCommandStates()
            Return
        End If
        BD_Title.Visibility = Visibility.Visible
        Dim stage = _session.Stage
        DocumentText.Text = _session.Project.Name
        DirtyText.Visibility = If(_session.IsDirty, Visibility.Visible, Visibility.Collapsed)
        Dim path = If(String.IsNullOrWhiteSpace(_session.FilePath), "（未保存）", _session.FilePath)
        DocumentText.ToolTip = path & vbCrLf & "当前关卡：" & If(stage Is Nothing, "", stage.CodeName)
        Title = "弹幕设计器 — " & DocumentText.Text & If(_session.IsDirty, " *", "") & " — DanmakuDesigner"
        UpdateCommandStates()
    End Sub

    Private Sub UpdateValidation()
        If ValidationStatusText Is Nothing Then Return
        Dim errors = If(_session.Project Is Nothing, 0, _session.ErrorCount())
        Dim warnings = If(_session.Project Is Nothing, 0, _session.WarningCount())
        ValidationStatusText.Text = errors & " 个错误 · " & warnings & " 个警告"
        ValidationStatusText.Foreground = If(errors > 0, Ui.Brush("#FFD7D7"), Ui.Brush("#FFFFFF"))
    End Sub

    Private Sub UpdateStatus()
        If StatusLeftText Is Nothing Then Return
        If _session.Project Is Nothing Then
            StatusLeftText.Text = "就绪"
            If TickStatusText IsNot Nothing Then TickStatusText.Text = ""
            If ViewStatusText IsNot Nothing Then ViewStatusText.Text = ""
            UpdateCommandStates()
            Return
        End If
        If _session.IsPlaying Then
            StatusLeftText.Text = "预览播放中 · 方向键移动，Shift 低速"
        ElseIf _session.BlockContext IsNot Nothing AndAlso _session.ActiveView = "blocks" Then
            StatusLeftText.Text = "积木编程 · " & _session.BlockContext.DisplayName
        Else
            StatusLeftText.Text = "就绪"
        End If
        If _session.World IsNot Nothing Then
            UpdateTickStatus(_session.World.Tick)
        End If
        UpdateViewStatus()
        UpdateCommandStates()
    End Sub

    Private Sub UpdateTickStatus(tick As Long)
        If TickStatusText Is Nothing OrElse _session.Stage Is Nothing Then Return
        TickStatusText.Text = "Tick " & Math.Max(0, tick) & " / " & _session.Stage.LengthTicks
    End Sub

    Private Sub UpdateViewStatus()
        If ViewStatusText Is Nothing Then Return
        If _session.Project Is Nothing Then
            ViewStatusText.Text = ""
            Return
        End If
        If _session.ActiveView = "resources" Then
            ViewStatusText.Text = "游戏资源 · " & _session.Project.Resources.Count & " 个"
            Return
        End If
        Dim name As String
        Select Case _session.ActiveView
            Case "blocks" : name = "积木编程"
            Case "background" : name = "背景编辑"
            Case "preview" : name = "试玩预览"
            Case "code" : name = "代码预览"
            Case "resources" : name = "游戏资源"
            Case Else : name = "时间轴"
        End Select
        ViewStatusText.Text = name & " · " & _session.Stage.Tracks.Count & " 轨 · " & _session.Stage.AllClips().Count() & " 片段"
        If _session.ActiveView = "background" Then
            ViewStatusText.Text = "背景编辑 · " & _session.Stage.BackgroundLayers.Count & " 图层 · " & _session.Stage.Tracks.Count & " 轨"
        End If
    End Sub

    Private Sub ApplyView(view As String)
        If Not _loaded Then Return
        If _session.Project Is Nothing Then Return
        _switchingTabs = True
        Try
            TabTimeline.IsChecked = (view = "timeline")
            TabBlocks.IsChecked = (view = "blocks")
            TabBackground.IsChecked = (view = "background")
            TabPreview.IsChecked = (view = "preview")
            TabCode.IsChecked = (view = "code")
            TabResources.IsChecked = (view = "resources")
            TimelineEditorView.Visibility = If(view = "timeline", Visibility.Visible, Visibility.Collapsed)
            BlockEditorViewControl.Visibility = If(view = "blocks", Visibility.Visible, Visibility.Collapsed)
            BackgroundEditorView.Visibility = If(view = "background", Visibility.Visible, Visibility.Collapsed)
            PreviewPanelControl.Visibility = If(view = "preview", Visibility.Visible, Visibility.Collapsed)
            CodePreviewControl.Visibility = If(view = "code", Visibility.Visible, Visibility.Collapsed)
            ResourceManagerView.Visibility = If(view = "resources", Visibility.Visible, Visibility.Collapsed)
        Finally
            _switchingTabs = False
        End Try
        Select Case view
            Case "timeline"
                TimelineEditorView.Refresh()
            Case "blocks"
                BlockEditorViewControl.Refresh()
                PaletteView.RefreshPalette()
            Case "background"
                BackgroundEditorView.Refresh()
            Case "preview"
                PreviewPanelControl.Focus()
            Case "code"
                CodePreviewControl.Refresh()
            Case "resources"
                ResourceManagerView.Refresh()
        End Select
        UpdateViewStatus()
        UpdateStatus()
    End Sub

    Private Sub Tab_Checked(sender As Object, e As RoutedEventArgs)
        If _switchingTabs OrElse Not _loaded Then Return
        Dim radio = TryCast(sender, RadioButton)
        If radio Is Nothing Then Return
        Dim tag = If(radio.Tag, "timeline").ToString()
        If _session.ActiveView <> tag Then _session.SetView(tag)
    End Sub

    ' ============================================================
    ' 文件命令
    ' ============================================================

    Private Sub New_Click(sender As Object, e As RoutedEventArgs)
        If Not ConfirmDiscard() Then Return
        _session.NewProject()
        _session.SetView("timeline")
        _session.AddLog(IssueSeverity.Info, "已新建空白工程。")
    End Sub

    Private Sub Open_Click(sender As Object, e As RoutedEventArgs)
        If Not ConfirmDiscard() Then Return
        Dim dialog As New Microsoft.Win32.OpenFileDialog With {
            .Title = "打开 DanmakuDesigner 工程",
            .Filter = "Danmaku Designer 工程 (*.dmkproj)|*.dmkproj|所有文件 (*.*)|*.*"
        }
        If dialog.ShowDialog() = True Then
            If _session.OpenProject(dialog.FileName) Then _session.SetView("timeline")
        End If
    End Sub

    Private Sub Save_Click(sender As Object, e As RoutedEventArgs)
        If _session.Project Is Nothing Then Return
        _session.SaveProject()
        UpdateTitle()
    End Sub

    Private Sub SaveAs_Click(sender As Object, e As RoutedEventArgs)
        If _session.Project Is Nothing Then Return
        _session.SaveProjectAs()
        UpdateTitle()
    End Sub

    Private Sub CloseProject_Click(sender As Object, e As RoutedEventArgs)
        If _session.Project Is Nothing Then Return
        If Not ConfirmDiscard() Then Return
        _session.CloseProject()
    End Sub

    Private Sub Export_Click(sender As Object, e As RoutedEventArgs)
        ExportCode()
    End Sub

    Public Sub ExportCode()
        If _session.Project Is Nothing Then Return
        _session.Validate()
        Dim dialog As New ExportDialog With {.Owner = Me}
        If dialog.ShowDialog() = True Then
            StatusLeftText.Text = "已导出解决方案：" & dialog.ResultFolder
            _session.AddLog(IssueSeverity.Info, "已导出完整 AgSTG 解决方案到 " & dialog.ResultFolder)
        End If
    End Sub

    Private Sub Exit_Click(sender As Object, e As RoutedEventArgs)
        Close()
    End Sub

    ' ============================================================
    ' 编辑 / 视图命令
    ' ============================================================

    Private Sub Undo_Click(sender As Object, e As RoutedEventArgs)
        _session.Undo()
    End Sub

    Private Sub Redo_Click(sender As Object, e As RoutedEventArgs)
        _session.Redo()
    End Sub

    Private Sub Delete_Click(sender As Object, e As RoutedEventArgs)
        DeleteCurrent()
    End Sub

    Private Sub DeleteCurrent()
        If _session.Project Is Nothing Then Return
        If IsTextEditing() Then Return
        If _session.ActiveView = "blocks" Then
            BlockEditorViewControl.DeleteSelectedBlock()
        ElseIf _session.ActiveView = "timeline" Then
            TimelineEditorView.DeleteSelected()
        End If
    End Sub

    Private Sub Duplicate_Click(sender As Object, e As RoutedEventArgs)
        DuplicateCurrent()
    End Sub

    Private Sub DuplicateCurrent()
        If _session.Project Is Nothing Then Return
        If IsTextEditing() Then Return
        If _session.ActiveView = "blocks" Then
            BlockEditorViewControl.DuplicateSelectedBlock()
        ElseIf _session.ActiveView = "timeline" Then
            TimelineEditorView.DuplicateSelected()
        End If
    End Sub

    Private Sub ViewTimeline_Click(sender As Object, e As RoutedEventArgs)
        If _session.Project Is Nothing Then Return
        _session.SetView("timeline")
    End Sub

    Private Sub ViewBlocks_Click(sender As Object, e As RoutedEventArgs)
        If _session.Project Is Nothing Then Return
        If _session.BlockContext Is Nothing AndAlso _session.Stage.EnemyTemplates.Count > 0 Then
            _session.EditTemplateBehavior(_session.Stage.EnemyTemplates(0))
        Else
            _session.SetView("blocks")
        End If
    End Sub

    Private Sub ViewPreview_Click(sender As Object, e As RoutedEventArgs)
        If _session.Project Is Nothing Then Return
        _session.SetView("preview")
    End Sub

    Private Sub ViewCode_Click(sender As Object, e As RoutedEventArgs)
        If _session.Project Is Nothing Then Return
        _session.SetView("code")
    End Sub

    Private Sub ViewResources_Click(sender As Object, e As RoutedEventArgs)
        If _session.Project Is Nothing Then Return
        _session.SetView("resources")
    End Sub

    Private Sub ViewBackground_Click(sender As Object, e As RoutedEventArgs)
        If _session.Project Is Nothing Then Return
        _session.SetView("background")
    End Sub

    Private Sub ToggleOutput_Click(sender As Object, e As RoutedEventArgs)
        Dim show = If(OutputMenu.IsChecked, True, False)
        OutputRow.Height = If(show, New GridLength(168), New GridLength(0))
        OutputSplitterRow.Height = If(show, New GridLength(4), New GridLength(0))
        OutputView.Visibility = If(show, Visibility.Visible, Visibility.Collapsed)
    End Sub

    Private Sub ResetLayout_Click(sender As Object, e As RoutedEventArgs)
        WorkspaceGrid.ColumnDefinitions(0).Width = New GridLength(248)
        WorkspaceGrid.ColumnDefinitions(2).Width = New GridLength(1, GridUnitType.Star)
        WorkspaceGrid.ColumnDefinitions(4).Width = New GridLength(312)
        RightColumnGrid.RowDefinitions(2).Height = New GridLength(330)
        OutputMenu.IsChecked = True
        OutputRow.Height = New GridLength(168)
        OutputSplitterRow.Height = New GridLength(4)
        OutputView.Visibility = Visibility.Visible
        StatusLeftText.Text = "已重置面板布局；拖动分栏线可再次调整。"
    End Sub

    ' ============================================================
    ' 关卡命令
    ' ============================================================

    Private Sub AddTemplate_Click(sender As Object, e As RoutedEventArgs)
        If _session.Project Is Nothing Then Return
        _session.SetView("timeline")
        Explorer.AddNewTemplate()
    End Sub

    Private Sub AddStage_Click(sender As Object, e As RoutedEventArgs)
        If _session.Project Is Nothing Then Return
        Explorer.AddNewStage()
    End Sub

    Private Sub AddPattern_Click(sender As Object, e As RoutedEventArgs)
        If _session.Project Is Nothing Then Return
        _session.SetView("timeline")
        Explorer.AddNewPattern()
    End Sub

    Private Sub AddTrack_Click(sender As Object, e As RoutedEventArgs)
        If _session.Project Is Nothing Then Return
        _session.SetView("timeline")
        TimelineEditorView.ShowAddTrackMenuForToolbar()
    End Sub

    Private Sub Validate_Click(sender As Object, e As RoutedEventArgs)
        If _session.Project Is Nothing Then Return
        _session.Validate()
        _session.AddLog(IssueSeverity.Info, "验证完成：" & _session.ErrorCount() & " 个错误，" & _session.WarningCount() & " 个警告。")
        OutputView.Refresh()
        StatusLeftText.Text = "验证完成：" & _session.ErrorCount() & " 个错误，" & _session.WarningCount() & " 个警告"
    End Sub

    Private Sub ResetPreview_Click(sender As Object, e As RoutedEventArgs)
        If _session.Project Is Nothing Then Return
        _session.RebuildWorld()
        _session.SetView("preview")
    End Sub

    Private Sub PlayPause_Click(sender As Object, e As RoutedEventArgs)
        If _session.Project Is Nothing Then Return
        _session.SetView("preview")
        Dispatcher.BeginInvoke(Sub() PreviewPanelControl.TogglePlay(), DispatcherPriority.Background)
    End Sub

    Private Sub RestartPreview_Click(sender As Object, e As RoutedEventArgs)
        If _session.Project Is Nothing Then Return
        _session.SetView("preview")
        Dispatcher.BeginInvoke(Sub() PreviewPanelControl.RestartPlayback(), DispatcherPriority.Background)
    End Sub

    Private Sub StopPreview_Click(sender As Object, e As RoutedEventArgs)
        If _session.Project Is Nothing Then Return
        _session.SetView("preview")
        Dispatcher.BeginInvoke(Sub() PreviewPanelControl.StopPlayback(), DispatcherPriority.Background)
    End Sub

    Private Sub Help_Click(sender As Object, e As RoutedEventArgs)
        MessageBox.Show(Me,
            "时间轴" & vbCrLf &
            "  · 拖动片段改变开始时间，拖动右边缘调整数量" & vbCrLf &
            "  · 双击空轨道在该位置生成片段，Delete 删除" & vbCrLf &
            "  · 拖动标尺 / 点击轨道可跳转预览帧" & vbCrLf &
            "  · Ctrl + 滚轮缩放时间轴，Alt 临时关闭吸附" & vbCrLf & vbCrLf &
            "积木编程" & vbCrLf &
            "  · 从右侧积木库拖入「事件积木」创建脚本" & vbCrLf &
            "  · 行为积木按顺序执行；C 形积木可嵌套" & vbCrLf &
            "  · 右键数字字段可插入「取值器」（血量、距离、随机数等）" & vbCrLf &
            "  · 「等待」只能放在脚本顶层" & vbCrLf & vbCrLf &
            "背景编辑" & vbCrLf &
            "  · Ctrl+6 打开背景编辑，添加 2D / 3D 图层" & vbCrLf &
            "  · 3D 图层可打开独立建模工具；左键旋转、右键平移、滚轮缩放" & vbCrLf &
            "  · 时间轴用菱形关键帧控制摄像机、2D 位移和粒子" & vbCrLf &
            "  · 3D 关键帧属性中「选择视角」可用 WASD + 鼠标确定位置" & vbCrLf & vbCrLf &
            "预览" & vbCrLf &
            "  · F5 播放/暂停，F6 从头播放，Shift+F5 停止" & vbCrLf &
            "  · 预览工具栏勾选「引擎」后，AgSTG STG 控件直接运行当前关卡" & vbCrLf &
            "  · 方向键移动自机，Shift 低速，R 重新开始" & vbCrLf & vbCrLf &
            "导出" & vbCrLf &
            "  · Ctrl+E 导出完整 AgSTG 解决方案；会解压内置引擎并替换关卡与 TH07 资源包。",
            "快捷键与操作说明", MessageBoxButton.OK, MessageBoxImage.Information)
    End Sub

    Private Sub About_Click(sender As Object, e As RoutedEventArgs)
        MessageBox.Show(Me,
            "DanmakuDesigner 1.0.0" & vbCrLf &
            "Powered by AgSTG 3" & vbCrLf & vbCrLf &
            "©AGANG",
            "关于 DanmakuDesigner", MessageBoxButton.OK, MessageBoxImage.Information)
    End Sub

    Public Sub FocusProperties()
        _session.SetView("timeline")
        PropertyView.BringIntoView()
        StatusLeftText.Text = "属性面板位于窗口右侧。"
    End Sub

    ' ============================================================
    ' 窗口命令
    ' ============================================================

    Private Sub Minimize_Click(sender As Object, e As RoutedEventArgs)
        WindowState = WindowState.Minimized
    End Sub

    Private Sub Maximize_Click(sender As Object, e As RoutedEventArgs)
        WindowState = If(WindowState = WindowState.Maximized, WindowState.Normal, WindowState.Maximized)
    End Sub

    ' ============================================================
    ' 原生最大化工作区约束（避免覆盖任务栏）
    ' ============================================================

    Private Const WM_GETMINMAXINFO As Integer = &H24
    Private Const MONITOR_DEFAULTTONEAREST As UInteger = 2

    <StructLayout(LayoutKind.Sequential)>
    Private Structure NativePoint
        Public X As Integer
        Public Y As Integer
    End Structure

    <StructLayout(LayoutKind.Sequential)>
    Private Structure NativeRect
        Public Left As Integer
        Public Top As Integer
        Public Right As Integer
        Public Bottom As Integer
    End Structure

    <StructLayout(LayoutKind.Sequential)>
    Private Structure MinMaxInfo
        Public Reserved As NativePoint
        Public MaxSize As NativePoint
        Public MaxPosition As NativePoint
        Public MinTrackSize As NativePoint
        Public MaxTrackSize As NativePoint
    End Structure

    <StructLayout(LayoutKind.Sequential)>
    Private Structure MonitorInfo
        Public cbSize As Integer
        Public rcMonitor As NativeRect
        Public rcWork As NativeRect
        Public dwFlags As Integer
    End Structure

    <DllImport("user32.dll")>
    Private Shared Function MonitorFromWindow(hwnd As IntPtr, flags As UInteger) As IntPtr
    End Function

    <DllImport("user32.dll", CharSet:=CharSet.Auto)>
    Private Shared Function GetMonitorInfo(hMonitor As IntPtr, ByRef info As MonitorInfo) As Boolean
    End Function

    Private _hwndSource As HwndSource

    Private Sub Window_SourceInitialized(sender As Object, e As EventArgs)
        _hwndSource = TryCast(PresentationSource.FromVisual(Me), HwndSource)
        If _hwndSource IsNot Nothing Then _hwndSource.AddHook(AddressOf WindowProc)
    End Sub

    Private Function WindowProc(hwnd As IntPtr, msg As Integer, wParam As IntPtr, lParam As IntPtr, ByRef handled As Boolean) As IntPtr
        If msg = WM_GETMINMAXINFO Then
            Try
                Dim info As New MonitorInfo With {.cbSize = Marshal.SizeOf(GetType(MonitorInfo))}
                Dim monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST)
                If monitor <> IntPtr.Zero AndAlso GetMonitorInfo(monitor, info) Then
                    Dim mmi = Marshal.PtrToStructure(Of MinMaxInfo)(lParam)
                    mmi.MaxPosition.X = info.rcWork.Left - info.rcMonitor.Left
                    mmi.MaxPosition.Y = info.rcWork.Top - info.rcMonitor.Top
                    mmi.MaxSize.X = info.rcWork.Right - info.rcWork.Left
                    mmi.MaxSize.Y = info.rcWork.Bottom - info.rcWork.Top
                    Marshal.StructureToPtr(mmi, lParam, True)
                    handled = True
                End If
            Catch
            End Try
        End If
        Return IntPtr.Zero
    End Function

    Private Sub UpdateMaximizeUi()
        If MaxIcon Is Nothing Then Return
        Dim maximized = WindowState = WindowState.Maximized
        MaxIcon.Source = New BitmapImage(New Uri(If(maximized,
                                                    "pack://application:,,,/Assets/Window/restore.png",
                                                    "pack://application:,,,/Assets/Window/maximize.png")))
        If AccentGlow IsNot Nothing Then AccentGlow.GlowSize = If(maximized, 0, 10)
        ' 最大化时关闭 WindowChrome 的自定义边缘缩放，避免还能拖边框改尺寸。
        ResizeMode = If(maximized, ResizeMode.NoResize, ResizeMode.CanResize)
        Dim chrome = WindowChrome.GetWindowChrome(Me)
        If chrome IsNot Nothing Then
            chrome.ResizeBorderThickness = If(maximized, New Thickness(0), New Thickness(10))
        End If
        ApplyAccentState(False)
    End Sub

    Private Sub Close_Click(sender As Object, e As RoutedEventArgs)
        Close()
    End Sub

    Private Sub Window_StateChanged(sender As Object, e As EventArgs)
        UpdateMaximizeUi()
    End Sub

    Private Sub Window_Closing(sender As Object, e As ComponentModel.CancelEventArgs)
        If _captureExit Then Return
        If Not _session.IsDirty Then Return
        Dim label = If(_session.Project Is Nothing, "工程", _session.Project.Name & If(_session.Stage Is Nothing, "", " / " & _session.Stage.Name))
        Dim result = MessageBox.Show(Me, "游戏工程【" & label & "】已修改，是否保存？", "保存更改",
                                     MessageBoxButton.YesNoCancel, MessageBoxImage.Question)
        If result = MessageBoxResult.Cancel Then
            e.Cancel = True
        ElseIf result = MessageBoxResult.Yes Then
            _session.SaveProject()
            If _session.IsDirty Then e.Cancel = True
        End If
    End Sub

    Private Sub Window_PreviewKeyDown(sender As Object, e As KeyEventArgs)
        Dim ctrl = (Keyboard.Modifiers And ModifierKeys.Control) = ModifierKeys.Control
        Dim shift = (Keyboard.Modifiers And ModifierKeys.Shift) = ModifierKeys.Shift
        If ctrl Then
            Select Case e.Key
                Case Key.N
                    New_Click(Nothing, Nothing)
                    e.Handled = True
                Case Key.O
                    Open_Click(Nothing, Nothing)
                    e.Handled = True
                Case Key.S
                    If shift Then SaveAs_Click(Nothing, Nothing) Else Save_Click(Nothing, Nothing)
                    e.Handled = True
                Case Key.E
                    ExportCode()
                    e.Handled = True
                Case Key.D1, Key.NumPad1
                    _session.SetView("timeline")
                    e.Handled = True
                Case Key.D2, Key.NumPad2
                    ViewBlocks_Click(Nothing, Nothing)
                    e.Handled = True
                Case Key.D3, Key.NumPad3
                    _session.SetView("preview")
                    e.Handled = True
                Case Key.D4, Key.NumPad4
                    _session.SetView("code")
                    e.Handled = True
                Case Key.D5, Key.NumPad5
                    _session.SetView("resources")
                    e.Handled = True
                Case Key.D6, Key.NumPad6
                    _session.SetView("background")
                    e.Handled = True
                Case Key.Z
                    If Not IsTextEditing() Then
                        _session.Undo()
                        e.Handled = True
                    End If
                Case Key.Y
                    If Not IsTextEditing() Then
                        _session.Redo()
                        e.Handled = True
                    End If
                Case Key.D
                    DuplicateCurrent()
                    e.Handled = True
            End Select
            Return
        End If
        Select Case e.Key
            Case Key.F5
                If shift Then
                    StopPreview_Click(Nothing, Nothing)
                Else
                    PlayPause_Click(Nothing, Nothing)
                End If
                e.Handled = True
            Case Key.F6
                RestartPreview_Click(Nothing, Nothing)
                e.Handled = True
            Case Key.F7
                Validate_Click(Nothing, Nothing)
                e.Handled = True
            Case Key.Delete
                If Not IsTextEditing() Then
                    DeleteCurrent()
                    e.Handled = True
                End If
        End Select
    End Sub

    Private Function IsTextEditing() As Boolean
        Dim focused = Keyboard.FocusedElement
        Return TypeOf focused Is TextBox OrElse TypeOf focused Is ComboBox OrElse TypeOf focused Is RichTextBox
    End Function

    Private Function ConfirmDiscard() As Boolean
        If _session.Project Is Nothing Then Return True
        If Not _session.IsDirty Then Return True
        Dim result = MessageBox.Show(Me, "当前工程有未保存的修改，继续操作会丢失这些修改。是否先保存？", "未保存的修改",
                                     MessageBoxButton.YesNoCancel, MessageBoxImage.Warning)
        If result = MessageBoxResult.Cancel Then Return False
        If result = MessageBoxResult.No Then Return True
        _session.SaveProject()
        Return Not _session.IsDirty
    End Function

    Public Sub SelectByKindAndId(kind As String, id As String)
        If _session.Stage Is Nothing Then Return
        Select Case kind
            Case "template"
                Dim template = _session.Stage.FindTemplate(id)
                If template IsNot Nothing Then _session.EditTemplateBehavior(template)
            Case "pattern"
                Dim pattern = _session.Stage.FindPattern(id)
                If pattern IsNot Nothing Then _session.EditPatternBehavior(pattern)
            Case Else
                _session.SelectById(kind, id)
        End Select
    End Sub

    Public Sub SeekPreviewTo(tick As Long)
        _session.SeekPreview(tick)
        PreviewPanelControl.RefreshFromSession()
    End Sub

End Class
