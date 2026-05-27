Imports System.Threading
Imports AgSTG
Imports AgSTG.Core
Imports ResourcePack.DD
Imports Windows.Win32.Storage
Imports DanmakuDesigner.PublicArea
Class MainWindow
    Public TM_Main As MediaTimer
    Private Maximized As Boolean = False
    Public Ticks As Long
    Private SW_FPS As New Stopwatch

    Private Sub Grid_MouseDown(sender As Object, e As MouseButtonEventArgs)

    End Sub
    Private Sub Window_Loaded(sender As Object, e As RoutedEventArgs)
        MW = Me
        TM_Main = New MediaTimer(60)
        TM_Main.Act.Add(AddressOf STG_Update)
        TM_Main.Act.Add(AddressOf UpdatePerfromance)
        TM_Main.Act.Add(AddressOf UpdateTimeLine)
        SetBorderColor(0)
        WorkArea.Visibility = Visibility.Hidden
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
        TM_Main.Stop()
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
            BorderUp.Height = New GridLength(0)
            BorderDown.Height = New GridLength(0)
            BorderLeft.Width = New GridLength(0)
            BorderRight.Width = New GridLength(0)
            BT_Maximize.Icon = Textures.BT_Restore
        Else
            BT_Maximize.Icon = Textures.BT_Maximize
            BorderUp.Height = New GridLength(6)
            BorderDown.Height = New GridLength(6)
            BorderLeft.Width = New GridLength(6)
            BorderRight.Width = New GridLength(6)
        End If
    End Sub

    Private Sub SetBorderColor(value As Byte)
        Select Case value
            Case 1
                For i = 0 To 1
                    Border11.GradientStops.Item(i).Color = Color.FromArgb(i * 64, 1, 121, 203)
                    Border12.GradientStops.Item(i).Color = Color.FromArgb(i * 64, 1, 121, 203)
                    Border13.GradientStops.Item(i).Color = Color.FromArgb(i * 64, 1, 121, 203)
                    Border21.GradientStops.Item(i).Color = Color.FromArgb(i * 64, 1, 121, 203)
                    Border23.GradientStops.Item(i).Color = Color.FromArgb(i * 64, 1, 121, 203)
                    Border31.GradientStops.Item(i).Color = Color.FromArgb(i * 64, 1, 121, 203)
                    Border32.GradientStops.Item(i).Color = Color.FromArgb(i * 64, 1, 121, 203)
                    Border33.GradientStops.Item(i).Color = Color.FromArgb(i * 64, 1, 121, 203)
                Next
                borderall.Stroke = New SolidColorBrush(Color.FromArgb(255, 1, 121, 203))
                BottomBar.Background = New SolidColorBrush(Color.FromArgb(255, 1, 121, 203))
                Exit Select
            Case 2
                For i = 0 To 1
                    Border11.GradientStops.Item(i).Color = Color.FromArgb(i * 64, 202, 81, 0)
                    Border12.GradientStops.Item(i).Color = Color.FromArgb(i * 64, 202, 81, 0)
                    Border13.GradientStops.Item(i).Color = Color.FromArgb(i * 64, 202, 81, 0)
                    Border21.GradientStops.Item(i).Color = Color.FromArgb(i * 64, 202, 81, 0)
                    Border23.GradientStops.Item(i).Color = Color.FromArgb(i * 64, 202, 81, 0)
                    Border31.GradientStops.Item(i).Color = Color.FromArgb(i * 64, 202, 81, 0)
                    Border32.GradientStops.Item(i).Color = Color.FromArgb(i * 64, 202, 81, 0)
                    Border33.GradientStops.Item(i).Color = Color.FromArgb(i * 64, 202, 81, 0)
                Next
                borderall.Stroke = New SolidColorBrush(Color.FromArgb(255, 202, 81, 0))
                BottomBar.Background = New SolidColorBrush(Color.FromArgb(255, 202, 81, 0))
                Exit Select
            Case Else
                For i = 0 To 1
                    Border11.GradientStops.Item(i).Color = Color.FromArgb(i * 64, 104, 33, 122)
                    Border12.GradientStops.Item(i).Color = Color.FromArgb(i * 64, 104, 33, 122)
                    Border13.GradientStops.Item(i).Color = Color.FromArgb(i * 64, 104, 33, 122)
                    Border21.GradientStops.Item(i).Color = Color.FromArgb(i * 64, 104, 33, 122)
                    Border23.GradientStops.Item(i).Color = Color.FromArgb(i * 64, 104, 33, 122)
                    Border31.GradientStops.Item(i).Color = Color.FromArgb(i * 64, 104, 33, 122)
                    Border32.GradientStops.Item(i).Color = Color.FromArgb(i * 64, 104, 33, 122)
                    Border33.GradientStops.Item(i).Color = Color.FromArgb(i * 64, 104, 33, 122)
                Next
                borderall.Stroke = New SolidColorBrush(Color.FromArgb(255, 104, 33, 122))
                BottomBar.Background = New SolidColorBrush(Color.FromArgb(255, 104, 33, 122))
                Exit Select
        End Select
    End Sub
    Public Sub STG_Update()
        Dispatcher.Invoke(Sub()
                              KeyState.Up = Keyboard.IsKeyDown(Key.Up)
                              KeyState.Down = Keyboard.IsKeyDown(Key.Down)
                              KeyState.Left = Keyboard.IsKeyDown(Key.Left)
                              KeyState.Right = Keyboard.IsKeyDown(Key.Right)
                              KeyState.Slow = Keyboard.IsKeyDown(Key.LeftShift)
                              KeyState.Shoot = Keyboard.IsKeyDown(Key.Z)
                              KeyState.Bomb = Keyboard.IsKeyDown(Key.X)
                              SW_Stage.STG.Render()
                          End Sub)
        If Ticks Mod 2 = 0 Then
            ResourcePack.Sounds.Sounds_Playing.Clear()
        End If

    End Sub
    Private Sub UpdatePerfromance()
        Static interval As Long
        Static time As Double
        If Ticks Mod 20 = 0 Then
            Dim fps As Double = 200000000 / SW_FPS.ElapsedTicks
            SW_FPS.Stop()
            interval = SW_FPS.ElapsedTicks
            Dispatcher.Invoke(Sub()
                                  SW_PF.CT_FPS.Add(fps)
                                  SW_PF.CT_MSPT.Add(time / 20)
                                  SW_PF.LB_Coordinate.Content = "X:" + STG.Player.X.ToString("F2") + " " + "Y:" + STG.Player.Y.ToString("F2")
                                  SW_PF.LB_Count.Content = "实体数：" + CStr(STG.Objects.Count)
                              End Sub)
            SW_FPS.Restart()
            time = 0
        End If
        time += TM_Main.MSPT
        Ticks += 1
    End Sub
    Private Sub UpdateTimeLine()
        Dispatcher.Invoke(Sub()
                              For Each e In Events
                                  If e.Tick = SW_Timeline.Position Then
                                      e.Run()
                                  End If
                              Next
                              SW_Timeline.Position += 1
                              If SW_Timeline.Position >= SW_Timeline.Length Then
                                  BT_Stop_Clicked()
                              End If
                          End Sub)
    End Sub

    Private Sub BT_Run_Clicked()
        SetBorderColor(2)
        TM_Main.Start()
    End Sub

    Private Sub BT_Pause_Clicked()
        SetBorderColor(1)
        TM_Main.Stop()
    End Sub

    Private Sub BT_Stop_Clicked()
        SetBorderColor(1)
        STG.Reset()
        SW_PF.CT_FPS.Clear()
        SW_PF.CT_MSPT.Clear()
        TM_Main.Stop()
        SW_Timeline.Position = 0
    End Sub

    Private Sub BT_NewFile_Clicked()
        SetBorderColor(1)
        WorkArea.Visibility = Visibility.Visible
    End Sub

    Private Sub BT_CloseFile_Clicked()
        SetBorderColor(0)
        WorkArea.Visibility = Visibility.Hidden
    End Sub

    Private Sub Window_Activated(sender As Object, e As EventArgs)
        Dim c As SolidColorBrush = BottomBar.Background
        For i = 0 To 1
            Border11.GradientStops.Item(i).Color = Color.FromArgb(i * 64, c.Color.R, c.Color.G, c.Color.B)
            Border12.GradientStops.Item(i).Color = Color.FromArgb(i * 64, c.Color.R, c.Color.G, c.Color.B)
            Border13.GradientStops.Item(i).Color = Color.FromArgb(i * 64, c.Color.R, c.Color.G, c.Color.B)
            Border21.GradientStops.Item(i).Color = Color.FromArgb(i * 64, c.Color.R, c.Color.G, c.Color.B)
            Border23.GradientStops.Item(i).Color = Color.FromArgb(i * 64, c.Color.R, c.Color.G, c.Color.B)
            Border31.GradientStops.Item(i).Color = Color.FromArgb(i * 64, c.Color.R, c.Color.G, c.Color.B)
            Border32.GradientStops.Item(i).Color = Color.FromArgb(i * 64, c.Color.R, c.Color.G, c.Color.B)
            Border33.GradientStops.Item(i).Color = Color.FromArgb(i * 64, c.Color.R, c.Color.G, c.Color.B)
        Next
        borderall.Stroke = c
        RC_Icon.Opacity = 1
        BT_Close.Opacity = 1
        BT_Maximize.Opacity = 1
        BT_Minimize.Opacity = 1
    End Sub

    Private Sub Window_Deactivated(sender As Object, e As EventArgs)
        For i = 0 To 1
            Border11.GradientStops.Item(i).Color = Color.FromArgb(i * 64, 67, 67, 70)
            Border12.GradientStops.Item(i).Color = Color.FromArgb(i * 64, 67, 67, 70)
            Border13.GradientStops.Item(i).Color = Color.FromArgb(i * 64, 67, 67, 70)
            Border21.GradientStops.Item(i).Color = Color.FromArgb(i * 64, 67, 67, 70)
            Border23.GradientStops.Item(i).Color = Color.FromArgb(i * 64, 67, 67, 70)
            Border31.GradientStops.Item(i).Color = Color.FromArgb(i * 64, 67, 67, 70)
            Border32.GradientStops.Item(i).Color = Color.FromArgb(i * 64, 67, 67, 70)
            Border33.GradientStops.Item(i).Color = Color.FromArgb(i * 64, 67, 67, 70)
        Next
        borderall.Stroke = New SolidColorBrush(Color.FromArgb(255, 67, 67, 70))
        RC_Icon.Opacity = 0.5
        BT_Close.Opacity = 0.5
        BT_Maximize.Opacity = 0.5
        BT_Minimize.Opacity = 0.5
    End Sub

    Private Sub BT_Refresh_Clicked()
        SW_Timeline.Length = 1000
    End Sub

    Private Sub Window_Closed(sender As Object, e As EventArgs)
        Application.Current.Shutdown()
    End Sub

    Private Sub Window_PreviewMouseDown(sender As Object, e As MouseButtonEventArgs)
        If FocusedTextBox IsNot Nothing Then
            Dim dep = TryCast(e.OriginalSource, DependencyObject)
            If dep Is Nothing Then Return
            If IsChildOf(dep, FocusedTextBox) Then
                Return
            End If
            Keyboard.ClearFocus()
            FocusedTextBox = Nothing
        End If
    End Sub

    Private Sub BT_Undo_Clicked()
        UndoMgr.Undo()
        SW_Property.UpdateData()
    End Sub

    Private Sub BT_Redo_Clicked()
        UndoMgr.Redo()
        SW_Property.UpdateData()
    End Sub
End Class
