Imports DanmakuDesigner.PublicArea
Public Class SubWindow_Performance
    Private Sub CT_MSPT_ScrollChanged(sender As Object, e As ScrollChangedEventArgs)
        CT_FPS.SV_Main.ScrollToHorizontalOffset(e.HorizontalOffset)
    End Sub

    Private Sub UserControl_Loaded(sender As Object, e As RoutedEventArgs)
        SW_Performance = Me
    End Sub
End Class
