Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Controls.Primitives
Imports System.Windows.Input
Imports System.Windows.Media
Imports DanmakuDesigner.Core

Public Class BlockEditorView

    Public Const BlockDragFormat As String = "DanmakuDesigner.BlockDragData"

    Public Property SelectedBlock As BlockNode = Nothing
    Public Property Scope As ProgramScope = ProgramScope.Enemy

    Private _context As BlockContext
    Private ReadOnly _scriptCards As New List(Of ScriptCardUi)
    Private ReadOnly _blockVisuals As New Dictionary(Of BlockNode, BlockVisual)
    Private ReadOnly _parentLists As New Dictionary(Of BlockNode, List(Of BlockNode))
    Private _insertLine As Border
    Private _loaded As Boolean
    Private _editingField As Boolean

    Public Sub New()
        InitializeComponent()
        _insertLine = New Border With {
            .Height = 2,
            .Background = Ui.Brush("#4FC1FF"),
            .IsHitTestVisible = False,
            .Visibility = Visibility.Collapsed,
            .CornerRadius = New CornerRadius(1)
        }
        OverlayCanvas.Children.Add(_insertLine)
        AddHandler Loaded, Sub()
            _loaded = True
            Refresh()
        End Sub
        AddHandler PreviewKeyDown, AddressOf Editor_KeyDown
        AddHandler ScriptHost.DragOver, AddressOf ScriptHost_DragOver
        AddHandler ScriptHost.Drop, AddressOf ScriptHost_Drop
        AddHandler ScriptHost.DragLeave, Sub() HideInsertLine()
        AllowDrop = True
        AddHandler Me.DragOver, AddressOf ScriptHost_DragOver
        AddHandler Me.Drop, AddressOf ScriptHost_Drop
        EmptyCard.IsHitTestVisible = False
    End Sub

    Public Sub Refresh()
        If Not _loaded Then Return
        Dim vertical = Scroll.VerticalOffset
        Dim horizontal = Scroll.HorizontalOffset
        HideInsertLine()
        SelectBlockInternal(Nothing, False)
        _blockVisuals.Clear()
        _parentLists.Clear()
        _scriptCards.Clear()
        ScriptHost.Children.Clear()
        _context = App.Session.BlockContext

        If _context Is Nothing Then
            BreadcrumbText.Text = "未打开行为"
            ScopeText.Text = "—"
            ScriptCountText.Text = ""
            EmptyCard.Visibility = Visibility.Visible
            EmptyTitle.Text = "还没有打开行为积木"
            EmptyText.Text = "在左侧资源管理器中双击一个符卡、敌机模板或子弹样式，即可用积木编写它的行为。"
            Return
        End If

        Scope = _context.Scope
        BreadcrumbText.Text = _context.DisplayName
        ScopeText.Text = If(_context.IsSpell, "符卡行为",
                            If(Scope = ProgramScope.Enemy, "敌机行为",
                               If(Scope = ProgramScope.Particle, "粒子行为", "子弹行为")))
        ScopeBadge.Background = Ui.Brush(If(_context.IsSpell, "#4A2C1A",
                                            If(Scope = ProgramScope.Enemy, "#3A2C4A",
                                               If(Scope = ProgramScope.Particle, "#123A3A", "#12384A"))))
        ScopeText.Foreground = Ui.Brush(If(_context.IsSpell, "#F48771",
                                           If(Scope = ProgramScope.Enemy, "#C792EA",
                                              If(Scope = ProgramScope.Particle, "#9CDCFE", "#4FC1FF"))))

        Dim program = _context.Program
        If program Is Nothing Then Return
        IndexLists(program)
        For i = 0 To program.Scripts.Count - 1
            Dim script = program.Scripts(i)
            If script Is Nothing OrElse script.Definition Is Nothing Then Continue For
            ScriptHost.Children.Add(BuildScriptCard(script, i))
        Next

        Dim totalBlocks = program.BlockCount
        ScriptCountText.Text = program.Scripts.Count & " 个脚本 · " & totalBlocks & " 块"
        EmptyCard.Visibility = If(program.Scripts.Count = 0, Visibility.Visible, Visibility.Collapsed)
        EmptyTitle.Text = "从事件积木开始"
        EmptyText.Text = "从右侧积木库拖入「当生成时」「每帧」等事件积木，然后把运动、攻击和控制积木拖到它下面。"

        Try
            Scroll.ScrollToVerticalOffset(vertical)
            Scroll.ScrollToHorizontalOffset(horizontal)
        Catch
        End Try
        UpdateSelectionVisual()
    End Sub

    Private Sub IndexLists(program As Program)
        For Each script In program.Scripts
            If script Is Nothing Then Continue For
            _parentLists(script) = program.Scripts
            IndexList(script.Children)
            IndexList(script.ElseChildren)
        Next
    End Sub

    Private Sub IndexList(list As List(Of BlockNode))
        If list Is Nothing Then Return
        For Each node In list
            If node Is Nothing Then Continue For
            _parentLists(node) = list
            IndexList(node.Children)
            IndexList(node.ElseChildren)
        Next
    End Sub

    Private Function BuildScriptCard(script As BlockNode, index As Integer) As Border
        Dim card As New Border With {
            .Background = Ui.Brush("#232324"),
            .BorderBrush = Ui.Brush("#303033"),
            .BorderThickness = New Thickness(1),
            .CornerRadius = New CornerRadius(4),
            .Padding = New Thickness(10, 10, 10, 8),
            .Margin = New Thickness(0, 0, 0, 16),
            .AllowDrop = True
        }
        Dim stack As New StackPanel()

        Dim hatVisual As New BlockVisual(script, script.Definition, Me, False, _context.Program.Scripts, index)
        stack.Children.Add(hatVisual)

        Dim bodyBorder As New Border With {
            .MinHeight = 20,
            .Background = Brushes.Transparent,
            .BorderBrush = Brushes.Transparent,
            .BorderThickness = New Thickness(1),
            .CornerRadius = New CornerRadius(2),
            .Padding = New Thickness(2, 2, 2, 2),
            .Margin = New Thickness(0, 3, 0, 0),
            .AllowDrop = True
        }
        Dim bodyPanel As New StackPanel()
        bodyBorder.Child = bodyPanel
        If script.Children.Count = 0 Then
            bodyPanel.Children.Add(New TextBlock With {
                .Text = "拖入行为积木",
                .FontSize = 9.5,
                .Foreground = Ui.Brush("#6A6A6A"),
                .Margin = New Thickness(12, 2, 4, 2),
                .IsHitTestVisible = False
            })
        Else
            For i = 0 To script.Children.Count - 1
                Dim child = script.Children(i)
                If child Is Nothing OrElse child.Definition Is Nothing Then Continue For
                bodyPanel.Children.Add(New BlockVisual(child, child.Definition, Me, False, script.Children, i))
            Next
        End If
        stack.Children.Add(bodyBorder)
        card.Child = stack

        Dim cardUi As New ScriptCardUi With {
            .Card = card,
            .Body = bodyBorder,
            .BodyPanel = bodyPanel,
            .Script = script
        }
        _scriptCards.Add(cardUi)

        AddHandler bodyBorder.DragOver, Sub(sender As Object, e As DragEventArgs)
                                                  Dim payload = TryCast(e.Data.GetData(BlockDragFormat), BlockDragData)
                                                  If payload Is Nothing OrElse payload.IsHat Then Return
                                                  e.Effects = payload.Effect
                                                  bodyBorder.BorderBrush = Ui.Brush("#4FC1FF")
                                                  HideInsertLine()
                                                  e.Handled = True
                                              End Sub
        AddHandler bodyBorder.DragLeave, Sub() bodyBorder.BorderBrush = Brushes.Transparent
        AddHandler bodyBorder.Drop, Sub(sender As Object, e As DragEventArgs)
                                                Dim payload = TryCast(e.Data.GetData(BlockDragFormat), BlockDragData)
                                                If payload Is Nothing OrElse payload.IsHat Then Return
                                                bodyBorder.BorderBrush = Brushes.Transparent
                                                PerformDrop(payload, script.Children, script.Children.Count)
                                                e.Handled = True
                                            End Sub

        AddHandler card.DragOver, Sub(sender As Object, e As DragEventArgs)
                                                Dim payload = TryCast(e.Data.GetData(BlockDragFormat), BlockDragData)
                                                If payload Is Nothing Then Return
                                                e.Effects = payload.Effect
                                                ShowInsertLine(card, False)
                                                e.Handled = True
                                            End Sub
        AddHandler card.Drop, Sub(sender As Object, e As DragEventArgs)
                                          Dim payload = TryCast(e.Data.GetData(BlockDragFormat), BlockDragData)
                                          If payload Is Nothing Then Return
                                          If payload.IsHat Then
                                              AddScript(payload.TypeId)
                                          Else
                                              PerformDrop(payload, script.Children, script.Children.Count)
                                          End If
                                          e.Handled = True
                                      End Sub
        AddHandler card.MouseLeftButtonDown, Sub(sender As Object, e As MouseButtonEventArgs) Focus()
        Return card
    End Function

    Public Sub RegisterVisual(visual As BlockVisual)
        If visual.Node Is Nothing Then Return
        _blockVisuals(visual.Node) = visual
        If SelectedBlock Is visual.Node Then visual.SetSelected(True)
    End Sub

    Public Sub SelectBlock(node As BlockNode)
        SelectBlockInternal(node, True)
    End Sub

    Private Sub SelectBlockInternal(node As BlockNode, notify As Boolean)
        SelectedBlock = node
        UpdateSelectionVisual()
        If notify AndAlso node IsNot Nothing Then
            App.Session.SelectItem(node)
        End If
        Focus()
    End Sub

    Private Sub UpdateSelectionVisual()
        For Each pair In _blockVisuals
            pair.Value.SetSelected(pair.Key Is SelectedBlock)
        Next
    End Sub

    Public Function GetVariableNames() As List(Of String)
        Dim names As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        Dim visited As New HashSet(Of BlockNode)()
        If _context IsNot Nothing AndAlso _context.Program IsNot Nothing Then
            For Each script In _context.Program.Scripts
                CollectVariableNames(script, names, visited)
            Next
        End If
        Return names.OrderBy(Function(n) n, StringComparer.OrdinalIgnoreCase).ToList()
    End Function

    Private Sub CollectVariableNames(block As BlockNode, names As HashSet(Of String), visited As HashSet(Of BlockNode))
        If block Is Nothing OrElse Not visited.Add(block) Then Return
        If block.TypeId = "set_var" OrElse block.TypeId = "add_var" Then
            Dim name = block.GetField("name").Trim()
            If name.Length > 0 Then names.Add(name)
        End If
        If block.TypeId = "if" AndAlso block.GetField("subj") = "var" Then
            Dim name = block.GetField("varName").Trim()
            If name.Length > 0 Then names.Add(name)
        End If
        For Each field In block.Fields
            If ValueExpression.IsReporter(field.Value) AndAlso ValueExpression.ReporterKind(field.Value) = "var" Then
                Dim name = ValueExpression.ReporterArg(field.Value).Trim()
                If name.Length > 0 Then names.Add(name)
            End If
        Next
        For Each child In block.Children
            CollectVariableNames(child, names, visited)
        Next
        For Each child In block.ElseChildren
            CollectVariableNames(child, names, visited)
        Next
    End Sub

    Public Sub FieldEdited(visual As BlockVisual, key As String, value As String, rebuild As Boolean)
        If visual Is Nothing OrElse visual.Node Is Nothing Then Return
        If visual.Node.GetField(key) = value Then Return
        If _editingField Then Return
        _editingField = True
        Try
            App.Session.BeginEdit()
            visual.Node.SetField(key, value)
            App.Session.CommitEdit()
            If rebuild Then
                Refresh()
            Else
                visual.Node.SetField(key, value)
            End If
        Finally
            _editingField = False
        End Try
    End Sub

    Public Sub ShowBodyHint(visual As BlockVisual, active As Boolean)
        _insertLine.Visibility = Visibility.Collapsed
        For Each pair In _blockVisuals
            pair.Value.HighlightBody(active AndAlso pair.Value Is visual)
        Next
    End Sub

    Public Sub ShowInsertLine(target As FrameworkElement, before As Boolean)
        If target Is Nothing Then Return
        For Each pair In _blockVisuals
            pair.Value.HighlightBody(False)
        Next
        Try
            Dim point = target.TranslatePoint(New Point(0, If(before, 0, target.ActualHeight)), OverlayCanvas)
            _insertLine.Width = Math.Max(24, target.ActualWidth)
            _insertLine.Background = Ui.Brush("#4FC1FF")
            Canvas.SetLeft(_insertLine, point.X)
            Canvas.SetTop(_insertLine, point.Y - 1)
            _insertLine.Visibility = Visibility.Visible
        Catch
            _insertLine.Visibility = Visibility.Collapsed
        End Try
    End Sub

    Public Sub HideInsertLine()
        If _insertLine IsNot Nothing Then _insertLine.Visibility = Visibility.Collapsed
        For Each pair In _blockVisuals
            pair.Value.HighlightBody(False)
        Next
    End Sub

    Public Sub PaletteClicked(def As BlockDefinition)
        If def Is Nothing Then Return
        If def.IsHat Then
            AddScript(def.TypeId)
        Else
            AddBlockToLastScript(def)
        End If
    End Sub

    Private Sub AddScript(typeId As String)
        If _context Is Nothing Then Return
        Dim def = BlockRegistry.Find(typeId)
        If def Is Nothing OrElse Not def.IsHat Then Return
        Dim node = BlockRegistry.CreateBlock(typeId, App.Session.Stage)
        App.Session.BeginEdit()
        _context.Program.Scripts.Add(node)
        App.Session.CommitEdit()
        SelectedBlock = node
        App.Session.SelectItem(node)
        Refresh()
    End Sub

    Private Sub AddBlockToLastScript(def As BlockDefinition)
        If _context Is Nothing OrElse def Is Nothing Then Return
        If _context.Program.Scripts.Count = 0 Then
            Dim hatType = If(_context.Scope = ProgramScope.Particle, "particle_every_frame", "every_frame")
            Dim hat = BlockRegistry.CreateBlock(hatType, App.Session.Stage)
            App.Session.BeginEdit()
            _context.Program.Scripts.Add(hat)
            App.Session.CommitEdit()
        End If
        Dim script = _context.Program.Scripts.Last()
        Dim node = BlockRegistry.CreateBlock(def.TypeId, App.Session.Stage)
        App.Session.BeginEdit()
        script.Children.Add(node)
        App.Session.CommitEdit()
        SelectedBlock = node
        App.Session.SelectItem(node)
        Refresh()
    End Sub

    Public Sub PerformDrop(payload As BlockDragData, targetList As List(Of BlockNode), index As Integer)
        If payload Is Nothing OrElse targetList Is Nothing Then Return
        If payload.IsHat Then Return
        Dim node As BlockNode
        App.Session.BeginEdit()
        If payload.FromPalette Then
            node = BlockRegistry.CreateBlock(payload.TypeId, App.Session.Stage)
        Else
            node = payload.SourceNode
            If node Is Nothing OrElse payload.SourceList Is Nothing Then
                App.Session.CancelEdit()
                Return
            End If
            Dim sourceIndex = payload.SourceList.IndexOf(node)
            If sourceIndex >= 0 Then
                payload.SourceList.RemoveAt(sourceIndex)
                If payload.SourceList Is targetList AndAlso sourceIndex < index Then index -= 1
            End If
        End If
        index = Math.Max(0, Math.Min(index, targetList.Count))
        targetList.Insert(index, node)
        App.Session.CommitEdit()
        SelectedBlock = node
        App.Session.SelectItem(node)
        Refresh()
    End Sub

    Public Sub DeleteBlock(node As BlockNode)
        If node Is Nothing Then Return
        Dim list As List(Of BlockNode) = Nothing
        If Not _parentLists.TryGetValue(node, list) Then Return
        App.Session.BeginEdit()
        list.Remove(node)
        App.Session.CommitEdit()
        SelectedBlock = Nothing
        App.Session.SelectItem(App.Session.Stage)
        Refresh()
    End Sub

    Public Sub DuplicateBlock(node As BlockNode)
        If node Is Nothing Then Return
        Dim list As List(Of BlockNode) = Nothing
        If Not _parentLists.TryGetValue(node, list) Then Return
        Dim clone = node.CloneNode()
        Dim index = list.IndexOf(node)
        App.Session.BeginEdit()
        list.Insert(Math.Max(0, index + 1), clone)
        App.Session.CommitEdit()
        SelectedBlock = clone
        App.Session.SelectItem(clone)
        Refresh()
    End Sub

    Public Sub ClearElse(node As BlockNode)
        If node Is Nothing Then Return
        App.Session.BeginEdit()
        node.ElseChildren.Clear()
        App.Session.CommitEdit()
        Refresh()
    End Sub

    Public Sub DeleteSelectedBlock()
        If SelectedBlock IsNot Nothing Then DeleteBlock(SelectedBlock)
    End Sub

    Public Sub ScrollToEnd()
        Scroll.ScrollToEnd()
    End Sub

    Public Sub DuplicateSelectedBlock()
        If SelectedBlock IsNot Nothing Then DuplicateBlock(SelectedBlock)
    End Sub

    Private Sub ScriptHost_DragOver(sender As Object, e As DragEventArgs)
        Dim payload = TryCast(e.Data.GetData(BlockDragFormat), BlockDragData)
        If payload Is Nothing Then Return
        e.Effects = payload.Effect
        If payload.IsHat Then
            If _scriptCards.Count > 0 Then
                ShowInsertLine(_scriptCards.Last().Card, False)
            Else
                HideInsertLine()
            End If
        Else
            If _scriptCards.Count > 0 Then ShowInsertLine(_scriptCards.Last().Card, False) Else HideInsertLine()
        End If
        e.Handled = True
    End Sub

    Private Sub ScriptHost_Drop(sender As Object, e As DragEventArgs)
        Dim payload = TryCast(e.Data.GetData(BlockDragFormat), BlockDragData)
        If payload Is Nothing Then Return
        If payload.IsHat Then
            AddScript(payload.TypeId)
        Else
            If _context IsNot Nothing AndAlso _context.Program.Scripts.Count > 0 Then
                Dim script = _context.Program.Scripts.Last()
                PerformDrop(payload, script.Children, script.Children.Count)
            Else
                AddBlockToLastScript(BlockRegistry.Find(payload.TypeId))
            End If
        End If
        HideInsertLine()
        e.Handled = True
    End Sub

    Private Sub NewScript_Click(sender As Object, e As RoutedEventArgs)
        If _context Is Nothing Then Return
        Dim menu As New ContextMenu()
        Dim header As New MenuItem With {.Header = "选择事件积木", .IsEnabled = False}
        menu.Items.Add(header)
        For Each def In BlockRegistry.ForScope(Scope, _context.IsBoss, _context.ParticleIs3D, _context.IsSpell).Where(Function(d) d.IsHat)
            Dim captured = def
            Dim item As New MenuItem With {.Header = def.Title}
            AddHandler item.Click, Sub() AddScript(captured.TypeId)
            menu.Items.Add(item)
        Next
        menu.PlacementTarget = TryCast(sender, UIElement)
        menu.Placement = PlacementMode.Bottom
        menu.IsOpen = True
    End Sub

    Private Sub Back_Click(sender As Object, e As RoutedEventArgs)
        App.Session.SetView("timeline")
    End Sub

    Private Sub Editor_KeyDown(sender As Object, e As KeyEventArgs)
        If TypeOf Keyboard.FocusedElement Is TextBox OrElse TypeOf Keyboard.FocusedElement Is ComboBox Then Return
        If SelectedBlock Is Nothing Then Return
        If e.Key = Key.Delete Then
            DeleteBlock(SelectedBlock)
            e.Handled = True
        ElseIf e.Key = Key.D AndAlso (Keyboard.Modifiers And ModifierKeys.Control) = ModifierKeys.Control Then
            DuplicateBlock(SelectedBlock)
            e.Handled = True
        End If
    End Sub

    Private Class ScriptCardUi
        Public Property Card As Border
        Public Property Body As Border
        Public Property BodyPanel As StackPanel
        Public Property Script As BlockNode
    End Class

End Class
