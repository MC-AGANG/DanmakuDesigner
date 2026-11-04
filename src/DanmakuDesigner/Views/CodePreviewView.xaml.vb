Imports System.Text.RegularExpressions
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Documents
Imports System.Windows.Media
Imports DanmakuDesigner.Core

Public Class CodePreviewView

    Private _generated As GeneratedStage
    Private _loaded As Boolean
    Private _updatingCombo As Boolean
    Private ReadOnly _tokenRegex As New Regex("'[^\r\n]*|""(?:[^""]|"""")*""|\b\d+(?:\.\d+)?\b|[A-Za-z_][A-Za-z0-9_]*", RegexOptions.Compiled)
    Private ReadOnly _keywordBrush = New SolidColorBrush(Color.FromRgb(86, 156, 214))
    Private ReadOnly _controlBrush = New SolidColorBrush(Color.FromRgb(197, 134, 192))
    Private ReadOnly _stringBrush = New SolidColorBrush(Color.FromRgb(206, 145, 120))
    Private ReadOnly _commentBrush = New SolidColorBrush(Color.FromRgb(106, 153, 85))
    Private ReadOnly _numberBrush = New SolidColorBrush(Color.FromRgb(181, 206, 168))
    Private ReadOnly _typeBrush = New SolidColorBrush(Color.FromRgb(78, 201, 176))
    Private ReadOnly _textBrush = New SolidColorBrush(Color.FromRgb(212, 212, 212))
    Private ReadOnly _lineNoBrush = New SolidColorBrush(Color.FromRgb(90, 90, 90))

    Private Shared ReadOnly Keywords As New HashSet(Of String)(StringComparer.Ordinal) From {
        "Public", "Private", "Friend", "Class", "Sub", "Function", "End", "Inherits", "Imports", "Option",
        "Dim", "New", "As", "Return", "If", "Then", "Else", "ElseIf", "For", "Next", "Select", "Case",
        "Overrides", "ReadOnly", "Shared", "Property", "Me", "MyBase", "Mod", "AndAlso", "OrElse", "Not",
        "True", "False", "Nothing", "Exit", "While", "Try", "Catch", "Finally", "With", "Namespace",
        "DirectCast", "CType", "CInt", "CByte", "CDbl", "SyncLock", "Step", "To", "ByVal", "ByRef", "Optional"
    }

    Private Shared ReadOnly ControlWords As New HashSet(Of String)(StringComparer.Ordinal) From {
        "If", "Then", "Else", "ElseIf", "For", "Next", "Select", "Case", "Return", "Exit", "While", "Try", "Catch", "Finally", "End"
    }

    Private Shared ReadOnly TypeWords As New HashSet(Of String)(StringComparer.Ordinal) From {
        "Integer", "Double", "Long", "Byte", "Boolean", "String", "Action", "Enemy", "Bullet", "Stage", "Vector",
        "List", "Timer", "Color", "Ellipse", "UserControl", "Canvas", "ImageBrush", "CLng", "CG", "Math", "STG"
    }

    Public Sub New()
        InitializeComponent()
        AddHandler Loaded, Sub()
            _loaded = True
            Refresh()
        End Sub
        FileCombo.SelectedIndex = 0
    End Sub

    Public Sub Refresh()
        If Not _loaded Then Return
        Dim stage = App.Session.Stage
        If stage Is Nothing Then
            _generated = Nothing
            CodeBox.Document = New FlowDocument()
            StatusText.Text = "没有打开的关卡"
            Return
        End If
        Try
            _generated = VBCodeGenerator.Generate(App.Session.ActiveStageProject, App.Session.EngineRepo)
            PopulateFileItems()
            ClassText.Text = "类名 " & _generated.StageClassName & " · 目标 net10.0-windows"
            RenderCurrent()
        Catch ex As Exception
            _generated = Nothing
            StatusText.Text = "生成失败：" & ex.Message
            CodeBox.Document = New FlowDocument(New Paragraph(New Run(ex.ToString())) With {.Foreground = Ui.Brush("#F14C4C")})
        End Try
    End Sub

    Private Sub PopulateFileItems()
        If _generated Is Nothing Then Return
        Dim index = Math.Max(0, FileCombo.SelectedIndex)
        _updatingCombo = True
        Try
            FileCombo.Items.Clear()
            Dim name = _generated.FileBaseName
            FileCombo.Items.Add(New ComboBoxItem With {.Content = name & ".vb · 关卡逻辑", .Tag = "stage"})
            FileCombo.Items.Add(New ComboBoxItem With {.Content = name & "bg.xaml · 背景", .Tag = "bgxaml"})
            FileCombo.Items.Add(New ComboBoxItem With {.Content = name & "bg.xaml.vb · 背景代码", .Tag = "bgcode"})
            FileCombo.Items.Add(New ComboBoxItem With {.Content = name & ".vbproj · 工程文件", .Tag = "project"})
            FileCombo.Items.Add(New ComboBoxItem With {.Content = "导出入门 · 集成说明", .Tag = "integration"})
            FileCombo.SelectedIndex = Math.Min(index, FileCombo.Items.Count - 1)
        Finally
            _updatingCombo = False
        End Try
    End Sub

    Private Function CurrentTag() As String
        Dim item = TryCast(FileCombo.SelectedItem, ComboBoxItem)
        Return If(item Is Nothing, "stage", CStr(item.Tag))
    End Function

    Private Function CurrentText() As String
        If _generated Is Nothing Then Return ""
        Select Case CurrentTag()
            Case "bgxaml" : Return _generated.BackgroundXaml
            Case "bgcode" : Return _generated.BackgroundCode
            Case "project" : Return _generated.ProjectFile
            Case "integration" : Return _generated.Integration
            Case Else : Return _generated.Code
        End Select
    End Function

    Private Function CurrentFileName() As String
        If _generated Is Nothing Then Return ""
        Select Case CurrentTag()
            Case "bgxaml" : Return _generated.StageClassName & "bg.xaml"
            Case "bgcode" : Return _generated.StageClassName & "bg.xaml.vb"
            Case "project" : Return _generated.StageClassName & ".vbproj"
            Case "integration" : Return "README-集成说明.txt"
            Case Else : Return _generated.StageClassName & ".vb"
        End Select
    End Function

    Private Sub RenderCurrent()
        Dim text = CurrentText()
        Dim lines = If(text, "").Replace(vbCrLf, vbLf).Split(ChrW(10))
        Dim doc As New FlowDocument With {
            .PagePadding = New Thickness(0),
            .FontFamily = New FontFamily("Consolas"),
            .FontSize = 12.5,
            .Foreground = _textBrush,
            .LineHeight = 17.5
        }
        For i = 0 To lines.Length - 1
            Dim line = lines(i)
            Dim paragraph As New Paragraph With {.Margin = New Thickness(0), .LineHeight = 17.5}
            paragraph.Inlines.Add(New Run((i + 1).ToString().PadLeft(4) & "  ") With {.Foreground = _lineNoBrush})
            AppendHighlighted(paragraph, line)
            doc.Blocks.Add(paragraph)
        Next
        CodeBox.Document = doc
        CodeBox.ScrollToHome()
        StatusText.Text = CurrentFileName() & " · " & lines.Length & " 行" & If(CurrentTag() = "integration", "", " · 由编辑器生成，重新导出会覆盖")
    End Sub

    Private Sub AppendHighlighted(paragraph As Paragraph, line As String)
        Dim index = 0
        For Each match As Match In _tokenRegex.Matches(line)
            If match.Index > index Then
                paragraph.Inlines.Add(New Run(line.Substring(index, match.Index - index)) With {.Foreground = _textBrush})
            End If
            Dim token = match.Value
            Dim brush = _textBrush
            If token.StartsWith("'") Then
                brush = _commentBrush
            ElseIf token.StartsWith("""") Then
                brush = _stringBrush
            ElseIf Char.IsDigit(token(0)) Then
                brush = _numberBrush
            ElseIf ControlWords.Contains(token) Then
                brush = _controlBrush
            ElseIf Keywords.Contains(token) Then
                brush = _keywordBrush
            ElseIf TypeWords.Contains(token) Then
                brush = _typeBrush
            End If
            paragraph.Inlines.Add(New Run(token) With {.Foreground = brush})
            index = match.Index + match.Length
        Next
        If index < line.Length Then
            paragraph.Inlines.Add(New Run(line.Substring(index)) With {.Foreground = _textBrush})
        End If
    End Sub

    Private Sub File_Changed(sender As Object, e As SelectionChangedEventArgs)
        If _loaded AndAlso Not _updatingCombo Then RenderCurrent()
    End Sub

    Private Sub Refresh_Click(sender As Object, e As RoutedEventArgs)
        Refresh()
    End Sub

    Private Sub Copy_Click(sender As Object, e As RoutedEventArgs)
        Dim text = CurrentText()
        If String.IsNullOrEmpty(text) Then Return
        Try
            Clipboard.SetText(text)
            StatusText.Text = "已复制 " & CurrentFileName() & " 到剪贴板"
        Catch ex As Exception
            StatusText.Text = "复制失败：" & ex.Message
        End Try
    End Sub

    Private Sub Export_Click(sender As Object, e As RoutedEventArgs)
        Dim main = TryCast(Window.GetWindow(Me), MainWindow)
        If main IsNot Nothing Then main.ExportCode()
    End Sub

End Class
