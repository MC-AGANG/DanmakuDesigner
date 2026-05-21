Imports DanmakuDesigner.PublicArea
Public Class SubWindow_Property

    Private Sub UserControl_Loaded(sender As Object, e As RoutedEventArgs)
        SW_Property = Me
    End Sub
    Public Sub Load(Target As EnemyAddEvent)
        PB_Enemy.Load(Target)
    End Sub

    Private Sub ScrollViewer_KeyDown(sender As Object, e As KeyEventArgs)
        If e.Key = Key.Up OrElse e.Key = Key.Left OrElse e.Key = Key.Right OrElse e.Key = Key.Down Then
            e.Handled = True
        End If
    End Sub
End Class
