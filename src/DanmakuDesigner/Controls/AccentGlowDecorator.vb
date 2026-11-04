Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Media

''' <summary>
''' 在内容四周绘制一圈随状态变色的外发光，并在内容边缘绘制 1px 强调线。
''' 颜色、发光宽度均通过依赖属性驱动，便于使用动画切换。
''' </summary>
Public Class AccentGlowDecorator
    Inherits Decorator

    Public Shared ReadOnly AccentColorProperty As DependencyProperty =
        DependencyProperty.Register(NameOf(AccentColor), GetType(Color), GetType(AccentGlowDecorator),
                                    New FrameworkPropertyMetadata(Color.FromRgb(104, 33, 122),
                                                                  FrameworkPropertyMetadataOptions.AffectsRender))

    Public Shared ReadOnly GlowSizeProperty As DependencyProperty =
        DependencyProperty.Register(NameOf(GlowSize), GetType(Double), GetType(AccentGlowDecorator),
                                    New FrameworkPropertyMetadata(8.0,
                                                                  FrameworkPropertyMetadataOptions.AffectsMeasure Or
                                                                  FrameworkPropertyMetadataOptions.AffectsRender))

    Public Property AccentColor As Color
        Get
            Return CType(GetValue(AccentColorProperty), Color)
        End Get
        Set(value As Color)
            SetValue(AccentColorProperty, value)
        End Set
    End Property

    Public Property GlowSize As Double
        Get
            Return CDbl(GetValue(GlowSizeProperty))
        End Get
        Set(value As Double)
            SetValue(GlowSizeProperty, value)
        End Set
    End Property

    Protected Overrides Function MeasureOverride(availableSize As Size) As Size
        Dim g = Math.Max(0, GlowSize)
        If Child IsNot Nothing Then
            Dim innerWidth = If(Double.IsInfinity(availableSize.Width), Double.PositiveInfinity, Math.Max(0, availableSize.Width - g * 2))
            Dim innerHeight = If(Double.IsInfinity(availableSize.Height), Double.PositiveInfinity, Math.Max(0, availableSize.Height - g * 2))
            Child.Measure(New Size(innerWidth, innerHeight))
        End If

        If Double.IsInfinity(availableSize.Width) OrElse Double.IsInfinity(availableSize.Height) Then
            Dim desired = If(Child Is Nothing, New Size(0, 0), Child.DesiredSize)
            Dim width = If(Double.IsInfinity(availableSize.Width), desired.Width + g * 2, availableSize.Width)
            Dim height = If(Double.IsInfinity(availableSize.Height), desired.Height + g * 2, availableSize.Height)
            Return New Size(width, height)
        End If
        Return availableSize
    End Function

    Protected Overrides Function ArrangeOverride(finalSize As Size) As Size
        Dim g = Math.Max(0, GlowSize)
        If Child IsNot Nothing Then
            Child.Arrange(New Rect(g, g, Math.Max(0, finalSize.Width - g * 2), Math.Max(0, finalSize.Height - g * 2)))
        End If
        Return finalSize
    End Function

    Protected Overrides Sub OnRender(dc As DrawingContext)
        MyBase.OnRender(dc)
        Dim g = Math.Max(0, GlowSize)
        Dim w = ActualWidth
        Dim h = ActualHeight
        If g <= 0 OrElse w <= g * 2 OrElse h <= g * 2 Then Return

        Dim c = AccentColor
        Dim edge = Color.FromArgb(96, c.R, c.G, c.B)
        Dim clear = Color.FromArgb(0, c.R, c.G, c.B)
        Dim innerWidth = w - g * 2
        Dim innerHeight = h - g * 2

        ' 上下左右四条渐隐光带
        dc.DrawRectangle(VerticalBrush(clear, edge), Nothing, New Rect(g, 0, innerWidth, g))
        dc.DrawRectangle(VerticalBrush(edge, clear), Nothing, New Rect(g, h - g, innerWidth, g))
        dc.DrawRectangle(HorizontalBrush(clear, edge), Nothing, New Rect(0, g, g, innerHeight))
        dc.DrawRectangle(HorizontalBrush(edge, clear), Nothing, New Rect(w - g, g, g, innerHeight))

        ' 四个角用径向渐变衔接
        dc.DrawRectangle(CornerBrush(c, New Point(1, 1)), Nothing, New Rect(0, 0, g, g))
        dc.DrawRectangle(CornerBrush(c, New Point(0, 1)), Nothing, New Rect(w - g, 0, g, g))
        dc.DrawRectangle(CornerBrush(c, New Point(1, 0)), Nothing, New Rect(0, h - g, g, g))
        dc.DrawRectangle(CornerBrush(c, New Point(0, 0)), Nothing, New Rect(w - g, h - g, g, g))

        ' 内容边缘的 1px 强调线（对应旧版的 borderall）
        Dim pen As New Pen(New SolidColorBrush(Color.FromRgb(c.R, c.G, c.B)), 1)
        pen.Freeze()
        dc.DrawRectangle(Nothing, pen, New Rect(g - 0.5, g - 0.5, innerWidth + 1, innerHeight + 1))
    End Sub

    Private Shared Function VerticalBrush(startColor As Color, endColor As Color) As LinearGradientBrush
        Dim brush As New LinearGradientBrush(startColor, endColor, New Point(0.5, 0), New Point(0.5, 1))
        brush.Freeze()
        Return brush
    End Function

    Private Shared Function HorizontalBrush(startColor As Color, endColor As Color) As LinearGradientBrush
        Dim brush As New LinearGradientBrush(startColor, endColor, New Point(0, 0.5), New Point(1, 0.5))
        brush.Freeze()
        Return brush
    End Function

    Private Shared Function CornerBrush(accent As Color, center As Point) As RadialGradientBrush
        Dim brush As New RadialGradientBrush()
        brush.Center = center
        brush.GradientOrigin = center
        brush.RadiusX = 1
        brush.RadiusY = 1
        brush.GradientStops.Add(New GradientStop(Color.FromArgb(96, accent.R, accent.G, accent.B), 0))
        brush.GradientStops.Add(New GradientStop(Color.FromArgb(0, accent.R, accent.G, accent.B), 1))
        brush.Freeze()
        Return brush
    End Function

End Class
