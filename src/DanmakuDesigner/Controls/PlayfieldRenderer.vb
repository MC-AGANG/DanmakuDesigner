Imports System.Globalization
Imports System.Windows
Imports System.Windows.Media
Imports DanmakuDesigner.Core

''' <summary>用 AgSTG 的真实贴图把预览世界绘制到 384×448 的弹幕区域。</summary>
Public Class PlayfieldRenderer
    Inherits FrameworkElement

    Public Property World As PreviewWorld
    Public Property ShowHitbox As Boolean = True
    Public Property ShowGrid As Boolean = False

    Private ReadOnly _white15 As SolidColorBrush = New SolidColorBrush(Color.FromArgb(38, 255, 255, 255))
    Private ReadOnly _gridPen As Pen = New Pen(New SolidColorBrush(Color.FromArgb(18, 255, 255, 255)), 1)
    Private ReadOnly _hitboxPen As Pen = New Pen(New SolidColorBrush(Color.FromArgb(220, 255, 255, 255)), 1)
    Private ReadOnly _dangerPen As Pen = New Pen(New SolidColorBrush(Color.FromArgb(200, 255, 80, 80)), 1)
    Private ReadOnly _panelBrush As New SolidColorBrush(Color.FromArgb(190, 12, 16, 24))

    Public Sub New()
        Width = 384
        Height = 448
        ClipToBounds = True
        Focusable = True
        SnapsToDevicePixels = True
        RenderOptions.SetBitmapScalingMode(Me, BitmapScalingMode.HighQuality)
    End Sub

    Protected Overrides Function MeasureOverride(availableSize As Size) As Size
        Return New Size(384, 448)
    End Function

    Protected Overrides Sub OnRender(dc As DrawingContext)
        MyBase.OnRender(dc)
        DrawBackground(dc)
        Dim world As PreviewWorld = Me.World
        If world Is Nothing OrElse world.Stage Is Nothing Then
            DrawTextAt(dc, "没有可预览的关卡", 16, New Point(192, 210), "#9A9A9A", True)
            Return
        End If

        Dim shakeX = 0.0
        Dim shakeY = 0.0
        If world.ShakeFrames > 0 Then
            shakeX = (world.Tick * 7 Mod 13 - 6) * Math.Min(1.0, world.ShakeFrames / 10.0)
            shakeY = (world.Tick * 11 Mod 11 - 5) * Math.Min(1.0, world.ShakeFrames / 10.0)
        End If
        dc.PushTransform(New TranslateTransform(shakeX, shakeY))

        If ShowGrid Then DrawGrid(dc)
        DrawLasers(dc, world)
        DrawEnemies(dc, world)
        DrawBullets(dc, world)
        DrawPlayer(dc, world)
        DrawHud(dc, world)
        dc.Pop()
    End Sub

    Private Sub DrawBackground(dc As DrawingContext)
        ' 预览先使用纯黑背景，避免动态星空/关卡渐变干扰弹幕判读。
        ' 关卡背景色仍保留在工程与导出代码中，后续可再做成可选项。
        dc.DrawRectangle(Brushes.Black, Nothing, New Rect(0, 0, 384, 448))
    End Sub

    Private Sub DrawGrid(dc As DrawingContext)
        For x = 24 To 360 Step 48
            dc.DrawLine(_gridPen, New Point(x, 0), New Point(x, 448))
        Next
        For y = 28 To 420 Step 56
            dc.DrawLine(_gridPen, New Point(0, y), New Point(384, y))
        Next
    End Sub

    Private Sub DrawBullets(dc As DrawingContext, world As PreviewWorld)
        For Each b In world.Bullets
            If b.Opacity <= 0.01 Then Continue For
            Dim info = EngineCatalog.BulletType(b.BulletType)
            Dim angle = If(info.Spin, b.Ticks * 8.0 + b.Angle, b.Direction)
            Dim scale = Math.Max(0.25, b.Scale)
            Dim w = b.SpriteWidth * scale
            Dim h = b.SpriteHeight * scale
            dc.PushOpacity(b.Opacity)
            dc.PushTransform(New RotateTransform(angle, b.X, b.Y))
            dc.PushTransform(New ScaleTransform(scale, scale, b.X, b.Y))
            Dim src = TextureService.Bullet(b.BulletType, b.BulletColor)
            If src IsNot Nothing Then
                dc.DrawImage(src, New Rect(b.X - b.SpriteWidth / 2, b.Y - b.SpriteHeight / 2, b.SpriteWidth, b.SpriteHeight))
            Else
                Dim color = FallbackColor(b.BulletColor)
                Dim brush As New SolidColorBrush(Color.FromArgb(235, color.R, color.G, color.B))
                brush.Freeze()
                Dim radius = Math.Max(2.0, b.Hitbox * 1.35)
                dc.DrawEllipse(brush, New Pen(New SolidColorBrush(Color.FromArgb(180, 255, 255, 255)), 1), New Point(b.X, b.Y), radius, radius)
            End If
            dc.Pop()
            dc.Pop()
            dc.Pop()
        Next
    End Sub

    Private Sub DrawEnemies(dc As DrawingContext, world As PreviewWorld)
        For Each e In world.Enemies
            If e.Opacity <= 0.01 Then Continue For
            Dim w = e.SpriteWidth * Math.Max(0.3, e.Scale)
            Dim h = e.SpriteHeight * Math.Max(0.3, e.Scale)
            dc.PushOpacity(e.Opacity)
            Dim rotate = 0.0
            If e.EnemyType = 0 Then rotate = (e.Ticks * 3) Mod 360
            dc.PushTransform(New RotateTransform(rotate, e.X, e.Y))

            If e.IsDying Then
                Dim dead = TextureService.DeadCircle()
                If dead IsNot Nothing Then
                    dc.DrawImage(dead, New Rect(e.X - w / 2, e.Y - h / 2, w, h))
                Else
                    Dim brush As New SolidColorBrush(Color.FromArgb(200, 255, 230, 170))
                    brush.Freeze()
                    dc.DrawEllipse(brush, Nothing, New Point(e.X, e.Y), w / 2, w / 2)
                End If
            Else
                Dim frame = EnemyFrame(e)
                Dim src = If(e.EnemyType = 100, TextureService.Boss(e.EnemyColor, frame), TextureService.Enemy(e.EnemyType, e.EnemyColor, frame))
                If src IsNot Nothing Then
                    Dim flip = If(Math.Sin(e.Direction * Math.PI / 180) < -0.25, -1.0, 1.0)
                    If flip < 0 Then
                        dc.PushTransform(New ScaleTransform(-1, 1, e.X, e.Y))
                    End If
                    dc.DrawImage(src, New Rect(e.X - w / 2, e.Y - h / 2, w, h))
                    If flip < 0 Then dc.Pop()
                Else
                    Dim brush As New SolidColorBrush(Color.FromArgb(235, 190, 120, 220))
                    brush.Freeze()
                    dc.DrawEllipse(brush, New Pen(Brushes.White, 1), New Point(e.X, e.Y), w / 3, h / 3)
                End If
                If e.HP < e.MaxHP AndAlso e.MaxHP > 0 Then
                    Dim ratio = Math.Max(0, e.HP / e.MaxHP)
                    dc.DrawRectangle(New SolidColorBrush(Color.FromArgb(180, 0, 0, 0)), Nothing, New Rect(e.X - 16, e.Y - h / 2 - 7, 32, 3))
                    Dim fill As New SolidColorBrush(If(ratio > 0.35, Color.FromRgb(242, 139, 139), Color.FromRgb(241, 76, 76)))
                    fill.Freeze()
                    dc.DrawRectangle(fill, Nothing, New Rect(e.X - 16, e.Y - h / 2 - 7, 32 * ratio, 3))
                End If
            End If
            dc.Pop()
            dc.Pop()
        Next
    End Sub

    Private Function EnemyFrame(e As PreviewActor) As Integer
        Select Case e.EnemyType
            Case 1, 2, 3, 4
                Dim frame = (e.Ticks \ 6) Mod 4
                If Math.Abs(Math.Sin(e.Direction * Math.PI / 180)) > 0.3 Then frame += 8
                Return Math.Max(0, Math.Min(11, frame))
            Case 5
                Return (e.Ticks \ 2) Mod 8
            Case 100
                Return (e.Ticks \ 8) Mod 4
            Case Else
                Return 0
        End Select
    End Function

    Private Sub DrawLasers(dc As DrawingContext, world As PreviewWorld)
        For Each l In world.Lasers
            If l.Opacity <= 0.01 Then Continue For
            Dim grow = If(l.Ticks < 30, 0.1, If(l.Ticks < 60, (l.Ticks - 30) / 30.0, 1.0))
            Dim w = Math.Max(1, l.LaserWidth * grow)
            Dim color = FallbackColor(l.BulletColor)
            dc.PushOpacity(l.Opacity)
            dc.PushTransform(New RotateTransform(l.Direction, l.X, l.Y))
            Dim outer As New SolidColorBrush(Color.FromArgb(90, color.R, color.G, color.B))
            outer.Freeze()
            Dim inner As New SolidColorBrush(Color.FromArgb(230, 255, 255, 255))
            inner.Freeze()
            dc.DrawRoundedRectangle(outer, Nothing, New Rect(l.X - w, l.Y - l.LaserLength, w * 2, l.LaserLength), w, w)
            dc.DrawRoundedRectangle(inner, Nothing, New Rect(l.X - w * 0.35, l.Y - l.LaserLength, w * 0.7, l.LaserLength), w * 0.35, w * 0.35)
            dc.Pop()
            dc.Pop()
        Next
    End Sub

    Private Sub DrawPlayer(dc As DrawingContext, world As PreviewWorld)
        Dim blink = world.PlayerInvin > 0 AndAlso (world.PlayerInvin \ 4) Mod 2 = 0
        Dim alpha = If(blink, 90, 255)
        dc.PushOpacity(alpha / 255.0)
        Dim src = TextureService.Player(0)
        If src IsNot Nothing Then
            Dim size = 48.0
            dc.DrawImage(src, New Rect(world.PlayerX - size / 2, world.PlayerY - size / 2 - 6, size, size))
        Else
            Dim geo As New StreamGeometry()
            Using ctx = geo.Open()
                ctx.BeginFigure(New Point(world.PlayerX, world.PlayerY - 20), True, True)
                ctx.LineTo(New Point(world.PlayerX - 14, world.PlayerY + 10), True, False)
                ctx.LineTo(New Point(world.PlayerX, world.PlayerY + 3), True, False)
                ctx.LineTo(New Point(world.PlayerX + 14, world.PlayerY + 10), True, False)
            End Using
            geo.Freeze()
            Dim brush As New LinearGradientBrush(Color.FromRgb(160, 220, 255), Color.FromRgb(70, 120, 255), 90)
            brush.Freeze()
            dc.DrawGeometry(brush, New Pen(New SolidColorBrush(Color.FromArgb(220, 255, 255, 255)), 1), geo)
        End If
        dc.Pop()

        If ShowHitbox Then
            Dim ring As New SolidColorBrush(Color.FromArgb(220, 255, 90, 90))
            ring.Freeze()
            dc.DrawEllipse(Nothing, New Pen(ring, 1.2), New Point(world.PlayerX, world.PlayerY), 7, 7)
            dc.DrawEllipse(ring, Nothing, New Point(world.PlayerX, world.PlayerY), 2.4, 2.4)
        End If
    End Sub

    Private Sub DrawHud(dc As DrawingContext, world As PreviewWorld)
        If Not String.IsNullOrWhiteSpace(world.MusicName) Then
            dc.DrawRoundedRectangle(_panelBrush, Nothing, New Rect(8, 8, Math.Min(250, 10 + world.MusicName.Length * 12), 22), 3, 3)
            DrawTextAt(dc, "♪ " & world.MusicName, 11, New Point(14, 19), "#DCDCAA", False, False)
        End If
        Dim tickText = "TICK " & Math.Max(0L, world.Tick).ToString("N0") & "   " & world.TimeText
        dc.DrawRoundedRectangle(_panelBrush, Nothing, New Rect(384 - 146, 8, 138, 22), 3, 3)
        DrawTextAt(dc, tickText, 10, New Point(384 - 140, 19), "#B8B8B8", False, False)

        If world.MessageTimer > 0 AndAlso Not String.IsNullOrWhiteSpace(world.ActiveMessage) Then
            Dim alpha = Math.Min(1.0, world.MessageTimer / 30.0)
            dc.PushOpacity(alpha)
            Dim width = Math.Min(360, 36 + world.ActiveMessage.Length * 15)
            dc.DrawRoundedRectangle(_panelBrush, Nothing, New Rect(192 - width / 2, 74, width, 30), 4, 4)
            DrawTextAt(dc, world.ActiveMessage, 14, New Point(192, 89), "#F1F1F1", True)
            dc.Pop()
        End If

        If world.IsCleared Then
            dc.DrawRectangle(New SolidColorBrush(Color.FromArgb(135, 4, 8, 14)), Nothing, New Rect(0, 0, 384, 448))
            DrawTextAt(dc, "关 卡 完 成", 28, New Point(192, 200), "#FFFFFF", True)
            DrawTextAt(dc, If(String.IsNullOrWhiteSpace(world.Stage.Name), "", world.Stage.Name), 13, New Point(192, 234), "#C8C8C8", False)
        ElseIf world.IsGameOver Then
            dc.DrawRectangle(New SolidColorBrush(Color.FromArgb(135, 20, 4, 8)), Nothing, New Rect(0, 0, 384, 448))
            DrawTextAt(dc, "GAME OVER", 28, New Point(192, 200), "#F14C4C", True)
            DrawTextAt(dc, "按 R 重新开始", 12, New Point(192, 232), "#DDDDDD", False)
        End If
    End Sub

    Private Sub DrawTextAt(dc As DrawingContext, text As String, size As Double, center As Point, color As String, bold As Boolean, Optional centered As Boolean = True)
        Try
            Dim dpi = VisualTreeHelper.GetDpi(Me)
            Dim typeface As New Typeface(New FontFamily("Microsoft YaHei UI, Segoe UI"), If(bold, FontStyles.Normal, FontStyles.Normal), If(bold, FontWeights.Bold, FontWeights.Normal), FontStretches.Normal)
            Dim brush As New SolidColorBrush(DirectCast(ColorConverter.ConvertFromString(color), Color))
            brush.Freeze()
            Dim formatted As New FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, size, brush, dpi.PixelsPerDip)
            Dim origin As New Point(If(centered, center.X - formatted.Width / 2, center.X), center.Y - formatted.Height / 2)
            dc.DrawText(formatted, origin)
        Catch
        End Try
    End Sub

    Private Function FallbackColor(index As Integer) As Color
        Select Case index
            Case 0 : Return Color.FromRgb(200, 200, 200)
            Case 1 : Return Color.FromRgb(150, 40, 40)
            Case 2 : Return Color.FromRgb(235, 70, 70)
            Case 3 : Return Color.FromRgb(175, 90, 220)
            Case 4 : Return Color.FromRgb(235, 90, 190)
            Case 5 : Return Color.FromRgb(40, 70, 170)
            Case 6 : Return Color.FromRgb(60, 120, 240)
            Case 7 : Return Color.FromRgb(60, 200, 220)
            Case 8 : Return Color.FromRgb(110, 200, 255)
            Case 9 : Return Color.FromRgb(40, 130, 80)
            Case 10 : Return Color.FromRgb(70, 190, 100)
            Case 11 : Return Color.FromRgb(170, 210, 90)
            Case 12 : Return Color.FromRgb(190, 160, 70)
            Case 13 : Return Color.FromRgb(240, 220, 90)
            Case 14 : Return Color.FromRgb(240, 160, 60)
            Case Else : Return Color.FromRgb(245, 245, 245)
        End Select
    End Function

End Class
