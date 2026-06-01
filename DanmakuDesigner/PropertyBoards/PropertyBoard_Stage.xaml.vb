Imports DanmakuDesigner.PublicArea
Public Class PropertyBoard_Stage
    Public Target As TreeNode.Node_Stage
    Public Sub Load(Target As TreeNode.Node_Stage)
        Me.Target = Target
        Visibility = Visibility.Visible
        UpdateData()
    End Sub
    Public Sub UpdateData()
        If Visibility = Visibility.Visible Then
            If SW_Project.Nodes(0).Children.Contains(Target) Then
                TB_Length.Text = Target.Length
            Else
                Visibility = Visibility.Hidden
            End If
        End If

    End Sub
    Private Sub TB_Length_LostKeyboardFocus(sender As Object, e As KeyboardFocusChangedEventArgs)
        UndoMgr.Execute(New PropertyChangeCommand(Of ULong)(Target, NameOf(Target.Length), Target.Length, Val(TB_Length.Text)))
    End Sub
End Class
