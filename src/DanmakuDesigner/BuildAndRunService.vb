Imports System.Diagnostics
Imports System.IO
Imports System.Linq
Imports System.Text
Imports System.Text.RegularExpressions
Imports System.Windows

''' <summary>检测 .NET 10 SDK、编译导出的解决方案并定位/启动生成的 exe。</summary>
Public Module BuildAndRunService

    Public Const DotNet10DownloadUrl As String = "https://dotnet.microsoft.com/download/dotnet/10.0"

    Public Property LastSolutionPath As String = ""

    Public Class BuildResult
        Public Property Success As Boolean
        Public Property Output As String = ""
        Public Property ExePath As String = ""
        Public Property SolutionPath As String = ""
    End Class

    Public Function HasDotNet10Sdk() As Boolean
        Try
            Dim psi As New ProcessStartInfo("dotnet", "--list-sdks") With {
                .UseShellExecute = False,
                .CreateNoWindow = True,
                .RedirectStandardOutput = True,
                .RedirectStandardError = True
            }
            Using dotnetProcess = Process.Start(psi)
                If dotnetProcess Is Nothing Then Return False
                Dim output = dotnetProcess.StandardOutput.ReadToEnd()
                dotnetProcess.WaitForExit(4000)
                Return output.Split({vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries).
                    Any(Function(line) Regex.IsMatch(line.Trim(), "^10\.\d"))
            End Using
        Catch
            Return False
        End Try
    End Function

    Public Function EnsureDotNet10Sdk(owner As Window) As Boolean
        If HasDotNet10Sdk() Then Return True
        Dim answer = MessageBox.Show(owner,
            "编译解决方案需要 .NET 10 SDK，但当前电脑未检测到。" & Environment.NewLine & Environment.NewLine &
            "是否打开微软官方下载页面？",
            "需要 .NET 10 SDK", MessageBoxButton.YesNo, MessageBoxImage.Warning)
        If answer = MessageBoxResult.Yes Then
            Try
                Process.Start(New ProcessStartInfo(DotNet10DownloadUrl) With {.UseShellExecute = True})
            Catch ex As Exception
                MessageBox.Show(owner, "无法打开浏览器：" & ex.Message, "打开下载页面", MessageBoxButton.OK, MessageBoxImage.Error)
            End Try
        End If
        Return False
    End Function

    Public Function CompileSolution(solutionPath As String, owner As Window) As BuildResult
        Dim result As New BuildResult With {.SolutionPath = solutionPath}
        If Not EnsureDotNet10Sdk(owner) Then Return result
        If String.IsNullOrWhiteSpace(solutionPath) OrElse Not File.Exists(solutionPath) Then
            MessageBox.Show(owner, "找不到解决方案文件：" & solutionPath, "编译", MessageBoxButton.OK, MessageBoxImage.Error)
            Return result
        End If

        Dim output As New StringBuilder()
        Try
            Dim psi As New ProcessStartInfo("dotnet",
                "build " & Quote(solutionPath) & " -c Release --nologo") With {
                .UseShellExecute = False,
                .CreateNoWindow = True,
                .RedirectStandardOutput = True,
                .RedirectStandardError = True,
                .WorkingDirectory = Path.GetDirectoryName(solutionPath)
            }
            Using process = New Process With {.StartInfo = psi}
                AddHandler process.OutputDataReceived, Sub(sender, e)
                                                            If e.Data IsNot Nothing Then
                                                                SyncLock output
                                                                    output.AppendLine(e.Data)
                                                                End SyncLock
                                                            End If
                                                        End Sub
                AddHandler process.ErrorDataReceived, Sub(sender, e)
                                                          If e.Data IsNot Nothing Then
                                                              SyncLock output
                                                                  output.AppendLine(e.Data)
                                                              End SyncLock
                                                          End If
                                                      End Sub
                process.Start()
                process.BeginOutputReadLine()
                process.BeginErrorReadLine()
                If Not process.WaitForExit(180000) Then
                    Try
                        process.Kill(True)
                    Catch
                    End Try
                    output.AppendLine("编译超时（180 秒）。")
                Else
                    process.WaitForExit()
                End If
                result.Success = process.ExitCode = 0
            End Using
        Catch ex As Exception
            output.AppendLine(ex.ToString())
        End Try

        result.Output = output.ToString()
        result.ExePath = FindExe(solutionPath)
        If result.Success AndAlso String.IsNullOrWhiteSpace(result.ExePath) Then
            result.Success = False
            result.Output &= Environment.NewLine & "编译成功，但未找到生成的 exe。"
        End If
        LastSolutionPath = solutionPath
        Return result
    End Function

    Public Function CompileAndReveal(solutionPath As String, owner As Window) As BuildResult
        Dim result = CompileSolution(solutionPath, owner)
        If result.Success Then
            RevealFile(result.ExePath)
        Else
            ShowBuildFailure(owner, result)
        End If
        Return result
    End Function

    Public Function CompileAndRun(solutionPath As String, owner As Window) As BuildResult
        Dim result = CompileSolution(solutionPath, owner)
        If result.Success Then
            Try
                Process.Start(New ProcessStartInfo(result.ExePath) With {
                    .UseShellExecute = True,
                    .WorkingDirectory = Path.GetDirectoryName(result.ExePath)
                })
            Catch ex As Exception
                MessageBox.Show(owner, "已编译，但启动 exe 失败：" & ex.Message, "编译并运行", MessageBoxButton.OK, MessageBoxImage.Error)
            End Try
        Else
            ShowBuildFailure(owner, result)
        End If
        Return result
    End Function

    Public Sub RevealFile(path As String)
        If String.IsNullOrWhiteSpace(path) OrElse Not File.Exists(path) Then Return
        Try
            Process.Start(New ProcessStartInfo("explorer.exe", "/select," & Quote(path)) With {
                .UseShellExecute = True
            })
        Catch
            Process.Start(New ProcessStartInfo("explorer.exe", Quote(IO.Path.GetDirectoryName(path))) With {
                .UseShellExecute = True
            })
        End Try
    End Sub

    Public Sub ShowBuildFailure(owner As Window, result As BuildResult)
        Dim details = If(String.IsNullOrWhiteSpace(result.Output), "编译失败。", result.Output.Trim())
        If details.Length > 4000 Then details = details.Substring(details.Length - 4000)
        MessageBox.Show(owner, "解决方案编译失败：" & Environment.NewLine & Environment.NewLine & details,
                        "编译失败", MessageBoxButton.OK, MessageBoxImage.Error)
    End Sub

    Public Function FindExe(solutionPath As String) As String
        Dim folder = Path.GetDirectoryName(solutionPath)
        Dim code = Path.GetFileNameWithoutExtension(solutionPath)
        Dim candidates = {
            Path.Combine(folder, code, "bin", "Release", "net10.0-windows7.0", code & ".exe"),
            Path.Combine(folder, code, "bin", "Release", "net10.0-windows", code & ".exe")
        }
        For Each candidate In candidates
            If File.Exists(candidate) Then Return candidate
        Next
        Dim releaseRoot = Path.Combine(folder, code, "bin", "Release")
        If Directory.Exists(releaseRoot) Then
            Try
                Return Directory.GetFiles(releaseRoot, code & ".exe", SearchOption.AllDirectories).FirstOrDefault()
            Catch
            End Try
        End If
        Return ""
    End Function

    Private Function Quote(value As String) As String
        Return """" & value.Replace("""", """""") & """"
    End Function
End Module
