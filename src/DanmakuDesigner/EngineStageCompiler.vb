Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports System.Reflection
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.VisualBasic
Imports DanmakuDesigner.Core

''' <summary>
''' 把当前关卡的生成代码在内存中编译成可加载的 Stage 程序集。
''' 不写工程文件、不启动 dotnet build；供预览面板中的 STG 控件直接运行。
''' </summary>
Public Module EngineStageCompiler

    Public Function Compile(stageProject As StageProject, engineRepo As String,
                            Optional resourceUris As IDictionary(Of String, String) = Nothing,
                            Optional audioNames As IDictionary(Of String, String) = Nothing) As CompiledEngineStage
        If stageProject Is Nothing Then Throw New ArgumentNullException(NameOf(stageProject))
        Dim clone = ProjectJson.Clone(stageProject)
        clone.Stage.CodeName = "StagePreview"
        Dim generated = VBCodeGenerator.Generate(clone, engineRepo, resourceUris, audioNames)
        Dim code = generated.Code
        ' 预览面板用 BackgroundRenderer 直接承载背景，不需要生成背景 UserControl 的 BAML。
        code = code.Replace("Private BG As StagePreviewbg", "Private BG As Object")
        code = code.Replace("Background = New StagePreviewbg()", "Background = Nothing")
        code = code.Replace("BG = DirectCast(Background, StagePreviewbg)", "BG = Nothing")

        Dim tree = VisualBasicSyntaxTree.ParseText(code)
        Dim references = BuildReferences()
        Dim options As New VisualBasicCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        options = options.WithOptionStrict(OptionStrict.Off)
        Dim globalImports = {
            "Microsoft.VisualBasic",
            "System",
            "System.Collections.Generic",
            "System.Diagnostics",
            "System.Linq",
            "System.Threading.Tasks",
            "System.Windows",
            "System.Windows.Controls",
            "System.Windows.Data",
            "System.Windows.Documents",
            "System.Windows.Input",
            "System.Windows.Media",
            "System.Windows.Media.Imaging",
            "System.Windows.Navigation",
            "System.Windows.Shapes"
        }.Select(Function(name) GlobalImport.Parse(name))
        options = options.WithGlobalImports(globalImports)
        Dim compilation = VisualBasicCompilation.Create("StagePreviewRuntime", {tree}, references, options)
        Using stream As New MemoryStream()
            Dim result = compilation.Emit(stream)
            If Not result.Success Then
                Dim errors = result.Diagnostics.
                    Where(Function(d) d.Severity = DiagnosticSeverity.[Error]).
                    Select(Function(d) d.ToString()).
                    Take(12)
                Throw New InvalidOperationException("预览代码编译失败：" & Environment.NewLine & String.Join(Environment.NewLine, errors))
            End If
            Return New CompiledEngineStage With {
                .AssemblyBytes = stream.ToArray(),
                .ClassName = "StagePreview"
            }
        End Using
    End Function

    Private Function BuildReferences() As List(Of MetadataReference)
        Dim result As New List(Of MetadataReference)
        Dim seen As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        For Each assembly In AppDomain.CurrentDomain.GetAssemblies()
            If assembly Is Nothing OrElse assembly.IsDynamic Then Continue For
            Dim location = ""
            Try
                location = assembly.Location
            Catch
            End Try
            If String.IsNullOrWhiteSpace(location) OrElse Not File.Exists(location) Then Continue For
            If Not seen.Add(location) Then Continue For
            Try
                result.Add(MetadataReference.CreateFromFile(location))
            Catch
            End Try
        Next
        Dim referenceTypes As System.Type() = {
            GetType(AgSTG.Stage),
            GetType(ResourcePack.Textures),
            GetType(ResourcePack.TH07.Textures),
            GetType(ManagedBass.Bass),
            GetType(wpfpslib.BlackHoleEffect),
            GetType(System.Windows.UIElement),
            GetType(System.Linq.Enumerable),
            GetType(Microsoft.VisualBasic.Constants),
            GetType(Object)
        }
        For Each referenceType In referenceTypes
            Try
                Dim location = referenceType.Assembly.Location
                If Not String.IsNullOrWhiteSpace(location) AndAlso seen.Add(location) Then
                    result.Add(MetadataReference.CreateFromFile(location))
                End If
            Catch
            End Try
        Next
        Return result
    End Function
End Module

Public Class CompiledEngineStage
    Public Property AssemblyBytes As Byte()
    Public Property ClassName As String = ""
End Class
