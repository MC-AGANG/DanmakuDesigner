Imports System.Linq
Imports System.Windows
Imports System.Windows.Controls
Imports DanmakuDesigner.Core

''' <summary>为 Boss 选择符卡并调整战斗顺序。</summary>
Public Class BossSpellDialog

    Private ReadOnly _all As New List(Of SpellItem)()
    Private ReadOnly _selected As New List(Of SpellItem)()

    Public Property ResultIds As New List(Of String)

    Public Sub New(spells As IEnumerable(Of BossSpellCard), selectedIds As IEnumerable(Of String))
        InitializeComponent()
        Dim requested = If(selectedIds, Enumerable.Empty(Of String)()).ToList()
        Dim selectedSet As New HashSet(Of String)(requested, StringComparer.Ordinal)
        If spells IsNot Nothing Then
            For Each spell In spells
                If spell Is Nothing Then Continue For
                Dim item As New SpellItem With {
                    .Id = spell.Id,
                    .Display = spell.Name & "  ·  " & TypeName(spell.Type)
                }
                _all.Add(item)
                If selectedSet.Contains(spell.Id) Then _selected.Add(item)
            Next
        End If
        _selected.Sort(Function(a, b) requested.IndexOf(a.Id).CompareTo(requested.IndexOf(b.Id)))
        RefreshLists()
    End Sub

    Private Shared Function TypeName(kind As BossSpellType) As String
        Select Case kind
            Case BossSpellType.Life : Return "普通符卡"
            Case BossSpellType.Time : Return "时符"
            Case Else : Return "仅非符"
        End Select
    End Function

    Private Sub RefreshLists()
        Dim selectedIds = _selected.Select(Function(x) x.Id).ToList()
        AvailableList.ItemsSource = _all.Where(Function(x) Not selectedIds.Contains(x.Id)).ToList()
        SelectedList.ItemsSource = _selected.ToList()
    End Sub

    Private Sub Add_Click(sender As Object, e As RoutedEventArgs)
        For Each item In AvailableList.SelectedItems.Cast(Of SpellItem)().ToList()
            _selected.Add(item)
        Next
        RefreshLists()
    End Sub

    Private Sub Remove_Click(sender As Object, e As RoutedEventArgs)
        For Each item In SelectedList.SelectedItems.Cast(Of SpellItem)().ToList()
            _selected.Remove(item)
        Next
        RefreshLists()
    End Sub

    Private Sub Up_Click(sender As Object, e As RoutedEventArgs)
        If SelectedList.SelectedItem Is Nothing Then Return
        Dim item = DirectCast(SelectedList.SelectedItem, SpellItem)
        Dim index = _selected.IndexOf(item)
        If index > 0 Then
            _selected.RemoveAt(index)
            _selected.Insert(index - 1, item)
            RefreshLists()
            SelectedList.SelectedItem = item
        End If
    End Sub

    Private Sub Down_Click(sender As Object, e As RoutedEventArgs)
        If SelectedList.SelectedItem Is Nothing Then Return
        Dim item = DirectCast(SelectedList.SelectedItem, SpellItem)
        Dim index = _selected.IndexOf(item)
        If index >= 0 AndAlso index < _selected.Count - 1 Then
            _selected.RemoveAt(index)
            _selected.Insert(index + 1, item)
            RefreshLists()
            SelectedList.SelectedItem = item
        End If
    End Sub

    Private Sub Ok_Click(sender As Object, e As RoutedEventArgs)
        ResultIds = _selected.Select(Function(x) x.Id).ToList()
        DialogResult = True
    End Sub

    Private Sub Cancel_Click(sender As Object, e As RoutedEventArgs)
        DialogResult = False
    End Sub

    Private Class SpellItem
        Public Property Id As String = ""
        Public Property Display As String = ""
    End Class
End Class
