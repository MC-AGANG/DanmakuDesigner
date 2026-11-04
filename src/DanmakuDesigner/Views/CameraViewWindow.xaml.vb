Imports System.Diagnostics
Imports System.Runtime.InteropServices
Imports System.Windows
Imports System.Windows.Input
Imports System.Windows.Media
Imports System.Windows.Media.Media3D
Imports DanmakuDesigner.Core

Public Class CameraPose
    Public Property Position As Point3D
    Public Property LookDirection As Vector3D
    Public Property FieldOfView As Double
End Class

Public Class CameraViewWindow
    Private ReadOnly _layer As BackgroundLayer
    Private _position As Point3D
    Private _look As Vector3D
    Private _fov As Double
    Private _yaw As Double
    Private _pitch As Double
    Private _camera As PerspectiveCamera
    Private _watch As Stopwatch
    Private _lastFrameSeconds As Double
    Private _rendering As Boolean
    Private _centerScreen As Point
    Private _Pose As CameraPose

    <StructLayout(LayoutKind.Sequential)>
    Private Structure NativePoint
        Public X As Integer
        Public Y As Integer
    End Structure

    <DllImport("user32.dll")>
    Private Shared Function SetCursorPos(x As Integer, y As Integer) As Boolean
    End Function

    <DllImport("user32.dll")>
    Private Shared Function GetCursorPos(ByRef point As NativePoint) As Boolean
    End Function

    Public Sub New(layer As BackgroundLayer, position As Point3D, look As Vector3D, fov As Double)
        InitializeComponent()
        _layer = layer
        _position = position
        _look = look
        _fov = fov
        If _layer IsNot Nothing Then LayerTitleText.Text = "· " & _layer.Name
        AddHandler Loaded, Sub() InitializeView()
    End Sub

    Private Sub InitializeView()
        If _layer Is Nothing Then Return
        If _look.LengthSquared < 0.0001 Then _look = New Vector3D(0, 0, 1)
        _look.Normalize()
        _pitch = Math.Asin(Math.Max(-1.0, Math.Min(1.0, _look.Y)))
        _yaw = Math.Atan2(_look.X, _look.Z)
        Dim view = BackgroundRenderer.BuildViewport(_layer, Nothing)
        _camera = TryCast(view.Camera, PerspectiveCamera)
        ViewportHost.Child = view
        UpdateCamera()
        _watch = Stopwatch.StartNew()
        _lastFrameSeconds = 0
        CenterCursor()
        If Not _rendering Then
            AddHandler CompositionTarget.Rendering, AddressOf OnRendering
            _rendering = True
        End If
        AddHandler Closed, Sub() StopRendering()
        Focus()
        Keyboard.Focus(Me)
    End Sub

    Private Sub StopRendering()
        If _rendering Then
            RemoveHandler CompositionTarget.Rendering, AddressOf OnRendering
            _rendering = False
        End If
        If _watch IsNot Nothing Then _watch.Stop()
    End Sub

    Private Sub OnRendering(sender As Object, e As EventArgs)
        If _camera Is Nothing OrElse Not IsActive Then Return
        Dim now = _watch.Elapsed.TotalSeconds
        Dim dt = Math.Max(0.001, Math.Min(0.05, now - _lastFrameSeconds))
        _lastFrameSeconds = now

        ' 鼠标：逐帧读取相对锁定中心的位置，随后把光标放回中心。
        Dim raw As New NativePoint()
        If GetCursorPos(raw) Then
            Dim dx = raw.X - _centerScreen.X
            Dim dy = raw.Y - _centerScreen.Y
            _yaw -= dx * 0.0024
            _pitch = Math.Max(-1.45, Math.Min(1.45, _pitch - dy * 0.0024))
            _look = New Vector3D(Math.Sin(_yaw) * Math.Cos(_pitch), Math.Sin(_pitch), Math.Cos(_yaw) * Math.Cos(_pitch))
        End If
        CenterCursor()

        ' 键盘：逐帧判断，移动始终在水平面；Space/Shift 控制垂直方向。
        Dim forward As New Vector3D(Math.Sin(_yaw), 0, Math.Cos(_yaw))
        Dim right As New Vector3D(Math.Cos(_yaw), 0, -Math.Sin(_yaw))
        Dim move As New Vector3D()
        If Keyboard.IsKeyDown(Key.W) Then move += forward
        If Keyboard.IsKeyDown(Key.S) Then move -= forward
        If Keyboard.IsKeyDown(Key.D) Then move -= right
        If Keyboard.IsKeyDown(Key.A) Then move += right
        If Keyboard.IsKeyDown(Key.Space) Then move += New Vector3D(0, 1, 0)
        If Keyboard.IsKeyDown(Key.LeftShift) OrElse Keyboard.IsKeyDown(Key.RightShift) Then move -= New Vector3D(0, 1, 0)
        If move.LengthSquared > 0.0001 Then
            move.Normalize()
            Dim speed = 3.0
            _position += move * speed * dt
        End If

        UpdateCamera()
    End Sub

    Private Sub UpdateCamera()
        If _camera Is Nothing Then Return
        _camera.Position = _position
        _camera.LookDirection = _look
        _camera.FieldOfView = Math.Max(1, Math.Min(170, _fov))
        PositionText.Text = "P " & _position.X.ToString("0.00") & ", " & _position.Y.ToString("0.00") & ", " & _position.Z.ToString("0.00") &
                            "   FOV " & _fov.ToString("0.0")
    End Sub

    Private Sub CenterCursor()
        If ViewportHost Is Nothing OrElse ViewportHost.ActualWidth <= 0 OrElse ViewportHost.ActualHeight <= 0 Then Return
        Dim screenCenter = ViewportHost.PointToScreen(New Point(ViewportHost.ActualWidth / 2.0, ViewportHost.ActualHeight / 2.0))
        SetCursorPos(CInt(Math.Round(screenCenter.X)), CInt(Math.Round(screenCenter.Y)))
        ' 某些 DPI 缩放下 SetCursorPos 的落点会与目标相差 1~4 像素；
        ' 保存实际落点，下一帧以它为基准，避免光标偏移被持续当成鼠标输入（表现为摄像机缓慢下坠）。
        Dim actual As New NativePoint()
        If GetCursorPos(actual) Then _centerScreen = New Point(actual.X, actual.Y)
    End Sub

    Private Sub Window_PreviewKeyDown(sender As Object, e As KeyEventArgs)
        Select Case e.Key
            Case Key.Enter
                Confirm()
                e.Handled = True
            Case Key.Escape
                DialogResult = False
                e.Handled = True
            Case Key.Space
                e.Handled = True
        End Select
    End Sub

    Private Sub Confirm()
        If _camera Is Nothing Then Return
        StopRendering()
        _Pose = New CameraPose With {
            .Position = _position,
            .LookDirection = _look,
            .FieldOfView = _fov
        }
        DialogResult = True
    End Sub

    Public ReadOnly Property ResultPose As CameraPose
        Get
            Return _Pose
        End Get
    End Property

    Private Sub Cancel_Click(sender As Object, e As RoutedEventArgs)
        DialogResult = False
    End Sub
End Class
