Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Controls.Primitives
Imports System.Windows.Input
Imports System.Windows.Media
Imports System.Windows.Threading
Imports DanmakuDesigner.Core

''' <summary>一块 Scratch 风格积木的可视化控件。</summary>
Public Class BlockVisual
    Inherits Border

    Public Property Node As BlockNode
    Public Property Definition As BlockDefinition
    Public Property Owner As BlockEditorView
    Public Property IsPalette As Boolean
    Public Property ParentList As List(Of BlockNode)
    Public Property ParentIndex As Integer

    Private ReadOnly _fields As New Dictionary(Of String, FrameworkElement)(StringComparer.OrdinalIgnoreCase)
    Private ReadOnly _fieldBoxes As New Dictionary(Of String, TextBox)(StringComparer.OrdinalIgnoreCase)
    Private _bodyPanel As StackPanel
    Private _elsePanel As StackPanel
    Private _bodyBorder As Border
    Private _elseBorder As Border
    Private _headerPanel As WrapPanel
    Private _dragStart As Point?
    Private _dragEnabled As Boolean = True
    Private _baseBorderBrush As Brush
    Private _paletteDragged As Boolean
    Private _comboOpen As Boolean

    Public Sub New(node As BlockNode, def As BlockDefinition, owner As BlockEditorView, isPalette As Boolean, parentList As List(Of BlockNode), parentIndex As Integer)
        Me.Node = node
        Me.Definition = def
        Me.Owner = owner
        Me.IsPalette = isPalette
        Me.ParentList = parentList
        Me.ParentIndex = parentIndex

        SnapsToDevicePixels = True
        UseLayoutRounding = True
        Focusable = True
        HorizontalAlignment = HorizontalAlignment.Left
        Margin = New Thickness(0, 0, 0, If(isPalette, 5, 0))
        Cursor = If(isPalette, Cursors.Hand, Cursors.Hand)
        AllowDrop = Not isPalette

        Build()
        If Not isPalette AndAlso owner IsNot Nothing Then owner.RegisterVisual(Me)
    End Sub

    Private Function CategoryFill() As Brush
        Return Ui.Brush(Definition.CategoryColor)
    End Function

    Private Function CategoryEdge() As Brush
        Return Ui.Brush(Darken(Definition.CategoryColor, 0.72))
    End Function

    Private Shared Function Darken(hex As String, factor As Double) As String
        Try
            Dim c = DirectCast(ColorConverter.ConvertFromString(hex), Color)
            Return String.Format("#{0:X2}{1:X2}{2:X2}", CByte(c.R * factor), CByte(c.G * factor), CByte(c.B * factor))
        Catch
            Return "#303030"
        End Try
    End Function

    Private Sub Build()
        Background = CategoryFill()
        BorderBrush = CategoryEdge()
        BorderThickness = New Thickness(1)
        _baseBorderBrush = BorderBrush
        Padding = New Thickness(9, 5, 9, 5)
        Select Case Definition.Kind
            Case BlockKind.Hat
                CornerRadius = New CornerRadius(12, 12, 3, 3)
            Case BlockKind.CBlock, BlockKind.CBlockElse
                CornerRadius = New CornerRadius(3)
                Padding = New Thickness(0)
            Case Else
                CornerRadius = New CornerRadius(3)
        End Select

        If Definition.Kind = BlockKind.CBlock OrElse Definition.Kind = BlockKind.CBlockElse Then
            BuildContainer()
        Else
            Dim content As New WrapPanel With {
                .Orientation = Orientation.Horizontal,
                .VerticalAlignment = VerticalAlignment.Center
            }
            BuildParts(content)
            Child = content
            _headerPanel = content
        End If

        If IsPalette Then
            _dragEnabled = True
            AddHandler MouseLeftButtonDown, Sub(sender As Object, e As MouseButtonEventArgs)
                                                _paletteDragged = False
                                                _dragStart = e.GetPosition(Me)
                                            End Sub
            AddHandler MouseMove, AddressOf Palette_MouseMove
            AddHandler MouseLeftButtonUp, Sub(sender As Object, e As MouseButtonEventArgs)
                                              If Not _paletteDragged Then Owner.PaletteClicked(Definition)
                                              _paletteDragged = False
                                          End Sub
            ToolTip = Definition.Tooltip
        Else
            AddHandler PreviewMouseLeftButtonDown, AddressOf Block_MouseDown
            AddHandler MouseMove, AddressOf Block_MouseMove
            AddHandler MouseRightButtonDown, AddressOf Block_RightClick
            AddHandler DragOver, AddressOf Block_DragOver
            AddHandler DragLeave, AddressOf Block_DragLeave
            AddHandler Drop, AddressOf Block_Drop
            AddHandler MouseEnter, Sub() If Owner.SelectedBlock IsNot Node Then BorderBrush = Ui.Brush("#FFFFFF")
            AddHandler MouseLeave, Sub() If Owner.SelectedBlock IsNot Node Then BorderBrush = _baseBorderBrush
        End If
    End Sub

    Private Sub BuildContainer()
        Dim root As New StackPanel()
        _headerPanel = New WrapPanel With {
            .Orientation = Orientation.Horizontal,
            .Margin = New Thickness(9, 5, 9, 4)
        }
        BuildParts(_headerPanel)
        root.Children.Add(_headerPanel)

        _bodyBorder = New Border With {
            .Background = Ui.Brush("#26000000"),
            .BorderBrush = Brushes.Transparent,
            .BorderThickness = New Thickness(1),
            .CornerRadius = New CornerRadius(2),
            .Margin = New Thickness(9, 2, 9, 4),
            .Padding = New Thickness(6, 4, 6, 4),
            .MinHeight = If(IsPalette, 14, 20),
            .AllowDrop = Not IsPalette
        }
        _bodyPanel = New StackPanel()
        _bodyBorder.Child = _bodyPanel
        root.Children.Add(_bodyBorder)

        If Not IsPalette Then
            AddHandler _bodyBorder.DragOver, Sub(sender As Object, e As DragEventArgs) Body_DragOver(e, _bodyBorder, Node.Children)
            AddHandler _bodyBorder.DragLeave, Sub()
                _bodyBorder.BorderBrush = Brushes.Transparent
                Owner.HideInsertLine()
            End Sub
            AddHandler _bodyBorder.Drop, Sub(sender As Object, e As DragEventArgs) Body_Drop(e, Node.Children)
        End If

        If Definition.Kind = BlockKind.CBlockElse Then
            Dim elseHeader As New TextBlock With {
                .Text = "否则",
                .FontSize = 11,
                .FontWeight = FontWeights.SemiBold,
                .Foreground = Ui.Brush("#0B1220"),
                .Margin = New Thickness(9, 1, 9, 2)
            }
            root.Children.Add(elseHeader)
            _elseBorder = New Border With {
                .Background = Ui.Brush("#26000000"),
                .BorderBrush = Brushes.Transparent,
                .BorderThickness = New Thickness(1),
                .CornerRadius = New CornerRadius(2),
                .Margin = New Thickness(9, 2, 9, 4),
                .Padding = New Thickness(6, 4, 6, 4),
                .MinHeight = If(IsPalette, 14, 20),
                .AllowDrop = Not IsPalette
            }
            _elsePanel = New StackPanel()
            _elseBorder.Child = _elsePanel
            root.Children.Add(_elseBorder)
            If Not IsPalette Then
                AddHandler _elseBorder.DragOver, Sub(sender As Object, e As DragEventArgs) Body_DragOver(e, _elseBorder, Node.ElseChildren)
                AddHandler _elseBorder.DragLeave, Sub()
                    _elseBorder.BorderBrush = Brushes.Transparent
                    Owner.HideInsertLine()
                End Sub
                AddHandler _elseBorder.Drop, Sub(sender As Object, e As DragEventArgs) Body_Drop(e, Node.ElseChildren)
            End If
        End If

        Dim footer As New TextBlock With {
            .Text = "结束",
            .FontSize = 10,
            .Foreground = Ui.Brush("#203040"),
            .Margin = New Thickness(9, 0, 9, 4),
            .Opacity = 0.7
        }
        root.Children.Add(footer)
        Child = root

        If Not IsPalette Then
            PopulateBody(_bodyPanel, Node.Children)
            If _elsePanel IsNot Nothing Then PopulateBody(_elsePanel, Node.ElseChildren)
        End If
    End Sub

    Private Sub PopulateBody(panel As StackPanel, list As List(Of BlockNode))
        panel.Children.Clear()
        For i = 0 To list.Count - 1
            Dim childDef = list(i).Definition
            If childDef Is Nothing Then Continue For
            Dim visual As New BlockVisual(list(i), childDef, Owner, False, list, i)
            panel.Children.Add(visual)
        Next
        If list.Count = 0 Then
            panel.Children.Add(New TextBlock With {
                .Text = "把积木拖到这里",
                .FontSize = 9.5,
                .Foreground = Ui.Brush("#60000000"),
                .Margin = New Thickness(3, 2, 3, 2),
                .IsHitTestVisible = False
            })
        End If
    End Sub

    Private Sub BuildParts(panel As WrapPanel)
        For Each part In Definition.Parts
            If part.IsField Then
                Dim field = Definition.FindField(part.FieldKey)
                If field Is Nothing Then Continue For
                Dim editor = MakeFieldEditor(field)
                editor.Margin = New Thickness(2, 0, 3, 0)
                _fields(part.FieldKey) = editor
                panel.Children.Add(editor)
            Else
                Dim label As New TextBlock With {
                    .Text = part.Text,
                    .FontSize = 11.5,
                    .Foreground = Ui.Brush("#0B1220"),
                    .FontWeight = FontWeights.SemiBold,
                    .VerticalAlignment = VerticalAlignment.Center
                }
                TextOptions.SetTextFormattingMode(label, TextFormattingMode.Display)
                panel.Children.Add(label)
            End If
        Next
        ApplyConditionalVisibility()
    End Sub

    Private Function MakeFieldEditor(field As FieldDef) As FrameworkElement
        If IsPalette Then
            Return MakePaletteField(field)
        End If
        Select Case field.Kind
            Case FieldKind.Choice
                Return MakeChoiceEditor(field)
            Case FieldKind.Text
                Return MakeTextEditor(field)
            Case Else
                Return MakeNumberEditor(field)
        End Select
    End Function

    Private Function MakePaletteField(field As FieldDef) As FrameworkElement
        Dim text As String
        If field.Kind = FieldKind.Choice Then
            Dim resources = If(App.Session.Project IsNot Nothing, App.Session.Project.Resources, Nothing)
            Dim options = field.Options(App.Session.Stage, resources)
            Dim selected = Node.GetField(field.Key)
            Dim match = options.FirstOrDefault(Function(o) String.Equals(o.Value, selected, StringComparison.OrdinalIgnoreCase))
            If match IsNot Nothing Then
                text = match.Display
            ElseIf field.OptionsSource = "patterns" Then
                text = "子弹样式"
            ElseIf field.OptionsSource = "audio" Then
                text = "音频资源"
            Else
                text = If(options.Count > 0, options(0).Display, field.Key)
            End If
        ElseIf field.Kind = FieldKind.Text Then
            text = If(Node.GetField(field.Key), field.DefaultValue)
        Else
            Dim raw = Node.GetField(field.Key)
            text = If(ValueExpression.IsReporter(raw), ValueExpression.Describe(raw), If(raw, field.DefaultValue))
        End If
        Return New Border With {
            .Background = Ui.Brush("#CCFFFFFF"),
            .CornerRadius = New CornerRadius(7),
            .Padding = New Thickness(5, 0, 5, 1),
            .Child = New TextBlock With {
                .Text = text,
                .FontSize = 10.5,
                .Foreground = Ui.Brush("#0B1220"),
                .VerticalAlignment = VerticalAlignment.Center
            }
        }
    End Function

    Private Function MakeChoiceEditor(field As FieldDef) As FrameworkElement
        Dim combo As New ComboBox With {
            .Height = 22,
            .Width = Math.Max(66, field.Width),
            .FontSize = 10.5,
            .Padding = New Thickness(3, 0, 0, 0),
            .Tag = field.Key,
            .ToolTip = field.Tooltip,
            .DisplayMemberPath = "Display",
            .SelectedValuePath = "Value"
        }
        Dim resources = If(App.Session.Project IsNot Nothing, App.Session.Project.Resources, Nothing)
        Dim options = field.Options(App.Session.Stage, resources)
        For Each opt In options
            combo.Items.Add(opt)
        Next
        combo.SelectedValue = Node.GetField(field.Key)
        Dim applySelection As Action = Sub()
                                           If IsPalette Then Return
                                           Dim selected = TryCast(combo.SelectedItem, FieldOption)
                                           If selected Is Nothing Then Return
                                           If selected.Value = Node.GetField(field.Key) Then Return
                                           ApplyFieldValue(field.Key, selected.Value, False)
                                           If field.Key = "subj" Then ApplyConditionalVisibility()
                                       End Sub
        AddHandler combo.SelectionChanged, Sub() applySelection.Invoke()
        AddHandler combo.DropDownOpened, Sub() _comboOpen = True
        AddHandler combo.DropDownClosed, Sub()
                                             _comboOpen = False
                                             ' 鼠标选择时 SelectedItem 可能稍后才更新，延迟到输入队列末尾再写回。
                                             combo.Dispatcher.BeginInvoke(New Action(Sub() applySelection.Invoke()), DispatcherPriority.Input)
                                         End Sub
        Return combo
    End Function

    Private Function MakeTextEditor(field As FieldDef) As FrameworkElement
        Dim box = MakeSmallTextBox(field, Node.GetField(field.Key), False)
        Return box
    End Function

    Private Function MakeNumberEditor(field As FieldDef) As FrameworkElement
        Dim raw = Node.GetField(field.Key)
        If ValueExpression.IsReporter(raw) Then
            Return MakeReporterPill(field, raw)
        End If
        Dim box = MakeSmallTextBox(field, raw, True)
        _fieldBoxes(field.Key) = box
        Return box
    End Function

    Private Function MakeSmallTextBox(field As FieldDef, value As String, numeric As Boolean) As TextBox
        Dim box As New TextBox With {
            .Style = TryCast(Application.Current.FindResource("BlockFieldTextBox"), Style),
            .Width = Math.Max(30, field.Width),
            .Text = If(value, ""),
            .Tag = If(value, ""),
            .ToolTip = If(String.IsNullOrWhiteSpace(field.Tooltip), "右键可插入取值器", field.Tooltip),
            .ContextMenu = BuildReporterMenu(field)
        }
        AddHandler box.LostKeyboardFocus, Sub() CommitBox(box, field, numeric)
        AddHandler box.KeyDown, Sub(sender As Object, e As KeyEventArgs)
                                    If e.Key = Key.Enter Then
                                        CommitBox(box, field, numeric)
                                        Owner.Focus()
                                        e.Handled = True
                                    ElseIf e.Key = Key.Escape Then
                                        box.Text = CStr(box.Tag)
                                        box.BorderBrush = Brushes.Transparent
                                        Owner.Focus()
                                        e.Handled = True
                                    End If
                                End Sub
        Return box
    End Function

    Private Function BuildReporterMenu(field As FieldDef) As ContextMenu
        Dim menu As New ContextMenu()
        Dim header As New MenuItem With {.Header = "插入取值器", .IsEnabled = False}
        menu.Items.Add(header)
        AddReporterItem(menu, field, "存在时间（帧）", "$ticks")
        If Owner.Scope = ProgramScope.Enemy Then AddReporterItem(menu, field, "血量百分比", "$hp")
        AddReporterItem(menu, field, "自机 X", "$px")
        AddReporterItem(menu, field, "自机 Y", "$py")
        AddReporterItem(menu, field, "与自机距离", "$dist")
        AddReporterItem(menu, field, "自身 X", "$x")
        AddReporterItem(menu, field, "自身 Y", "$y")
        AddReporterItem(menu, field, "场上子弹数", "$bullets")
        AddReporterItem(menu, field, "场上敌机数", "$enemies")
        menu.Items.Add(New Separator())
        Dim randomItem As New MenuItem With {.Header = "随机范围…"}
        AddHandler randomItem.Click, Sub()
                                          Dim value = InputDialog.ShowInput(Window.GetWindow(Me), "随机范围", "输入最小值与最大值，用逗号分隔：", "0, 10",
                                                                            Function(text)
                                                                                Dim parts = text.Split(","c)
                                                                                If parts.Length <> 2 Then Return "格式应为：最小值,最大值"
                                                                                Dim a, b As Double
                                                                                If Not Double.TryParse(parts(0).Trim(), Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, a) Then Return "最小值不是数字。"
                                                                                If Not Double.TryParse(parts(1).Trim(), Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, b) Then Return "最大值不是数字。"
                                                                                If b < a Then Return "最大值应大于最小值。"
                                                                                Return Nothing
                                                                            End Function)
                                          If value IsNot Nothing Then
                                              Dim parts = value.Split(","c)
                                              SetReporter(field, "$rand:" & parts(0).Trim() & "," & parts(1).Trim())
                                          End If
                                      End Sub
        menu.Items.Add(randomItem)
        Dim varItem As New MenuItem With {.Header = "变量…"}
        AddHandler varItem.Click, Sub()
                                        Dim value = InputDialog.ShowInput(Window.GetWindow(Me), "变量名", "输入变量名：", "n",
                                                                          Function(text) If(String.IsNullOrWhiteSpace(text), "变量名不能为空。", Nothing))
                                        If value IsNot Nothing Then SetReporter(field, "$var:" & value.Trim())
                                    End Sub
        menu.Items.Add(varItem)
        Return menu
    End Function

    Private Sub AddReporterItem(menu As ContextMenu, field As FieldDef, header As String, expression As String)
        Dim item As New MenuItem With {.Header = header}
        AddHandler item.Click, Sub() SetReporter(field, expression)
        menu.Items.Add(item)
    End Sub

    Private Sub SetReporter(field As FieldDef, expression As String)
        ApplyFieldValue(field.Key, expression, True)
    End Sub

    Private Function MakeReporterPill(field As FieldDef, raw As String) As FrameworkElement
        Dim border As New Border With {
            .Background = Ui.Brush("#0B1220"),
            .CornerRadius = New CornerRadius(8),
            .Padding = New Thickness(6, 0, 6, 1),
            .ToolTip = "取值器：" & ValueExpression.Describe(raw) & vbCrLf & "右键可改回数字",
            .Cursor = Cursors.Hand
        }
        border.Child = New TextBlock With {
            .Text = "ƒ " & ValueExpression.Describe(raw),
            .FontSize = 10.5,
            .Foreground = Ui.Brush("#DCDCAA"),
            .VerticalAlignment = VerticalAlignment.Center
        }
        Dim menu As New ContextMenu()
        Dim revert As New MenuItem With {.Header = "改回数字"}
        AddHandler revert.Click, Sub() ApplyFieldValue(field.Key, "0", True)
        menu.Items.Add(revert)
        border.ContextMenu = menu
        Return border
    End Function

    Private Sub ApplyConditionalVisibility()
        If Definition Is Nothing Then Return
        If Definition.TypeId <> "if" Then Return
        Dim isVar = Node.GetField("subj") = "var"
        Dim control As FrameworkElement = Nothing
        If _fields.TryGetValue("varName", control) Then
            control.Visibility = If(isVar, Visibility.Visible, Visibility.Collapsed)
        End If
    End Sub

    Private Sub CommitBox(box As TextBox, field As FieldDef, numeric As Boolean)
        If IsPalette Then Return
        Dim value = box.Text
        If value = CStr(box.Tag) Then Return
        If numeric AndAlso Not ValueExpression.IsReporter(value) Then
            Dim number As Double
            If Not Double.TryParse(value, Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, number) Then
                box.BorderBrush = Ui.Brush("#F14C4C")
                Return
            End If
        End If
        box.BorderBrush = Brushes.Transparent
        ApplyFieldValue(field.Key, value, False)
        box.Tag = value
    End Sub

    Private Sub ApplyFieldValue(key As String, value As String, rebuild As Boolean)
        Owner.FieldEdited(Me, key, value, rebuild)
    End Sub

    ' ---------------- 拖拽 ----------------

    Private Sub Palette_MouseMove(sender As Object, e As MouseEventArgs)
        If e.LeftButton <> MouseButtonState.Pressed Then Return
        If _dragStart Is Nothing Then _dragStart = e.GetPosition(Me)
        Dim pos = e.GetPosition(Me)
        If Math.Abs(pos.X - _dragStart.Value.X) < 4 AndAlso Math.Abs(pos.Y - _dragStart.Value.Y) < 4 Then Return
        _dragStart = Nothing
        _paletteDragged = True
        Dim payload As New BlockDragData With {
            .TypeId = Definition.TypeId,
            .FromPalette = True,
            .IsHat = Definition.IsHat
        }
        Dim data As New DataObject(BlockEditorView.BlockDragFormat, payload)
        DragDrop.DoDragDrop(Me, data, DragDropEffects.Copy Or DragDropEffects.Move)
    End Sub

    Private Sub Block_MouseDown(sender As Object, e As MouseButtonEventArgs)
        If _comboOpen Then Return
        If Not IsInteractive(e.OriginalSource) Then
            _dragStart = e.GetPosition(Me)
            Owner.SelectBlock(Node)
            Focus()
        End If
    End Sub

    Private Sub Block_MouseMove(sender As Object, e As MouseEventArgs)
        If _comboOpen Then Return
        If IsPalette OrElse e.LeftButton <> MouseButtonState.Pressed Then Return
        If _dragStart Is Nothing Then Return
        Dim pos = e.GetPosition(Me)
        If Math.Abs(pos.X - _dragStart.Value.X) < 4 AndAlso Math.Abs(pos.Y - _dragStart.Value.Y) < 4 Then Return
        _dragStart = Nothing
        If Definition.IsHat Then Return
        Dim payload As New BlockDragData With {
            .TypeId = Definition.TypeId,
            .SourceNode = Node,
            .SourceList = ParentList,
            .SourceIndex = ParentIndex,
            .FromPalette = False,
            .IsHat = False
        }
        Dim data As New DataObject(BlockEditorView.BlockDragFormat, payload)
        DragDrop.DoDragDrop(Me, data, DragDropEffects.Move)
    End Sub

    Private Sub Block_RightClick(sender As Object, e As MouseButtonEventArgs)
        If IsInteractive(e.OriginalSource) Then Return
        Owner.SelectBlock(Node)
        Dim menu As New ContextMenu()
        Dim deleteItem As New MenuItem With {.Header = "删除积木"}
        AddHandler deleteItem.Click, Sub() Owner.DeleteBlock(Node)
        menu.Items.Add(deleteItem)
        Dim duplicateItem As New MenuItem With {.Header = "复制积木"}
        AddHandler duplicateItem.Click, Sub() Owner.DuplicateBlock(Node)
        menu.Items.Add(duplicateItem)
        If Definition.Kind = BlockKind.CBlockElse Then
            menu.Items.Add(New Separator())
            Dim hasElse = Node.ElseChildren IsNot Nothing AndAlso Node.ElseChildren.Count > 0
            If hasElse Then
                Dim elseItem As New MenuItem With {.Header = "清空「否则」分支"}
                AddHandler elseItem.Click, Sub() Owner.ClearElse(Node)
                menu.Items.Add(elseItem)
            End If
        End If
        menu.PlacementTarget = Me
        menu.IsOpen = True
        e.Handled = True
    End Sub

    Private Function IsInteractive(source As Object) As Boolean
        Dim current = TryCast(source, DependencyObject)
        While current IsNot Nothing
            If TypeOf current Is TextBox OrElse TypeOf current Is ComboBox OrElse TypeOf current Is ComboBoxItem OrElse TypeOf current Is ListBoxItem OrElse TypeOf current Is Popup OrElse TypeOf current Is Button Then Return True
            current = VisualTreeHelper.GetParent(current)
        End While
        Return False
    End Function

    ' ---------------- 放置目标 ----------------

    Private Sub Block_DragOver(sender As Object, e As DragEventArgs)
        If IsPalette Then Return
        Dim payload = TryCast(e.Data.GetData(BlockEditorView.BlockDragFormat), BlockDragData)
        If payload Is Nothing Then Return
        If payload.IsHat Then
            e.Effects = DragDropEffects.None
            e.Handled = True
            Return
        End If
        If payload.SourceNode IsNot Nothing AndAlso IsSelfOrDescendant(payload.SourceNode, Node) Then
            e.Effects = DragDropEffects.None
            e.Handled = True
            Return
        End If
        Dim pos = e.GetPosition(Me)
        If Definition.IsContainer AndAlso pos.Y > 26 Then
            Dim inside = If(pos.Y < ActualHeight - 24, True, False)
            If inside Then
                e.Effects = payload.Effect
                Owner.ShowBodyHint(Me, True)
                e.Handled = True
                Return
            End If
        End If
        Dim before = pos.Y < ActualHeight / 2
        e.Effects = payload.Effect
        Owner.ShowInsertLine(Me, before)
        e.Handled = True
    End Sub

    Private Sub Block_DragLeave(sender As Object, e As DragEventArgs)
        If Not IsPalette Then
            Owner.HideInsertLine()
            Owner.ShowBodyHint(Me, False)
        End If
    End Sub

    Private Sub Block_Drop(sender As Object, e As DragEventArgs)
        If IsPalette Then Return
        Dim payload = TryCast(e.Data.GetData(BlockEditorView.BlockDragFormat), BlockDragData)
        If payload Is Nothing OrElse payload.IsHat Then Return
        If payload.SourceNode IsNot Nothing AndAlso IsSelfOrDescendant(payload.SourceNode, Node) Then Return
        Owner.HideInsertLine()
        Dim pos = e.GetPosition(Me)
        If Definition.IsContainer AndAlso pos.Y > 26 AndAlso pos.Y < ActualHeight - 24 Then
            Owner.PerformDrop(payload, Node.Children, Node.Children.Count)
        Else
            Dim list = ParentList
            If list Is Nothing Then Return
            Dim index = If(pos.Y < ActualHeight / 2, ParentIndex, ParentIndex + 1)
            Owner.PerformDrop(payload, list, index)
        End If
        Owner.ShowBodyHint(Me, False)
        e.Handled = True
    End Sub

    Private Sub Body_DragOver(e As DragEventArgs, border As Border, targetList As List(Of BlockNode))
        Dim payload = TryCast(e.Data.GetData(BlockEditorView.BlockDragFormat), BlockDragData)
        If payload Is Nothing Then Return
        If payload.IsHat Then
            e.Effects = DragDropEffects.None
            e.Handled = True
            Return
        End If
        If payload.SourceNode IsNot Nothing AndAlso (payload.SourceNode Is Node OrElse IsSelfOrDescendant(payload.SourceNode, Node)) Then
            e.Effects = DragDropEffects.None
            e.Handled = True
            Return
        End If
        e.Effects = payload.Effect
        border.BorderBrush = Ui.Brush("#0B1220")
        Owner.HideInsertLine()
        e.Handled = True
    End Sub

    Private Sub Body_Drop(e As DragEventArgs, targetList As List(Of BlockNode))
        Dim payload = TryCast(e.Data.GetData(BlockEditorView.BlockDragFormat), BlockDragData)
        If payload Is Nothing OrElse payload.IsHat Then Return
        If payload.SourceNode IsNot Nothing AndAlso (payload.SourceNode Is Node OrElse IsSelfOrDescendant(payload.SourceNode, Node)) Then Return
        _bodyBorder.BorderBrush = Brushes.Transparent
        Owner.PerformDrop(payload, targetList, targetList.Count)
        e.Handled = True
    End Sub

    Private Shared Function IsSelfOrDescendant(ancestor As BlockNode, candidate As BlockNode) As Boolean
        If ancestor Is candidate Then Return True
        If ancestor.Children IsNot Nothing Then
            For Each subNode In ancestor.Children
                If IsSelfOrDescendant(subNode, candidate) Then Return True
            Next
        End If
        If ancestor.ElseChildren IsNot Nothing Then
            For Each subNode In ancestor.ElseChildren
                If IsSelfOrDescendant(subNode, candidate) Then Return True
            Next
        End If
        Return False
    End Function

    Public Sub SetSelected(selected As Boolean)
        BorderBrush = If(selected, Ui.Brush("#FFFFFF"), _baseBorderBrush)
        BorderThickness = New Thickness(If(selected, 2, 1))
    End Sub

    Public Sub HighlightBody(active As Boolean)
        If _bodyBorder IsNot Nothing Then
            _bodyBorder.BorderBrush = If(active, Ui.Brush("#0B1220"), Brushes.Transparent)
        End If
        If _elseBorder IsNot Nothing Then
            _elseBorder.BorderBrush = If(active, Ui.Brush("#0B1220"), Brushes.Transparent)
        End If
    End Sub

End Class

Public Class BlockDragData
    Public Property TypeId As String
    Public Property SourceNode As BlockNode
    Public Property SourceList As List(Of BlockNode)
    Public Property SourceIndex As Integer
    Public Property FromPalette As Boolean
    Public Property IsHat As Boolean

    ''' <summary>调色板拖入是复制；画布内移动是移动。目标必须返回与源允许效果一致的值，否则 WPF 会显示禁止光标。</summary>
    Public ReadOnly Property Effect As DragDropEffects
        Get
            Return If(FromPalette, DragDropEffects.Copy, DragDropEffects.Move)
        End Get
    End Property
End Class
