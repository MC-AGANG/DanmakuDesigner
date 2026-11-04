Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Media
Imports System.Windows.Shapes

''' <summary>轻量级实时曲线：用于预览面板的 FPS / 帧耗时。</summary>
Public Class PerformanceChart
    Inherits UserControl

    Private ReadOnly _canvas As New Canvas()
    Private ReadOnly _line As New Polyline()
    Private ReadOnly _fill As New Polygon()
    Private ReadOnly _values As New List(Of Double)()
    Private _maximum As Double = 120

    Public Sub New()
        Height = 64
        MinHeight = 40
        Background = Ui.Brush("#151515")
        _canvas.ClipToBounds = True
        _line.StrokeThickness = 1.4
        _line.StrokeLineJoin = PenLineJoin.Round
        _fill.StrokeThickness = 0
        _fill.Opacity = 0.35
        _canvas.Children.Add(_fill)
        _canvas.Children.Add(_line)
        Content = _canvas
        AddHandler SizeChanged, Sub() Draw()
        SetColor("#58B6FF")
    End Sub

    Public Property Maximum As Double
        Get
            Return _maximum
        End Get
        Set(value As Double)
            _maximum = Math.Max(1, value)
            Draw()
        End Set
    End Property

    Public Sub SetColor(hex As String)
        Dim color = Ui.Brush(hex).Color
        _line.Stroke = New SolidColorBrush(color)
        _fill.Fill = New SolidColorBrush(Color.FromArgb(70, color.R, color.G, color.B))
    End Sub

    Public Sub Clear()
        _values.Clear()
        Draw()
    End Sub

    Public Sub Add(value As Double)
        If Double.IsNaN(value) OrElse Double.IsInfinity(value) Then value = 0
        _values.Add(Math.Max(0, value))
        While _values.Count > 240
            _values.RemoveAt(0)
        End While
        Draw()
    End Sub

    Private Sub Draw()
        Dim width = _canvas.ActualWidth
        Dim height = _canvas.ActualHeight
        _canvas.Children.Clear()
        If width <= 1 OrElse height <= 1 Then Return

        For i = 1 To 3
            Dim y = height * i / 4.0
            _canvas.Children.Add(New Line With {
                .X1 = 0, .X2 = width, .Y1 = y, .Y2 = y,
                .Stroke = Ui.Brush("#2A2A2A"), .StrokeThickness = 1
            })
        Next

        If _values.Count < 2 Then
            _canvas.Children.Add(_fill)
            _canvas.Children.Add(_line)
            Return
        End If

        Dim points As New PointCollection()
        Dim fillPoints As New PointCollection()
        For i = 0 To _values.Count - 1
            Dim x = width * i / Math.Max(1, _values.Count - 1)
            Dim y = height - Math.Min(1.0, _values(i) / _maximum) * (height - 2) - 1
            points.Add(New Point(x, y))
            fillPoints.Add(New Point(x, y))
        Next
        fillPoints.Add(New Point(width, height))
        fillPoints.Add(New Point(0, height))
        _line.Points = points
        _fill.Points = fillPoints
        _canvas.Children.Add(_fill)
        _canvas.Children.Add(_line)
    End Sub
End Class
