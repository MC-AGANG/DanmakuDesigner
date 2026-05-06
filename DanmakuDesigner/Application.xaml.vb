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
        Await Task.Run(Sub()
                           Textures.Load()
                           Sounds.Load()
                       End Sub)
    End Function
End Class
