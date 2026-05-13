Imports AgSTG
Public Class SW_Stage
    Public Shared STG As New STG
    Private Sub UserControl_SizeChanged(sender As Object, e As SizeChangedEventArgs)
        If ActualHeight / 7 > ActualWidth / 6 Then
            Me_Scale.ScaleX = ActualWidth / 384
            Me_Scale.ScaleY = ActualWidth / 384
        Else
            Me_Scale.ScaleX = ActualHeight / 448
            Me_Scale.ScaleY = ActualHeight / 448
        End If
    End Sub

    Private Sub UserControl_Loaded(sender As Object, e As RoutedEventArgs)
        GD_Main.Children.Add(STG)
        STG.Height = 448
        STG.Width = 384
        STG.Reset()
    End Sub
End Class
