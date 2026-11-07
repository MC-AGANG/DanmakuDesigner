Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Input
Imports System.Windows.Media
Imports System.Windows.Threading
Imports DanmakuDesigner.Core
Imports AgSTG.Core

''' <summary>
''' 预览面板里的 AgSTG STG 控件。关卡代码在内存中即时装入，不导出工程、不启动 dotnet build。
''' </summary>
Public Class EnginePreviewSurface
    Inherits UserControl

    Private _engine As AgSTG.STG
    Private _stage As AgSTG.Stage
    Private _stageProject As StageProject
    Private _engineDifficulty As AgSTG.Difficulty = AgSTG.Difficulty.Normal
    Private _backgroundHost As ContentControl
    Private _frame As Long
    Private _cleared As Boolean
    Private _gameOver As Boolean
    Private _handlersHooked As Boolean

    Public Property InputX As Integer
    Public Property InputY As Integer
    Public Property InputSlow As Boolean

    Public Sub New()
        Width = 384
        Height = 448
        Background = Brushes.Black
    End Sub

    Private Shared Function ToEngineDifficulty(mode As DifficultyMode) As AgSTG.Difficulty
        Select Case mode
            Case DifficultyMode.Easy : Return AgSTG.Difficulty.Easy
            Case DifficultyMode.Hard : Return AgSTG.Difficulty.Hard
            Case DifficultyMode.Lunatic : Return AgSTG.Difficulty.Lunatic
            Case DifficultyMode.Extra : Return AgSTG.Difficulty.Extra
            Case Else : Return AgSTG.Difficulty.Normal
        End Select
    End Function

    Public ReadOnly Property IsReady As Boolean
        Get
            Return _engine IsNot Nothing AndAlso _stage IsNot Nothing
        End Get
    End Property

    Public ReadOnly Property Tick As Long
        Get
            Return _frame
        End Get
    End Property

    Public ReadOnly Property IsCleared As Boolean
        Get
            Return _cleared
        End Get
    End Property

    Public ReadOnly Property IsGameOver As Boolean
        Get
            Return _gameOver
        End Get
    End Property

    Public ReadOnly Property EnemyCount As Integer
        Get
            If _engine Is Nothing Then Return 0
            Return AgSTG.STG.SearchEnemy().Count
        End Get
    End Property

    Public ReadOnly Property BulletCount As Integer
        Get
            If _engine Is Nothing Then Return 0
            Return AgSTG.STG.SearchBullet().Count
        End Get
    End Property

    ''' <summary>直接读取引擎对象列表，包含自机、敌机、子弹、激光等全部实体。</summary>
    Public ReadOnly Property EntityCount As Integer
        Get
            If _engine Is Nothing Then Return 0
            Return AgSTG.STG.Objects.Count
        End Get
    End Property

    Public ReadOnly Property PlayerLives As Integer
        Get
            If _engine Is Nothing Then Return 0
            Return AgSTG.STG.Life
        End Get
    End Property

    Public ReadOnly Property Graze As Long
        Get
            If _engine Is Nothing Then Return 0
            Return AgSTG.STG.Graze
        End Get
    End Property

    Public Sub LoadStage(stageProject As StageProject, engineRepo As String)
        If stageProject Is Nothing Then Return
        _stageProject = stageProject
        _engineDifficulty = ToEngineDifficulty(stageProject.Stage.Difficulty)
        Dim resourceUris As New Dictionary(Of String, String)(StringComparer.Ordinal)
        Dim audioNames As New Dictionary(Of String, String)(StringComparer.Ordinal)
        If App.Session.Project IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(App.Session.ProjectDirectory) Then
            For Each resource In App.Session.Project.Resources
                If resource Is Nothing Then Continue For
                If resource.Kind = GameResourceKind.Audio Then audioNames(resource.Id) = resource.Name
                If resource.Kind <> GameResourceKind.Image AndAlso resource.Kind <> GameResourceKind.Audio Then Continue For
                Dim fullPath = IO.Path.Combine(App.Session.ProjectDirectory, resource.RelativePath.Replace("/"c, "\"c))
                If IO.File.Exists(fullPath) Then resourceUris(resource.Id) = New Uri(fullPath).AbsoluteUri
            Next
        End If
        Dim compiled = EngineStageCompiler.Compile(stageProject, engineRepo, resourceUris, audioNames)
        Dim assembly = Reflection.Assembly.Load(compiled.AssemblyBytes)
        Dim stageType = assembly.GetType(compiled.ClassName, True)
        _stage = TryCast(Activator.CreateInstance(stageType, _engineDifficulty), AgSTG.Stage)
        If _stage Is Nothing Then Throw New InvalidOperationException("预览代码没有生成 AgSTG.Stage。")

        If _engine Is Nothing Then
            _engine = New AgSTG.STG With {.Width = 384, .Height = 448}
            Me.Content = _engine
        End If
        Me.UpdateLayout()
        If AgSTG.STG.NameArea IsNot Nothing Then
            ' BossName 的 Stars 数组在 Loaded 中创建；拖动播放头直接跳到 Boss 之后时，
            ' 必须先确保该控件已经完成加载，避免 Initialize 访问 Nothing。
            Dim nameVisibility = AgSTG.STG.NameArea.Visibility
            AgSTG.STG.NameArea.Visibility = Visibility.Visible
            AgSTG.STG.NameArea.UpdateLayout()
            AgSTG.STG.NameArea.Visibility = nameVisibility
            Me.Dispatcher.BeginInvoke(New Action(Sub()
                                                     Dim deferredVisibility = AgSTG.STG.NameArea.Visibility
                                                     AgSTG.STG.NameArea.Visibility = Visibility.Visible
                                                     AgSTG.STG.NameArea.UpdateLayout()
                                                     AgSTG.STG.NameArea.Visibility = deferredVisibility
                                                 End Sub), System.Windows.Threading.DispatcherPriority.Loaded)
        End If
        If Not _handlersHooked Then
            AddHandler AgSTG.STG.GameClear, AddressOf OnGameClear
            AddHandler AgSTG.STG.GameOver, AddressOf OnGameOver
            _handlersHooked = True
        End If

        If AgSTG.STG.BackLayer IsNot Nothing Then AgSTG.STG.BackLayer.Children.Clear()
        AgSTG.STG.Stages.Clear()
        AgSTG.STG.Stages.Add(_stage)
        AgSTG.STG.CurrentStage = 0
        AgSTG.STG.Difficulty = _engineDifficulty
        AgSTG.STG.PlayerID = 0
        AgSTG.STG.Reset()
        AgSTG.STG.Life = 99

        _backgroundHost = New ContentControl With {.Width = 384, .Height = 448, .IsHitTestVisible = False}
        If AgSTG.STG.BackLayer IsNot Nothing Then AgSTG.STG.BackLayer.Children.Insert(0, _backgroundHost)
        _frame = 0
        _cleared = False
        _gameOver = False
        UpdateBackground()
    End Sub

    Public Sub Reset()
        If _engine Is Nothing Then Return
        If AgSTG.STG.BackLayer IsNot Nothing Then AgSTG.STG.BackLayer.Children.Clear()
        AgSTG.STG.Reset()
        AgSTG.STG.Life = 99
        If _backgroundHost IsNot Nothing AndAlso AgSTG.STG.BackLayer IsNot Nothing Then
            AgSTG.STG.BackLayer.Children.Insert(0, _backgroundHost)
        End If
        _frame = 0
        _cleared = False
        _gameOver = False
        UpdateBackground()
    End Sub

    Public Sub Advance()
        If _engine Is Nothing Then Return
        KeyState.Up = InputY < 0
        KeyState.Down = InputY > 0
        KeyState.Left = InputX < 0
        KeyState.Right = InputX > 0
        KeyState.Slow = InputSlow
        KeyState.Shoot = Keyboard.IsKeyDown(Key.Z)
        KeyState.Bomb = Keyboard.IsKeyDown(Key.X)
        _engine.Render()
        ' 调试预览：残机不参与，被弹后保持 99 条，避免游戏结束。
        AgSTG.STG.Life = 99
        _frame += 1
        If _frame Mod 30 = 0 Then UpdateBackground()
    End Sub

    Public Sub SeekTo(targetTick As Long)
        If _stageProject Is Nothing Then Return
        Reset()
        Dim target = Math.Max(0L, Math.Min(targetTick, CLng(_stageProject.Stage.LengthTicks)))
        For i = 0 To target - 1
            Advance()
        Next
    End Sub

    Public ReadOnly Property TimeText As String
        Get
            Return TimeSpan.FromSeconds(_frame / 60.0).ToString("mm\:ss\.ff")
        End Get
    End Property

    Public ReadOnly Property LengthTicks As Integer
        Get
            Return If(_stageProject Is Nothing, 3600, _stageProject.Stage.LengthTicks)
        End Get
    End Property

    Private Sub UpdateBackground()
        If _backgroundHost Is Nothing OrElse _stageProject Is Nothing Then Return
        _backgroundHost.Content = BackgroundRenderer.BuildPreview(_stageProject.Stage, _frame)
    End Sub

    Private Sub OnGameClear()
        _cleared = True
    End Sub

    Private Sub OnGameOver()
        ' 调试模式不因残机耗尽停止。
        _gameOver = False
        If _engine IsNot Nothing Then AgSTG.STG.Life = 99
    End Sub
End Class
