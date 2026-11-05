Imports System.Windows
Imports System.Windows.Controls
Imports DanmakuDesigner.Core

Public Class BlockPaletteView

    Public Property Editor As BlockEditorView
    Private _loaded As Boolean

    Public Sub New()
        InitializeComponent()
        AddHandler Loaded, Sub()
            _loaded = True
            RefreshPalette()
        End Sub
    End Sub

    Public Sub RefreshPalette()
        If Not _loaded Then Return
        Dim context = App.Session.BlockContext
        PaletteHost.Children.Clear()
        SearchHint.Visibility = If(String.IsNullOrEmpty(SearchBox.Text), Visibility.Visible, Visibility.Collapsed)
        If context Is Nothing OrElse Editor Is Nothing Then
            ContextText.Text = "未选择行为"
            AddEmptyState()
            Return
        End If
        ContextText.Text = If(context.Scope = ProgramScope.Enemy, "敌机行为",
                              If(context.Scope = ProgramScope.Particle, "粒子行为", "子弹行为"))
        Dim filter = If(SearchBox.Text, "").Trim()
        Dim any = False

        Dim eventDefs = BlockRegistry.Palette(BlockCategory.Trigger, context.Scope, True, context.IsBoss, context.ParticleIs3D).Where(Function(d) Matches(d, filter)).ToList()
        If eventDefs.Count > 0 Then
            PaletteHost.Children.Add(GroupHeader("事件积木", "脚本的开头", "#C792EA", "拖入画布会创建新脚本"))
            For Each def In eventDefs
                PaletteHost.Children.Add(MakePaletteItem(def))
            Next
            any = True
        End If

        Dim motionDefs = BlockRegistry.Palette(BlockCategory.Motion, context.Scope, False, context.IsBoss, context.ParticleIs3D).Where(Function(d) Matches(d, filter)).ToList()
        If motionDefs.Count > 0 Then
            PaletteHost.Children.Add(GroupHeader("运动", "速度 / 方向 / 位置", "#4FC1FF", Nothing))
            For Each def In motionDefs
                PaletteHost.Children.Add(MakePaletteItem(def))
            Next
            any = True
        End If

        Dim attackDefs = BlockRegistry.Palette(BlockCategory.Attack, context.Scope, False, context.IsBoss, context.ParticleIs3D).Where(Function(d) Matches(d, filter)).ToList()
        If attackDefs.Count > 0 Then
            PaletteHost.Children.Add(GroupHeader("攻击 / 演出", "弹幕、激光、音效", "#F48771", Nothing))
            For Each def In attackDefs
                PaletteHost.Children.Add(MakePaletteItem(def))
            Next
            any = True
        End If

        Dim controlDefs = BlockRegistry.Palette(BlockCategory.Control, context.Scope, False, context.IsBoss, context.ParticleIs3D).Where(Function(d) Matches(d, filter)).ToList()
        If controlDefs.Count > 0 Then
            PaletteHost.Children.Add(GroupHeader("控制 / 变量", "条件、循环、等待", "#E2C08D", Nothing))
            For Each def In controlDefs
                PaletteHost.Children.Add(MakePaletteItem(def))
            Next
            any = True
        End If

        Dim particleDefs = BlockRegistry.Palette(BlockCategory.Particle, context.Scope, False, context.IsBoss, context.ParticleIs3D).Where(Function(d) Matches(d, filter)).ToList()
        If particleDefs.Count > 0 Then
            PaletteHost.Children.Add(GroupHeader("粒子", "速度、位置、尺寸、颜色", "#9CDCFE", Nothing))
            For Each def In particleDefs
                PaletteHost.Children.Add(MakePaletteItem(def))
            Next
            any = True
        End If

        If Not any Then
            PaletteHost.Children.Add(New TextBlock With {
                .Text = "没有匹配的积木。",
                .FontSize = 11,
                .Foreground = Ui.Brush("#7A7A7A"),
                .Margin = New Thickness(4, 12, 4, 4)
            })
        End If
    End Sub

    Private Function Matches(def As BlockDefinition, filter As String) As Boolean
        If String.IsNullOrEmpty(filter) Then Return True
        Return def.Title.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 OrElse
               def.Tooltip.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0
    End Function

    Private Function MakePaletteItem(def As BlockDefinition) As Border
        Dim node = BlockRegistry.CreateBlock(def.TypeId, App.Session.Stage)
        Dim visual As New BlockVisual(node, def, Editor, True, Nothing, 0) With {
            .MaxWidth = 292,
            .HorizontalAlignment = HorizontalAlignment.Left,
            .ToolTip = def.Tooltip & vbCrLf & vbCrLf & "拖入画布，或单击追加。"
        }
        Return visual
    End Function

    Private Function GroupHeader(title As String, subtitle As String, color As String, hint As String) As Border
        Dim border As New Border With {
            .Margin = New Thickness(0, 8, 0, 6),
            .Padding = New Thickness(0, 0, 0, 4),
            .BorderBrush = Ui.Brush("#3A3A3D"),
            .BorderThickness = New Thickness(0, 0, 0, 1)
        }
        Dim grid As New Grid()
        grid.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(3)})
        grid.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})
        grid.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})
        Dim accent As New Border With {.Background = Ui.Brush(color), .CornerRadius = New CornerRadius(1), .Margin = New Thickness(0, 2, 7, 2)}
        grid.Children.Add(accent)
        Dim panel As New StackPanel With {.Margin = New Thickness(0, 0, 0, 2)}
        panel.Children.Add(New TextBlock With {
            .Text = title,
            .FontSize = 11,
            .FontWeight = FontWeights.SemiBold,
            .Foreground = Ui.Brush("#DADADA")
        })
        panel.Children.Add(New TextBlock With {
            .Text = subtitle,
            .FontSize = 9,
            .Foreground = Ui.Brush("#7A7A7A"),
            .Margin = New Thickness(0, 1, 0, 0)
        })
        Grid.SetColumn(panel, 1)
        grid.Children.Add(panel)
        If Not String.IsNullOrEmpty(hint) Then
            Dim hintText As New TextBlock With {
                .Text = hint,
                .FontSize = 8.5,
                .Foreground = Ui.Brush("#7F8FA6"),
                .VerticalAlignment = VerticalAlignment.Center
            }
            Grid.SetColumn(hintText, 2)
            grid.Children.Add(hintText)
        End If
        border.Child = grid
        Return border
    End Function

    Private Sub AddEmptyState()
        Dim panel As New StackPanel With {.Margin = New Thickness(6, 18, 6, 6)}
        Dim img = Icon("Toolbox_16x.png", 28)
        If img IsNot Nothing Then
            img.HorizontalAlignment = HorizontalAlignment.Left
            img.Opacity = 0.6
            panel.Children.Add(img)
        End If
        panel.Children.Add(New TextBlock With {
            .Text = "还没有打开行为",
            .FontSize = 13,
            .FontWeight = FontWeights.SemiBold,
            .Foreground = Ui.Brush("#D0D0D0"),
            .Margin = New Thickness(0, 10, 0, 4)
        })
        panel.Children.Add(New TextBlock With {
            .Text = "在左侧资源管理器中双击敌机模板或子弹样式，积木库会显示适用的积木。",
            .FontSize = 10.5,
            .Foreground = Ui.Brush("#8A8A8A"),
            .TextWrapping = TextWrapping.Wrap,
            .LineHeight = 17
        })
        PaletteHost.Children.Add(panel)
    End Sub

    Private Sub Search_Changed(sender As Object, e As TextChangedEventArgs)
        If Not _loaded Then Return
        SearchHint.Visibility = If(String.IsNullOrEmpty(SearchBox.Text), Visibility.Visible, Visibility.Collapsed)
        RefreshPalette()
    End Sub

End Class
