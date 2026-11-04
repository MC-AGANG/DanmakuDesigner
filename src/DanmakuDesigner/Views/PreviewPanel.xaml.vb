Imports System.Diagnostics
Imports System.Windows
Imports System.Windows.Input
Imports System.Windows.Threading
Imports DanmakuDesigner.Core

Public Class PreviewPanel

    Public Event TickChanged(tick As Long)

    Private ReadOnly _timer As DispatcherTimer
    Private ReadOnly _watch As New Stopwatch()
    Private Renderer As PlayfieldRenderer
    Private _engineSurface As EnginePreviewSurface
    Private _useEngine As Boolean = True
    Private _engineError As String = ""
    Private _accumulator As Double
    Private _speed As Double = 1
    Private _world As PreviewWorld
    Private _window As Window
    Private _inputX As Integer
    Private _inputY As Integer
    Private _inputSlow As Boolean
    Private _seeking As Boolean
    Private _lastSeek As DateTime = DateTime.MinValue
    Private _fpsWatch As New Stopwatch()
    Private _perfFrameStart As Long
    Private _perfFrames As Integer
    Private _perfWorkTicks As Long
    Private _statTickCounter As Integer

    Public Sub New()
        InitializeComponent()
        Renderer = New PlayfieldRenderer()
        RendererHost.Children.Add(Renderer)
        _engineSurface = New EnginePreviewSurface() With {.Visibility = Visibility.Collapsed}
        RendererHost.Children.Add(_engineSurface)
        _timer = New DispatcherTimer(TimeSpan.FromMilliseconds(15), DispatcherPriority.Render, AddressOf Timer_Tick, Dispatcher)
        _timer.Stop()
        AddHandler Loaded, AddressOf Panel_Loaded
        AddHandler Unloaded, AddressOf Panel_Unloaded
        AddHandler SizeChanged, AddressOf Panel_SizeChanged
        SpeedCombo.SelectedIndex = 1
        HitboxCheck.IsChecked = True
        GridCheck.IsChecked = False
        Renderer.ShowHitbox = True
    End Sub

    Private Sub Panel_Loaded(sender As Object, e As RoutedEventArgs)
        _window = Window.GetWindow(Me)
        If _window IsNot Nothing Then
            AddHandler _window.PreviewKeyDown, AddressOf Window_KeyDown
            AddHandler _window.PreviewKeyUp, AddressOf Window_KeyUp
            AddHandler _window.Deactivated, AddressOf Window_Deactivated
        End If
        RefreshFromSession()
    End Sub

    Private Sub Panel_Unloaded(sender As Object, e As RoutedEventArgs)
        _timer.Stop()
        If _window IsNot Nothing Then
            RemoveHandler _window.PreviewKeyDown, AddressOf Window_KeyDown
            RemoveHandler _window.PreviewKeyUp, AddressOf Window_KeyUp
            RemoveHandler _window.Deactivated, AddressOf Window_Deactivated
        End If
    End Sub

    Private Sub Panel_SizeChanged(sender As Object, e As SizeChangedEventArgs)
        UpdateSeekBar()
    End Sub

    Public Sub RefreshFromSession()
        _timer.Stop()
        _world = App.Session.World
        Renderer.World = _world
        If _world IsNot Nothing Then
            GodModeCheck.IsChecked = _world.GodMode
            Renderer.ShowHitbox = If(HitboxCheck.IsChecked, True, False)
            Renderer.ShowGrid = If(GridCheck.IsChecked, True, False)
            If _world.Stage IsNot Nothing Then
                StageNameText.Text = _world.Stage.Name
                StageMetaText.Text = _world.Stage.Difficulty.ToString().ToUpperInvariant() & " · " & (_world.Stage.LengthTicks / 60.0).ToString("0.0") & " 秒 · " & _world.Stage.EnemyTemplates.Count & " 种敌机"
            End If
        End If
        If _useEngine Then
            LoadEngineSurface()
        Else
            Renderer.Visibility = Visibility.Visible
            _engineSurface.Visibility = Visibility.Collapsed
        End If
        UpdatePlayUi()
        UpdateStats()
        Renderer.InvalidateVisual()
    End Sub

    Private Sub LoadEngineSurface()
        _engineError = ""
        Renderer.Visibility = Visibility.Collapsed
        _engineSurface.Visibility = Visibility.Visible
        Dim stageProject = App.Session.ActiveStageProject
        If stageProject Is Nothing Then
            _engineError = "没有活动关卡"
            _engineSurface.Visibility = Visibility.Collapsed
            Renderer.Visibility = Visibility.Visible
            Return
        End If
        Try
            _engineSurface.LoadStage(stageProject, App.Session.EngineRepo)
        Catch ex As Exception
            _engineError = ex.Message
            App.Session.AddLog(IssueSeverity.Warning, "引擎预览失败：" & ex.ToString())
            _engineSurface.Visibility = Visibility.Collapsed
            Renderer.Visibility = Visibility.Visible
        End Try
    End Sub

    Private Sub EngineMode_Changed(sender As Object, e As RoutedEventArgs)
        If _engineSurface Is Nothing Then Return
        _useEngine = If(EngineModeCheck.IsChecked, True, False)
        If _useEngine Then
            LoadEngineSurface()
        Else
            Renderer.Visibility = Visibility.Visible
            _engineSurface.Visibility = Visibility.Collapsed
        End If
        UpdateStats()
    End Sub

    ''' <summary>时间轴跳转等外部操作修改了预览世界后，刷新画面。</summary>
    Public Sub NotifyExternalSeek()
        If _timer.IsEnabled Then Return
        _world = App.Session.World
        Renderer.World = _world
        If _useEngine AndAlso _engineSurface.IsReady AndAlso _world IsNot Nothing Then
            Try
                _engineSurface.SeekTo(_world.Tick)
            Catch ex As Exception
                _engineError = ex.Message
            End Try
        Else
            Renderer.InvalidateVisual()
        End If
        UpdateStats()
    End Sub

    Public Sub Play()
        If _world Is Nothing Then Return
        If _useEngine Then
            If Not _engineSurface.IsReady Then LoadEngineSurface()
            If Not _engineSurface.IsReady Then
                UpdateStats()
                Return
            End If
            If _engineSurface.IsGameOver OrElse _engineSurface.IsCleared Then _engineSurface.Reset()
        ElseIf _world.IsGameOver OrElse _world.IsCleared Then
            _world.Reset()
        End If
        App.Session.SetPlaying(True)
        _watch.Restart()
        _fpsWatch.Restart()
        _perfFrameStart = 0
        _perfFrames = 0
        _perfWorkTicks = 0
        _statTickCounter = 0
        _accumulator = 0
        _timer.Start()
        UpdatePlayUi()
        UpdateStats()
    End Sub

    Public Sub Pause()
        _timer.Stop()
        App.Session.SetPlaying(False)
        UpdatePlayUi()
        UpdateStats()
    End Sub

    Public Sub TogglePlay()
        If _timer.IsEnabled Then
            Pause()
        Else
            Play()
        End If
    End Sub

    Public Sub StopPlayback()
        _timer.Stop()
        App.Session.SetPlaying(False)
        If _useEngine Then
            If _engineSurface IsNot Nothing AndAlso _engineSurface.IsReady Then _engineSurface.Reset()
        ElseIf _world IsNot Nothing Then
            _world.Reset()
            Renderer.InvalidateVisual()
        End If
        UpdatePlayUi()
        UpdateStats()
        RaiseEvent TickChanged(0)
    End Sub

    Public Sub RestartPlayback()
        If _useEngine Then
            If _engineSurface IsNot Nothing Then _engineSurface.Reset()
        ElseIf _world IsNot Nothing Then
            _world.Reset()
        End If
        Play()
    End Sub

    Public ReadOnly Property IsRunning As Boolean
        Get
            Return _timer.IsEnabled
        End Get
    End Property

    Private Sub Timer_Tick(sender As Object, e As EventArgs)
        _perfFrameStart = Stopwatch.GetTimestamp()
        _statTickCounter = (_statTickCounter + 1) Mod 20
        Dim updateStatsNow = (_statTickCounter = 0)
        Try
            If _useEngine Then
                If _engineSurface Is Nothing OrElse Not _engineSurface.IsReady Then
                    UpdateStats()
                    Return
                End If
                Dim engineElapsed = _watch.Elapsed.TotalSeconds
                _watch.Restart()
                _accumulator += engineElapsed * 60.0 * _speed
                Dim engineSteps = 0
                _engineSurface.InputX = _inputX
                _engineSurface.InputY = _inputY
                _engineSurface.InputSlow = _inputSlow OrElse Keyboard.IsKeyDown(Key.LeftShift)
                While _accumulator >= 1.0 AndAlso engineSteps < 8
                    _engineSurface.Advance()
                    _accumulator -= 1
                    engineSteps += 1
                End While
                If _engineSurface.IsCleared Then
                    _timer.Stop()
                    App.Session.SetPlaying(False)
                    UpdatePlayUi()
                    updateStatsNow = True
                End If
                If updateStatsNow Then UpdateStats()
                RaiseEvent TickChanged(_engineSurface.Tick)
                Return
            End If
            If _world Is Nothing Then Return
            Dim elapsed = _watch.Elapsed.TotalSeconds
            _watch.Restart()
            _accumulator += elapsed * 60.0 * _speed
            Dim steps = 0
            _world.InputX = _inputX
            _world.InputY = _inputY
            _world.InputSlow = _inputSlow OrElse Keyboard.IsKeyDown(Key.LeftShift)
            While _accumulator >= 1.0 AndAlso steps < 8
                _world.Advance()
                _accumulator -= 1
                steps += 1
            End While
            If steps = 0 Then
                Renderer.InvalidateVisual()
                Return
            End If
            If _world.IsCleared Then
                _timer.Stop()
                App.Session.SetPlaying(False)
                UpdatePlayUi()
                updateStatsNow = True
            End If
            Renderer.InvalidateVisual()
            If updateStatsNow Then UpdateStats()
            RaiseEvent TickChanged(_world.Tick)
        Finally
            RecordPerformance()
        End Try
    End Sub

    Private Sub RecordPerformance()
        If Not _timer.IsEnabled OrElse _perfFrameStart = 0 Then Return
        Dim now = Stopwatch.GetTimestamp()
        _perfWorkTicks += Math.Max(0, now - _perfFrameStart)
        _perfFrames += 1
        If _perfFrames < 30 Then Return

        Dim frames = _perfFrames
        Dim elapsed = _fpsWatch.Elapsed.TotalSeconds
        _fpsWatch.Restart()
        Dim fps = If(elapsed > 0.0001, frames / elapsed, 0.0)
        Dim frameMs = _perfWorkTicks * 1000.0 / (Stopwatch.Frequency * frames)
        _perfFrames = 0
        _perfWorkTicks = 0
        FpsValue.Text = fps.ToString("0") & " FPS"
        FpsChartValue.Text = fps.ToString("0") & " FPS"
        LoadChartValue.Text = frameMs.ToString("0.0") & " ms"
        If FpsChart IsNot Nothing Then FpsChart.Add(fps)
        If LoadChart IsNot Nothing Then LoadChart.Add(frameMs)
    End Sub

    Private Sub Play_Click(sender As Object, e As RoutedEventArgs)
        TogglePlay()
    End Sub

    Private Sub Stop_Click(sender As Object, e As RoutedEventArgs)
        StopPlayback()
    End Sub

    Private Sub Restart_Click(sender As Object, e As RoutedEventArgs)
        RestartPlayback()
    End Sub

    Private Sub Speed_Changed(sender As Object, e As SelectionChangedEventArgs)
        Dim item = TryCast(SpeedCombo.SelectedItem, ComboBoxItem)
        If item IsNot Nothing Then
            Dim value As Double = 1
            If Double.TryParse(CStr(item.Tag), Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, value) Then
                _speed = value
            End If
        End If
    End Sub

    Private Sub GodMode_Changed(sender As Object, e As RoutedEventArgs)
        If _world IsNot Nothing Then _world.GodMode = If(GodModeCheck.IsChecked, True, False)
    End Sub

    Private Sub Display_Changed(sender As Object, e As RoutedEventArgs)
        Renderer.ShowHitbox = If(HitboxCheck.IsChecked, True, False)
        Renderer.ShowGrid = If(GridCheck.IsChecked, True, False)
        Renderer.InvalidateVisual()
    End Sub

    Private Sub Damage_Click(sender As Object, e As RoutedEventArgs)
        If _useEngine Then
            If _engineSurface Is Nothing OrElse Not _engineSurface.IsReady Then Return
            For Each enemyObject In AgSTG.STG.SearchEnemy()
                If enemyObject.HP > 0 Then enemyObject.Damage(Math.Max(1, enemyObject.HP * 0.15))
            Next
            UpdateStats()
            Return
        End If
        If _world Is Nothing Then Return
        For Each actor In _world.Enemies
            If Not actor.IsDying Then
                actor.HP -= Math.Max(1, actor.MaxHP * 0.15)
            End If
        Next
        Renderer.InvalidateVisual()
        UpdateStats()
    End Sub

    Private Sub ClearBullets_Click(sender As Object, e As RoutedEventArgs)
        If _useEngine Then
            If _engineSurface Is Nothing OrElse Not _engineSurface.IsReady Then Return
            AgSTG.STG.ClearBullet()
            UpdateStats()
            Return
        End If
        If _world Is Nothing Then Return
        _world.BreakAllBullets()
        Renderer.InvalidateVisual()
        UpdateStats()
    End Sub

    Private Sub UpdatePlayUi()
        Dim running = _timer.IsEnabled
        PlayText.Text = If(running, "暂停", "播放")
        PlayIcon.Source = IconLibrary.Get(If(running, "Pause_16x.png", "Run_16xMD.png"))
        Dim atTick = If(_useEngine, If(_engineSurface Is Nothing, 0L, _engineSurface.Tick), If(_world Is Nothing, 0L, _world.Tick))
        Dim cleared = If(_useEngine, _engineSurface IsNot Nothing AndAlso _engineSurface.IsCleared, _world IsNot Nothing AndAlso _world.IsCleared)
        Dim over = If(_useEngine, _engineSurface IsNot Nothing AndAlso _engineSurface.IsGameOver, _world IsNot Nothing AndAlso _world.IsGameOver)
        PausedOverlay.Visibility = If(Not running AndAlso atTick > 0 AndAlso Not cleared AndAlso Not over, Visibility.Visible, Visibility.Collapsed)
    End Sub

    Private Sub UpdateStats()
        If _useEngine Then
            If _engineSurface IsNot Nothing AndAlso _engineSurface.IsReady Then
                TimeValue.Text = _engineSurface.TimeText
                TickValue.Text = Math.Max(0L, _engineSurface.Tick).ToString("N0") & " / " & _engineSurface.LengthTicks.ToString("N0")
                EntityValue.Text = _engineSurface.EntityCount.ToString()
                MusicValue.Text = "AgSTG"
                If _engineSurface.IsGameOver Then
                    StatusValue.Text = "被弹 · 游戏结束"
                    StatusDot.Fill = Ui.Brush("#F14C4C")
                ElseIf _engineSurface.IsCleared Then
                    StatusValue.Text = "关卡完成"
                    StatusDot.Fill = Ui.Brush("#89D185")
                ElseIf _timer.IsEnabled Then
                    StatusValue.Text = "引擎播放中"
                    StatusDot.Fill = Ui.Brush("#89D185")
                ElseIf _engineSurface.Tick > 0 Then
                    StatusValue.Text = "已暂停"
                    StatusDot.Fill = Ui.Brush("#CCA700")
                Else
                    StatusValue.Text = "就绪"
                    StatusDot.Fill = Ui.Brush("#6E7681")
                End If
            Else
                TimeValue.Text = "00:00.00"
                TickValue.Text = "0 / " & If(_world Is Nothing, 3600, _world.Stage.LengthTicks)
                EntityValue.Text = "0"
                MusicValue.Text = "AgSTG"
                StatusValue.Text = If(String.IsNullOrWhiteSpace(_engineError), "引擎准备中", "引擎错误：" & _engineError)
                StatusDot.Fill = Ui.Brush(If(String.IsNullOrWhiteSpace(_engineError), "#CCA700", "#F14C4C"))
            End If
            UpdateSeekBar()
            Return
        End If
        If _world Is Nothing Then Return
        TimeValue.Text = _world.TimeText
        TickValue.Text = Math.Max(0L, _world.Tick).ToString("N0") & " / " & _world.Stage.LengthTicks.ToString("N0")
        EntityValue.Text = (_world.Enemies.Count + _world.Bullets.Count + _world.Lasers.Count + 1).ToString()
        MusicValue.Text = If(String.IsNullOrWhiteSpace(_world.MusicName), "—", _world.MusicName)

        If _world.IsGameOver Then
            StatusValue.Text = "被弹 · 游戏结束"
            StatusDot.Fill = Ui.Brush("#F14C4C")
        ElseIf _world.IsCleared Then
            StatusValue.Text = "关卡完成"
            StatusDot.Fill = Ui.Brush("#89D185")
        ElseIf _timer.IsEnabled Then
            StatusValue.Text = "播放中"
            StatusDot.Fill = Ui.Brush("#89D185")
        ElseIf _world.Tick > 0 Then
            StatusValue.Text = "已暂停"
            StatusDot.Fill = Ui.Brush("#CCA700")
        Else
            StatusValue.Text = "就绪"
            StatusDot.Fill = Ui.Brush("#6E7681")
        End If
        UpdateSeekBar()
    End Sub

    Private Sub UpdateSeekBar()
        If SeekCanvas Is Nothing OrElse SeekTrack Is Nothing Then Return
        Dim width = Math.Max(0, SeekCanvas.ActualWidth)
        SeekTrack.Width = width
        SeekTrack.Height = 4
        SeekFill.Height = 4
        Dim length = If(_useEngine, If(_engineSurface Is Nothing, 3600, Math.Max(1, _engineSurface.LengthTicks)),
                        If(_world Is Nothing, 3600, Math.Max(1, _world.Stage.LengthTicks)))
        Dim tick = If(_useEngine, If(_engineSurface Is Nothing, 0L, Math.Max(0L, _engineSurface.Tick)),
                      If(_world Is Nothing, 0L, Math.Max(0L, _world.Tick)))
        Dim ratio = Math.Min(1.0, tick / length)
        SeekFill.Width = Math.Max(0, width * ratio)
        Canvas.SetLeft(SeekThumb, Math.Max(0, Math.Min(width - 10, width * ratio - 5)))
        Canvas.SetTop(SeekThumb, 3)
    End Sub

    Private Sub Seek_MouseDown(sender As Object, e As MouseButtonEventArgs)
        _seeking = True
        Pause()
        SeekTo(e.GetPosition(SeekCanvas).X)
        SeekCanvas.CaptureMouse()
    End Sub

    Private Sub Seek_MouseMove(sender As Object, e As MouseEventArgs)
        If Not _seeking Then Return
        If (DateTime.Now - _lastSeek).TotalMilliseconds < 40 Then Return
        _lastSeek = DateTime.Now
        SeekTo(e.GetPosition(SeekCanvas).X)
    End Sub

    Private Sub Seek_MouseUp(sender As Object, e As MouseButtonEventArgs)
        _seeking = False
        SeekCanvas.ReleaseMouseCapture()
    End Sub

    Private Sub SeekTo(x As Double)
        If _world Is Nothing Then Return
        Dim width = Math.Max(1, SeekCanvas.ActualWidth)
        Dim ratio = Math.Max(0, Math.Min(1, x / width))
        Dim length = If(_useEngine AndAlso _engineSurface IsNot Nothing, _engineSurface.LengthTicks, _world.Stage.LengthTicks)
        Dim target = CLng(Math.Round(ratio * length))
        Mouse.OverrideCursor = Cursors.Wait
        Try
            App.Session.SeekPreview(target)
            _world = App.Session.World
            If _useEngine AndAlso _engineSurface.IsReady Then
                _engineSurface.SeekTo(target)
            End If
            Renderer.World = _world
            Renderer.InvalidateVisual()
            UpdateStats()
            RaiseEvent TickChanged(_world.Tick)
        Finally
            Mouse.OverrideCursor = Nothing
        End Try
    End Sub

    Private Function IsTypingInTextBox() As Boolean
        Return TypeOf Keyboard.FocusedElement Is TextBox OrElse TypeOf Keyboard.FocusedElement Is ComboBox
    End Function

    Private Sub Window_KeyDown(sender As Object, e As KeyEventArgs)
        If App.Session.ActiveView <> "preview" Then Return
        If IsTypingInTextBox() Then Return
        Select Case e.Key
            Case Key.Left
                _inputX = -1
                e.Handled = True
            Case Key.Right
                _inputX = 1
                e.Handled = True
            Case Key.Up
                _inputY = -1
                e.Handled = True
            Case Key.Down
                _inputY = 1
                e.Handled = True
            Case Key.LeftShift
                _inputSlow = True
            Case Key.Space
                TogglePlay()
                e.Handled = True
            Case Key.R
                If _world IsNot Nothing Then
                    _world.Reset()
                    Renderer.InvalidateVisual()
                    UpdateStats()
                End If
                e.Handled = True
            Case Key.Escape
                Pause()
                e.Handled = True
        End Select
    End Sub

    Private Sub Window_KeyUp(sender As Object, e As KeyEventArgs)
        Select Case e.Key
            Case Key.Left, Key.Right
                _inputX = 0
            Case Key.Up, Key.Down
                _inputY = 0
            Case Key.LeftShift
                _inputSlow = False
        End Select
    End Sub

    Private Sub Window_Deactivated(sender As Object, e As EventArgs)
        _inputX = 0
        _inputY = 0
        _inputSlow = False
    End Sub

End Class
