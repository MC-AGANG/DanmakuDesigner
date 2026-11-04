Imports System.Linq
Imports System.Windows
Imports System.Windows.Controls
Imports DanmakuDesigner.Core

''' <summary>为 Boss 的普通/移动贴图选择一个有序的游戏图片资源列表。</summary>
Public Class BossTextureDialog

    Private ReadOnly _all As New List(Of TextureItem)()
    Private ReadOnly _selected As New List(Of TextureItem)()

    Public Property ResultIds As New List(Of String)

    Public Sub New(title As String, selectedIds As IEnumerable(Of String))
        InitializeComponent()
        Me.Title = title
        HeaderText.Text = title
        Dim requested = If(selectedIds, Enumerable.Empty(Of String)()).ToList()
        Dim selectedSet As New HashSet(Of String)(requested, StringComparer.Ordinal)
        If App.Session.Project IsNot Nothing Then
            For Each resource In App.Session.Project.Resources
                If resource Is Nothing OrElse resource.Kind <> GameResourceKind.Image Then Continue For
                Dim item As New TextureItem With {
                    .Id = resource.Id,
                    .Display = resource.Name & If(String.IsNullOrWhiteSpace(resource.RelativePath), "", "  ·  " & resource.RelativePath)
                }
                _all.Add(item)
                If selectedSet.Contains(resource.Id) Then _selected.Add(item)
            Next
        End If
        _selected.Sort(Function(a, b) requested.IndexOf(a.Id).CompareTo(requested.IndexOf(b.Id)))
        RefreshLists()
    End Sub

    Private Sub RefreshLists()
        Dim selectedIds = _selected.Select(Function(x) x.Id).ToList()
        AvailableList.ItemsSource = _all.Where(Function(x) Not selectedIds.Contains(x.Id)).ToList()
        SelectedList.ItemsSource = _selected.ToList()
    End Sub

    Private Sub Add_Click(sender As Object, e As RoutedEventArgs)
        For Each item In AvailableList.SelectedItems.Cast(Of TextureItem)().ToList()
            _selected.Add(item)
        Next
        RefreshLists()
    End Sub

    Private Sub Remove_Click(sender As Object, e As RoutedEventArgs)
        For Each item In SelectedList.SelectedItems.Cast(Of TextureItem)().ToList()
            _selected.Remove(item)
        Next
        RefreshLists()
    End Sub

    Private Sub Up_Click(sender As Object, e As RoutedEventArgs)
        If SelectedList.SelectedItem Is Nothing Then Return
        Dim item = DirectCast(SelectedList.SelectedItem, TextureItem)
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
        Dim item = DirectCast(SelectedList.SelectedItem, TextureItem)
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

    Private Class TextureItem
        Public Property Id As String = ""
        Public Property Display As String = ""
    End Class
End Class
