Option Strict On
Option Infer On

Imports System.IO
Imports System.Text
Imports System.Text.Encodings.Web
Imports System.Text.Json
Imports System.Text.Json.Serialization

Namespace Global.DanmakuDesigner.Core

    ''' <summary>工程文件的 JSON 读写。格式保持可读，方便手改与版本管理。</summary>
    Public Module ProjectJson

        Public ReadOnly Options As JsonSerializerOptions = CreateOptions()

        Private Function CreateOptions() As JsonSerializerOptions
            Dim options As New JsonSerializerOptions With {
                .WriteIndented = True,
                .PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                .PropertyNameCaseInsensitive = True,
                .DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                .Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }
            options.Converters.Add(New JsonStringEnumConverter(JsonNamingPolicy.CamelCase))
            Return options
        End Function

        Public Function ToJson(project As StageProject) As String
            Return JsonSerializer.Serialize(project, Options)
        End Function

        Public Function FromJson(json As String) As StageProject
            Dim project = JsonSerializer.Deserialize(Of StageProject)(json, Options)
            If project Is Nothing Then Throw New InvalidDataException("无法解析工程文件。")
            project.Normalize()
            Return project
        End Function

        Public Function ToGameJson(project As GameProject) As String
            Return JsonSerializer.Serialize(project, Options)
        End Function

        ''' <summary>读取多关卡工程；旧的单关卡 .dmkproj 会自动包装成一个关卡。</summary>
        Public Function FromGameJson(json As String) As GameProject
            Using document = JsonDocument.Parse(json)
                Dim root = document.RootElement
                If root.ValueKind = JsonValueKind.Object AndAlso root.TryGetProperty("stages", Nothing) Then
                    Dim game = JsonSerializer.Deserialize(Of GameProject)(json, Options)
                    If game Is Nothing Then Throw New InvalidDataException("无法解析游戏工程文件。")
                    game.Normalize()
                    Return game
                End If
            End Using

            Dim stage = FromJson(json)
            Dim legacy As New GameProject With {.Name = If(stage.Name, "未命名游戏")}
            legacy.Stages.Add(stage)
            legacy.Normalize()
            Return legacy
        End Function

        Public Function Clone(Of T)(value As T) As T
            Dim json = JsonSerializer.Serialize(value, Options)
            Return JsonSerializer.Deserialize(Of T)(json, Options)
        End Function

        Public Sub Save(filePath As String, project As StageProject)
            project.Normalize()
            WriteFile(filePath, ToJson(project))
        End Sub

        Public Sub SaveGame(filePath As String, project As GameProject)
            project.Normalize()
            WriteFile(filePath, ToGameJson(project))
        End Sub

        Private Sub WriteFile(filePath As String, json As String)
            Dim dir = System.IO.Path.GetDirectoryName(filePath)
            If Not String.IsNullOrEmpty(dir) Then Directory.CreateDirectory(dir)
            Dim temp = filePath & ".tmp"
            File.WriteAllText(temp, json, New UTF8Encoding(False))
            If File.Exists(filePath) Then File.Delete(filePath)
            File.Move(temp, filePath)
        End Sub

        Public Function Load(filePath As String) As StageProject
            Dim json = File.ReadAllText(filePath, Encoding.UTF8)
            Return FromJson(json)
        End Function

        Public Function LoadGame(filePath As String) As GameProject
            Dim json = File.ReadAllText(filePath, Encoding.UTF8)
            Return FromGameJson(json)
        End Function

    End Module

End Namespace
