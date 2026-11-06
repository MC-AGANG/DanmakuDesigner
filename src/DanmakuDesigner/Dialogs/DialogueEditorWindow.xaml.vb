Imports System.IO
Imports System.Linq
Imports System.Windows
Imports System.Windows.Controls
Imports DanmakuDesigner.Core

''' <summary>AgSTG CSV 对话的行编辑器。</summary>
Public Class DialogueEditorWindow

    Private ReadOnly _lineItems As New List(Of LineItem)()
    Private ReadOnly _illustrationChoices As New List(Of ChoiceItem)()
    Private _dialogue As DialogueDefinition
    Private _current As DialogueLine
    Private _loading As Boolean

    Public Property ResultDialogue As DialogueDefinition

    Public Sub New(dialogue As DialogueDefinition)
        InitializeComponent()
        _dialogue = ProjectJson.Clone(dialogue)
        _dialogue.Normalize()
        NameBox.Text = _dialogue.Name

        SpeakerBox.ItemsSource = New List(Of ChoiceItem) From {
            New ChoiceItem("0", "玩家"),
            New ChoiceItem("1", "敌方 / Boss")
        }
        SpeakerBox.DisplayMemberPath = "Display"
        SpeakerBox.SelectedValuePath = "Id"

        AfterBox.ItemsSource = New List(Of ChoiceItem) From {
            New ChoiceItem(DialogueAfterAction.None.ToString(), "不结束"),
            New ChoiceItem(DialogueAfterAction.StartBoss.ToString(), "Boss 开打"),
            New ChoiceItem(DialogueAfterAction.NextStage.ToString(), "进入下一关")
        }
        AfterBox.DisplayMemberPath = "Display"
        AfterBox.SelectedValuePath = "Id"
        AfterBox.SelectedValue = _dialogue.AfterAction.ToString()

        Dim bosses As New List(Of ChoiceItem)
        If App.Session IsNot Nothing AndAlso App.Session.Stage IsNot Nothing Then
            For Each bossTemplate In App.Session.Stage.EnemyTemplates
                If bossTemplate IsNot Nothing AndAlso bossTemplate.IsBoss Then bosses.Add(New ChoiceItem(bossTemplate.Id, bossTemplate.Name))
            Next
        End If
        BossBox.ItemsSource = bosses
        BossBox.DisplayMemberPath = "Display"
        BossBox.SelectedValuePath = "Id"
        If bosses.Count > 0 Then
            BossBox.SelectedValue = If(String.IsNullOrWhiteSpace(_dialogue.BossTemplateId), bosses(0).Id, _dialogue.BossTemplateId)
        End If

        _illustrationChoices.Add(New ChoiceItem("", "无立绘"))
        For Each image In ResourceChoices.ImageResourceChoices()
            _illustrationChoices.Add(New ChoiceItem(image.Value, image.Display))
        Next
        PlayerIllustrationBox.ItemsSource = _illustrationChoices
        EnemyIllustrationBox.ItemsSource = _illustrationChoices
        IllustrationBox.ItemsSource = _illustrationChoices
        PlayerIllustrationBox.SelectedValue = If(String.IsNullOrWhiteSpace(_dialogue.PlayerIllustrationId), "", _dialogue.PlayerIllustrationId)
        EnemyIllustrationBox.SelectedValue = If(String.IsNullOrWhiteSpace(_dialogue.EnemyIllustrationId), "", _dialogue.EnemyIllustrationId)

        For Each line In _dialogue.Lines
            _lineItems.Add(New LineItem(line, _lineItems.Count))
        Next
        If _lineItems.Count = 0 Then
            _lineItems.Add(New LineItem(New DialogueLine(), 0))
            _dialogue.Lines.Add(_lineItems(0).Line)
        End If
        RefreshLines()
        LinesList.SelectedIndex = 0
        UpdateAfterControls()
    End Sub

    Private Sub RefreshLines()
        _loading = True
        Dim selected = LinesList.SelectedIndex
        LinesList.ItemsSource = Nothing
        LinesList.ItemsSource = _lineItems
        If selected >= 0 AndAlso selected < _lineItems.Count Then LinesList.SelectedIndex = selected
        _loading = False
    End Sub

    Private Sub LoadLine(line As DialogueLine)
        _loading = True
        _current = line
        If line Is Nothing Then
            ContentBox.Text = ""
            IllustrationBox.SelectedValue = ""
            CdBox.Text = "15"
            AutoBox.IsChecked = False
        Else
            SpeakerBox.SelectedValue = If(line.Speaker = DialogueSpeaker.Enemy, "1", "0")
            ContentBox.Text = line.Content.Replace("<br/>", vbCrLf).Replace("<br>", vbCrLf)
            IllustrationBox.SelectedValue = If(line.IllustrationId, "")
            CdBox.Text = Math.Max(0, line.CD).ToString()
            AutoBox.IsChecked = line.Auto
        End If
        _loading = False
    End Sub

    Private Sub SaveCurrent()
        If _current Is Nothing Then Return
        _current.Speaker = If(String.Equals(SpeakerBox.SelectedValue?.ToString(), "1"), DialogueSpeaker.Enemy, DialogueSpeaker.Player)
        _current.Content = ContentBox.Text
        Dim value As Integer
        Dim illustration = TryCast(IllustrationBox.SelectedItem, ChoiceItem)
        _current.IllustrationId = If(illustration Is Nothing, "", illustration.Id)
        If Not String.IsNullOrWhiteSpace(_current.IllustrationId) Then _current.Illustration = 0
        _current.CD = If(Integer.TryParse(CdBox.Text, value), Math.Max(0, value), 15)
        _current.Auto = AutoBox.IsChecked = True
    End Sub

    Private Sub LinesList_SelectionChanged(sender As Object, e As SelectionChangedEventArgs)
        If _loading Then Return
        SaveCurrent()
        If LinesList.SelectedItem Is Nothing Then Return
        LoadLine(DirectCast(LinesList.SelectedItem, LineItem).Line)
    End Sub

    Private Sub SpeakerBox_SelectionChanged(sender As Object, e As SelectionChangedEventArgs)
        If _loading Then Return
        SaveCurrent()
        LinesList.Items.Refresh()
    End Sub

    Private Sub Field_TextChanged(sender As Object, e As TextChangedEventArgs)
        If _loading Then Return
        SaveCurrent()
        LinesList.Items.Refresh()
    End Sub

    Private Sub Field_Changed(sender As Object, e As RoutedEventArgs)
        If _loading Then Return
        SaveCurrent()
        LinesList.Items.Refresh()
    End Sub

    Private Sub AddLine_Click(sender As Object, e As RoutedEventArgs)
        SaveCurrent()
        Dim line As New DialogueLine()
        _dialogue.Lines.Add(line)
        _lineItems.Add(New LineItem(line, _lineItems.Count))
        RefreshLines()
        LinesList.SelectedIndex = _lineItems.Count - 1
        ContentBox.Focus()
    End Sub

    Private Sub RemoveLine_Click(sender As Object, e As RoutedEventArgs)
        If LinesList.SelectedItem Is Nothing Then Return
        Dim item = DirectCast(LinesList.SelectedItem, LineItem)
        Dim index = _lineItems.IndexOf(item)
        _lineItems.RemoveAt(index)
        _dialogue.Lines.Remove(item.Line)
        RefreshLines()
        If _lineItems.Count > 0 Then LinesList.SelectedIndex = Math.Min(index, _lineItems.Count - 1) Else _current = Nothing
    End Sub

    Private Sub UpLine_Click(sender As Object, e As RoutedEventArgs)
        If LinesList.SelectedItem Is Nothing Then Return
        Dim item = DirectCast(LinesList.SelectedItem, LineItem)
        Dim index = _lineItems.IndexOf(item)
        If index <= 0 Then Return
        _lineItems.RemoveAt(index)
        _lineItems.Insert(index - 1, item)
        _dialogue.Lines.Remove(item.Line)
        _dialogue.Lines.Insert(index - 1, item.Line)
        RefreshLines()
        LinesList.SelectedIndex = index - 1
    End Sub

    Private Sub DownLine_Click(sender As Object, e As RoutedEventArgs)
        If LinesList.SelectedItem Is Nothing Then Return
        Dim item = DirectCast(LinesList.SelectedItem, LineItem)
        Dim index = _lineItems.IndexOf(item)
        If index < 0 OrElse index >= _lineItems.Count - 1 Then Return
        _lineItems.RemoveAt(index)
        _lineItems.Insert(index + 1, item)
        _dialogue.Lines.Remove(item.Line)
        _dialogue.Lines.Insert(index + 1, item.Line)
        RefreshLines()
        LinesList.SelectedIndex = index + 1
    End Sub

    Private Sub AfterBox_SelectionChanged(sender As Object, e As SelectionChangedEventArgs)
        If _loading Then Return
        Dim value As DialogueAfterAction
        If Not [Enum].TryParse(AfterBox.SelectedValue?.ToString(), value) Then value = DialogueAfterAction.None
        _dialogue.AfterAction = value
        UpdateAfterControls()
    End Sub

    Private Sub UpdateAfterControls()
        BossBox.IsEnabled = _dialogue.AfterAction = DialogueAfterAction.StartBoss
    End Sub

    Private Sub ImportCsv_Click(sender As Object, e As RoutedEventArgs)
        Dim dialog As New Microsoft.Win32.OpenFileDialog With {
            .Title = "导入 AgSTG CSV 对话",
            .Filter = "CSV 对话 (*.csv)|*.csv|所有文件 (*.*)|*.*"
        }
        If dialog.ShowDialog() <> True Then Return
        Try
            Dim definition = DialogueCsv.Parse(File.ReadAllText(dialog.FileName), _dialogue.Name)
            _dialogue.Lines = definition.Lines
            _lineItems.Clear()
            For Each line In _dialogue.Lines
                _lineItems.Add(New LineItem(line, _lineItems.Count))
            Next
            If _lineItems.Count = 0 Then _lineItems.Add(New LineItem(New DialogueLine(), 0))
            _dialogue.Lines.Clear()
            For Each item In _lineItems
                _dialogue.Lines.Add(item.Line)
            Next
            RefreshLines()
            LinesList.SelectedIndex = 0
            MessageBox.Show(Me, "已导入 " & _dialogue.Lines.Count & " 句对话。", "导入 CSV", MessageBoxButton.OK, MessageBoxImage.Information)
        Catch ex As Exception
            MessageBox.Show(Me, "导入失败：" & ex.Message, "导入 CSV", MessageBoxButton.OK, MessageBoxImage.Error)
        End Try
    End Sub

    Private Sub ExportCsv_Click(sender As Object, e As RoutedEventArgs)
        SaveCurrent()
        Dim dialog As New Microsoft.Win32.SaveFileDialog With {
            .Title = "导出 AgSTG CSV 对话",
            .Filter = "CSV 对话 (*.csv)|*.csv",
            .FileName = SafeFileName(NameBox.Text) & ".csv"
        }
        If dialog.ShowDialog() <> True Then Return
        Try
            File.WriteAllText(dialog.FileName, DialogueCsv.ToCsv(_dialogue), New Text.UTF8Encoding(False))
        Catch ex As Exception
            MessageBox.Show(Me, "导出失败：" & ex.Message, "导出 CSV", MessageBoxButton.OK, MessageBoxImage.Error)
        End Try
    End Sub

    Private Function SafeFileName(name As String) As String
        Dim value = If(String.IsNullOrWhiteSpace(name), "dialog", name.Trim())
        For Each c In Path.GetInvalidFileNameChars()
            value = value.Replace(c, "_"c)
        Next
        Return value
    End Function

    Private Sub Ok_Click(sender As Object, e As RoutedEventArgs)
        SaveCurrent()
        _dialogue.Name = If(String.IsNullOrWhiteSpace(NameBox.Text), "对话", NameBox.Text.Trim())
        Dim selectedBoss = TryCast(BossBox.SelectedItem, ChoiceItem)
        _dialogue.BossTemplateId = If(selectedBoss Is Nothing, "", selectedBoss.Id)
        Dim playerIllustration = TryCast(PlayerIllustrationBox.SelectedItem, ChoiceItem)
        Dim enemyIllustration = TryCast(EnemyIllustrationBox.SelectedItem, ChoiceItem)
        _dialogue.PlayerIllustrationId = If(playerIllustration Is Nothing, "", playerIllustration.Id)
        _dialogue.EnemyIllustrationId = If(enemyIllustration Is Nothing, "", enemyIllustration.Id)
        _dialogue.Lines.Clear()
        For Each item In _lineItems
            _dialogue.Lines.Add(item.Line)
        Next
        _dialogue.Normalize()
        ResultDialogue = ProjectJson.Clone(_dialogue)
        DialogResult = True
    End Sub

    Private Sub Cancel_Click(sender As Object, e As RoutedEventArgs)
        DialogResult = False
    End Sub

    Private Class ChoiceItem
        Public ReadOnly Property Id As String
        Public ReadOnly Property Display As String
        Public Sub New(id As String, display As String)
            Me.Id = id
            Me.Display = display
        End Sub
        Public Overrides Function ToString() As String
            Return Display
        End Function
    End Class

    Private Class LineItem
        Public ReadOnly Property Line As DialogueLine
        Public Sub New(line As DialogueLine, index As Integer)
            Me.Line = line
        End Sub
        Public ReadOnly Property Display As String
            Get
                Dim who = If(Line.Speaker = DialogueSpeaker.Enemy, "敌", "我")
                Dim text = Line.Content?.Replace(vbCrLf, " ").Replace(vbCr, " ").Replace(vbLf, " ")
                If text Is Nothing Then text = ""
                If text.Length > 42 Then text = text.Substring(0, 42) & "…"
                Return who & " · " & text
            End Get
        End Property
    End Class
End Class
