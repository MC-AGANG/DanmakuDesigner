Public Class Chart
    Public Event ScrollChanged(sender As Object, e As ScrollChangedEventArgs)
    Public Property Color As Color
        Set(value As Color)
            _Color = Color
            PT_Main.Stroke = New SolidColorBrush(Color)
            PT_GS1.Color = Color.FromArgb(160, Color.R, Color.G, Color.B)
            PT_GS2.Color = Color.FromArgb(0, Color.R, Color.G, Color.B)
        End Set
        Get
            Return _Color
        End Get
    End Property
    Private _Color As Color = Color.FromArgb(255, 88, 182, 255)
    Public Property ShowScrollBar As Boolean
        Set(value As Boolean)
            If value Then
                SV_Main.HorizontalScrollBarVisibility = ScrollBarVisibility.Visible
            Else
                SV_Main.HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden
            End If
        End Set
        Get
            If SV_Main.HorizontalScrollBarVisibility = ScrollBarVisibility.Visible Then
                Return True
            Else
                Return False
            End If
        End Get
    End Property
    Public Property Max As Double
        Set(value As Double)
            _Max = value
            LB_Max.Content = CStr(Max)
            LB_Mid.Content = CStr((Max + Min) / 2)
        End Set
        Get
            Return _Max
        End Get
    End Property
    Private _Max As Double = 100
    Public Property Min As Double
        Set(value As Double)
            _Min = value
            LB_Min.Content = CStr(Min)
            LB_Mid.Content = CStr((Max + Min) / 2)
        End Set
        Get
            Return _Min
        End Get
    End Property
    Private _Min As Double = 0
    Private Values As New List(Of Double)
    Private PG As New PathGeometry
    Private PF As New PathFigure
    Private Sub UserControl_Loaded(sender As Object, e As RoutedEventArgs)
        PF.StartPoint = New Point(0, CV_Main.ActualHeight - 1)
        PF.IsClosed = True
        PG.Figures.Add(PF)
        PT_Main.Data = PG
    End Sub
    Public Sub Clear()
        Values.Clear()
        CV_Main.Width = 0
        PF.Segments.Clear()
        PF.Segments.Add(New LineSegment(New Point(CV_Main.Width - 1, CV_Main.ActualHeight - 1), True))

    End Sub
    Public Sub Add(value As Double)
        If Double.IsInfinity(value) OrElse Double.IsNaN(value) Then
            value = 0
        End If
        Values.Add(value)
        CV_Main.Width = Values.Count * 2
        PF.Segments.Remove(PF.Segments.Last)
        PF.Segments.Add(New LineSegment(New Point(CV_Main.Width - 1, GetY(value)), True))
        PF.Segments.Add(New LineSegment(New Point(CV_Main.Width - 1, CV_Main.ActualHeight - 1), True))
        PT_Main.Data = PG
        SV_Main.ScrollToRightEnd()
    End Sub

    Private Sub UserControl_SizeChanged(sender As Object, e As SizeChangedEventArgs)
        PF.StartPoint = New Point(0, CV_Main.ActualHeight - 1)
        PF.Segments.Clear()
        For i = 0 To Values.Count - 1
            PF.Segments.Add(New LineSegment(New Point(i * 2 + 1, GetY(Values(i))), True))
        Next
        PF.Segments.Add(New LineSegment(New Point(CV_Main.Width - 1, CV_Main.ActualHeight - 1), True))
        PT_Main.Data = PG
    End Sub
    Private Function GetY(value As Double) As Double
        Return (Max - (value - Min)) * CV_Main.ActualHeight / (Max - Min)
    End Function

    Private Sub SV_Main_ScrollChanged(sender As Object, e As ScrollChangedEventArgs)
        RaiseEvent ScrollChanged(sender, e)
    End Sub
End Class
