Imports ResourcePack
Class Application
    Private LW As LoadWindow
    Private Async Sub Application_Startup(sender As Object, e As StartupEventArgs)
        LW = New LoadWindow
        LW.Show()
        Await LoadResource()
        Dim mw As New MainWindow
        mw.Show()
        LW.Close()
    End Sub

    ' Application-level events, such as Startup, Exit, and DispatcherUnhandledException
    ' can be handled in this file.
    Private Async Function LoadResource() As Task
        Textures.Load()
        Sounds.Load()
        DD.Textures.Load()
    End Function
End Class
