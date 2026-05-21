Imports DanmakuDesigner.PublicArea
Public Class SubWindow_TimeLine
    Private RC_Lines As New List(Of Rectangle)
    Private LB_Time As New List(Of Label)
    Public Property Length As Long
        Set(value As Long)
            _Length = value
            CV_Main.Width = Length * 2
            CV_Time.Width = Length * 2
            UpdateTimeLine()
        End Set
        Get
            Return _Length
        End Get
    End Property
    Private _Length As Long = 1000
    Public Property Position As ULong
        Set(value As ULong)
            _Position = value
            Canvas.SetLeft(RC_ProgressMain, Position * 2 - 1)
            Canvas.SetLeft(RC_ProgressTime, Position * 2 - 3)
        End Set
        Get
            Return _Position
        End Get
    End Property
    Private _Position As ULong
    Private Sub UserControl_Loaded(sender As Object, e As RoutedEventArgs)
        SW_Timeline = Me
        UpdateTimeLine()
    End Sub
    Private Sub UpdateTimeLine()
        If RC_Lines.Count < Length \ 30 Then
            For i = RC_Lines.Count To Length \ 30
                Dim r As New Rectangle
                CV_Time.Children.Add(r)
                If i Mod 5 = 0 Then
                    r.Fill = New SolidColorBrush(Color.FromRgb(96, 96, 96))
                    r.Height = 18
                    Canvas.SetTop(r, 6)
                Else
                    r.Fill = New SolidColorBrush(Color.FromRgb(64, 64, 64))
                    r.Height = 12
                    Canvas.SetTop(r, 12)
                End If
                r.Width = 1
                Canvas.SetLeft(r, i * 30 * 2)
            Next
        End If
        If LB_Time.Count < Length \ 300 Then
            For i = LB_Time.Count To Length \ 300
                Dim l As New Label
                CV_Time.Children.Add(l)
                l.Foreground = New SolidColorBrush(Color.FromRgb(128, 128, 128))
                l.Content = CStr(i * 300)
                l.Margin = New Thickness(0, 2, 2, 2)
                Canvas.SetLeft(l, 300 * i * 2)
            Next
        End If
    End Sub

    Private Sub SV_Main_ScrollChanged(sender As Object, e As ScrollChangedEventArgs)
        SV_Time.ScrollToHorizontalOffset(e.HorizontalOffset)
    End Sub

    Private Sub CV_Time_MouseDown(sender As Object, e As MouseButtonEventArgs)
        Position = e.GetPosition(CV_Time).X \ 2
    End Sub

    Private Sub BT_AddEnemy_Clicked()
        Dim e As New EnemyAddEvent With {.Tick = Position}
        Events.Add(e)
        CV_Main.Children.Add(e)
    End Sub

    Private Sub SV_Main_PreviewKeyDown(sender As Object, e As KeyEventArgs)
        If e.Key = Key.Up OrElse e.Key = Key.Left OrElse e.Key = Key.Right OrElse e.Key = Key.Down Then
            e.Handled = True
        End If
    End Sub

    Private Sub SV_Time_PreviewKeyDown(sender As Object, e As KeyEventArgs)
        If e.Key = Key.Up OrElse e.Key = Key.Left OrElse e.Key = Key.Right OrElse e.Key = Key.Down Then
            e.Handled = True
        End If
    End Sub

    Private Sub ScrollViewer_PreviewKeyDown(sender As Object, e As KeyEventArgs)
        If e.Key = Key.Up OrElse e.Key = Key.Left OrElse e.Key = Key.Right OrElse e.Key = Key.Down Then
            e.Handled = True
        End If
    End Sub
End Class
