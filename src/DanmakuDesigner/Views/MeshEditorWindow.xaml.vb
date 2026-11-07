Imports System.Globalization
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Input
Imports System.Windows.Media
Imports System.Windows.Media.Media3D
Imports DanmakuDesigner.Core

Public Class MeshEditorWindow
    Private ReadOnly _layer As BackgroundLayer
    Private _selectedId As String = ""
    Private _updating As Boolean
    Private _camera As PerspectiveCamera
    Private _cameraPosition As Point3D
    Private _cameraLook As Vector3D
    Private _orbitTarget As New Point3D(0, 0, 0)
    Private _orbitDistance As Double = 7
    Private _orbitYaw As Double
    Private _orbitPitch As Double
    Private _leftDragging As Boolean
    Private _rightDragging As Boolean
    Private _lastMouse As Point

    Public Sub New(layer As BackgroundLayer)
        InitializeComponent()
        _layer = layer
        If _layer IsNot Nothing Then
            _layer.Normalize()
            LayerTitleText.Text = "· " & _layer.Name
            _cameraPosition = New Point3D(_layer.CameraX, _layer.CameraY, _layer.CameraZ)
            _cameraLook = New Vector3D(_layer.LookX, _layer.LookY, _layer.LookZ)
            If _cameraLook.LengthSquared < 0.0001 Then _cameraLook = New Vector3D(0, 0, 1)
            _cameraLook.Normalize()
            _orbitYaw = Math.Atan2(_cameraLook.X, _cameraLook.Z)
            _orbitPitch = Math.Asin(Math.Max(-1.0, Math.Min(1.0, _cameraLook.Y)))
            _orbitDistance = Math.Max(0.8, (_cameraPosition - _orbitTarget).Length)
        End If
        AddHandler Loaded, Sub() Refresh()
    End Sub

    Private Sub Refresh()
        If _updating Then Return
        If _layer Is Nothing Then Return
        If _layer.Meshes.Count > 0 AndAlso _layer.FindMesh(_selectedId) Is Nothing Then
            _selectedId = _layer.Meshes(0).Id
        End If
        BuildMeshList()
        BuildInspector()
        BuildPreview()
    End Sub

    Private ReadOnly Property SelectedMesh As BackgroundMesh
        Get
            If _layer Is Nothing Then Return Nothing
            Dim mesh = _layer.FindMesh(_selectedId)
            If mesh IsNot Nothing Then Return mesh
            mesh = _layer.Meshes.FirstOrDefault()
            If mesh IsNot Nothing Then _selectedId = mesh.Id
            Return mesh
        End Get
    End Property

    Private Sub BuildMeshList()
        MeshHost.Children.Clear()
        For Each mesh In _layer.Meshes.AsEnumerable().Reverse()
            If mesh Is Nothing Then Continue For
            Dim selected = String.Equals(mesh.Id, _selectedId, StringComparison.Ordinal)
            Dim border As New Border With {
                .Height = 44,
                .Background = Ui.Brush(If(selected, "#102B40", "#252526")),
                .BorderBrush = Ui.Brush(If(selected, "#0179CB", "#303033")),
                .BorderThickness = New Thickness(0, 0, 0, 1),
                .Cursor = Cursors.Hand,
                .Padding = New Thickness(10, 0, 8, 0)
            }
            Dim panel As New StackPanel With {.VerticalAlignment = VerticalAlignment.Center}
            panel.Children.Add(New TextBlock With {
                .Text = mesh.Name,
                .FontSize = 12,
                .Foreground = Ui.Brush("#E6E6E6"),
                .TextTrimming = TextTrimming.CharacterEllipsis
            })
            panel.Children.Add(New TextBlock With {
                .Text = MeshName(mesh.Kind),
                .FontSize = 10,
                .Foreground = Ui.Brush("#8A8A8A"),
                .Margin = New Thickness(0, 2, 0, 0)
            })
            border.Child = panel
            Dim captured = mesh
            AddHandler border.MouseLeftButtonDown, Sub(sender As Object, e As MouseButtonEventArgs)
                                                       _selectedId = captured.Id
                                                       Refresh()
                                                   End Sub
            Dim menu As New ContextMenu()
            Dim duplicate As New MenuItem With {.Header = "复制几何体"}
            AddHandler duplicate.Click, Sub() DuplicateMesh(captured)
            menu.Items.Add(duplicate)
            menu.Items.Add(New Separator())
            Dim meshIndex = _layer.Meshes.IndexOf(captured)
            Dim moveUp As New MenuItem With {.Header = "上移一层（更靠前）", .IsEnabled = meshIndex < _layer.Meshes.Count - 1}
            AddHandler moveUp.Click, Sub() MoveMesh(captured, 1)
            menu.Items.Add(moveUp)
            Dim moveDown As New MenuItem With {.Header = "下移一层（更靠后）", .IsEnabled = meshIndex > 0}
            AddHandler moveDown.Click, Sub() MoveMesh(captured, -1)
            menu.Items.Add(moveDown)
            menu.Items.Add(New Separator())
            Dim deleteItem As New MenuItem With {.Header = "删除几何体"}
            AddHandler deleteItem.Click, Sub()
                                             _selectedId = captured.Id
                                             DeleteSelectedMesh()
                                         End Sub
            menu.Items.Add(deleteItem)
            AddHandler border.MouseRightButtonDown, Sub(sender As Object, e As MouseButtonEventArgs)
                                                        _selectedId = captured.Id
                                                    End Sub
            border.ContextMenu = menu
            MeshHost.Children.Add(border)
        Next
        If _layer.Meshes.Count = 0 Then
            MeshHost.Children.Add(New TextBlock With {
                .Text = "还没有几何体。" & vbCrLf & "使用上方按钮添加。",
                .FontSize = 11,
                .Foreground = Ui.Brush("#777777"),
                .TextWrapping = TextWrapping.Wrap,
                .Margin = New Thickness(12, 14, 12, 0),
                .LineHeight = 19
            })
        End If
    End Sub

    Private Sub BuildPreview()
        ViewportHost.Child = Nothing
        If _layer Is Nothing Then Return
        Dim view = BackgroundRenderer.BuildViewport(_layer, _selectedId)
        _camera = TryCast(view.Camera, PerspectiveCamera)
        ViewportHost.Child = view
        ApplyCamera()
        StatusText.Text = _layer.Meshes.Count & " 个几何体 · 左键拖动旋转 · 右键拖动平移 · 滚轮缩放"
    End Sub

    Private Sub ApplyCamera()
        If _camera Is Nothing Then Return
        _camera.Position = _cameraPosition
        _camera.LookDirection = _cameraLook
        _camera.FieldOfView = Math.Max(1, Math.Min(170, _layer.FieldOfView))
    End Sub

    Private Sub BuildInspector()
        InspectorHost.Children.Clear()
        Dim mesh = SelectedMesh
        If mesh Is Nothing Then
            InspectorHost.Children.Add(Ui.Label("没有选中的几何体", 11, "#8A8A8A"))
            Return
        End If
        _updating = True
        Try
            InspectorHost.Children.Add(TextRow("名称", mesh.Name, Sub(value)
                                                                      If String.IsNullOrWhiteSpace(value) Then Return
                                                                      Apply(Sub() mesh.Name = value.Trim())
                                                                  End Sub))
            InspectorHost.Children.Add(ReadOnlyRow("类型", MeshName(mesh.Kind)))
            Dim meshOrder = _layer.Meshes.IndexOf(mesh)
            Dim orderPanel As New WrapPanel With {.Margin = New Thickness(0, 2, 0, 8)}
            Dim upButton As New Button With {
                .Content = "▲ 上移一层",
                .Style = TryCast(Application.Current.FindResource("VS.Button"), Style),
                .Padding = New Thickness(8, 3, 8, 3),
                .Margin = New Thickness(0, 0, 6, 0),
                .ToolTip = "在几何体列表中向前移动，用于调整透明遮挡顺序",
                .IsEnabled = meshOrder < _layer.Meshes.Count - 1
            }
            AddHandler upButton.Click, Sub() MoveMesh(mesh, 1)
            Dim downButton As New Button With {
                .Content = "▼ 下移一层",
                .Style = TryCast(Application.Current.FindResource("VS.Button"), Style),
                .Padding = New Thickness(8, 3, 8, 3),
                .ToolTip = "在几何体列表中向后移动，用于调整透明遮挡顺序",
                .IsEnabled = meshOrder > 0
            }
            AddHandler downButton.Click, Sub() MoveMesh(mesh, -1)
            orderPanel.Children.Add(upButton)
            orderPanel.Children.Add(downButton)
            InspectorHost.Children.Add(orderPanel)
            InspectorHost.Children.Add(Ui.Label("列表越靠上，几何体越靠前；透明贴图按这个顺序参与绘制。", 10, "#8A8A8A"))
            InspectorHost.Children.Add(ColorRow("颜色", mesh.Color, Sub(value) Apply(Sub() mesh.Color = value)))
            InspectorHost.Children.Add(NumberRow("不透明度", mesh.Opacity, Sub(value) Apply(Sub() mesh.Opacity = Math.Max(0, Math.Min(1, value)))))

            InspectorHost.Children.Add(Ui.SectionTitle("位置"))
            InspectorHost.Children.Add(NumberRow("X", mesh.X, Sub(value) Apply(Sub() mesh.X = value)))
            InspectorHost.Children.Add(NumberRow("Y", mesh.Y, Sub(value) Apply(Sub() mesh.Y = value)))
            InspectorHost.Children.Add(NumberRow("Z", mesh.Z, Sub(value) Apply(Sub() mesh.Z = value)))

            InspectorHost.Children.Add(Ui.SectionTitle("旋转 / 缩放"))
            InspectorHost.Children.Add(NumberRow("旋转 X°", mesh.RotationX, Sub(value) Apply(Sub() mesh.RotationX = value)))
            InspectorHost.Children.Add(NumberRow("旋转 Y°", mesh.RotationY, Sub(value) Apply(Sub() mesh.RotationY = value)))
            InspectorHost.Children.Add(NumberRow("旋转 Z°", mesh.RotationZ, Sub(value) Apply(Sub() mesh.RotationZ = value)))
            InspectorHost.Children.Add(NumberRow("缩放 X", mesh.ScaleX, Sub(value) Apply(Sub() mesh.ScaleX = If(Math.Abs(value) < 0.001, 0.001, value))))
            InspectorHost.Children.Add(NumberRow("缩放 Y", mesh.ScaleY, Sub(value) Apply(Sub() mesh.ScaleY = If(Math.Abs(value) < 0.001, 0.001, value))))
            InspectorHost.Children.Add(NumberRow("缩放 Z", mesh.ScaleZ, Sub(value) Apply(Sub() mesh.ScaleZ = If(Math.Abs(value) < 0.001, 0.001, value))))

            InspectorHost.Children.Add(Ui.SectionTitle("尺寸"))
            Select Case mesh.Kind
                Case BackgroundMeshKind.Cylinder
                    InspectorHost.Children.Add(NumberRow("半径", mesh.Radius, Sub(value) Apply(Sub() mesh.Radius = Math.Max(0.01, value))))
                    InspectorHost.Children.Add(NumberRow("高度", mesh.Height, Sub(value) Apply(Sub() mesh.Height = Math.Max(0.01, value))))
                    InspectorHost.Children.Add(NumberRow("分段", mesh.Segments, Sub(value) Apply(Sub() mesh.Segments = Math.Max(3, Math.Min(64, CInt(value))))))
                Case BackgroundMeshKind.Sphere
                    InspectorHost.Children.Add(NumberRow("半径", mesh.Radius, Sub(value) Apply(Sub() mesh.Radius = Math.Max(0.01, value))))
                    InspectorHost.Children.Add(NumberRow("分段", mesh.Segments, Sub(value) Apply(Sub() mesh.Segments = Math.Max(3, Math.Min(64, CInt(value))))))
                Case BackgroundMeshKind.Rectangle, BackgroundMeshKind.Triangle
                    InspectorHost.Children.Add(NumberRow("宽度", mesh.SizeX, Sub(value) Apply(Sub() mesh.SizeX = Math.Max(0.01, value))))
                    InspectorHost.Children.Add(NumberRow("高度", mesh.SizeY, Sub(value) Apply(Sub() mesh.SizeY = Math.Max(0.01, value))))
                    Dim imageChoices = ResourceChoices.ImageFillChoices()
                    If imageChoices.Count > 1 Then
                        InspectorHost.Children.Add(ComboRow("填充图片", imageChoices, mesh.ImageResourceId,
                                                            Sub(value) Apply(Sub() mesh.ImageResourceId = value)))
                    Else
                        InspectorHost.Children.Add(ReadOnlyRow("填充图片", "先在游戏资源中导入图片"))
                    End If
                Case Else
                    InspectorHost.Children.Add(NumberRow("尺寸 X", mesh.SizeX, Sub(value) Apply(Sub() mesh.SizeX = Math.Max(0.01, value))))
                    InspectorHost.Children.Add(NumberRow("尺寸 Y", mesh.SizeY, Sub(value) Apply(Sub() mesh.SizeY = Math.Max(0.01, value))))
                    InspectorHost.Children.Add(NumberRow("尺寸 Z", mesh.SizeZ, Sub(value) Apply(Sub() mesh.SizeZ = Math.Max(0.01, value))))
            End Select
        Finally
            _updating = False
        End Try
    End Sub

    Private Sub AddMesh(kind As BackgroundMeshKind)
        If _layer Is Nothing Then Return
        Dim mesh As New BackgroundMesh With {
            .Kind = kind,
            .Name = MeshName(kind) & " " & (_layer.Meshes.Count + 1).ToString(),
            .Color = "#FF5AA9E6"
        }
        Select Case kind
            Case BackgroundMeshKind.Box
                mesh.SizeX = 2 : mesh.SizeY = 2 : mesh.SizeZ = 2
                mesh.RotationX = 18 : mesh.RotationY = 28
            Case BackgroundMeshKind.Cylinder
                mesh.Radius = 1 : mesh.Height = 2
            Case BackgroundMeshKind.Sphere
                mesh.Radius = 1
            Case BackgroundMeshKind.Rectangle, BackgroundMeshKind.Triangle
                mesh.SizeX = 2 : mesh.SizeY = 2
        End Select
        App.Session.BeginEdit()
        _layer.Meshes.Add(mesh)
        App.Session.CommitEdit()
        _selectedId = mesh.Id
        Refresh()
    End Sub

    Private Sub AddBox_Click(sender As Object, e As RoutedEventArgs)
        AddMesh(BackgroundMeshKind.Box)
    End Sub

    Private Sub AddCylinder_Click(sender As Object, e As RoutedEventArgs)
        AddMesh(BackgroundMeshKind.Cylinder)
    End Sub

    Private Sub AddSphere_Click(sender As Object, e As RoutedEventArgs)
        AddMesh(BackgroundMeshKind.Sphere)
    End Sub

    Private Sub AddRectangle_Click(sender As Object, e As RoutedEventArgs)
        AddMesh(BackgroundMeshKind.Rectangle)
    End Sub

    Private Sub AddTriangle_Click(sender As Object, e As RoutedEventArgs)
        AddMesh(BackgroundMeshKind.Triangle)
    End Sub

    Private Sub DeleteMesh_Click(sender As Object, e As RoutedEventArgs)
        DeleteSelectedMesh()
    End Sub

    Private Sub DeleteSelectedMesh()
        Dim mesh = SelectedMesh
        If mesh Is Nothing Then Return
        App.Session.BeginEdit()
        _layer.Meshes.Remove(mesh)
        App.Session.CommitEdit()
        _selectedId = ""
        Refresh()
    End Sub

    Private Sub DuplicateMesh(mesh As BackgroundMesh)
        If mesh Is Nothing Then Return
        Dim copy = ProjectJson.Clone(mesh)
        copy.Id = Ids.NewId("mesh")
        copy.Name = mesh.Name & " 副本"
        App.Session.BeginEdit()
        Dim index = _layer.Meshes.IndexOf(mesh)
        If index >= 0 Then
            _layer.Meshes.Insert(index + 1, copy)
        Else
            _layer.Meshes.Add(copy)
        End If
        App.Session.CommitEdit()
        _selectedId = copy.Id
        Refresh()
    End Sub

    Private Sub MoveMesh(mesh As BackgroundMesh, delta As Integer)
        If mesh Is Nothing Then Return
        Dim index = _layer.Meshes.IndexOf(mesh)
        Dim target = index + delta
        If index < 0 OrElse target < 0 OrElse target >= _layer.Meshes.Count Then Return
        App.Session.BeginEdit()
        _layer.Meshes.RemoveAt(index)
        _layer.Meshes.Insert(target, mesh)
        App.Session.CommitEdit()
        _selectedId = mesh.Id
        Refresh()
    End Sub

    Private Sub Apply(action As Action)
        If _updating Then Return
        App.Session.BeginEdit()
        action.Invoke()
        App.Session.CommitEdit()
        Refresh()
    End Sub

    Private Sub Viewport_MouseLeftButtonDown(sender As Object, e As MouseButtonEventArgs)
        _leftDragging = True
        _lastMouse = e.GetPosition(ViewportHost)
        ViewportHost.CaptureMouse()
        e.Handled = True
    End Sub

    Private Sub Viewport_MouseRightButtonDown(sender As Object, e As MouseButtonEventArgs)
        _rightDragging = True
        _lastMouse = e.GetPosition(ViewportHost)
        ViewportHost.CaptureMouse()
        e.Handled = True
    End Sub

    Private Sub Viewport_MouseLeftButtonUp(sender As Object, e As MouseButtonEventArgs)
        _leftDragging = False
        If Not _rightDragging Then ViewportHost.ReleaseMouseCapture()
    End Sub

    Private Sub Viewport_MouseRightButtonUp(sender As Object, e As MouseButtonEventArgs)
        _rightDragging = False
        If Not _leftDragging Then ViewportHost.ReleaseMouseCapture()
    End Sub

    Private Sub Viewport_MouseMove(sender As Object, e As MouseEventArgs)
        If Not _leftDragging AndAlso Not _rightDragging Then Return
        Dim point = e.GetPosition(ViewportHost)
        Dim dx = point.X - _lastMouse.X
        Dim dy = point.Y - _lastMouse.Y
        _lastMouse = point
        If _leftDragging Then
            _orbitYaw -= dx * 0.007
            _orbitPitch = Math.Max(-1.45, Math.Min(1.45, _orbitPitch - dy * 0.007))
            Dim offset As New Vector3D(-Math.Sin(_orbitYaw) * Math.Cos(_orbitPitch), -Math.Sin(_orbitPitch), -Math.Cos(_orbitYaw) * Math.Cos(_orbitPitch))
            _cameraPosition = _orbitTarget + offset * _orbitDistance
            _cameraLook = _orbitTarget - _cameraPosition
        ElseIf _rightDragging Then
            Dim forward = _cameraLook
            If forward.LengthSquared < 0.0001 Then forward = New Vector3D(0, 0, 1)
            forward.Normalize()
            Dim right = Vector3D.CrossProduct(New Vector3D(0, 1, 0), forward)
            If right.LengthSquared < 0.0001 Then right = New Vector3D(1, 0, 0)
            right.Normalize()
            Dim up = Vector3D.CrossProduct(right, forward)
            Dim scale = Math.Max(0.001, _orbitDistance * 0.0018)
            Dim delta = right * (dx * scale) + up * (-dy * scale)
            _orbitTarget += delta
            _cameraPosition += delta
        End If
        ApplyCamera()
        e.Handled = True
    End Sub

    Private Sub Viewport_MouseWheel(sender As Object, e As MouseWheelEventArgs)
        If _camera Is Nothing Then Return
        _orbitDistance = Math.Max(0.8, Math.Min(60.0, _orbitDistance * Math.Exp(-e.Delta * 0.001)))
        Dim direction = _cameraPosition - _orbitTarget
        If direction.LengthSquared < 0.0001 Then direction = New Vector3D(0, 0, 1)
        direction.Normalize()
        _cameraPosition = _orbitTarget + direction * _orbitDistance
        _cameraLook = _orbitTarget - _cameraPosition
        ApplyCamera()
        e.Handled = True
    End Sub

    Private Sub Close_Click(sender As Object, e As RoutedEventArgs)
        Close()
    End Sub

    Private Shared Function MeshName(kind As BackgroundMeshKind) As String
        Select Case kind
            Case BackgroundMeshKind.Cylinder : Return "圆柱体"
            Case BackgroundMeshKind.Sphere : Return "球体"
            Case BackgroundMeshKind.Rectangle : Return "矩形面"
            Case BackgroundMeshKind.Triangle : Return "三角形面"
            Case Else : Return "长方体"
        End Select
    End Function

    Private Function TextRow(label As String, value As String, apply As Action(Of String)) As Grid
        Dim box = Ui.MakeTextBox(value)
        AddHandler box.LostKeyboardFocus, Sub()
                                             If _updating OrElse box.Text = value Then Return
                                             apply.Invoke(box.Text)
                                         End Sub
        AddHandler box.KeyDown, Sub(sender As Object, e As KeyEventArgs)
                                    If e.Key = Key.Enter Then
                                        If _updating OrElse box.Text = value Then Return
                                        apply.Invoke(box.Text)
                                        e.Handled = True
                                    End If
                                End Sub
        Return Ui.PropertyRow(label, box)
    End Function

    Private Function NumberRow(label As String, value As Double, apply As Action(Of Double)) As Grid
        Dim text = value.ToString("0.####", CultureInfo.InvariantCulture)
        Dim box = Ui.MakeTextBox(text)
        AddHandler box.LostKeyboardFocus, Sub()
                                             Dim parsed As Double
                                             If _updating OrElse Not Double.TryParse(box.Text, NumberStyles.Float, CultureInfo.InvariantCulture, parsed) Then Return
                                             apply.Invoke(parsed)
                                         End Sub
        AddHandler box.KeyDown, Sub(sender As Object, e As KeyEventArgs)
                                    If e.Key = Key.Enter Then
                                        Dim parsed As Double
                                        If _updating OrElse Not Double.TryParse(box.Text, NumberStyles.Float, CultureInfo.InvariantCulture, parsed) Then Return
                                        apply.Invoke(parsed)
                                        e.Handled = True
                                    End If
                                End Sub
        Return Ui.PropertyRow(label, box)
    End Function

    Private Function ComboRow(label As String, options As IEnumerable(Of ChoiceInfo), selected As String, apply As Action(Of String)) As Grid
        Dim combo = Ui.MakeCombo(options, selected)
        AddHandler combo.SelectionChanged, Sub()
                                               If _updating Then Return
                                               apply.Invoke(Ui.ComboValue(combo))
                                           End Sub
        Return Ui.PropertyRow(label, combo)
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
                                             If _updating OrElse box.Text = value Then Return
                                             swatch.Background = Ui.Brush(box.Text)
                                             apply.Invoke(box.Text)
                                         End Sub
        AddHandler box.KeyDown, Sub(sender As Object, e As KeyEventArgs)
                                    If e.Key = Key.Enter Then
                                        If _updating OrElse box.Text = value Then Return
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
