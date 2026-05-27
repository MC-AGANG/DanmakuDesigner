Imports DanmakuDesigner.PublicArea
Public Class PropertyBoard_EnemyClear
    Public Target As EnemyClearEvent
    Public Sub UpdateData()
        If Visibility = Visibility.Visible Then
            If Events.Contains(Target) Then
                TB_Tick.Text = Target.Tick
                CB_Tag.IsChecked = Target.UseTag
                TB_Tag.Text = Target.Tag
            Else
                Visibility = Visibility.Hidden
            End If
        End If
    End Sub
    Public Sub Load(Target As EnemyClearEvent)
        Me.Target = Target
        Visibility = Visibility.Visible
        UpdateData()
    End Sub
    Private Sub TB_Tag_LostKeyboardFocus(sender As Object, e As KeyboardFocusChangedEventArgs)
        UndoMgr.Execute(New PropertyChangeCommand(Of String)(Target, NameOf(Target.Tag), Target.Tag, TB_Tag.Text))
    End Sub

    Private Sub CB_Tag_Checked(sender As Object, e As RoutedEventArgs)
        UndoMgr.Execute(New PropertyChangeCommand(Of Boolean)(Target, NameOf(Target.UseTag), Target.UseTag, CB_Tag.IsChecked))
    End Sub

    Private Sub CB_Tag_Unchecked(sender As Object, e As RoutedEventArgs)
        UndoMgr.Execute(New PropertyChangeCommand(Of Boolean)(Target, NameOf(Target.UseTag), Target.UseTag, CB_Tag.IsChecked))
    End Sub

    Private Sub TB_Tick_LostKeyboardFocus(sender As Object, e As KeyboardFocusChangedEventArgs)
        UndoMgr.Execute(New PropertyChangeCommand(Of Long)(Target, NameOf(Target.Tick), Target.Tick, Val(TB_Tick.Text)))
    End Sub
End Class
