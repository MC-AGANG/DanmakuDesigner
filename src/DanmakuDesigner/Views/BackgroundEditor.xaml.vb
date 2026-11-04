Imports System.Globalization
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Input
Imports System.Windows.Media
Imports DanmakuDesigner.Core

Public Class BackgroundEditor
    Private _selectedId As String = ""
    Private _updating As Boolean
    Private _meshEditor As MeshEditorWindow
    Private _previewTick As Long

    Public Sub Refresh()
        If Not _updating Then
            BuildLayerList()
            BuildInspector()
            SyncPreviewTick()
            BuildPreview()
        End If
    End Sub

    Public Sub SelectLayer(layerId As String)
        If String.IsNullOrWhiteSpace(layerId) Then Return
        _selectedId = layerId
        Refresh()
    End Sub

    Public Sub SetPreviewTick(tick As Long)
        If Stage Is Nothing Then Return
        _previewTick = Math.Max(0, Math.Min(Stage.LengthTicks, tick))
        SyncPreviewTick()
        BuildPreview()
    End Sub

    Private Sub SyncPreviewTick()
        If TickSlider Is Nothing OrElse Stage Is Nothing Then Return
        _previewTick = Math.Max(0, Math.Min(Stage.LengthTicks, _previewTick))
        _updating = True
        Try
            TickSlider.Minimum = 0
            TickSlider.Maximum = Math.Max(1, Stage.LengthTicks)
            TickSlider.Value = _previewTick
            TickText.Text = _previewTick.ToString() & "f"
        Finally
            _updating = False
        End Try
    End Sub

    Private Sub TickSlider_ValueChanged(sender As Object, e As RoutedPropertyChangedEventArgs(Of Double))
        If _updating OrElse TickSlider Is Nothing OrElse Not IsLoaded OrElse Stage Is Nothing Then Return
        _previewTick = CInt(Math.Round(TickSlider.Value))
        If TickText IsNot Nothing Then TickText.Text = _previewTick.ToString() & "f"
        BuildPreview()
    End Sub

    Private ReadOnly Property Stage As StageDocument
        Get
            Return App.Session.Stage
        End Get
    End Property

    Private ReadOnly Property SelectedLayer As BackgroundLayer
        Get
            If Stage Is Nothing Then Return Nothing
            Dim layer = Stage.FindBackgroundLayer(_selectedId)
            If layer IsNot Nothing Then Return layer
            layer = Stage.BackgroundLayers.FirstOrDefault()
            If layer IsNot Nothing Then _selectedId = layer.Id
            Return layer
        End Get
    End Property

    Private Sub BuildLayerList()
        LayerHost.Children.Clear()
        If Stage Is Nothing Then
            LayerCountText.Text = "0"
            MeshButton.IsEnabled = False
            Return
        End If
        If Stage.BackgroundLayers.Count > 0 AndAlso Stage.FindBackgroundLayer(_selectedId) Is Nothing Then
            _selectedId = Stage.BackgroundLayers(0).Id
        End If
        LayerCountText.Text = Stage.BackgroundLayers.Count.ToString()
        If SummaryText IsNot Nothing Then
            Dim count2d = Stage.BackgroundLayers.Where(Function(l) l.Kind = BackgroundLayerKind.Layer2D).Count()
            Dim count3d = Stage.BackgroundLayers.Where(Function(l) l.Kind = BackgroundLayerKind.Layer3D).Count()
            SummaryText.Text = "2D " & count2d & " · 3D " & count3d & " · 导出为 XAML"
        End If
        For Each layer In Stage.BackgroundLayers
            If layer Is Nothing Then Continue For
            LayerHost.Children.Add(MakeLayerRow(layer))
        Next
        If Stage.BackgroundLayers.Count = 0 Then
            Dim empty As New TextBlock With {
                .Text = "还没有背景图层。" & vbCrLf & "点击上方「2D 图层」或「3D 图层」开始。",
                .FontSize = 11,
                .Foreground = Ui.Brush("#777777"),
                .TextWrapping = TextWrapping.Wrap,
                .Margin = New Thickness(12, 14, 12, 0),
                .LineHeight = 19
            }
            LayerHost.Children.Add(empty)
        End If
        MeshButton.IsEnabled = SelectedLayer IsNot Nothing AndAlso SelectedLayer.Kind = BackgroundLayerKind.Layer3D
    End Sub

    Private Function MakeLayerRow(layer As BackgroundLayer) As Border
        Dim selected = String.Equals(layer.Id, _selectedId, StringComparison.Ordinal)
        Dim border As New Border With {
            .Height = 48,
            .Background = Ui.Brush(If(selected, "#102B40", "#252526")),
            .BorderBrush = Ui.Brush(If(selected, "#0179CB", "#303033")),
            .BorderThickness = New Thickness(0, 0, 0, 1),
            .Padding = New Thickness(0),
            .Cursor = Cursors.Hand,
            .ToolTip = If(layer.Kind = BackgroundLayerKind.Layer3D, "3D 背景图层；双击打开建模工具", "2D 背景图层")
        }
        Dim grid As New Grid()
        grid.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(3)})
        grid.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(30)})
        grid.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})
        grid.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})

        grid.Children.Add(New Border With {
            .Background = Ui.Brush(If(layer.Kind = BackgroundLayerKind.Layer3D, "#CE9178", "#4FC1FF")),
            .CornerRadius = New CornerRadius(0, 1, 1, 0)
        })

        Dim image = Ui.Icon(If(layer.Kind = BackgroundLayerKind.Layer3D, "Media_16x.png", "Property_16x.png"), 16)
        If image IsNot Nothing Then
            image.Margin = New Thickness(6, 0, 0, 0)
            image.VerticalAlignment = VerticalAlignment.Center
            Grid.SetColumn(image, 1)
            grid.Children.Add(image)
        End If

        Dim textPanel As New StackPanel With {.VerticalAlignment = VerticalAlignment.Center, .Margin = New Thickness(0, 0, 4, 0)}
        textPanel.Children.Add(New TextBlock With {
            .Text = layer.Name,
            .FontSize = 12,
            .Foreground = Ui.Brush("#E6E6E6"),
            .TextTrimming = TextTrimming.CharacterEllipsis
        })
        Dim subtitle = If(layer.Kind = BackgroundLayerKind.Layer3D,
                          "3D · " & layer.Meshes.Count & " 个几何体",
                          "2D · " & ShapeName(layer.Shape))
        textPanel.Children.Add(New TextBlock With {
            .Text = subtitle,
            .FontSize = 10,
            .Foreground = Ui.Brush("#8A8A8A"),
            .TextTrimming = TextTrimming.CharacterEllipsis,
            .Margin = New Thickness(0, 2, 0, 0)
        })
        Grid.SetColumn(textPanel, 2)
        grid.Children.Add(textPanel)

        Dim eye As New TextBlock With {
            .Text = If(layer.Visible, "◉", "○"),
            .FontSize = 15,
            .Foreground = Ui.Brush(If(layer.Visible, "#C8C8C8", "#666666")),
            .VerticalAlignment = VerticalAlignment.Center,
            .Margin = New Thickness(4, 0, 9, 0),
            .ToolTip = If(layer.Visible, "隐藏图层", "显示图层"),
            .Cursor = Cursors.Hand
        }
        AddHandler eye.MouseLeftButtonDown, Sub(sender As Object, e As MouseButtonEventArgs)
                                                 e.Handled = True
                                                 ToggleVisible(layer)
                                             End Sub
        Grid.SetColumn(eye, 3)
        grid.Children.Add(eye)

        border.Child = grid
        AddHandler border.MouseLeftButtonDown, Sub(sender As Object, e As MouseButtonEventArgs)
                                                  _selectedId = layer.Id
                                                  If e.ClickCount = 2 AndAlso layer.Kind = BackgroundLayerKind.Layer3D Then
                                                      OpenMeshEditor(layer)
                                                  Else
                                                      Refresh()
                                                  End If
                                              End Sub
        Return border
    End Function

    Private Sub BuildPreview()
        PreviewHost.Children.Clear()
        If Stage Is Nothing Then
            PreviewHint.Text = "没有打开的关卡。"
            Return
        End If
        PreviewHost.Children.Add(BackgroundRenderer.BuildPreview(Stage, _previewTick))
        Dim layer = SelectedLayer
        If Stage.BackgroundLayers.Count = 0 Then
            PreviewHint.Text = "使用上方按钮添加 2D / 3D 背景图层；导出时会生成 XAML。"
        ElseIf layer Is Nothing Then
            PreviewHint.Text = "选择一个图层查看属性。"
        Else
            PreviewHint.Text = "当前图层：" & layer.Name & " · " & If(layer.Kind = BackgroundLayerKind.Layer3D, "3D", "2D")
        End If
    End Sub

    Private Sub BuildInspector()
        InspectorHost.Children.Clear()
        Dim layer = SelectedLayer
        If layer Is Nothing Then
            InspectorHost.Children.Add(Ui.Label("没有选中的背景图层", 11, "#8A8A8A"))
            Return
        End If
        _updating = True
        Try
            BuildInspectorCore(layer)
        Finally
            _updating = False
        End Try
    End Sub

    Private Sub BuildInspectorCore(layer As BackgroundLayer)
        Dim header As New Border With {
            .Background = Ui.Brush("#2A2A2B"),
            .CornerRadius = New CornerRadius(3),
            .Padding = New Thickness(8, 6, 8, 6),
            .Margin = New Thickness(0, 0, 0, 9)
        }
        header.Child = New TextBlock With {
            .Text = layer.Name,
            .FontSize = 12,
            .FontWeight = FontWeights.SemiBold,
            .Foreground = Ui.Brush("#E6E6E6"),
            .TextTrimming = TextTrimming.CharacterEllipsis
        }
        InspectorHost.Children.Add(header)
        InspectorHost.Children.Add(TextRow("图层名称", layer.Name, Sub(value)
                                                                         If String.IsNullOrWhiteSpace(value) Then Return
                                                                         Apply(Sub() layer.Name = value.Trim())
                                                                     End Sub))
        InspectorHost.Children.Add(ReadOnlyRow("类型", If(layer.Kind = BackgroundLayerKind.Layer3D, "3D 图层", "2D 图层")))
        InspectorHost.Children.Add(CheckRow("可见", layer.Visible, Sub(value) Apply(Sub() layer.Visible = value)))
        InspectorHost.Children.Add(NumberRow("不透明度", layer.Opacity, Sub(value) Apply(Sub() layer.Opacity = Math.Max(0, Math.Min(1, value))), "0 ~ 1"))

        If layer.Kind = BackgroundLayerKind.Layer2D Then
            Build2DInspector(layer)
        Else
            Build3DInspector(layer)
        End If
    End Sub

    Private Sub Build2DInspector(layer As BackgroundLayer)
        InspectorHost.Children.Add(Ui.SectionTitle("2D 图层"))
        Dim shapes = New List(Of ChoiceInfo) From {
            New ChoiceInfo(BackgroundShape2D.Rectangle.ToString(), "矩形面"),
            New ChoiceInfo(BackgroundShape2D.Ellipse.ToString(), "椭圆面"),
            New ChoiceInfo(BackgroundShape2D.ParticleField.ToString(), "粒子场")
        }
        InspectorHost.Children.Add(ComboRow("形状", shapes, layer.Shape.ToString(), Sub(value)
                                                                                         Dim shape As BackgroundShape2D
                                                                                         If [Enum].TryParse(value, shape) Then Apply(Sub() layer.Shape = shape)
                                                                                     End Sub))
        If layer.Shape = BackgroundShape2D.ParticleField Then
            InspectorHost.Children.Add(NumberRow("粒子数量", layer.ParticleCount, Sub(value) Apply(Sub() layer.ParticleCount = Math.Max(0, Math.Min(5000, CInt(value))))))
            InspectorHost.Children.Add(ColorRow("粒子颜色", layer.ParticleColor, Sub(value) Apply(Sub() layer.ParticleColor = value)))
            InspectorHost.Children.Add(NumberRow("最小尺寸", layer.ParticleMinSize, Sub(value) Apply(Sub() layer.ParticleMinSize = Math.Max(0.2, value))))
            InspectorHost.Children.Add(NumberRow("最大尺寸", layer.ParticleMaxSize, Sub(value) Apply(Sub() layer.ParticleMaxSize = Math.Max(0.2, value))))
            InspectorHost.Children.Add(NumberRow("速度 X", layer.ParticleSpeedX, Sub(value) Apply(Sub() layer.ParticleSpeedX = value)))
            InspectorHost.Children.Add(NumberRow("速度 Y", layer.ParticleSpeedY, Sub(value) Apply(Sub() layer.ParticleSpeedY = value)))
            InspectorHost.Children.Add(NumberRow("散布", layer.ParticleSpread, Sub(value) Apply(Sub() layer.ParticleSpread = Math.Max(0, value))))
        Else
            InspectorHost.Children.Add(NumberRow("X", layer.X, Sub(value) Apply(Sub() layer.X = value)))
            InspectorHost.Children.Add(NumberRow("Y", layer.Y, Sub(value) Apply(Sub() layer.Y = value)))
            InspectorHost.Children.Add(NumberRow("宽度", layer.Width, Sub(value) Apply(Sub() layer.Width = Math.Max(1, value))))
            InspectorHost.Children.Add(NumberRow("高度", layer.Height, Sub(value) Apply(Sub() layer.Height = Math.Max(1, value))))
            Dim imageChoices = ResourceChoices.ImageFillChoices()
            If imageChoices.Count > 1 Then
                InspectorHost.Children.Add(ComboRow("填充图片", imageChoices, layer.ImageResourceId, Sub(value) Apply(Sub() layer.ImageResourceId = value)))
            Else
                InspectorHost.Children.Add(ReadOnlyRow("填充图片", "先在游戏资源中导入图片"))
            End If
            InspectorHost.Children.Add(ColorRow("填充颜色（备用）", layer.Fill, Sub(value) Apply(Sub() layer.Fill = value)))
            InspectorHost.Children.Add(ColorRow("描边", layer.Stroke, Sub(value) Apply(Sub() layer.Stroke = value)))
            InspectorHost.Children.Add(NumberRow("描边宽度", layer.StrokeThickness, Sub(value) Apply(Sub() layer.StrokeThickness = Math.Max(0, value))))
            InspectorHost.Children.Add(NumberRow("圆角", layer.CornerRadius, Sub(value) Apply(Sub() layer.CornerRadius = Math.Max(0, value))))
        End If
    End Sub

    Private Sub Build3DInspector(layer As BackgroundLayer)
        InspectorHost.Children.Add(Ui.SectionTitle("3D 图层"))
        Dim openButton As New Button With {
            .Content = "打开 3D 建模工具…",
            .Style = TryCast(Application.Current.FindResource("VS.AccentButton"), Style),
            .Margin = New Thickness(0, 0, 0, 8),
            .Padding = New Thickness(10, 4, 10, 4)
        }
        AddHandler openButton.Click, Sub() OpenMeshEditor(layer)
        InspectorHost.Children.Add(openButton)
        InspectorHost.Children.Add(ReadOnlyRow("几何体", layer.Meshes.Count.ToString() & " 个"))
        If layer.Meshes.Count > 0 Then
            For Each mesh In layer.Meshes
                If mesh Is Nothing Then Continue For
                Dim text As New TextBlock With {
                    .Text = "· " & mesh.Name & "  (" & MeshName(mesh.Kind) & ")",
                    .FontSize = 10.5,
                    .Foreground = Ui.Brush("#B0B0B0"),
                    .Margin = New Thickness(2, 1, 0, 2),
                    .TextTrimming = TextTrimming.CharacterEllipsis
                }
                InspectorHost.Children.Add(text)
            Next
        Else
            InspectorHost.Children.Add(Ui.Label("还没有几何体，打开建模工具添加。", 10.5, "#8A8A8A"))
        End If

        InspectorHost.Children.Add(Ui.SectionTitle("摄像机"))
        InspectorHost.Children.Add(NumberRow("位置 X", layer.CameraX, Sub(value) Apply(Sub() layer.CameraX = value)))
        InspectorHost.Children.Add(NumberRow("位置 Y", layer.CameraY, Sub(value) Apply(Sub() layer.CameraY = value)))
        InspectorHost.Children.Add(NumberRow("位置 Z", layer.CameraZ, Sub(value) Apply(Sub() layer.CameraZ = value)))
        InspectorHost.Children.Add(NumberRow("朝向 X", layer.LookX, Sub(value) Apply(Sub() layer.LookX = value)))
        InspectorHost.Children.Add(NumberRow("朝向 Y", layer.LookY, Sub(value) Apply(Sub() layer.LookY = value)))
        InspectorHost.Children.Add(NumberRow("朝向 Z", layer.LookZ, Sub(value) Apply(Sub() layer.LookZ = value)))
        InspectorHost.Children.Add(NumberRow("视野 FOV", layer.FieldOfView, Sub(value) Apply(Sub() layer.FieldOfView = Math.Max(1, Math.Min(170, value)))))

        InspectorHost.Children.Add(Ui.SectionTitle("光照"))
        InspectorHost.Children.Add(ColorRow("环境光", layer.AmbientColor, Sub(value) Apply(Sub() layer.AmbientColor = value)))
        InspectorHost.Children.Add(ColorRow("方向光", layer.LightColor, Sub(value) Apply(Sub() layer.LightColor = value)))
        InspectorHost.Children.Add(NumberRow("光源 X", layer.LightX, Sub(value) Apply(Sub() layer.LightX = value)))
        InspectorHost.Children.Add(NumberRow("光源 Y", layer.LightY, Sub(value) Apply(Sub() layer.LightY = value)))
        InspectorHost.Children.Add(NumberRow("光源 Z", layer.LightZ, Sub(value) Apply(Sub() layer.LightZ = value)))
    End Sub

    Private Sub ToggleVisible(layer As BackgroundLayer)
        Apply(Sub() layer.Visible = Not layer.Visible)
    End Sub

    Private Sub Add2D_Click(sender As Object, e As RoutedEventArgs)
        If Stage Is Nothing Then Return
        Dim layer As New BackgroundLayer With {
            .Name = "2D 图层 " & (Stage.BackgroundLayers.Count + 1).ToString(),
            .Kind = BackgroundLayerKind.Layer2D,
            .Shape = BackgroundShape2D.Rectangle,
            .Width = 260,
            .Height = 150,
            .X = 62,
            .Y = 150,
            .Fill = "#552A6F9E"
        }
        AddLayer(layer)
    End Sub

    Private Sub Add3D_Click(sender As Object, e As RoutedEventArgs)
        If Stage Is Nothing Then Return
        Dim layer As New BackgroundLayer With {
            .Name = "3D 图层 " & (Stage.BackgroundLayers.Count + 1).ToString(),
            .Kind = BackgroundLayerKind.Layer3D,
            .CameraZ = -7,
            .Meshes = New List(Of BackgroundMesh)
        }
        layer.Meshes.Add(New BackgroundMesh With {
            .Name = "长方体 1",
            .Kind = BackgroundMeshKind.Box,
            .SizeX = 2,
            .SizeY = 2,
            .SizeZ = 2,
            .RotationX = 18,
            .RotationY = 28
        })
        AddLayer(layer)
    End Sub

    Private Sub AddLayer(layer As BackgroundLayer)
        App.Session.BeginEdit()
        Stage.BackgroundLayers.Add(layer)
        App.Session.CommitEdit()
        _selectedId = layer.Id
        Refresh()
    End Sub

    Private Sub DeleteLayer_Click(sender As Object, e As RoutedEventArgs)
        Dim layer = SelectedLayer
        If layer Is Nothing Then Return
        If MessageBox.Show(Window.GetWindow(Me), "确定删除背景图层【" & layer.Name & "】吗？" & vbCrLf &
                           "引用它的背景动画轨道也会被删除。", "删除背景图层", MessageBoxButton.YesNo, MessageBoxImage.Warning) <> MessageBoxResult.Yes Then Return
        If _meshEditor IsNot Nothing Then _meshEditor.Close()
        App.Session.BeginEdit()
        Stage.BackgroundLayers.Remove(layer)
        For Each track In Stage.Tracks.Where(Function(t) t.Kind = TrackKind.Background AndAlso String.Equals(t.BackgroundLayerId, layer.Id, StringComparison.Ordinal)).ToList()
            Stage.Tracks.Remove(track)
        Next
        App.Session.CommitEdit()
        _selectedId = ""
        Refresh()
    End Sub

    Private Sub OpenMeshEditor_Click(sender As Object, e As RoutedEventArgs)
        Dim layer = SelectedLayer
        If layer Is Nothing OrElse layer.Kind <> BackgroundLayerKind.Layer3D Then
            MessageBox.Show(Window.GetWindow(Me), "请选择一个 3D 背景图层。", "3D 建模", MessageBoxButton.OK, MessageBoxImage.Information)
            Return
        End If
        OpenMeshEditor(layer)
    End Sub

    Private Sub OpenMeshEditor(layer As BackgroundLayer)
        If _meshEditor IsNot Nothing Then
            _meshEditor.Close()
            _meshEditor = Nothing
        End If
        _meshEditor = New MeshEditorWindow(layer)
        _meshEditor.Owner = Window.GetWindow(Me)
        AddHandler _meshEditor.Closed, Sub()
                                          _meshEditor = Nothing
                                          Refresh()
                                      End Sub
        _meshEditor.Show()
    End Sub

    Private Sub Apply(action As Action)
        If _updating Then Return
        _updating = True
        Try
            App.Session.BeginEdit()
            action.Invoke()
            App.Session.CommitEdit()
        Finally
            _updating = False
        End Try
        Refresh()
    End Sub

    Private Shared Function ShapeName(shape As BackgroundShape2D) As String
        Select Case shape
            Case BackgroundShape2D.Ellipse : Return "椭圆面"
            Case BackgroundShape2D.ParticleField : Return "粒子场"
            Case Else : Return "矩形面"
        End Select
    End Function

    Private Shared Function MeshName(kind As BackgroundMeshKind) As String
        Select Case kind
            Case BackgroundMeshKind.Cylinder : Return "圆柱体"
            Case BackgroundMeshKind.Sphere : Return "球体"
            Case BackgroundMeshKind.Rectangle : Return "矩形面"
            Case BackgroundMeshKind.Triangle : Return "三角形面"
            Case Else : Return "长方体"
        End Select
    End Function

    ' ============================================================
    ' 属性行
    ' ============================================================

    Private Function TextRow(label As String, value As String, apply As Action(Of String)) As Grid
        Dim box = Ui.MakeTextBox(value)
        box.Tag = If(value, "")
        AddHandler box.LostKeyboardFocus, Sub() CommitText(box, apply)
        AddHandler box.KeyDown, Sub(sender As Object, e As KeyEventArgs)
                                    If e.Key = Key.Enter Then
                                        CommitText(box, apply)
                                        e.Handled = True
                                    End If
                                End Sub
        Return Ui.PropertyRow(label, box)
    End Function

    Private Sub CommitText(box As TextBox, apply As Action(Of String))
        If _updating Then Return
        If box.Text = CStr(box.Tag) Then Return
        apply.Invoke(box.Text)
    End Sub

    Private Function NumberRow(label As String, value As Double, apply As Action(Of Double), Optional hint As String = "") As Grid
        Dim box = Ui.MakeTextBox(value.ToString("0.####", CultureInfo.InvariantCulture))
        box.Tag = box.Text
        AddHandler box.LostKeyboardFocus, Sub() CommitNumber(box, apply)
        AddHandler box.KeyDown, Sub(sender As Object, e As KeyEventArgs)
                                    If e.Key = Key.Enter Then
                                        CommitNumber(box, apply)
                                        e.Handled = True
                                    End If
                                End Sub
        Return Ui.PropertyRow(label, box, hint)
    End Function

    Private Sub CommitNumber(box As TextBox, apply As Action(Of Double))
        Dim value As Double
        If Not Double.TryParse(box.Text, NumberStyles.Float, CultureInfo.InvariantCulture, value) AndAlso
           Not Double.TryParse(box.Text, NumberStyles.Float, CultureInfo.CurrentCulture, value) Then
            box.Text = CStr(box.Tag)
            Return
        End If
        apply.Invoke(value)
    End Sub

    Private Function ComboRow(label As String, options As IEnumerable(Of ChoiceInfo), selected As String, apply As Action(Of String)) As Grid
        Dim combo = Ui.MakeCombo(options, selected)
        AddHandler combo.SelectionChanged, Sub()
                                               If _updating Then Return
                                               apply.Invoke(Ui.ComboValue(combo))
                                           End Sub
        Return Ui.PropertyRow(label, combo)
    End Function

    Private Function CheckRow(label As String, value As Boolean, apply As Action(Of Boolean)) As Grid
        Dim check As New CheckBox With {.IsChecked = value, .VerticalAlignment = VerticalAlignment.Center}
        AddHandler check.Click, Sub()
                                    If _updating Then Return
                                    apply.Invoke(If(check.IsChecked, True, False))
                                End Sub
        Return Ui.PropertyRow(label, check)
    End Function

    Private Function ColorRow(label As String, value As String, apply As Action(Of String)) As Grid
        Dim panel As New Grid()
        panel.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})
        panel.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(26)})
        Dim box = Ui.MakeTextBox(value)
        Dim swatch As New Border With {
            .Width = 22,
            .Height = 22,
            .CornerRadius = New CornerRadius(3),
            .Background = Ui.Brush(value),
            .BorderBrush = Ui.Brush("#555555"),
            .BorderThickness = New Thickness(1),
            .Margin = New Thickness(4, 0, 0, 0),
            .Cursor = Cursors.Hand,
            .ToolTip = "点击打开调色盘"
        }
        AddHandler swatch.MouseLeftButtonUp, Sub(sender As Object, e As MouseButtonEventArgs)
                                                     ColorPalette.Show(swatch, box.Text, Sub(color)
                                                                                             box.Text = color
                                                                                             swatch.Background = Ui.Brush(color)
                                                                                             apply.Invoke(color)
                                                                                         End Sub)
                                                     e.Handled = True
                                                 End Sub
        AddHandler box.LostKeyboardFocus, Sub()
                                             If _updating Then Return
                                             If box.Text = value Then Return
                                             swatch.Background = Ui.Brush(box.Text)
                                             apply.Invoke(box.Text)
                                         End Sub
        AddHandler box.KeyDown, Sub(sender As Object, e As KeyEventArgs)
                                    If e.Key = Key.Enter Then
                                        If _updating Then Return
                                        swatch.Background = Ui.Brush(box.Text)
                                        apply.Invoke(box.Text)
                                        e.Handled = True
                                    End If
                                End Sub
        Grid.SetColumn(box, 0)
        Grid.SetColumn(swatch, 1)
        panel.Children.Add(box)
        panel.Children.Add(swatch)
        Return Ui.PropertyRow(label, panel)
    End Function

    Private Function ReadOnlyRow(label As String, value As String) As Grid
        Return Ui.PropertyRow(label, New TextBlock With {
            .Text = value,
            .FontSize = 11,
            .Foreground = Ui.Brush("#B0B0B0"),
            .VerticalAlignment = VerticalAlignment.Center,
            .TextTrimming = TextTrimming.CharacterEllipsis
        })
    End Function
End Class
