Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Input
Imports System.Windows.Media
Imports DanmakuDesigner.Core

Public Class ProjectExplorer

    Private _templatesExpanded As Boolean = True
    Private _patternsExpanded As Boolean = True
    Private _spellsExpanded As Boolean = True
    Private _dialoguesExpanded As Boolean = True
    Private _tracksExpanded As Boolean = True
    Private _stagesExpanded As Boolean = True
    Private _loaded As Boolean

    Public Sub New()
        InitializeComponent()
        AddHandler Loaded, Sub()
            _loaded = True
            Refresh()
        End Sub
    End Sub

    Public Sub Refresh()
        If Not _loaded Then Return
        Dim stage = App.Session.Stage
        Dim game = App.Session.Project
        If stage Is Nothing OrElse game Is Nothing Then
            ListHost.Children.Clear()
            Return
        End If
        ListHost.Children.Clear()

        ListHost.Children.Add(MakeGameRow(game))
        ListHost.Children.Add(MakeGroupHeader("游戏关卡", game.Stages.Count, _stagesExpanded,
                                              Sub()
                                                  _stagesExpanded = Not _stagesExpanded
                                                  Refresh()
                                              End Sub,
                                              "Application_16x.png", Sub() AddStage()))
        If _stagesExpanded Then
            For Each stageProject In game.Stages
                ListHost.Children.Add(MakeStageProjectRow(stageProject))
            Next
        End If
        ListHost.Children.Add(New Border With {.Height = 1, .Background = Ui.Brush("#333337"), .Margin = New Thickness(8, 5, 8, 5)})

        ListHost.Children.Add(MakeGroupHeader("敌机模板", stage.EnemyTemplates.Count, _templatesExpanded,
                                              Sub()
                                                  _templatesExpanded = Not _templatesExpanded
                                                  Refresh()
                                              End Sub,
                                              "Enemy_16x.png", Sub() AddTemplate()))
        If _templatesExpanded Then
            For Each tmpl In stage.EnemyTemplates
                ListHost.Children.Add(MakeTemplateRow(tmpl))
            Next
            If stage.EnemyTemplates.Count = 0 Then ListHost.Children.Add(MakeEmptyHint("还没有敌机模板，点击右上角 ＋ 创建。", 1))
        End If

        ListHost.Children.Add(MakeGroupHeader("子弹样式", stage.BulletPatterns.Count, _patternsExpanded,
                                              Sub()
                                                  _patternsExpanded = Not _patternsExpanded
                                                  Refresh()
                                              End Sub,
                                              "Bullet_16x.png", Sub() AddPattern()))
        If _patternsExpanded Then
            For Each pattern In stage.BulletPatterns
                ListHost.Children.Add(MakePatternRow(pattern))
            Next
            If stage.BulletPatterns.Count = 0 Then ListHost.Children.Add(MakeEmptyHint("还没有子弹样式，点击右上角 ＋ 创建。", 1))
        End If

        ListHost.Children.Add(MakeGroupHeader("符卡", stage.SpellCards.Count, _spellsExpanded,
                                              Sub()
                                                  _spellsExpanded = Not _spellsExpanded
                                                  Refresh()
                                              End Sub,
                                              "SpellCard_16x.png", Sub() AddSpell()))
        If _spellsExpanded Then
            For Each spell In stage.SpellCards
                ListHost.Children.Add(MakeSpellRow(spell))
            Next
            If stage.SpellCards.Count = 0 Then ListHost.Children.Add(MakeEmptyHint("还没有符卡，点击右上角 ＋ 创建。", 1))
        End If

        ListHost.Children.Add(MakeGroupHeader("对话", stage.Dialogues.Count, _dialoguesExpanded,
                                              Sub()
                                                  _dialoguesExpanded = Not _dialoguesExpanded
                                                  Refresh()
                                              End Sub,
                                              "Document_16x.png", Sub() AddDialogue()))
        If _dialoguesExpanded Then
            For Each dialogue In stage.Dialogues
                ListHost.Children.Add(MakeDialogueRow(dialogue))
            Next
            If stage.Dialogues.Count = 0 Then ListHost.Children.Add(MakeEmptyHint("还没有对话，点击右上角 ＋ 创建。", 1))
        End If

        ListHost.Children.Add(MakeGroupHeader("时间轴轨道", stage.Tracks.Count, _tracksExpanded,
                                              Sub()
                                                  _tracksExpanded = Not _tracksExpanded
                                                  Refresh()
                                              End Sub,
                                              "Timeline_16x.png", Nothing))
        If _tracksExpanded Then
            For Each track In stage.Tracks
                ListHost.Children.Add(MakeTrackRow(track))
            Next
            If stage.Tracks.Count = 0 Then ListHost.Children.Add(MakeEmptyHint("时间轴上还没有轨道。", 1))
        End If
    End Sub

    Private Function MakeStageRow(stage As StageDocument) As Border
        Dim selected = TypeOf App.Session.Selection Is StageDocument
        Dim row = MakeBaseRow("Application_16x.png", stage.Name, stage.Difficulty.ToString().ToUpperInvariant() & " · " & (stage.LengthTicks / 60.0).ToString("0.0") & "s · " & stage.Tracks.Count & " 轨", 6, selected)
        AddHandler row.MouseLeftButtonDown, Sub(sender As Object, e As MouseButtonEventArgs)
                                                App.Session.SelectItem(stage)
                                                e.Handled = True
                                            End Sub
        Return row
    End Function

    Private Function MakeGameRow(game As GameProject) As Border
        Dim subtitle = game.Stages.Count & " 个关卡 · " & If(String.IsNullOrWhiteSpace(game.Author), "未设置作者", game.Author)
        Dim row = MakeBaseRow("Application_16x.png", game.Name, subtitle, 6, Object.ReferenceEquals(App.Session.Selection, game))
        row.ToolTip = game.Name & vbCrLf & game.Stages.Count & " 个关卡" & vbCrLf & "双击重命名游戏；右键修改作者"
        AddHandler row.MouseLeftButtonDown, Sub(sender As Object, e As MouseButtonEventArgs)
                                                 If e.ClickCount = 2 Then
                                                     RenameGame()
                                                 Else
                                                     App.Session.SelectItem(game)
                                                 End If
                                                 e.Handled = True
                                             End Sub
        AddHandler row.MouseRightButtonDown, Sub(sender As Object, e As MouseButtonEventArgs)
                                                 Dim menu As New ContextMenu()
                                                 menu.Items.Add(MakeMenuItem("重命名游戏…", Sub() RenameGame()))
                                                 menu.Items.Add(MakeMenuItem("修改作者…", Sub() EditGameAuthor()))
                                                 menu.PlacementTarget = row
                                                 menu.IsOpen = True
                                                 e.Handled = True
                                             End Sub
        Return row
    End Function

    Private Sub RenameGame()
        If App.Session.Project Is Nothing Then Return
        Dim name = InputDialog.ShowInput(Window.GetWindow(Me), "重命名游戏", "游戏名称：", App.Session.Project.Name,
                                         Function(text) If(String.IsNullOrWhiteSpace(text), "名称不能为空。", Nothing))
        If name Is Nothing Then Return
        App.Session.BeginEdit()
        App.Session.Project.Name = name
        App.Session.CommitEdit()
        Refresh()
    End Sub

    Private Sub EditGameAuthor()
        If App.Session.Project Is Nothing Then Return
        Dim author = InputDialog.ShowInput(Window.GetWindow(Me), "游戏作者", "作者：", App.Session.Project.Author)
        If author Is Nothing Then Return
        App.Session.BeginEdit()
        App.Session.Project.Author = author
        App.Session.CommitEdit()
        Refresh()
    End Sub

    Private Function MakeStageProjectRow(stageProject As StageProject) As Border
        Dim active = App.Session.ActiveStageProject Is stageProject
        Dim stage = stageProject.Stage
        Dim subtitle = stage.CodeName & " · " & stage.Difficulty.ToString().ToUpperInvariant() & " · " &
                       (stage.LengthTicks / 60.0).ToString("0.0") & "s · " & stage.Tracks.Count & " 轨"
        Dim row = MakeBaseRow("Timeline_16x.png", stageProject.Name, subtitle, 18,
                              active OrElse (TypeOf App.Session.Selection Is StageDocument AndAlso App.Session.Selection Is stage))
        If active Then row.BorderBrush = Ui.Brush("#007ACC")
        AddHandler row.MouseLeftButtonDown, Sub(sender As Object, e As MouseButtonEventArgs)
                                                App.Session.ActivateStage(stageProject)
                                                e.Handled = True
                                            End Sub
        AddHandler row.MouseRightButtonDown, Sub(sender As Object, e As MouseButtonEventArgs)
                                                 App.Session.SelectItem(stageProject.Stage)
                                                 Dim menu As New ContextMenu()
                                                 menu.Items.Add(MakeMenuItem("重命名关卡…", Sub() RenameStage(stageProject)))
                                                 menu.Items.Add(MakeMenuItem("复制关卡", Sub() DuplicateStage(stageProject)))
                                                 menu.Items.Add(MakeMenuItem("删除关卡", Sub() DeleteStage(stageProject)))
                                                 menu.PlacementTarget = row
                                                 menu.IsOpen = True
                                                 e.Handled = True
                                             End Sub
        Return row
    End Function

    Private Function MakeGroupHeader(title As String, count As Integer, expanded As Boolean, toggle As Action, iconName As String, addAction As Action) As Border
        Dim border As New Border With {
            .Height = 26,
            .Margin = New Thickness(4, 3, 4, 1),
            .CornerRadius = New CornerRadius(2),
            .Background = Brushes.Transparent,
            .Cursor = Cursors.Hand
        }
        Dim grid As New Grid()
        grid.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(18)})
        grid.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(18)})
        grid.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})
        grid.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})
        grid.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})

        Dim glyph As New TextBlock With {
            .Text = If(expanded, "▾", "▸"),
            .FontSize = 10,
            .Foreground = Ui.Brush("#A0A0A0"),
            .HorizontalAlignment = HorizontalAlignment.Center,
            .VerticalAlignment = VerticalAlignment.Center
        }
        grid.Children.Add(glyph)
        Dim img = Icon(iconName, 14)
        If img IsNot Nothing Then
            img.Opacity = 0.9
            Grid.SetColumn(img, 1)
            grid.Children.Add(img)
        End If
        Dim text As New TextBlock With {
            .Text = title,
            .FontSize = 11,
            .FontWeight = FontWeights.SemiBold,
            .Foreground = Ui.Brush("#D0D0D0"),
            .VerticalAlignment = VerticalAlignment.Center,
            .Margin = New Thickness(3, 0, 0, 0)
        }
        Grid.SetColumn(text, 2)
        grid.Children.Add(text)
        Dim badge = Ui.Badge(count.ToString(), "#3A3A3D", "#C8C8C8")
        Grid.SetColumn(badge, 3)
        badge.Margin = New Thickness(0, 0, 4, 0)
        grid.Children.Add(badge)

        If addAction IsNot Nothing Then
            Dim add As New Button With {
                .Content = "＋",
                .Style = TryCast(Application.Current.FindResource("VS.ToolButton"), Style),
                .Width = 20,
                .Height = 20,
                .Padding = New Thickness(0),
                .ToolTip = "新建" & title
            }
            Grid.SetColumn(add, 4)
            AddHandler add.Click, Sub() addAction.Invoke()
            grid.Children.Add(add)
        End If

        AddHandler border.MouseLeftButtonDown, Sub(sender As Object, e As MouseButtonEventArgs)
                                                toggle.Invoke()
                                                e.Handled = True
                                            End Sub
        AddHandler border.MouseEnter, Sub() border.Background = Ui.Brush("#2F2F32")
        AddHandler border.MouseLeave, Sub() border.Background = Brushes.Transparent
        border.Child = grid
        Return border
    End Function

    Private Function MakeTemplateRow(tmpl As EnemyTemplate) As Border
        Dim selected = TypeOf App.Session.Selection Is EnemyTemplate AndAlso DirectCast(App.Session.Selection, EnemyTemplate).Id = tmpl.Id
        Dim subtitle = "HP " & tmpl.HP.ToString("0.#") & " · " & tmpl.Behavior.Scripts.Count & " 脚本 · " & tmpl.Behavior.BlockCount & " 块"
        Dim row = MakeBaseRow("Enemy_16x.png", tmpl.Name, subtitle, 17, selected)
        Dim colorLabels = EngineCatalog.EnemyColors(tmpl.EnemyType)
        Dim colorName = If(tmpl.Color >= 0 AndAlso tmpl.Color < colorLabels.Count, colorLabels(tmpl.Color), "—")
        row.ToolTip = tmpl.Name & vbCrLf & EngineCatalog.EnemyType(tmpl.EnemyType).Display & " · " & colorName & vbCrLf &
                      "HP " & tmpl.HP & " · 存在 " & tmpl.Life & " 帧 · 速度 " & tmpl.Speed & " · 方向 " & tmpl.Direction & "°" & vbCrLf &
                      "双击编辑积木行为"
        AddHandler row.MouseLeftButtonDown, Sub(sender As Object, e As MouseButtonEventArgs)
                                                If e.ClickCount = 2 Then
                                                    App.Session.EditTemplateBehavior(tmpl)
                                                Else
                                                    App.Session.SelectItem(tmpl)
                                                End If
                                                e.Handled = True
                                            End Sub
        AddHandler row.MouseRightButtonDown, Sub(sender As Object, e As MouseButtonEventArgs)
                                                 App.Session.SelectItem(tmpl)
                                                 ShowTemplateMenu(tmpl, row)
                                                 e.Handled = True
                                             End Sub
        Return row
    End Function

    Private Function MakePatternRow(pattern As BulletPattern) As Border
        Dim selected = TypeOf App.Session.Selection Is BulletPattern AndAlso DirectCast(App.Session.Selection, BulletPattern).Id = pattern.Id
        Dim subtitle = EngineCatalog.BulletType(pattern.BulletType).Display & " · " & EngineCatalog.BulletColorName(pattern.Color) & " · " & pattern.Behavior.BlockCount & " 块"
        Dim row = MakeBaseRow("Bullet_16x.png", pattern.Name, subtitle, 17, selected)
        row.ToolTip = pattern.Name & vbCrLf & "子弹：" & EngineCatalog.BulletType(pattern.BulletType).Display & " · " & EngineCatalog.BulletColorName(pattern.Color) & vbCrLf & "双击编辑子弹行为积木"
        AddHandler row.MouseLeftButtonDown, Sub(sender As Object, e As MouseButtonEventArgs)
                                                If e.ClickCount = 2 Then
                                                    App.Session.EditPatternBehavior(pattern)
                                                Else
                                                    App.Session.SelectItem(pattern)
                                                End If
                                                e.Handled = True
                                            End Sub
        AddHandler row.MouseRightButtonDown, Sub(sender As Object, e As MouseButtonEventArgs)
                                                 App.Session.SelectItem(pattern)
                                                 ShowPatternMenu(pattern, row)
                                                 e.Handled = True
                                             End Sub
        Return row
    End Function

    Private Function MakeSpellRow(spell As BossSpellCard) As Border
        Dim selected = TypeOf App.Session.Selection Is BossSpellCard AndAlso DirectCast(App.Session.Selection, BossSpellCard).Id = spell.Id
        Dim typeName = If(spell.Type = BossSpellType.Time, "时符", If(spell.Type = BossSpellType.Life, "普通符卡", "仅非符"))
        Dim subtitle = typeName & " · " & spell.Behavior.Scripts.Count & " 脚本 · " & spell.Behavior.BlockCount & " 块"
        Dim row = MakeBaseRow("SpellCard_16x.png", spell.Name, subtitle, 17, selected)
        row.ToolTip = spell.Name & vbCrLf & typeName & " · 符卡 HP " & spell.SpellHP.ToString("0.#") & vbCrLf & "双击编辑符卡行为积木"
        AddHandler row.MouseLeftButtonDown, Sub(sender As Object, e As MouseButtonEventArgs)
                                                If e.ClickCount = 2 Then
                                                    App.Session.EditSpellBehavior(spell)
                                                Else
                                                    App.Session.SelectItem(spell)
                                                End If
                                                e.Handled = True
                                            End Sub
        AddHandler row.MouseRightButtonDown, Sub(sender As Object, e As MouseButtonEventArgs)
                                                 App.Session.SelectItem(spell)
                                                 ShowSpellMenu(spell, row)
                                                 e.Handled = True
                                             End Sub
        Return row
    End Function

    Private Function MakeDialogueRow(dialogue As DialogueDefinition) As Border
        Dim selected = TypeOf App.Session.Selection Is DialogueDefinition AndAlso DirectCast(App.Session.Selection, DialogueDefinition).Id = dialogue.Id
        Dim afterText = If(dialogue.AfterAction = DialogueAfterAction.StartBoss, "Boss 开打",
                           If(dialogue.AfterAction = DialogueAfterAction.NextStage, "下一关", "不结束"))
        Dim subtitle = dialogue.Lines.Count & " 句 · " & afterText
        Dim row = MakeBaseRow("Document_16x.png", dialogue.Name, subtitle, 17, selected)
        row.ToolTip = dialogue.Name & vbCrLf & subtitle & vbCrLf & "双击编辑对话"
        AddHandler row.MouseLeftButtonDown, Sub(sender As Object, e As MouseButtonEventArgs)
                                                If e.ClickCount = 2 Then
                                                    EditDialogue(dialogue)
                                                Else
                                                    App.Session.SelectItem(dialogue)
                                                End If
                                                e.Handled = True
                                            End Sub
        AddHandler row.MouseRightButtonDown, Sub(sender As Object, e As MouseButtonEventArgs)
                                                 App.Session.SelectItem(dialogue)
                                                 ShowDialogueMenu(dialogue, row)
                                                 e.Handled = True
                                             End Sub
        Return row
    End Function

    Private Function MakeTrackRow(track As TimelineTrack) As Border
        Dim selected = TypeOf App.Session.Selection Is TimelineTrack AndAlso DirectCast(App.Session.Selection, TimelineTrack).Id = track.Id
        Dim border As New Border With {
            .Height = 26,
            .Margin = New Thickness(17, 0, 4, 0),
            .CornerRadius = New CornerRadius(2),
            .Background = If(selected, Ui.Brush("#264F78"), Brushes.Transparent),
            .Cursor = Cursors.Hand,
            .Padding = New Thickness(0)
        }
        Dim grid As New Grid()
        grid.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(16)})
        grid.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})
        grid.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})
        Dim dot As New Ellipse With {
            .Width = 7,
            .Height = 7,
            .Fill = Ui.Brush(track.Accent),
            .HorizontalAlignment = HorizontalAlignment.Center,
            .VerticalAlignment = VerticalAlignment.Center
        }
        grid.Children.Add(dot)
        Dim text As New TextBlock With {
            .Text = track.Name,
            .FontSize = 11.5,
            .Foreground = Ui.Brush(If(selected, "#FFFFFF", "#C8C8C8")),
            .VerticalAlignment = VerticalAlignment.Center,
            .TextTrimming = TextTrimming.CharacterEllipsis,
            .Margin = New Thickness(4, 0, 4, 0)
        }
        Grid.SetColumn(text, 1)
        grid.Children.Add(text)
        Dim countText As New TextBlock With {
            .Text = track.Clips.Count.ToString(),
            .FontSize = 9,
            .Foreground = Ui.Brush("#8A8A8A"),
            .VerticalAlignment = VerticalAlignment.Center,
            .Margin = New Thickness(0, 0, 6, 0)
        }
        Grid.SetColumn(countText, 2)
        grid.Children.Add(countText)
        border.Child = grid
        AddHandler border.MouseLeftButtonDown, Sub(sender As Object, e As MouseButtonEventArgs)
                                                   App.Session.SelectItem(track)
                                                   App.Session.SetView("timeline")
                                                   e.Handled = True
                                               End Sub
        AddHandler border.MouseEnter, Sub()
                                          If Not selected Then border.Background = Ui.Brush("#2F2F32")
                                      End Sub
        AddHandler border.MouseLeave, Sub()
                                          If Not selected Then border.Background = Brushes.Transparent
                                      End Sub
        Return border
    End Function

    Private Function MakeBaseRow(iconName As String, title As String, subtitle As String, indent As Double, selected As Boolean) As Border
        Dim border As New Border With {
            .Height = 42,
            .Margin = New Thickness(indent, 0, 4, 0),
            .CornerRadius = New CornerRadius(3),
            .Background = If(selected, Ui.Brush("#264F78"), Brushes.Transparent),
            .Cursor = Cursors.Hand,
            .Padding = New Thickness(0)
        }
        Dim grid As New Grid()
        grid.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(24)})
        grid.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})
        Dim img = Icon(iconName, 16)
        If img IsNot Nothing Then
            img.Margin = New Thickness(4, 0, 0, 0)
            img.HorizontalAlignment = HorizontalAlignment.Left
            grid.Children.Add(img)
        End If
        Dim panel As New StackPanel With {.VerticalAlignment = VerticalAlignment.Center, .Margin = New Thickness(0, 0, 6, 0)}
        panel.Children.Add(New TextBlock With {
            .Text = title,
            .FontSize = 12,
            .Foreground = Ui.Brush(If(selected, "#FFFFFF", "#E0E0E0")),
            .TextTrimming = TextTrimming.CharacterEllipsis
        })
        panel.Children.Add(New TextBlock With {
            .Text = subtitle,
            .FontSize = 10,
            .Foreground = Ui.Brush(If(selected, "#CFE4F5", "#8A8A8A")),
            .TextTrimming = TextTrimming.CharacterEllipsis,
            .Margin = New Thickness(0, 1, 0, 0)
        })
        Grid.SetColumn(panel, 1)
        grid.Children.Add(panel)
        border.Child = grid
        AddHandler border.MouseEnter, Sub()
                                          If Not selected Then border.Background = Ui.Brush("#2F2F32")
                                      End Sub
        AddHandler border.MouseLeave, Sub()
                                          If Not selected Then border.Background = Brushes.Transparent
                                      End Sub
        Return border
    End Function

    Private Function MakeEmptyHint(text As String, indent As Integer) As TextBlock
        Return New TextBlock With {
            .Text = text,
            .FontSize = 10,
            .Foreground = Ui.Brush("#707070"),
            .TextWrapping = TextWrapping.Wrap,
            .Margin = New Thickness(20 + indent * 8, 3, 12, 6)
        }
    End Function

    Private Sub AddTemplate_Click(sender As Object, e As RoutedEventArgs)
        AddTemplate()
    End Sub

    Private Sub AddTemplate()
        Dim stage = App.Session.Stage
        If stage Is Nothing Then Return
        Dim index = stage.EnemyTemplates.Count + 1
        Dim tmpl As New EnemyTemplate With {
            .Name = "新敌机 " & index,
            .EnemyType = 1,
            .Color = 0,
            .HP = 20,
            .Life = 360,
            .Speed = 2,
            .Direction = 180
        }
        App.Session.BeginEdit()
        stage.EnemyTemplates.Add(tmpl)
        App.Session.CommitEdit()
        App.Session.SelectItem(tmpl)
        Refresh()
        App.Session.AddLog(IssueSeverity.Info, "已创建敌机模板：" & tmpl.Name & "，可在属性面板调整，或双击编辑积木。")
    End Sub

    Private Sub AddPattern_Click(sender As Object, e As RoutedEventArgs)
        AddPattern()
    End Sub

    Private Sub AddPattern()
        Dim stage = App.Session.Stage
        If stage Is Nothing Then Return
        Dim index = stage.BulletPatterns.Count + 1
        Dim pattern As New BulletPattern With {
            .Name = "新子弹 " & index,
            .BulletType = 3,
            .Color = 15
        }
        App.Session.BeginEdit()
        stage.BulletPatterns.Add(pattern)
        App.Session.CommitEdit()
        App.Session.SelectItem(pattern)
        Refresh()
        App.Session.AddLog(IssueSeverity.Info, "已创建子弹样式：" & pattern.Name & "。")
    End Sub

    Private Sub ShowTemplateMenu(tmpl As EnemyTemplate, target As FrameworkElement)
        Dim menu As New ContextMenu()
        menu.Items.Add(MakeMenuItem("编辑积木行为", Sub() App.Session.EditTemplateBehavior(tmpl)))
        menu.Items.Add(MakeMenuItem("重命名…", Sub() RenameTemplate(tmpl)))
        menu.Items.Add(MakeMenuItem("复制模板", Sub() DuplicateTemplate(tmpl)))
        menu.Items.Add(New Separator())
        menu.Items.Add(MakeMenuItem("删除模板", Sub() DeleteTemplate(tmpl)))
        menu.PlacementTarget = target
        menu.IsOpen = True
    End Sub

    Private Sub ShowPatternMenu(pattern As BulletPattern, target As FrameworkElement)
        Dim menu As New ContextMenu()
        menu.Items.Add(MakeMenuItem("编辑子弹行为", Sub() App.Session.EditPatternBehavior(pattern)))
        menu.Items.Add(MakeMenuItem("重命名…", Sub() RenamePattern(pattern)))
        menu.Items.Add(MakeMenuItem("复制样式", Sub() DuplicatePattern(pattern)))
        menu.Items.Add(New Separator())
        menu.Items.Add(MakeMenuItem("删除样式", Sub() DeletePattern(pattern)))
        menu.PlacementTarget = target
        menu.IsOpen = True
    End Sub

    Private Sub AddSpell()
        Dim stage = App.Session.Stage
        If stage Is Nothing Then Return
        Dim spell As New BossSpellCard With {
            .Name = "符卡 " & (stage.SpellCards.Count + 1),
            .Type = BossSpellType.Life
        }
        App.Session.BeginEdit()
        stage.SpellCards.Add(spell)
        App.Session.CommitEdit()
        App.Session.SelectItem(spell)
        Refresh()
        App.Session.AddLog(IssueSeverity.Info, "已创建符卡：" & spell.Name & "。")
    End Sub

    Private Sub AddDialogue()
        Dim stage = App.Session.Stage
        If stage Is Nothing Then Return
        Dim dialogue As New DialogueDefinition With {.Name = "对话 " & (stage.Dialogues.Count + 1)}
        dialogue.Lines.Add(New DialogueLine With {.Speaker = DialogueSpeaker.Player, .Content = "……", .CD = 15})
        App.Session.BeginEdit()
        stage.Dialogues.Add(dialogue)
        App.Session.CommitEdit()
        App.Session.SelectItem(dialogue)
        Refresh()
    End Sub

    Private Sub EditDialogue(dialogue As DialogueDefinition)
        If dialogue Is Nothing Then Return
        Dim editor As New DialogueEditorWindow(dialogue)
        editor.Owner = Window.GetWindow(Me)
        If editor.ShowDialog() <> True Then Return
        App.Session.BeginEdit()
        dialogue.Name = editor.ResultDialogue.Name
        dialogue.Lines = editor.ResultDialogue.Lines
        dialogue.AfterAction = editor.ResultDialogue.AfterAction
        dialogue.BossTemplateId = editor.ResultDialogue.BossTemplateId
        App.Session.CommitEdit()
        App.Session.SelectItem(dialogue)
        Refresh()
    End Sub

    Private Sub ShowDialogueMenu(dialogue As DialogueDefinition, target As FrameworkElement)
        Dim menu As New ContextMenu()
        menu.Items.Add(MakeMenuItem("编辑对话", Sub() EditDialogue(dialogue)))
        menu.Items.Add(MakeMenuItem("重命名…", Sub() RenameDialogue(dialogue)))
        menu.Items.Add(MakeMenuItem("复制对话", Sub() DuplicateDialogue(dialogue)))
        menu.Items.Add(New Separator())
        menu.Items.Add(MakeMenuItem("删除对话", Sub() DeleteDialogue(dialogue)))
        menu.PlacementTarget = target
        menu.IsOpen = True
    End Sub

    Private Sub RenameDialogue(dialogue As DialogueDefinition)
        Dim value = InputDialog.ShowInput(Window.GetWindow(Me), "重命名对话", "名称：", dialogue.Name,
                                          Function(text) If(String.IsNullOrWhiteSpace(text), "名称不能为空。", Nothing))
        If value Is Nothing Then Return
        App.Session.BeginEdit()
        dialogue.Name = value.Trim()
        App.Session.CommitEdit()
        Refresh()
    End Sub

    Private Sub DuplicateDialogue(dialogue As DialogueDefinition)
        Dim copy = ProjectJson.Clone(dialogue)
        copy.Id = Ids.NewId("dialog")
        copy.Name = dialogue.Name & " 副本"
        App.Session.BeginEdit()
        App.Session.Stage.Dialogues.Add(copy)
        App.Session.CommitEdit()
        App.Session.SelectItem(copy)
        Refresh()
    End Sub

    Private Sub DeleteDialogue(dialogue As DialogueDefinition)
        If MessageBox.Show(Window.GetWindow(Me), "确定删除对话【" & dialogue.Name & "】吗？", "删除对话", MessageBoxButton.YesNo, MessageBoxImage.Warning) <> MessageBoxResult.Yes Then Return
        App.Session.BeginEdit()
        App.Session.Stage.Dialogues.Remove(dialogue)
        App.Session.CommitEdit()
        App.Session.SelectItem(App.Session.Stage)
        Refresh()
    End Sub

    Private Sub ShowSpellMenu(spell As BossSpellCard, target As FrameworkElement)
        Dim menu As New ContextMenu()
        menu.Items.Add(MakeMenuItem("编辑符卡积木", Sub() App.Session.EditSpellBehavior(spell)))
        menu.Items.Add(MakeMenuItem("重命名…", Sub() RenameSpell(spell)))
        menu.Items.Add(MakeMenuItem("复制符卡", Sub() DuplicateSpell(spell)))
        menu.Items.Add(New Separator())
        menu.Items.Add(MakeMenuItem("删除符卡", Sub() DeleteSpell(spell)))
        menu.PlacementTarget = target
        menu.IsOpen = True
    End Sub

    Private Sub RenameSpell(spell As BossSpellCard)
        Dim value = InputDialog.ShowInput(Window.GetWindow(Me), "重命名符卡", "名称：", spell.Name,
                                          Function(text) If(String.IsNullOrWhiteSpace(text), "名称不能为空。", Nothing))
        If value Is Nothing Then Return
        App.Session.BeginEdit()
        spell.Name = value.Trim()
        App.Session.CommitEdit()
        Refresh()
    End Sub

    Private Sub DuplicateSpell(spell As BossSpellCard)
        Dim copy = ProjectJson.Clone(spell)
        copy.Id = Ids.NewId("spell")
        copy.Name = spell.Name & " 副本"
        App.Session.BeginEdit()
        App.Session.Stage.SpellCards.Add(copy)
        App.Session.CommitEdit()
        App.Session.SelectItem(copy)
        Refresh()
    End Sub

    Private Sub DeleteSpell(spell As BossSpellCard)
        If MessageBox.Show(Window.GetWindow(Me), "确定删除符卡【" & spell.Name & "】吗？" & vbCrLf & "所有 Boss 中的引用也会移除。", "删除符卡", MessageBoxButton.YesNo, MessageBoxImage.Warning) <> MessageBoxResult.Yes Then Return
        Dim stage = App.Session.Stage
        App.Session.BeginEdit()
        stage.SpellCards.Remove(spell)
        For Each tmpl In stage.EnemyTemplates
            tmpl.SpellCardIds.Remove(spell.Id)
        Next
        App.Session.CommitEdit()
        App.Session.SelectItem(stage)
        Refresh()
    End Sub

    Private Sub RenameTemplate(tmpl As EnemyTemplate)
        Dim value = InputDialog.ShowInput(Window.GetWindow(Me), "重命名敌机模板", "名称：", tmpl.Name,
                                          Function(text) If(String.IsNullOrWhiteSpace(text), "名称不能为空。", Nothing))
        If value Is Nothing Then Return
        App.Session.BeginEdit()
        tmpl.Name = value.Trim()
        App.Session.CommitEdit()
        Refresh()
    End Sub

    Private Sub RenamePattern(pattern As BulletPattern)
        Dim value = InputDialog.ShowInput(Window.GetWindow(Me), "重命名子弹样式", "名称：", pattern.Name,
                                          Function(text) If(String.IsNullOrWhiteSpace(text), "名称不能为空。", Nothing))
        If value Is Nothing Then Return
        App.Session.BeginEdit()
        pattern.Name = value.Trim()
        App.Session.CommitEdit()
        Refresh()
    End Sub

    Private Sub DuplicateTemplate(tmpl As EnemyTemplate)
        Dim copy = ProjectJson.Clone(tmpl)
        copy.Id = Ids.NewId("enemy")
        copy.Name = tmpl.Name & " 副本"
        App.Session.BeginEdit()
        App.Session.Stage.EnemyTemplates.Add(copy)
        App.Session.CommitEdit()
        App.Session.SelectItem(copy)
        Refresh()
    End Sub

    Private Sub DuplicatePattern(pattern As BulletPattern)
        Dim copy = ProjectJson.Clone(pattern)
        copy.Id = Ids.NewId("bullet")
        copy.Name = pattern.Name & " 副本"
        App.Session.BeginEdit()
        App.Session.Stage.BulletPatterns.Add(copy)
        App.Session.CommitEdit()
        App.Session.SelectItem(copy)
        Refresh()
    End Sub

    Private Sub DeleteTemplate(tmpl As EnemyTemplate)
        Dim stage = App.Session.Stage
        Dim usedBy = stage.Tracks.Where(Function(t) t.TemplateId = tmpl.Id).ToList()
        Dim message = "确定删除敌机模板【" & tmpl.Name & "】吗？"
        If usedBy.Count > 0 Then message &= vbCrLf & "有 " & usedBy.Count & " 条轨道正在使用它，删除后这些轨道将失去模板。"
        If MessageBox.Show(Window.GetWindow(Me), message, "删除敌机模板", MessageBoxButton.YesNo, MessageBoxImage.Warning) <> MessageBoxResult.Yes Then Return
        App.Session.BeginEdit()
        stage.EnemyTemplates.Remove(tmpl)
        For Each track In usedBy
            track.TemplateId = ""
        Next
        App.Session.CommitEdit()
        App.Session.SelectItem(stage)
        Refresh()
    End Sub

    Private Sub DeletePattern(pattern As BulletPattern)
        If MessageBox.Show(Window.GetWindow(Me), "确定删除子弹样式【" & pattern.Name & "】吗？" & vbCrLf & "引用它的发射积木会失效。", "删除子弹样式", MessageBoxButton.YesNo, MessageBoxImage.Warning) <> MessageBoxResult.Yes Then Return
        App.Session.BeginEdit()
        App.Session.Stage.BulletPatterns.Remove(pattern)
        App.Session.CommitEdit()
        App.Session.SelectItem(App.Session.Stage)
        Refresh()
    End Sub

    Private Function MakeMenuItem(header As String, action As Action) As MenuItem
        Dim item As New MenuItem With {.Header = header}
        AddHandler item.Click, Sub(sender As Object, e As RoutedEventArgs) action.Invoke()
        Return item
    End Function

    Public Sub AddNewTemplate()
        AddTemplate()
    End Sub

    Public Sub AddNewPattern()
        AddPattern()
    End Sub

    Private Sub AddStage()
        If App.Session.Project Is Nothing Then Return
        Dim defaultName = "关卡 " & (App.Session.Project.Stages.Count + 1)
        Dim name = InputDialog.ShowInput(Window.GetWindow(Me), "新建关卡", "关卡名称：", defaultName,
                                         Function(text) If(String.IsNullOrWhiteSpace(text), "名称不能为空。", Nothing))
        If name Is Nothing Then Return
        App.Session.AddStage(name)
        Refresh()
    End Sub

    Public Sub AddNewStage()
        AddStage()
    End Sub

    Private Sub RenameStage(stageProject As StageProject)
        If stageProject Is Nothing Then Return
        Dim name = InputDialog.ShowInput(Window.GetWindow(Me), "重命名关卡", "关卡名称：", stageProject.Name,
                                         Function(text) If(String.IsNullOrWhiteSpace(text), "名称不能为空。", Nothing))
        If name Is Nothing Then Return
        App.Session.BeginEdit()
        stageProject.Name = name
        stageProject.Stage.Name = name
        App.Session.CommitEdit()
        Refresh()
    End Sub

    Private Sub DuplicateStage(stageProject As StageProject)
        If stageProject Is Nothing Then Return
        App.Session.DuplicateStage(stageProject)
        Refresh()
    End Sub

    Private Sub DeleteStage(stageProject As StageProject)
        If stageProject Is Nothing Then Return
        If App.Session.Project.Stages.Count <= 1 Then
            MessageBox.Show(Window.GetWindow(Me), "至少要保留一个关卡。", "删除关卡", MessageBoxButton.OK, MessageBoxImage.Information)
            Return
        End If
        If MessageBox.Show(Window.GetWindow(Me), "确定删除关卡【" & stageProject.Name & "】吗？", "删除关卡",
                           MessageBoxButton.YesNo, MessageBoxImage.Warning) <> MessageBoxResult.Yes Then Return
        App.Session.DeleteStage(stageProject)
        Refresh()
    End Sub

End Class
