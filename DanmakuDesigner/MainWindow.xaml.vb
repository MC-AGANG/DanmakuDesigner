Imports System.Threading
Imports AgSTG
Imports AgSTG.Core
Imports ResourcePack.DD
Class MainWindow
    Public TM_Main As MediaTimer
    Private Maximized As Boolean = False
    Public Ticks As Long
    Private SW_FPS As New Stopwatch

    Private Sub Grid_MouseDown(sender As Object, e As MouseButtonEventArgs)

    End Sub
    Private Sub Window_Loaded(sender As Object, e As RoutedEventArgs)
        TM_Main = New MediaTimer(60)
        TM_Main.Act.Add(AddressOf STG_Update)
        TM_Main.Act.Add(AddressOf FpsUpdate)
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
            BT_Maximize.Icon = Textures.BT_Restore
        Else
            BT_Maximize.Icon = Textures.BT_Maximize
        End If
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
    Private Sub FpsUpdate()
        Static interval As Long
        If Ticks Mod 120 = 0 Then
            Dim fps As Double = 1200000000 / SW_FPS.ElapsedTicks
            SW_FPS.Stop()
            interval = SW_FPS.ElapsedTicks
            Dispatcher.Invoke(Sub()
                                  'Debug.WriteLine(fps.ToString("F2"))
                              End Sub)
            SW_FPS.Restart()
        End If
        Ticks += 1
    End Sub

    Private Sub BT_Run_Clicked()
        TM_Main.Start()
    End Sub

    Private Sub BT_Pause_Clicked()
        TM_Main.Stop()
    End Sub
End Class
