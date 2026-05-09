Imports ResourcePack.DD
Class MainWindow
    Private Maximized As Boolean = False

    Private Sub Grid_MouseDown(sender As Object, e As MouseButtonEventArgs)

    End Sub
    Private Sub Window_Loaded(sender As Object, e As RoutedEventArgs)

    End Sub

    Private Sub BT_Minimize_Clicked()
        WindowState = WindowState.Minimized
    End Sub

    Private Sub BT_Maximize_Clicked()
        If Maximized Then
            WindowState = WindowState.Normal
            BT_Maximize.Icon = Textures.BT_Maximize
            Maximized = False
        Else
            WindowState = WindowState.Maximized
            BT_Maximize.Icon = Textures.BT_Restore
            Maximized = True
        End If
    End Sub

    Private Sub BT_Close_Clicked()
        Close()
    End Sub

    Private Sub Rectangle_MouseDown(sender As Object, e As MouseButtonEventArgs)
        If e.LeftButton = MouseButtonState.Pressed Then
            DragMove()

        End If
    End Sub

    Private Sub Window_SizeChanged(sender As Object, e As SizeChangedEventArgs)
        If WindowState = WindowState.Maximized Then
            Maximized = True
        ElseIf WindowState = WindowState.Normal Then
            Maximized = False
        End If
        If Maximized Then
            BT_Maximize.Icon = Textures.BT_Restore
        Else
            BT_Maximize.Icon = Textures.BT_Maximize
        End If
    End Sub
End Class
