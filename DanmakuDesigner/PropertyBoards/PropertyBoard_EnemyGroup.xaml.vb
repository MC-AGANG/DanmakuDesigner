Imports DanmakuDesigner.PublicArea
Public Class PropertyBoard_EnemyGroup
    Public Target As EnemyGroupEvent
    Private Sub UserControl_Loaded(sender As Object, e As RoutedEventArgs)
        CB_EnemyType.Items.Add("阴阳玉")
        CB_EnemyType.Items.Add("小妖精")
        CB_EnemyType.Items.Add("小蝴蝶")
        CB_EnemyType.Items.Add("中蝴蝶")
        CB_EnemyType.Items.Add("大蝴蝶")
        CB_EnemyType.Items.Add("幽灵")
        CB_EnemyType.Items.Add("乌鸦")
    End Sub

    Private Sub BT_Item_Clicked()
        Dim W As New ToolWindow_ItemEditor With {.Items = Target.Items}
        If W.ShowDialog Then
            UndoMgr.Execute(New PropertyChangeCommand(Of String)(Target, NameOf(Target.Items), Target.Items, W.Items))
        End If
    End Sub
    Public Sub UpdateData()
        If Visibility = Visibility.Visible Then
            If Events.Contains(Target) Then
                TB_Tick.Text = Target.Tick
                CB_EnemyType.SelectedIndex = Target.EnemyType
                CB_EnemyColor.SelectedIndex = Target.EnemyColor
                TB_X.Text = Target.X
                TB_Y.Text = Target.Y
                TB_HP.Text = Target.HP
                TB_Life.Text = Target.Life
                TB_Speed.Text = Target.Speed
                TB_Direction.Text = Target.Direction
                TB_Defence.Text = Target.Defence
                TB_Tag.Text = Target.Tag

                TB_Count.Text = Target.Count
                TB_X_End.Text = Target.X_End
                TB_Y_End.Text = Target.Y_End
                TB_Speed_End.Text = Target.Speed_End
                TB_Direction_End.Text = Target.Direction_End
            Else
                Visibility = Visibility.Hidden
            End If
        End If
    End Sub
    Public Sub Load(target As EnemyGroupEvent)
        Me.Target = target
        Visibility = Visibility.Visible
        UpdateData()
    End Sub

    Private Sub TB_Tick_LostKeyboardFocus(sender As Object, e As KeyboardFocusChangedEventArgs)
        UndoMgr.Execute(New PropertyChangeCommand(Of Long)(Target, NameOf(Target.Tick), Target.Tick, Val(TB_Tick.Text)))
    End Sub

    Private Sub CB_EnemyType_SelectionChanged(sender As Object, e As SelectionChangedEventArgs)
        UndoMgr.Execute(New PropertyChangeCommand(Of Byte)(Target, NameOf(Target.EnemyType), Target.EnemyType, CB_EnemyType.SelectedIndex))
        Select Case CB_EnemyType.SelectedIndex
            Case 0, 5
                CB_EnemyColor.Items.Clear()
                CB_EnemyColor.Items.Add("红")
                CB_EnemyColor.Items.Add("绿")
                CB_EnemyColor.Items.Add("蓝")
                CB_EnemyColor.Items.Add("黄")
            Case 1
                CB_EnemyColor.Items.Clear()
                CB_EnemyColor.Items.Add("红")
                CB_EnemyColor.Items.Add("绿")
                CB_EnemyColor.Items.Add("蓝")
                CB_EnemyColor.Items.Add("黄")
                CB_EnemyColor.Items.Add("红（反色）")
                CB_EnemyColor.Items.Add("绿（反色）")
                CB_EnemyColor.Items.Add("蓝（反色）")
                CB_EnemyColor.Items.Add("黄（反色）")
            Case 2
                CB_EnemyColor.Items.Clear()
                CB_EnemyColor.Items.Add("蓝")
                CB_EnemyColor.Items.Add("红")
            Case 3
                CB_EnemyColor.Items.Clear()
                CB_EnemyColor.Items.Add("黄")
                CB_EnemyColor.Items.Add("青")
            Case 4
                CB_EnemyColor.Items.Clear()
                CB_EnemyColor.Items.Add("正常")
                CB_EnemyColor.Items.Add("反色")
            Case 6
                CB_EnemyColor.Items.Clear()
                CB_EnemyColor.Items.Add("黑色")
        End Select
        CB_EnemyColor.SelectedIndex = 0
    End Sub

    Private Sub CB_EnemyColor_SelectionChanged(sender As Object, e As SelectionChangedEventArgs)
        If CB_EnemyColor.SelectedIndex >= 0 Then
            UndoMgr.Execute(New PropertyChangeCommand(Of Byte)(Target, NameOf(Target.EnemyColor), Target.EnemyColor, CB_EnemyColor.SelectedIndex))
        End If
    End Sub

    Private Sub TB_X_LostKeyboardFocus(sender As Object, e As KeyboardFocusChangedEventArgs)
        UndoMgr.Execute(New PropertyChangeCommand(Of Double)(Target, NameOf(Target.X), Target.X, Val(TB_X.Text)))
    End Sub

    Private Sub TB_Y_LostKeyboardFocus(sender As Object, e As KeyboardFocusChangedEventArgs)
        UndoMgr.Execute(New PropertyChangeCommand(Of Double)(Target, NameOf(Target.Y), Target.Y, Val(TB_Y.Text)))
    End Sub

    Private Sub TB_HP_LostKeyboardFocus(sender As Object, e As KeyboardFocusChangedEventArgs)
        UndoMgr.Execute(New PropertyChangeCommand(Of Double)(Target, NameOf(Target.HP), Target.HP, Val(TB_HP.Text)))
    End Sub

    Private Sub TB_Life_LostKeyboardFocus(sender As Object, e As KeyboardFocusChangedEventArgs)
        UndoMgr.Execute(New PropertyChangeCommand(Of Long)(Target, NameOf(Target.Life), Target.Life, Val(TB_Life.Text)))
    End Sub

    Private Sub TB_Speed_LostKeyboardFocus(sender As Object, e As KeyboardFocusChangedEventArgs)
        UndoMgr.Execute(New PropertyChangeCommand(Of Double)(Target, NameOf(Target.Speed), Target.Speed, Val(TB_Speed.Text)))
    End Sub

    Private Sub TB_Direction_LostKeyboardFocus(sender As Object, e As KeyboardFocusChangedEventArgs)
        UndoMgr.Execute(New PropertyChangeCommand(Of Double)(Target, NameOf(Target.Direction), Target.Direction, Val(TB_Direction.Text)))
    End Sub

    Private Sub TB_Defence_LostKeyboardFocus(sender As Object, e As KeyboardFocusChangedEventArgs)
        UndoMgr.Execute(New PropertyChangeCommand(Of Double)(Target, NameOf(Target.Defence), Target.Defence, Val(TB_Defence.Text)))
    End Sub

    Private Sub TB_Tag_LostKeyboardFocus(sender As Object, e As KeyboardFocusChangedEventArgs)
        UndoMgr.Execute(New PropertyChangeCommand(Of String)(Target, NameOf(Target.Tag), Target.Tag, TB_Tag.Text))
    End Sub

    Private Sub TB_X_End_LostKeyboardFocus(sender As Object, e As KeyboardFocusChangedEventArgs)
        UndoMgr.Execute(New PropertyChangeCommand(Of Double)(Target, NameOf(Target.X_End), Target.X_End, Val(TB_X_End.Text)))
    End Sub

    Private Sub TB_Y_End_LostKeyboardFocus(sender As Object, e As KeyboardFocusChangedEventArgs)
        UndoMgr.Execute(New PropertyChangeCommand(Of Double)(Target, NameOf(Target.Y_End), Target.Y_End, Val(TB_Y_End.Text)))
    End Sub

    Private Sub TB_Speed_End_LostKeyboardFocus(sender As Object, e As KeyboardFocusChangedEventArgs)
        UndoMgr.Execute(New PropertyChangeCommand(Of Double)(Target, NameOf(Target.Speed_End), Target.Speed_End, Val(TB_Speed_End.Text)))
    End Sub

    Private Sub TB_Direction_End_LostKeyboardFocus(sender As Object, e As KeyboardFocusChangedEventArgs)
        UndoMgr.Execute(New PropertyChangeCommand(Of Double)(Target, NameOf(Target.Direction_End), Target.Direction_End, Val(TB_Direction_End.Text)))
    End Sub

    Private Sub TB_Count_LostKeyboardFocus(sender As Object, e As KeyboardFocusChangedEventArgs)
        If Val(TB_Count.Text) <= 0 Then
            TB_Count.Text = 0
        End If
        UndoMgr.Execute(New PropertyChangeCommand(Of UInteger)(Target, NameOf(Target.Count), Target.Count, Val(TB_Count.Text)))
    End Sub
End Class
