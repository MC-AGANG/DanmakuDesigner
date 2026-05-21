Imports AgSTG
Imports DanmakuDesigner.PublicArea
Public Class PropertyBoard_Enemy
    Public Target As EnemyAddEvent
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
        TW_ItemEditor.Load(Target.Target)
    End Sub
    Public Sub Load(target As EnemyAddEvent)
        Me.Target = target
        Visibility = Visibility.Visible
        TB_Tick.Text = target.Tick
        CB_EnemyType.SelectedIndex = target.Target.EnemyType
        CB_EnemyColor.SelectedIndex = target.Target.Color
        TB_X.Text = target.Target.X
        TB_Y.Text = target.Target.Y
        TB_HP.Text = target.Target.HP
        TB_Life.Text = target.Target.Life
        TB_Speed.Text = target.Target.Speed
        TB_Direction.Text = target.Target.Direction
        TB_Defence.Text = target.Target.Defence
        TB_Tag.Text = target.Target.Tag
    End Sub
    Private Sub TB_Tick_TextChanged(sender As Object, e As TextChangedEventArgs)
        Target.Tick = TB_Tick.Text
    End Sub

    Private Sub CB_EnemyType_SelectionChanged(sender As Object, e As SelectionChangedEventArgs)
        Target.Target.EnemyType = CB_EnemyType.SelectedIndex
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
    End Sub

    Private Sub CB_EnemyColor_SelectionChanged(sender As Object, e As SelectionChangedEventArgs)
        Target.Target.Color = CB_EnemyColor.SelectedIndex
    End Sub

    Private Sub TB_X_TextChanged(sender As Object, e As TextChangedEventArgs)
        Target.Target.X = Val(TB_X.Text)
    End Sub

    Private Sub TB_Y_TextChanged(sender As Object, e As TextChangedEventArgs)
        Target.Target.Y = Val(TB_Y.Text)
    End Sub

    Private Sub TB_HP_TextChanged(sender As Object, e As TextChangedEventArgs)
        Target.Target.HP = Val(TB_HP.Text)
    End Sub

    Private Sub TB_Life_TextChanged(sender As Object, e As TextChangedEventArgs)
        Target.Target.Life = Val(TB_Life.Text)
    End Sub

    Private Sub TB_Speed_TextChanged(sender As Object, e As TextChangedEventArgs)
        Target.Target.Speed = Val(TB_Speed.Text)
    End Sub

    Private Sub TB_Direction_TextChanged(sender As Object, e As TextChangedEventArgs)
        Target.Target.Direction = Val(TB_Direction.Text)
    End Sub

    Private Sub TB_Defence_TextChanged(sender As Object, e As TextChangedEventArgs)
        Target.Target.Defence = Val(TB_Defence.Text)
    End Sub

    Private Sub TB_Tag_TextChanged(sender As Object, e As TextChangedEventArgs)
        Target.Target.Tag = TB_Tag.Text
    End Sub
End Class
