Imports AgSTG
Imports DanmakuDesigner.PublicArea
Public Class PropertyBoard_BulletGroup
    Public Target As BulletGroupEvent
    Private Sub UserControl_Loaded(sender As Object, e As RoutedEventArgs)
        CB_BulletType.Items.Add("激光")
        CB_BulletType.Items.Add("鳞弹")
        CB_BulletType.Items.Add("环弹")
        CB_BulletType.Items.Add("小玉")
        CB_BulletType.Items.Add("米弹")
        CB_BulletType.Items.Add("苦无")
        CB_BulletType.Items.Add("棱弹")
        CB_BulletType.Items.Add("札弹")
        CB_BulletType.Items.Add("座药")
        CB_BulletType.Items.Add("黑心弹")
        CB_BulletType.Items.Add("小星弹")
        CB_BulletType.Items.Add("星弹")
        CB_BulletType.Items.Add("中玉")
        CB_BulletType.Items.Add("蝶弹")
        CB_BulletType.Items.Add("刀弹")
        CB_BulletType.Items.Add("椭弹")
        CB_BulletType.Items.Add("心弹")
        CB_BulletType.Items.Add("箭头")
        CB_BulletType.Items.Add("小光玉")
        CB_BulletType.Items.Add("点弹")
        CB_BulletType.Items.Add("大玉")
    End Sub
    Public Sub UpdateData()
        If Visibility = Visibility.Visible Then
            If Events.Contains(Target) Then
                TB_Tick.Text = Target.Tick
                TB_Count.Text = Target.Count
                CB_Sniper.IsChecked = Target.Sniper
                CB_BulletType.SelectedIndex = Target.BulletType
                CB_BulletColor.SelectedIndex = Target.BulletColor
                TB_X.Text = Target.X
                TB_Y.Text = Target.Y
                TB_Speed.Text = Target.Speed
                TB_Direction.Text = Target.Direction
                CB_Breakable.IsChecked = Target.Breakable
                TB_Tag.Text = Target.Tag
                TB_X_End.Text = Target.X_End
                TB_Y_End.Text = Target.Y_End
                TB_Speed_End.Text = Target.Speed_End
                TB_Direction_End.Text = Target.Direction_End
            Else
                Visibility = Visibility.Hidden
            End If
        End If
    End Sub
    Public Sub Load(target As BulletGroupEvent)
        Me.Target = target
        Visibility = Visibility.Visible
        UpdateData()
    End Sub
    Private Sub TB_Tick_LostKeyboardFocus(sender As Object, e As KeyboardFocusChangedEventArgs)
        UndoMgr.Execute(New PropertyChangeCommand(Of Long)(Target, NameOf(Target.Tick), Target.Tick, Val(TB_Tick.Text)))
    End Sub
    Private Sub CB_Sniper_Checked(sender As Object, e As RoutedEventArgs)
        UndoMgr.Execute(New PropertyChangeCommand(Of Boolean)(Target, NameOf(Target.Sniper), Target.Sniper, Val(CB_Sniper.IsChecked)))
    End Sub

    Private Sub CB_Sniper_Unchecked(sender As Object, e As RoutedEventArgs)
        UndoMgr.Execute(New PropertyChangeCommand(Of Boolean)(Target, NameOf(Target.Sniper), Target.Sniper, Val(CB_Sniper.IsChecked)))
    End Sub

    Private Sub CB_BulletType_SelectionChanged(sender As Object, e As SelectionChangedEventArgs)
        UndoMgr.Execute(New PropertyChangeCommand(Of Byte)(Target, NameOf(Target.BulletType), Target.BulletType, CB_BulletType.SelectedIndex))
        If CB_BulletType.SelectedIndex = 0 Then
            LB_Speed.Content = "长度|寿命"
        Else
            LB_Speed.Content = "速度"
        End If
        CB_BulletColor.Items.Clear()
        Select Case CB_BulletType.SelectedIndex
            Case 0 To 10, 19
                CB_BulletColor.Items.Add("灰")
                CB_BulletColor.Items.Add("暗红")
                CB_BulletColor.Items.Add("红")
                CB_BulletColor.Items.Add("紫")
                CB_BulletColor.Items.Add("品红")
                CB_BulletColor.Items.Add("深蓝")
                CB_BulletColor.Items.Add("蓝")
                CB_BulletColor.Items.Add("青")
                CB_BulletColor.Items.Add("天蓝")
                CB_BulletColor.Items.Add("深绿")
                CB_BulletColor.Items.Add("绿")
                CB_BulletColor.Items.Add("黄绿")
                CB_BulletColor.Items.Add("土黄")
                CB_BulletColor.Items.Add("黄")
                CB_BulletColor.Items.Add("橙")
                CB_BulletColor.Items.Add("白")
            Case 11 To 18
                CB_BulletColor.Items.Add("灰")
                CB_BulletColor.Items.Add("红")
                CB_BulletColor.Items.Add("紫")
                CB_BulletColor.Items.Add("蓝")
                CB_BulletColor.Items.Add("青")
                CB_BulletColor.Items.Add("绿")
                CB_BulletColor.Items.Add("黄")
                CB_BulletColor.Items.Add("白")
            Case 20
                CB_BulletColor.Items.Add("红")
                CB_BulletColor.Items.Add("蓝")
                CB_BulletColor.Items.Add("绿")
                CB_BulletColor.Items.Add("黄")
        End Select
        CB_BulletColor.SelectedIndex = 0
    End Sub

    Private Sub CB_BulletColor_SelectionChanged(sender As Object, e As SelectionChangedEventArgs)
        If CB_BulletColor.SelectedIndex >= 0 Then
            UndoMgr.Execute(New PropertyChangeCommand(Of Byte)(Target, NameOf(Target.BulletColor), Target.BulletColor, CB_BulletColor.SelectedIndex))
        End If

    End Sub

    Private Sub TB_X_LostKeyboardFocus(sender As Object, e As KeyboardFocusChangedEventArgs)
        UndoMgr.Execute(New PropertyChangeCommand(Of Double)(Target, NameOf(Target.X), Target.X, Val(TB_X.Text)))
    End Sub

    Private Sub TB_Y_LostKeyboardFocus(sender As Object, e As KeyboardFocusChangedEventArgs)
        UndoMgr.Execute(New PropertyChangeCommand(Of Double)(Target, NameOf(Target.Y), Target.Y, Val(TB_Y.Text)))
    End Sub

    Private Sub TB_Speed_LostKeyboardFocus(sender As Object, e As KeyboardFocusChangedEventArgs)
        UndoMgr.Execute(New PropertyChangeCommand(Of Double)(Target, NameOf(Target.Speed), Target.Speed, Val(TB_Speed.Text)))
    End Sub

    Private Sub TB_Direction_LostKeyboardFocus(sender As Object, e As KeyboardFocusChangedEventArgs)
        UndoMgr.Execute(New PropertyChangeCommand(Of Double)(Target, NameOf(Target.Direction), Target.Direction, Val(TB_Direction.Text)))
    End Sub

    Private Sub CB_Breakable_Checked(sender As Object, e As RoutedEventArgs)
        UndoMgr.Execute(New PropertyChangeCommand(Of Boolean)(Target, NameOf(Target.Breakable), Target.Breakable, Val(CB_Breakable.IsChecked)))
    End Sub

    Private Sub CB_Breakable_Unchecked(sender As Object, e As RoutedEventArgs)
        UndoMgr.Execute(New PropertyChangeCommand(Of Boolean)(Target, NameOf(Target.Breakable), Target.Breakable, Val(CB_Breakable.IsChecked)))
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
        UndoMgr.Execute(New PropertyChangeCommand(Of UInteger)(Target, NameOf(Target.Count), Target.Count, Val(TB_Count.Text)))
    End Sub
End Class
