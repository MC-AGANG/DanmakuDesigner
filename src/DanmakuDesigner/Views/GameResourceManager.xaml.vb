Imports System.IO
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Input
Imports System.Windows.Media
Imports System.Windows.Media.Imaging
Imports DanmakuDesigner.Core

Public Class GameResourceManager

    Private _loaded As Boolean
    Private _selected As GameResource

    Public Sub New()
        InitializeComponent()
        AddHandler Loaded, Sub()
            _loaded = True
            Refresh()
        End Sub
    End Sub

    Public Sub Refresh()
        If Not _loaded Then Return
        ListHost.Children.Clear()
        Dim game = App.Session.Project
        If game Is Nothing Then
            _selected = Nothing
            UpdatePreview()
            Return
        End If

        If game.Resources.Count = 0 Then
            ListHost.Children.Add(New TextBlock With {
                .Text = "还没有游戏资源。" & vbCrLf & "点击右上角导入图片或音频。",
                .FontSize = 11,
                .Foreground = Ui.Brush("#7A7A7A"),
                .TextWrapping = TextWrapping.Wrap,
                .Margin = New Thickness(14, 16, 14, 8)
            })
        Else
            For Each resource In game.Resources
                ListHost.Children.Add(MakeResourceRow(resource))
            Next
        End If

        If _selected IsNot Nothing AndAlso Not game.Resources.Contains(_selected) Then _selected = Nothing
        If _selected Is Nothing AndAlso game.Resources.Count > 0 Then _selected = game.Resources(0)
        UpdatePreview()
        UpdateButtons()
    End Sub

    Private Function MakeResourceRow(resource As GameResource) As Border
        Dim selected = resource Is _selected
        Dim border As New Border With {
            .Height = 58,
            .Margin = New Thickness(6, 2, 6, 2),
            .Padding = New Thickness(8, 6, 8, 6),
            .CornerRadius = New CornerRadius(2),
            .Background = If(selected, Ui.Brush("#264F78"), Brushes.Transparent),
            .BorderBrush = If(selected, Ui.Brush("#007ACC"), Brushes.Transparent),
            .BorderThickness = New Thickness(1),
            .Cursor = Cursors.Hand
        }
        Dim grid As New Grid()
        grid.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(40)})
        grid.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})
        grid.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})

        Dim icon As New Image With {
            .Source = IconLibrary.Get(If(resource.Kind = GameResourceKind.Audio, "AudioPlayback_16x.png", "Media_16x.png")),
            .Width = 22,
            .Height = 22,
            .VerticalAlignment = VerticalAlignment.Center
        }
        grid.Children.Add(icon)

        Dim texts As New StackPanel With {.VerticalAlignment = VerticalAlignment.Center}
        texts.Children.Add(New TextBlock With {
            .Text = resource.Name,
            .FontSize = 11.5,
            .Foreground = Ui.Brush("#E0E0E0"),
            .TextTrimming = TextTrimming.CharacterEllipsis
        })
        texts.Children.Add(New TextBlock With {
            .Text = KindText(resource.Kind) & " · " & resource.RelativePath,
            .FontSize = 9.5,
            .Foreground = Ui.Brush("#8A8A8A"),
            .TextTrimming = TextTrimming.CharacterEllipsis,
            .Margin = New Thickness(0, 2, 0, 0)
        })
        Grid.SetColumn(texts, 1)
        grid.Children.Add(texts)

        Dim kindBadge = Ui.Badge(KindText(resource.Kind), "#3A3A3D", "#B8B8B8")
        Grid.SetColumn(kindBadge, 2)
        kindBadge.VerticalAlignment = VerticalAlignment.Center
        grid.Children.Add(kindBadge)

        border.Child = grid
        AddHandler border.MouseLeftButtonDown, Sub(sender As Object, e As MouseButtonEventArgs)
                                                  _selected = resource
                                                  Refresh()
                                                  e.Handled = True
                                              End Sub
        AddHandler border.MouseRightButtonDown, Sub(sender As Object, e As MouseButtonEventArgs)
                                                   _selected = resource
                                                   Refresh()
                                                   Dim menu As New ContextMenu()
                                                   Dim rename As New MenuItem With {.Header = "重命名…"}
                                                   AddHandler rename.Click, Sub() RenameResource(resource)
                                                   menu.Items.Add(rename)
                                                   Dim open As New MenuItem With {.Header = "打开文件"}
                                                   AddHandler open.Click, Sub() OpenResource(resource)
                                                   menu.Items.Add(open)
                                                   menu.Items.Add(New Separator())
                                                   Dim remove As New MenuItem With {.Header = "移除资源"}
                                                   AddHandler remove.Click, Sub() RemoveResource(resource)
                                                   menu.Items.Add(remove)
                                                   menu.PlacementTarget = border
                                                   menu.IsOpen = True
                                                   e.Handled = True
                                               End Sub
        Return border
    End Function

    Private Shared Function KindText(kind As GameResourceKind) As String
        Select Case kind
            Case GameResourceKind.Image : Return "图片"
            Case GameResourceKind.Audio : Return "音频"
            Case Else : Return "其他"
        End Select
    End Function

    Private Sub UpdatePreview()
        UpdateButtons()
        PreviewImage.Source = Nothing
        If _selected Is Nothing Then
            PreviewPlaceholder.Visibility = Visibility.Visible
            PreviewPlaceholder.Text = "选择左侧资源查看预览"
            ResourceNameText.Text = "未选择资源"
            ResourceKindText.Text = ""
            ResourcePathText.Text = ""
            Return
        End If

        ResourceNameText.Text = _selected.Name
        ResourceKindText.Text = KindText(_selected.Kind) & " · " & _selected.RelativePath
        ResourcePathText.Text = "工程相对路径：" & _selected.RelativePath

        Dim fullPath = App.Session.ResourceFullPath(_selected)
        If _selected.Kind <> GameResourceKind.Image Then
            PreviewPlaceholder.Visibility = Visibility.Visible
            PreviewPlaceholder.Text = If(_selected.Kind = GameResourceKind.Audio, "音频资源" & vbCrLf & "导出到 GameResources 资源包", "该资源没有可预览的图像")
            Return
        End If
        If String.IsNullOrWhiteSpace(fullPath) OrElse Not File.Exists(fullPath) Then
            PreviewPlaceholder.Visibility = Visibility.Visible
            PreviewPlaceholder.Text = "资源文件不存在：" & vbCrLf & fullPath
            Return
        End If
        Try
            Dim image As New BitmapImage()
            image.BeginInit()
            image.CacheOption = BitmapCacheOption.OnLoad
            image.UriSource = New Uri(fullPath, UriKind.Absolute)
            image.EndInit()
            image.Freeze()
            PreviewImage.Source = image
            PreviewPlaceholder.Visibility = Visibility.Collapsed
        Catch ex As Exception
            PreviewPlaceholder.Visibility = Visibility.Visible
            PreviewPlaceholder.Text = "无法预览图片：" & ex.Message
        End Try
    End Sub

    Private Sub UpdateButtons()
        Dim has = _selected IsNot Nothing
        If RemoveButton IsNot Nothing Then RemoveButton.IsEnabled = has
        If RenameButton IsNot Nothing Then RenameButton.IsEnabled = has
        If OpenResourceButton IsNot Nothing Then OpenResourceButton.IsEnabled = has
        If OpenFolderButton IsNot Nothing Then OpenFolderButton.IsEnabled = App.Session.Project IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(App.Session.ProjectDirectory)
    End Sub

    Private Sub ImportImage_Click(sender As Object, e As RoutedEventArgs)
        ImportResources("图片资源 (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif|所有文件 (*.*)|*.*")
    End Sub

    Private Sub ImportAudio_Click(sender As Object, e As RoutedEventArgs)
        ImportResources("音频资源 (*.wav;*.mp3;*.ogg;*.flac)|*.wav;*.mp3;*.ogg;*.flac|所有文件 (*.*)|*.*")
    End Sub

    Private Sub ImportResources(filter As String)
        If App.Session.Project Is Nothing Then Return
        If String.IsNullOrWhiteSpace(App.Session.ProjectDirectory) Then
            App.Session.SaveProject()
            If String.IsNullOrWhiteSpace(App.Session.ProjectDirectory) Then Return
        End If
        Dim dialog As New Microsoft.Win32.OpenFileDialog With {
            .Title = "导入游戏资源",
            .Filter = filter,
            .Multiselect = True
        }
        If dialog.ShowDialog() <> True Then Return
        Try
            Dim last As GameResource = Nothing
            For Each file In dialog.FileNames
                Dim resource = App.Session.AddResource(file)
                If resource IsNot Nothing Then last = resource
            Next
            _selected = last
            Refresh()
        Catch ex As Exception
            MessageBox.Show(Window.GetWindow(Me), "导入资源失败：" & ex.Message, "游戏资源", MessageBoxButton.OK, MessageBoxImage.Warning)
        End Try
    End Sub

    Private Sub Remove_Click(sender As Object, e As RoutedEventArgs)
        RemoveResource(_selected)
    End Sub

    Private Sub RemoveResource(resource As GameResource)
        If resource Is Nothing Then Return
        If MessageBox.Show(Window.GetWindow(Me), "从工程中移除资源【" & resource.Name & "】吗？" & vbCrLf &
                           "工程内的资源文件不会被删除。", "移除资源",
                           MessageBoxButton.YesNo, MessageBoxImage.Question) <> MessageBoxResult.Yes Then Return
        App.Session.RemoveResource(resource)
        _selected = Nothing
        Refresh()
    End Sub

    Private Sub Rename_Click(sender As Object, e As RoutedEventArgs)
        RenameResource(_selected)
    End Sub

    Private Sub RenameResource(resource As GameResource)
        If resource Is Nothing Then Return
        Dim value = InputDialog.ShowInput(Window.GetWindow(Me), "重命名资源", "资源名称：", resource.Name,
                                          Function(text) If(String.IsNullOrWhiteSpace(text), "名称不能为空。", Nothing))
        If value Is Nothing Then Return
        App.Session.BeginEdit()
        resource.Name = value
        App.Session.CommitEdit()
        Refresh()
    End Sub

    Private Sub OpenResource_Click(sender As Object, e As RoutedEventArgs)
        OpenResource(_selected)
    End Sub

    Private Sub OpenResource(resource As GameResource)
        If resource Is Nothing Then Return
        Dim path = App.Session.ResourceFullPath(resource)
        If File.Exists(path) Then
            Try
                Process.Start(New ProcessStartInfo(path) With {.UseShellExecute = True})
            Catch
            End Try
        End If
    End Sub

    Private Sub OpenFolder_Click(sender As Object, e As RoutedEventArgs)
        Dim dir = App.Session.ProjectDirectory
        If String.IsNullOrWhiteSpace(dir) Then Return
        dir = Path.Combine(dir, "Resources")
        Directory.CreateDirectory(dir)
        Try
            Process.Start(New ProcessStartInfo("explorer.exe", dir) With {.UseShellExecute = True})
        Catch
        End Try
    End Sub

End Class
