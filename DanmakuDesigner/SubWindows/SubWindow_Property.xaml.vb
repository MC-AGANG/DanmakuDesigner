Imports DanmakuDesigner.PublicArea
Public Class SubWindow_Property

    Private Sub UserControl_Loaded(sender As Object, e As RoutedEventArgs)
        SW_Property = Me
    End Sub
    Public Sub Load(Target As EnemyAddEvent)
        HideAll()
        PB_Enemy.Load(Target)
    End Sub
    Public Sub Load(Target As EnemyGroupEvent)
        HideAll()
        PB_EnemyGroup.Load(Target)
    End Sub
    Public Sub Load(Target As EnemyClearEvent)
        HideAll()
        PB_EnemyClear.Load(Target)
    End Sub
    Public Sub Load(Target As BulletAddEvent)
        HideAll()
        PB_Bullet.Load(Target)
    End Sub
    Public Sub Load(Target As BulletGroupEvent)
        HideAll()
        PB_BulletGroup.Load(Target)
    End Sub
    Public Sub Load(Target As BulletClearEvent)
        HideAll()
        PB_BulletClear.Load(Target)
    End Sub
    Public Sub HideAll()
        PB_Enemy.Visibility = Visibility.Hidden
        PB_EnemyGroup.Visibility = Visibility.Hidden
        PB_EnemyClear.Visibility = Visibility.Hidden
        PB_Bullet.Visibility = Visibility.Hidden
        PB_BulletGroup.Visibility = Visibility.Hidden
        PB_BulletClear.Visibility = Visibility.Hidden
    End Sub
    Private Sub ScrollViewer_KeyDown(sender As Object, e As KeyEventArgs)
        If e.Key = Key.Up OrElse e.Key = Key.Left OrElse e.Key = Key.Right OrElse e.Key = Key.Down Then
            e.Handled = True
        End If
    End Sub
    Public Sub UpdateData()
        PB_Enemy.UpdateData()
        PB_EnemyGroup.UpdateData()
        PB_EnemyClear.UpdateData()
        PB_Bullet.UpdateData()
        PB_BulletGroup.UpdateData()
        PB_BulletClear.UpdateData()
    End Sub
End Class
