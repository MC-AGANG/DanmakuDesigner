Public Class SW_Performance
    Private Sub CT_MSPT_ScrollChanged(sender As Object, e As ScrollChangedEventArgs)
        CT_FPS.SV_Main.ScrollToHorizontalOffset(e.HorizontalOffset)
    End Sub
End Class
