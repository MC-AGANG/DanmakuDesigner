Imports System.Windows.Media
Imports System.Windows.Media.Imaging

''' <summary>按需读取 AgSTG 自带的贴图；加载失败时编辑器会自动使用矢量占位图形。</summary>
Public Module TextureService

    Public Property Available As Boolean = False
    Public Property ErrorMessage As String = ""

    Public Sub Initialize()
        Try
            ResourcePack.Textures.Load()
            Try
                ResourcePack.TH07.Textures.Load()
            Catch
                ' TH07 的额外贴图不是必需的。
            End Try
            Available = True
        Catch ex As Exception
            Available = False
            ErrorMessage = ex.Message
        End Try
    End Sub

    Private Function ClampIndex(value As Integer, maxValue As Integer) As Integer
        If value < 0 Then Return 0
        If value > maxValue Then Return maxValue
        Return value
    End Function

    Public Function Bullet(type As Integer, color As Integer) As ImageSource
        If Not Available Then Return Nothing
        Try
            Dim brush = ResourcePack.Textures.bullet(ClampIndex(type, 20), ClampIndex(color, 15))
            If brush Is Nothing Then Return Nothing
            Return brush.ImageSource
        Catch
            Return Nothing
        End Try
    End Function

    Public Function Enemy(type As Integer, color As Integer, frame As Integer) As ImageSource
        If Not Available Then Return Nothing
        Try
            Dim brush = ResourcePack.Textures.enemy(ClampIndex(type, 6), ClampIndex(color, 7), ClampIndex(frame, 10))
            If brush Is Nothing Then Return Nothing
            Return brush.ImageSource
        Catch
            Return Nothing
        End Try
    End Function

    Public Function Player(frame As Integer) As ImageSource
        If Not Available Then Return Nothing
        Try
            Dim brush = ResourcePack.Textures.player(0, 0, ClampIndex(frame, 7))
            If brush Is Nothing Then Return Nothing
            Return brush.ImageSource
        Catch
            Return Nothing
        End Try
    End Function

    Public Function PlayerHitbox() As ImageSource
        If Not Available Then Return Nothing
        Try
            Return ResourcePack.Textures.player_hitbox.ImageSource
        Catch
            Return Nothing
        End Try
    End Function

    Public Function DeadCircle() As ImageSource
        If Not Available Then Return Nothing
        Try
            Return ResourcePack.Textures.deadcircle.ImageSource
        Catch
            Return Nothing
        End Try
    End Function

    Public Function Boss(id As Integer, frame As Integer) As ImageSource
        If Not Available Then Return Nothing
        Try
            Return ResourcePack.TH07.Textures.boss(ClampIndex(id, 8), ClampIndex(frame, 16)).ImageSource
        Catch
            Return Nothing
        End Try
    End Function

End Module

''' <summary>读取随程序打包的 VS2017 官方图标。</summary>
Public Module IconLibrary

    Private ReadOnly Cache As New Dictionary(Of String, ImageSource)(StringComparer.OrdinalIgnoreCase)

    Public Function [Get](name As String) As ImageSource
        If String.IsNullOrEmpty(name) Then Return Nothing
        Dim cached As ImageSource = Nothing
        If Cache.TryGetValue(name, cached) Then Return cached
        Try
            Dim image As New BitmapImage()
            image.BeginInit()
            image.UriSource = New Uri("pack://application:,,,/Assets/Icons/" & name, UriKind.Absolute)
            image.CacheOption = BitmapCacheOption.OnLoad
            image.EndInit()
            image.Freeze()
            Cache(name) = image
            Return image
        Catch
            Return Nothing
        End Try
    End Function

    Public Function [Get](name As String, size As Double) As System.Windows.Controls.Image
        Dim source = [Get](name)
        Dim img As New System.Windows.Controls.Image With {
            .Source = source,
            .Width = size,
            .Height = size,
            .Stretch = Stretch.Uniform,
            .VerticalAlignment = VerticalAlignment.Center,
            .HorizontalAlignment = HorizontalAlignment.Center,
            .SnapsToDevicePixels = True
        }
        RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality)
        Return img
    End Function

End Module
