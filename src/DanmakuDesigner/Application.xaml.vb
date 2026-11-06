Imports System.Windows
Imports System.Windows.Threading
Imports DanmakuDesigner.Core

Public Class App
    Inherits Application

    Public Shared ReadOnly Property Session As New EditorSession()

    Public Shared Sub LogStartup(message As String)
        If String.IsNullOrEmpty(Environment.GetEnvironmentVariable("DANDESIGN_DEBUG")) Then Return
        Try
            Dim path = IO.Path.Combine(IO.Path.GetTempPath(), "DanmakuDesigner-startup.log")
            IO.File.AppendAllText(path, DateTime.Now.ToString("HH:mm:ss.fff") & "  " & message & Environment.NewLine)
        Catch
        End Try
    End Sub

    Private Sub Application_Startup(sender As Object, e As StartupEventArgs)
        LogStartup("startup args: " & String.Join(" ", e.Args))
        AddHandler DispatcherUnhandledException, AddressOf App_DispatcherUnhandledException
        ShutdownMode = ShutdownMode.OnMainWindowClose
        TextureService.Initialize()
        LogStartup("textures available=" & TextureService.Available & " error=" & TextureService.ErrorMessage)

        Dim openPath = ArgValue(e.Args, "--open")
        Try
            Session.Initialize(openPath)
            LogStartup("session initialized; stage=" & If(Session.Stage Is Nothing, "NOTHING", Session.Stage.Name))
        Catch ex As Exception
            LogStartup("session init failed: " & ex.ToString())
            Session.AddLog(IssueSeverity.[Error], "初始化失败：" & ex.Message)
        End Try

        Dim exportPath = ArgValue(e.Args, "--export")
        If Not String.IsNullOrWhiteSpace(exportPath) AndAlso Session.Project IsNot Nothing Then
            Try
                IO.Directory.CreateDirectory(exportPath)
                Dim templateZip = IO.Path.Combine(AppContext.BaseDirectory, "EngineTemplate.zip")
                Dim result = SolutionExportService.ExportSolution(Session.Project, exportPath, Session.Project.Name, templateZip, Session.ProjectDirectory)
                For Each file In result.Files
                    If file.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase) Then
                        BuildAndRunService.LastSolutionPath = file
                        Exit For
                    End If
                Next
                IO.File.WriteAllText(IO.Path.Combine(exportPath, ".export-ok"), String.Join(Environment.NewLine, result.Files))
            Catch ex As Exception
                Try
                    IO.File.WriteAllText(IO.Path.Combine(exportPath, ".export-error"), ex.ToString())
                Catch
                End Try
            End Try
            Shutdown(0)
            Return
        End If

        Dim window As New MainWindow()
        MainWindow = window
        Dim tab = ArgValue(e.Args, "--tab")
        Dim templateId = ArgValue(e.Args, "--template")
        Dim patternId = ArgValue(e.Args, "--pattern")
        Dim tick = ParseLong(ArgValue(e.Args, "--preview-tick"), -1)
        Dim autoPlay = HasFlag(e.Args, "--play")
        Dim capture = ArgValue(e.Args, "--capture")
        Dim delay = CInt(ParseLong(ArgValue(e.Args, "--delay"), 900))
        window.ApplyStartupOptions(tab, templateId, patternId, tick, autoPlay, capture, delay, HasFlag(e.Args, "--uitest"))
        window.Show()
        LogStartup("window shown; capture=" & capture & " delay=" & delay)
    End Sub

    Private Sub App_DispatcherUnhandledException(sender As Object, e As DispatcherUnhandledExceptionEventArgs)
        e.Handled = True
        LogStartup("UNHANDLED: " & e.Exception.ToString())
        Try
            IO.File.AppendAllText(IO.Path.Combine(IO.Path.GetTempPath(), "DanmakuDesigner-crash.log"),
                                  DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") & Environment.NewLine &
                                  e.Exception.ToString() & Environment.NewLine & Environment.NewLine)
        Catch
        End Try
        Try
            Session.AddLog(IssueSeverity.[Error], "未处理的异常：" & e.Exception.Message)
        Catch
        End Try
        Try
            MessageBox.Show("发生未处理的错误：" & vbCrLf & vbCrLf & e.Exception.ToString(),
                            "DanmakuDesigner", MessageBoxButton.OK, MessageBoxImage.[Error])
        Catch
            ' 调度器正在关闭或已经暂停时，错误对话框本身也可能失败；不要再次进入异常处理。
        End Try
    End Sub

    Private Function ArgValue(args As String(), name As String) As String
        For i = 0 To args.Length - 2
            If String.Equals(args(i), name, StringComparison.OrdinalIgnoreCase) Then Return args(i + 1)
        Next
        Return ""
    End Function

    Private Function HasFlag(args As String(), name As String) As Boolean
        For Each arg In args
            If String.Equals(arg, name, StringComparison.OrdinalIgnoreCase) Then Return True
        Next
        Return False
    End Function

    Private Function ParseLong(text As String, fallback As Long) As Long
        Dim value As Long
        If Long.TryParse(text, value) Then Return value
        Return fallback
    End Function

End Class
