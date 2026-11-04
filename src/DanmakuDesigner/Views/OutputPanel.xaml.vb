Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Input
Imports System.Windows.Media
Imports DanmakuDesigner.Core

Public Class OutputPanel

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
        Dim issues = If(App.Session.Issues, New List(Of ValidationIssue))
        Dim errors = issues.Where(Function(i) i.Severity = IssueSeverity.[Error]).Count()
        Dim warnings = issues.Where(Function(i) i.Severity = IssueSeverity.Warning).Count()
        Dim infos = issues.Where(Function(i) i.Severity = IssueSeverity.Info).Count()
        ErrorCountText.Text = errors & " 错误"
        WarningCountText.Text = warnings & " 警告"
        InfoCountText.Text = (infos + App.Session.Logs.Count) & " 信息"
        ErrorBadge.Background = Ui.Brush(If(errors > 0, "#4A2020", "#2A2A2B"))
        WarningBadge.Background = Ui.Brush(If(warnings > 0, "#403618", "#2A2A2B"))

        ListHost.Children.Clear()
        Dim rows As New List(Of ValidationIssue)
        rows.AddRange(issues)
        If ProblemsOnlyCheck IsNot Nothing AndAlso ProblemsOnlyCheck.IsChecked = False Then
            rows.AddRange(App.Session.Logs.Skip(Math.Max(0, App.Session.Logs.Count - 80)))
        End If
        rows = rows.OrderByDescending(Function(i) CInt(i.Severity)).ThenBy(Function(i) i.ObjectName).ToList()

        If rows.Count = 0 Then
            ListHost.Children.Add(New TextBlock With {
                .Text = "没有输出。按 F7 或点击刷新按钮重新验证工程。",
                .FontSize = 11,
                .Foreground = Ui.Brush("#7A7A7A"),
                .Margin = New Thickness(12, 8, 12, 4)
            })
            Return
        End If

        For Each issue In rows
            ListHost.Children.Add(MakeRow(issue))
        Next
    End Sub

    Private Function MakeRow(issue As ValidationIssue) As Border
        Dim border As New Border With {
            .Height = 23,
            .Background = Brushes.Transparent,
            .Cursor = If(issue.TargetKind = "log", Cursors.Arrow, Cursors.Hand),
            .Tag = issue,
            .ToolTip = issue.Message
        }
        Dim grid As New Grid()
        grid.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(24)})
        grid.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})
        grid.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(160)})
        Dim iconName As String
        Dim color As String
        Select Case issue.Severity
            Case IssueSeverity.[Error]
                iconName = "StatusCriticalError_16x.png"
                color = "#F48771"
            Case IssueSeverity.Warning
                iconName = "StatusWarning_16x.png"
                color = "#DCDCAA"
            Case Else
                iconName = "StatusInformation_16x.png"
                color = "#9A9A9A"
        End Select
        Dim img = Icon(iconName, 14)
        If img IsNot Nothing Then
            img.HorizontalAlignment = HorizontalAlignment.Center
            grid.Children.Add(img)
        End If
        Dim message As New TextBlock With {
            .Text = issue.Message,
            .FontSize = 11,
            .Foreground = Ui.Brush(color),
            .VerticalAlignment = VerticalAlignment.Center,
            .TextTrimming = TextTrimming.CharacterEllipsis,
            .Margin = New Thickness(2, 0, 8, 0)
        }
        Grid.SetColumn(message, 1)
        grid.Children.Add(message)
        Dim location As New TextBlock With {
            .Text = If(issue.TargetKind = "", issue.ObjectName, issue.LocationText),
            .FontSize = 9.5,
            .Foreground = Ui.Brush("#7A7A7A"),
            .VerticalAlignment = VerticalAlignment.Center,
            .TextTrimming = TextTrimming.CharacterEllipsis,
            .Margin = New Thickness(0, 0, 8, 0)
        }
        Grid.SetColumn(location, 2)
        grid.Children.Add(location)
        border.Child = grid
        AddHandler border.MouseEnter, Sub() border.Background = Ui.Brush("#2F2F32")
        AddHandler border.MouseLeave, Sub() border.Background = Brushes.Transparent
        AddHandler border.MouseLeftButtonDown, Sub(sender As Object, e As MouseButtonEventArgs)
                                                   If e.ClickCount = 2 Then
                                                       Navigate(issue)
                                                   End If
                                                   e.Handled = True
                                               End Sub
        Return border
    End Function

    Private Sub Navigate(issue As ValidationIssue)
        If issue Is Nothing Then Return
        If issue.Tick >= 0 Then
            App.Session.SetView("timeline")
            App.Session.SeekPreview(issue.Tick)
            Return
        End If
        Select Case issue.TargetKind
            Case "template"
                Dim template = App.Session.Stage.FindTemplate(issue.TargetId)
                If template IsNot Nothing Then App.Session.SelectItem(template)
            Case "pattern"
                Dim pattern = App.Session.Stage.FindPattern(issue.TargetId)
                If pattern IsNot Nothing Then App.Session.SelectItem(pattern)
            Case "track"
                Dim track = App.Session.Stage.FindTrack(issue.TargetId)
                If track IsNot Nothing Then
                    App.Session.SelectItem(track)
                    App.Session.SetView("timeline")
                End If
            Case "clip"
                Dim clip = App.Session.Stage.FindClip(issue.TargetId)
                If clip IsNot Nothing Then
                    App.Session.SelectItem(clip)
                    App.Session.SetView("timeline")
                    App.Session.SeekPreview(clip.StartTick)
                End If
            Case "block"
                Dim template = App.Session.Stage.FindTemplate(issue.TargetId)
                If template IsNot Nothing Then
                    App.Session.EditTemplateBehavior(template)
                    Return
                End If
                Dim pattern = App.Session.Stage.FindPattern(issue.TargetId)
                If pattern IsNot Nothing Then
                    App.Session.EditPatternBehavior(pattern)
                    Return
                End If
            Case Else
                App.Session.SelectItem(App.Session.Stage)
                App.Session.SetView("timeline")
        End Select
    End Sub

    Private Sub Validate_Click(sender As Object, e As RoutedEventArgs)
        App.Session.Validate()
        App.Session.AddLog(IssueSeverity.Info, "已重新验证工程。")
        Refresh()
    End Sub

    Private Sub Clear_Click(sender As Object, e As RoutedEventArgs)
        App.Session.Logs.Clear()
        Refresh()
    End Sub

    Private Sub Filter_Changed(sender As Object, e As RoutedEventArgs)
        Refresh()
    End Sub

End Class
