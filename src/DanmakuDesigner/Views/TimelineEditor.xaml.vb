Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Controls.Primitives
Imports System.Windows.Input
Imports System.Windows.Media
Imports System.Windows.Shapes
Imports DanmakuDesigner.Core

Public Class TimelineEditor

    Private Const RowHeight As Double = 48
    Private Const RulerHeight As Double = 30
    Private Const BasePx As Double = 0.75

    Private _px As Double = BasePx
    Private _contentWidth As Double = 900
    Private _playhead As Long = 0
    Private _selectedClip As TimelineClip = Nothing
    Private _selectedTrack As TimelineTrack = Nothing
    Private ReadOnly _clipVisuals As New Dictionary(Of TimelineClip, ClipUi)
    Private ReadOnly _trackHeaders As New Dictionary(Of TimelineTrack, Border)
    Private _rulerPlayhead As Line
    Private _rulerHead As Polygon
    Private _lanePlayhead As Line
    Private _drag As DragState
    Private _seeking As Boolean
    Private _lastSeek As DateTime = DateTime.MinValue
    Private _loaded As Boolean
    Private _suppressScrollSync As Boolean

    Public Sub New()
        InitializeComponent()
        AddHandler Loaded, AddressOf Timeline_Loaded
        AddHandler Unloaded, AddressOf Timeline_Unloaded
        AddHandler PreviewKeyDown, AddressOf Timeline_KeyDown
        AddHandler LaneScroll.ScrollChanged, AddressOf LaneScroll_ScrollChanged
        AddHandler LaneScroll.PreviewMouseWheel, AddressOf LaneScroll_PreviewMouseWheel
        AddHandler LaneCanvas.MouseLeftButtonDown, AddressOf Lane_MouseLeftButtonDown
        AddHandler LaneCanvas.MouseRightButtonDown, AddressOf Lane_MouseRightButtonDown
        AddHandler RulerCanvas.MouseLeftButtonDown, AddressOf Ruler_MouseLeftButtonDown
        AddHandler RulerCanvas.MouseMove, AddressOf Ruler_MouseMove
        AddHandler RulerCanvas.MouseLeftButtonUp, AddressOf Ruler_MouseLeftButtonUp
        AddHandler RulerCanvas.MouseLeave, AddressOf Ruler_MouseLeave
    End Sub

    Private Sub Timeline_Loaded(sender As Object, e As RoutedEventArgs)
        _loaded = True
        Refresh()
    End Sub

    Private Sub Timeline_Unloaded(sender As Object, e As RoutedEventArgs)
        _loaded = False
    End Sub

    ' ============================================================
    ' 公共接口
    ' ============================================================

    Public Sub Refresh()
        If Not _loaded Then Return
        Dim stage = App.Session.Stage
        If stage Is Nothing Then
            LaneCanvas.Children.Clear()
            RulerCanvas.Children.Clear()
            HeaderHost.Children.Clear()
            Return
        End If

        Dim hOffset = LaneScroll.HorizontalOffset
        Dim vOffset = LaneScroll.VerticalOffset
        _selectedClip = TryCast(App.Session.Selection, TimelineClip)
        _selectedTrack = TryCast(App.Session.Selection, TimelineTrack)
        If _selectedTrack Is Nothing AndAlso _selectedClip IsNot Nothing Then _selectedTrack = stage.TrackOfClip(_selectedClip)
        _suppressScrollSync = True
        Try
            _contentWidth = Math.Max(400, Math.Max(LaneScroll.ViewportWidth - 4, stage.LengthTicks * _px + 120))
            RulerCanvas.Width = _contentWidth
            RulerCanvas.Height = RulerHeight
            LaneCanvas.Width = _contentWidth
            LaneCanvas.Height = Math.Max(240, stage.Tracks.Count * RowHeight + 24)

            BuildRuler(stage)
            BuildHeaders(stage)
            BuildLanes(stage)
            SetPlayhead(_playhead, False)
            TrackCountText.Text = stage.Tracks.Count.ToString()
            ZoomText.Text = Math.Round(_px / BasePx * 100).ToString() & "%"
            If SnapCheck IsNot Nothing Then
                SnapCheck.ToolTip = "拖动/缩放时吸附到网格（" & SnapGridStep() & " 帧）；按住 Alt 临时关闭吸附，Shift 吸附到主刻度。"
            End If
            UpdateSelectionVisuals()
        Finally
            _suppressScrollSync = False
        End Try

        Try
            LaneScroll.ScrollToHorizontalOffset(Math.Min(hOffset, Math.Max(0, LaneCanvas.Width - LaneScroll.ViewportWidth)))
            LaneScroll.ScrollToVerticalOffset(vOffset)
            RulerScroll.ScrollToHorizontalOffset(LaneScroll.HorizontalOffset)
            HeaderScroll.ScrollToVerticalOffset(LaneScroll.VerticalOffset)
        Catch
        End Try
    End Sub

    Public Sub SetPlayhead(tick As Long, Optional showInView As Boolean = False)
        Dim stage = App.Session.Stage
        Dim length = If(stage Is Nothing, 3600, stage.LengthTicks)
        _playhead = Math.Max(0, Math.Min(length, tick))
        Dim x = _playhead * _px
        If _rulerPlayhead IsNot Nothing Then
            _rulerPlayhead.X1 = x
            _rulerPlayhead.X2 = x
            Canvas.SetLeft(_rulerHead, x - 5)
        End If
        If _lanePlayhead IsNot Nothing Then
            _lanePlayhead.X1 = x
            _lanePlayhead.X2 = x
        End If
        PlayheadText.Text = _playhead.ToString("N0") & " 帧 · " & TimeSpan.FromSeconds(_playhead / 60.0).ToString("m\:ss\.ff")
        If showInView Then
            Dim offset = x
            If offset < LaneScroll.HorizontalOffset Then
                LaneScroll.ScrollToHorizontalOffset(Math.Max(0, offset - 60))
            ElseIf offset > LaneScroll.HorizontalOffset + LaneScroll.ViewportWidth - 60 Then
                LaneScroll.ScrollToHorizontalOffset(offset - LaneScroll.ViewportWidth + 80)
            End If
        End If
    End Sub

    Public Sub UpdateSelection()
        _selectedClip = TryCast(App.Session.Selection, TimelineClip)
        _selectedTrack = TryCast(App.Session.Selection, TimelineTrack)
        If _selectedTrack Is Nothing AndAlso _selectedClip IsNot Nothing Then
            _selectedTrack = App.Session.Stage.TrackOfClip(_selectedClip)
        End If
        UpdateSelectionVisuals()
    End Sub

    Public Sub ScrollToTrack(track As TimelineTrack)
        Dim stage = App.Session.Stage
        If stage Is Nothing OrElse track Is Nothing Then Return
        Dim index = stage.Tracks.IndexOf(track)
        If index >= 0 Then
            LaneScroll.ScrollToVerticalOffset(index * RowHeight)
        End If
    End Sub

    ' ============================================================
    ' 绘制
    ' ============================================================

    Private Function TickStep() As Integer
        For Each candidate In {15, 30, 60, 120, 300, 600, 1200, 1800, 3600}
            If candidate * _px >= 54 Then Return candidate
        Next
        Return 3600
    End Function

    ''' <summary>吸附步长与时间轴实际绘制的小网格保持一致。</summary>
    Private Function SnapGridStep() As Integer
        Return Math.Max(1, TickStep() \ 2)
    End Function

    Private Sub BuildRuler(stage As StageDocument)
        RulerCanvas.Children.Clear()
        _rulerPlayhead = Nothing
        _rulerHead = Nothing
        Dim stepTicks = TickStep()
        Dim majorStep = stepTicks
        Dim minorStep = Math.Max(1, stepTicks \ 2)
        Dim length = stage.LengthTicks
        Dim height = RulerHeight

        For t = 0 To length Step minorStep
            Dim x = t * _px
            Dim major = (t Mod majorStep = 0)
            Dim line As New Line With {
                .X1 = x,
                .X2 = x,
                .Y1 = If(major, 17.0, 24.0),
                .Y2 = height,
                .Stroke = Ui.Brush(If(major, "#5C5C60", "#3C3C40")),
                .StrokeThickness = 1,
                .SnapsToDevicePixels = True
            }
            RulerCanvas.Children.Add(line)
            If major Then
                Dim label As New TextBlock With {
                    .Text = If(majorStep >= 60, TimeSpan.FromSeconds(t / 60.0).ToString("m\:ss"), t.ToString()),
                    .FontSize = 10,
                    .FontFamily = New FontFamily("Consolas"),
                    .Foreground = Ui.Brush("#A0A0A0"),
                    .IsHitTestVisible = False
                }
                Canvas.SetLeft(label, x + 4)
                Canvas.SetTop(label, 2)
                RulerCanvas.Children.Add(label)
            End If
        Next

        Dim endX = length * _px
        Dim endLine As New Line With {
            .X1 = endX, .X2 = endX, .Y1 = 0, .Y2 = height,
            .Stroke = Ui.Brush("#007ACC"), .StrokeThickness = 1, .Opacity = 0.6
        }
        RulerCanvas.Children.Add(endLine)

        _rulerPlayhead = New Line With {
            .X1 = _playhead * _px,
            .X2 = _playhead * _px,
            .Y1 = 0,
            .Y2 = height,
            .Stroke = Ui.Brush("#F14C4C"),
            .StrokeThickness = 1
        }
        RulerCanvas.Children.Add(_rulerPlayhead)
        _rulerHead = New Polygon With {
            .Points = New PointCollection({New Point(0, 0), New Point(10, 0), New Point(5, 8)}),
            .Fill = Ui.Brush("#F14C4C"),
            .IsHitTestVisible = False
        }
        Canvas.SetTop(_rulerHead, 1)
        Canvas.SetLeft(_rulerHead, _playhead * _px - 5)
        RulerCanvas.Children.Add(_rulerHead)
    End Sub

    Private Sub BuildHeaders(stage As StageDocument)
        HeaderHost.Children.Clear()
        _trackHeaders.Clear()
        For Each track In stage.Tracks
            Dim border As New Border With {
                .Height = RowHeight,
                .Background = Ui.Brush("#252526"),
                .BorderBrush = Ui.Brush("#303033"),
                .BorderThickness = New Thickness(0, 0, 0, 1),
                .Cursor = Cursors.Hand,
                .Tag = track,
                .Padding = New Thickness(0)
            }
            Dim grid As New Grid()
            grid.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(3)})
            grid.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(28)})
            grid.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})
            grid.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})

            Dim accent As New Border With {
                .Background = Ui.Brush(track.Accent),
                .CornerRadius = New CornerRadius(0, 1, 1, 0)
            }
            grid.Children.Add(accent)

            Dim img = Icon(If(track.Kind = TrackKind.Cue, "Timer_16x.png", If(track.Kind = TrackKind.Background, "Media_16x.png", "Bug_16x.png")), 16)
            If img IsNot Nothing Then
                img.Margin = New Thickness(4, 0, 0, 0)
                img.HorizontalAlignment = HorizontalAlignment.Left
                Grid.SetColumn(img, 1)
                grid.Children.Add(img)
            End If

            Dim textPanel As New StackPanel With {.VerticalAlignment = VerticalAlignment.Center, .Margin = New Thickness(0, 0, 4, 0)}
            Dim title As New TextBlock With {
                .Text = track.Name,
                .FontSize = 12,
                .Foreground = Ui.Brush("#E6E6E6"),
                .TextTrimming = TextTrimming.CharacterEllipsis
            }
            Dim subtitle As New TextBlock With {
                .Text = TrackSubtitle(track),
                .FontSize = 10,
                .Foreground = Ui.Brush("#8A8A8A"),
                .TextTrimming = TextTrimming.CharacterEllipsis,
                .Margin = New Thickness(0, 2, 0, 0)
            }
            textPanel.Children.Add(title)
            textPanel.Children.Add(subtitle)
            Grid.SetColumn(textPanel, 2)
            grid.Children.Add(textPanel)

            Dim actions As New StackPanel With {.Orientation = Orientation.Horizontal, .VerticalAlignment = VerticalAlignment.Center, .Margin = New Thickness(0, 0, 4, 0)}
            Dim capturedTrack = track
            actions.Children.Add(MakeTrackGlyphButton("M", If(track.IsMuted, "取消静音", "静音轨道"), If(track.IsMuted, "#E2C08D", "#777777"),
                                                      Sub() ToggleTrackFlag(capturedTrack, True)))
            actions.Children.Add(MakeTrackGlyphButton("L", If(track.IsLocked, "解锁轨道", "锁定轨道"), If(track.IsLocked, "#DCDCAA", "#777777"),
                                                      Sub() ToggleTrackFlag(capturedTrack, False)))
            Grid.SetColumn(actions, 3)
            grid.Children.Add(actions)

            border.Child = grid
            AddHandler border.MouseLeftButtonDown, Sub(sender2, e2) TrackHeader_Click(capturedTrack, e2)
            AddHandler border.MouseRightButtonDown, Sub(sender2, e2) TrackHeader_RightClick(capturedTrack, e2)
            HeaderHost.Children.Add(border)
            _trackHeaders(track) = border
        Next
        HeaderHost.Height = Math.Max(240, stage.Tracks.Count * RowHeight + 24)
    End Sub

    Private Function MakeTrackGlyphButton(glyph As String, tooltip As String, color As String, action As Action) As Border
        Dim border As New Border With {
            .Width = 18,
            .Height = 18,
            .Margin = New Thickness(2, 0, 0, 0),
            .CornerRadius = New CornerRadius(2),
            .Background = Brushes.Transparent,
            .Cursor = Cursors.Hand,
            .ToolTip = tooltip
        }
        border.Child = New TextBlock With {
            .Text = glyph,
            .FontSize = 9,
            .FontWeight = FontWeights.Bold,
            .Foreground = Ui.Brush(color),
            .HorizontalAlignment = HorizontalAlignment.Center,
            .VerticalAlignment = VerticalAlignment.Center
        }
        AddHandler border.MouseLeftButtonDown, Sub(sender As Object, e As MouseButtonEventArgs)
                                                   e.Handled = True
                                                   action.Invoke()
                                               End Sub
        AddHandler border.MouseEnter, Sub() border.Background = Ui.Brush("#3A3A3D")
        AddHandler border.MouseLeave, Sub() border.Background = Brushes.Transparent
        Return border
    End Function

    Private Function TrackSubtitle(track As TimelineTrack) As String
        If track.Kind = TrackKind.Cue Then
            Return "事件 · " & track.Clips.Count & " 个"
        End If
        If track.Kind = TrackKind.Background Then
            Dim layer = App.Session.Stage.FindBackgroundLayer(track.BackgroundLayerId)
            Dim layerName = If(layer Is Nothing, "（图层已删除）", layer.Name)
            Return layerName & " · " & track.Clips.Count & " 个关键帧"
        End If
        Dim tmpl = App.Session.Stage.FindTemplate(track.TemplateId)
        Dim name = If(tmpl Is Nothing, "（未绑定模板）", tmpl.Name)
        Return name & " · " & track.Clips.Count & " 个片段"
    End Function

    Private Sub BuildLanes(stage As StageDocument)
        LaneCanvas.Children.Clear()
        _clipVisuals.Clear()
        _lanePlayhead = Nothing

        Dim height = stage.Tracks.Count * RowHeight
        Dim stepTicks = TickStep()
        Dim minorStep = Math.Max(1, stepTicks \ 2)

        For i = 0 To stage.Tracks.Count - 1
            Dim track = stage.Tracks(i)
            Dim top = i * RowHeight
            Dim row As New Rectangle With {
                .Width = _contentWidth,
                .Height = RowHeight,
                .Fill = Ui.Brush(If(i Mod 2 = 0, "#1E1E1E", "#202021")),
                .IsHitTestVisible = False
            }
            Canvas.SetLeft(row, 0)
            Canvas.SetTop(row, top)
            LaneCanvas.Children.Add(row)

            If track Is _selectedTrack OrElse (App.Session.Selection IsNot Nothing AndAlso TypeOf App.Session.Selection Is TimelineClip AndAlso stage.TrackOfClip(TryCast(App.Session.Selection, TimelineClip)) Is track) Then
                Dim selectedRow As New Rectangle With {
                    .Width = _contentWidth,
                    .Height = RowHeight,
                    .Fill = Ui.Brush("#102B40"),
                    .IsHitTestVisible = False
                }
                Canvas.SetLeft(selectedRow, 0)
                Canvas.SetTop(selectedRow, top)
                LaneCanvas.Children.Add(selectedRow)
            End If

            Dim line As New Line With {
                .X1 = 0, .X2 = _contentWidth,
                .Y1 = top + RowHeight, .Y2 = top + RowHeight,
                .Stroke = Ui.Brush("#2E2E31"), .StrokeThickness = 1, .IsHitTestVisible = False
            }
            LaneCanvas.Children.Add(line)

            If track.IsMuted Then
                Dim muted As New Rectangle With {
                    .Width = _contentWidth,
                    .Height = RowHeight,
                    .Fill = Ui.Brush("#66000000"),
                    .IsHitTestVisible = False
                }
                Canvas.SetLeft(muted, 0)
                Canvas.SetTop(muted, top)
                LaneCanvas.Children.Add(muted)
            End If
        Next

        For t = 0 To stage.LengthTicks Step minorStep
            Dim x = t * _px
            Dim major = (t Mod stepTicks = 0)
            Dim line As New Line With {
                .X1 = x, .X2 = x, .Y1 = 0, .Y2 = Math.Max(height, LaneCanvas.Height),
                .Stroke = Ui.Brush(If(major, "#303033", "#28282A")),
                .StrokeThickness = 1,
                .IsHitTestVisible = False
            }
            LaneCanvas.Children.Add(line)
        Next

        For i = 0 To stage.Tracks.Count - 1
            Dim track = stage.Tracks(i)
            For Each tlClip In track.Clips
                Dim visual = CreateClipVisual(track, tlClip, i)
                _clipVisuals(tlClip) = visual
                LaneCanvas.Children.Add(visual.Border)
            Next
        Next

        _lanePlayhead = New Line With {
            .X1 = _playhead * _px,
            .X2 = _playhead * _px,
            .Y1 = 0,
            .Y2 = Math.Max(height, LaneCanvas.Height),
            .Stroke = Ui.Brush("#F14C4C"),
            .StrokeThickness = 1,
            .IsHitTestVisible = False
        }
        LaneCanvas.Children.Add(_lanePlayhead)
    End Sub

    Private Function CreateClipVisual(track As TimelineTrack, tlClip As TimelineClip, trackIndex As Integer) As ClipUi
        If track.Kind = TrackKind.Cue Then Return CreateCueMarker(track, tlClip, trackIndex)
        If track.Kind = TrackKind.Background Then Return CreateBackgroundKeyframe(track, tlClip, trackIndex)
        Dim accent = track.Accent
        Dim width = ClipWidth(track, tlClip)
        Dim border As New Border With {
            .Width = width,
            .Height = RowHeight - 14,
            .Background = Ui.Brush(AccentFill(accent, 70)),
            .BorderBrush = Ui.Brush(accent),
            .BorderThickness = New Thickness(1),
            .CornerRadius = New CornerRadius(3),
            .Cursor = Cursors.Hand,
            .Tag = tlClip,
            .Padding = New Thickness(0),
            .ToolTip = ClipTooltip(track, tlClip)
        }
        Canvas.SetLeft(border, tlClip.StartTick * _px)
        Canvas.SetTop(border, trackIndex * RowHeight + 7)

        Dim grid As New Grid()
        grid.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(4)})
        grid.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})
        Dim bar As New Border With {
            .Background = Ui.Brush(accent),
            .CornerRadius = New CornerRadius(2, 0, 0, 2)
        }
        grid.Children.Add(bar)

        Dim panel As New StackPanel With {.Margin = New Thickness(6, 3, 5, 2), .VerticalAlignment = VerticalAlignment.Center}
        Dim title As New TextBlock With {
            .Text = ClipTitle(track, tlClip),
            .FontSize = 11,
            .FontWeight = FontWeights.SemiBold,
            .Foreground = Ui.Brush("#F2F2F2"),
            .TextTrimming = TextTrimming.CharacterEllipsis
        }
        Dim subtitle As New TextBlock With {
            .Text = ClipSubtitle(track, tlClip),
            .FontSize = 9.5,
            .Foreground = Ui.Brush("#D8D8D8"),
            .Opacity = 0.86,
            .TextTrimming = TextTrimming.CharacterEllipsis,
            .Margin = New Thickness(0, 1, 0, 0)
        }
        panel.Children.Add(title)
        panel.Children.Add(subtitle)
        Grid.SetColumn(panel, 1)
        grid.Children.Add(panel)
        border.Child = grid

        Dim clipUi As New ClipUi With {
            .Clip = tlClip,
            .Track = track,
            .Border = border,
            .Title = title,
            .Subtitle = subtitle,
            .OffsetX = 0
        }
        AddHandler border.MouseLeftButtonDown, Sub(sender As Object, e As MouseButtonEventArgs) Clip_MouseDown(clipUi, e)
        AddHandler border.MouseMove, Sub(sender As Object, e As MouseEventArgs) Clip_MouseMove(clipUi, e)
        AddHandler border.MouseLeftButtonUp, Sub(sender As Object, e As MouseButtonEventArgs) Clip_MouseUp(clipUi, e)
        AddHandler border.MouseRightButtonDown, Sub(sender As Object, e As MouseButtonEventArgs) Clip_RightClick(clipUi, e)
        AddHandler border.MouseEnter, Sub()
                                          If _selectedClip IsNot tlClip Then border.Background = Ui.Brush(AccentFill(accent, 95))
                                      End Sub
        AddHandler border.MouseLeave, Sub()
                                          If _selectedClip IsNot tlClip Then border.Background = Ui.Brush(AccentFill(accent, 70))
                                      End Sub
        Return clipUi
    End Function

    Private Function ClipWidth(track As TimelineTrack, tlClip As TimelineClip) As Double
        If track IsNot Nothing AndAlso track.Kind = TrackKind.Background Then
            If tlClip IsNot Nothing AndAlso tlClip.IsLoopEvent Then Return Math.Max(126, Math.Max(1, tlClip.LoopLength) * _px + 28)
            Return Math.Max(90, Math.Max(1, tlClip.AnimationLength) * _px + 34)
        End If
        If tlClip.Count <= 1 Then Return 90
        Return Math.Max(90, tlClip.DurationTicks * _px + 34)
    End Function

    Private Function CreateBackgroundKeyframe(track As TimelineTrack, tlClip As TimelineClip, trackIndex As Integer) As ClipUi
        If tlClip.IsLoopEvent Then Return CreateLoopEvent(track, tlClip, trackIndex)
        Dim accent = track.Accent
        Dim border As New Border With {
            .Width = 126,
            .Height = RowHeight - 12,
            .Background = Brushes.Transparent,
            .Cursor = Cursors.Hand,
            .Tag = tlClip,
            .Padding = New Thickness(0),
            .ToolTip = ClipTooltip(track, tlClip)
        }
        Canvas.SetLeft(border, tlClip.StartTick * _px - 9)
        Canvas.SetTop(border, trackIndex * RowHeight + 6)

        Dim grid As New Grid()
        grid.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(18)})
        grid.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})
        Dim diamond As New Border With {
            .Width = 12,
            .Height = 12,
            .Background = Ui.Brush(accent),
            .BorderBrush = Ui.Brush("#F2F2F2"),
            .BorderThickness = New Thickness(1),
            .HorizontalAlignment = HorizontalAlignment.Left,
            .VerticalAlignment = VerticalAlignment.Center,
            .RenderTransformOrigin = New Point(0.5, 0.5),
            .RenderTransform = New RotateTransform(45)
        }
        grid.Children.Add(diamond)
        Dim title As New TextBlock With {
            .Text = ClipTitle(track, tlClip),
            .FontSize = 9.5,
            .Foreground = Ui.Brush("#D8D8D8"),
            .VerticalAlignment = VerticalAlignment.Center,
            .TextTrimming = TextTrimming.CharacterEllipsis,
            .Margin = New Thickness(3, 0, 0, 0)
        }
        Grid.SetColumn(title, 1)
        grid.Children.Add(title)
        border.Child = grid

        Dim clipUi As New ClipUi With {
            .Clip = tlClip,
            .Track = track,
            .Border = border,
            .Title = title,
            .Subtitle = Nothing,
            .OffsetX = 0,
            .IsKeyframe = True
        }
        AddHandler border.MouseLeftButtonDown, Sub(sender As Object, e As MouseButtonEventArgs) Clip_MouseDown(clipUi, e)
        AddHandler border.MouseMove, Sub(sender As Object, e As MouseEventArgs) Clip_MouseMove(clipUi, e)
        AddHandler border.MouseLeftButtonUp, Sub(sender As Object, e As MouseButtonEventArgs) Clip_MouseUp(clipUi, e)
        AddHandler border.MouseRightButtonDown, Sub(sender As Object, e As MouseButtonEventArgs) Clip_RightClick(clipUi, e)
        AddHandler border.MouseEnter, Sub() diamond.BorderBrush = Ui.Brush("#FFFFFF")
        AddHandler border.MouseLeave, Sub() diamond.BorderBrush = Ui.Brush("#F2F2F2")
        Return clipUi
    End Function

    Private Function CreateLoopEvent(track As TimelineTrack, tlClip As TimelineClip, trackIndex As Integer) As ClipUi
        Dim accent = track.Accent
        Dim width = Math.Max(126, Math.Max(1, tlClip.LoopLength) * _px + 28)
        Dim border As New Border With {
            .Width = width,
            .Height = RowHeight - 14,
            .Background = Ui.Brush(AccentFill(accent, 95)),
            .BorderBrush = Ui.Brush(accent),
            .BorderThickness = New Thickness(1),
            .CornerRadius = New CornerRadius(3),
            .Cursor = Cursors.Hand,
            .Tag = tlClip,
            .Padding = New Thickness(0),
            .ToolTip = ClipTooltip(track, tlClip)
        }
        Canvas.SetLeft(border, tlClip.StartTick * _px)
        Canvas.SetTop(border, trackIndex * RowHeight + 7)

        Dim grid As New Grid()
        grid.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(4)})
        grid.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(26)})
        grid.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})
        Dim bar As New Border With {
            .Background = Ui.Brush(accent),
            .CornerRadius = New CornerRadius(2, 0, 0, 2)
        }
        grid.Children.Add(bar)
        Dim glyph As New TextBlock With {
            .Text = "↻",
            .FontSize = 15,
            .FontWeight = FontWeights.SemiBold,
            .Foreground = Ui.Brush("#F2F2F2"),
            .HorizontalAlignment = HorizontalAlignment.Center,
            .VerticalAlignment = VerticalAlignment.Center
        }
        Grid.SetColumn(glyph, 1)
        grid.Children.Add(glyph)
        Dim panel As New StackPanel With {.Margin = New Thickness(4, 0, 6, 0), .VerticalAlignment = VerticalAlignment.Center}
        Dim title As New TextBlock With {
            .Text = ClipTitle(track, tlClip),
            .FontSize = 10.5,
            .FontWeight = FontWeights.SemiBold,
            .Foreground = Ui.Brush("#F2F2F2"),
            .TextTrimming = TextTrimming.CharacterEllipsis
        }
        Dim subtitle As New TextBlock With {
            .Text = ClipSubtitle(track, tlClip),
            .FontSize = 9,
            .Foreground = Ui.Brush("#D8D8D8"),
            .Opacity = 0.86,
            .TextTrimming = TextTrimming.CharacterEllipsis
        }
        panel.Children.Add(title)
        panel.Children.Add(subtitle)
        Grid.SetColumn(panel, 2)
        grid.Children.Add(panel)
        border.Child = grid

        Dim clipUi As New ClipUi With {
            .Clip = tlClip,
            .Track = track,
            .Border = border,
            .Title = title,
            .Subtitle = subtitle,
            .OffsetX = 0,
            .IsKeyframe = False
        }
        AddHandler border.MouseLeftButtonDown, Sub(sender As Object, e As MouseButtonEventArgs) Clip_MouseDown(clipUi, e)
        AddHandler border.MouseMove, Sub(sender As Object, e As MouseEventArgs) Clip_MouseMove(clipUi, e)
        AddHandler border.MouseLeftButtonUp, Sub(sender As Object, e As MouseButtonEventArgs) Clip_MouseUp(clipUi, e)
        AddHandler border.MouseRightButtonDown, Sub(sender As Object, e As MouseButtonEventArgs) Clip_RightClick(clipUi, e)
        AddHandler border.MouseEnter, Sub() If _selectedClip IsNot tlClip Then border.Background = Ui.Brush(AccentFill(accent, 130))
        AddHandler border.MouseLeave, Sub() If _selectedClip IsNot tlClip Then border.Background = Ui.Brush(AccentFill(accent, 95))
        Return clipUi
    End Function

    Private Function CreateCueMarker(track As TimelineTrack, tlClip As TimelineClip, trackIndex As Integer) As ClipUi
        Dim accent = track.Accent
        Dim glyph As String
        Select Case tlClip.Cue
            Case CueKind.Music : glyph = "♪"
            Case CueKind.ClearBullets : glyph = "清"
            Case CueKind.Shake : glyph = "震"
            Case CueKind.EndStage : glyph = "终"
            Case Else : glyph = "•"
        End Select
        Dim border As New Border With {
            .Width = 30,
            .Height = 30,
            .Background = Ui.Brush(AccentFill(accent, 165)),
            .BorderBrush = Ui.Brush(accent),
            .BorderThickness = New Thickness(1),
            .CornerRadius = New CornerRadius(15),
            .Cursor = Cursors.Hand,
            .Tag = tlClip,
            .ToolTip = ClipTooltip(track, tlClip),
            .Child = New TextBlock With {
                .Text = glyph,
                .FontSize = 12,
                .FontWeight = FontWeights.SemiBold,
                .Foreground = Ui.Brush("#FFFFFF"),
                .HorizontalAlignment = HorizontalAlignment.Center,
                .VerticalAlignment = VerticalAlignment.Center
            }
        }
        Dim offset = If(tlClip.StartTick * _px < 16, 2 - tlClip.StartTick * _px, -15.0)
        Dim markerLeft = tlClip.StartTick * _px + offset
        Canvas.SetLeft(border, markerLeft)
        Canvas.SetTop(border, trackIndex * RowHeight + 9)
        Dim clipUi As New ClipUi With {
            .Clip = tlClip,
            .Track = track,
            .Border = border,
            .Title = Nothing,
            .Subtitle = Nothing,
            .OffsetX = offset
        }
        AddHandler border.MouseLeftButtonDown, Sub(sender As Object, e As MouseButtonEventArgs) Clip_MouseDown(clipUi, e)
        AddHandler border.MouseMove, Sub(sender As Object, e As MouseEventArgs) Clip_MouseMove(clipUi, e)
        AddHandler border.MouseLeftButtonUp, Sub(sender As Object, e As MouseButtonEventArgs) Clip_MouseUp(clipUi, e)
        AddHandler border.MouseRightButtonDown, Sub(sender As Object, e As MouseButtonEventArgs) Clip_RightClick(clipUi, e)
        AddHandler border.MouseEnter, Sub()
                                          If _selectedClip IsNot tlClip Then border.Background = Ui.Brush(AccentFill(accent, 210))
                                      End Sub
        AddHandler border.MouseLeave, Sub()
                                          If _selectedClip IsNot tlClip Then border.Background = Ui.Brush(AccentFill(accent, 165))
                                      End Sub
        Return clipUi
    End Function

    Private Function ClipTitle(track As TimelineTrack, tlClip As TimelineClip) As String
        If track.Kind = TrackKind.Cue Then
            Select Case tlClip.Cue
                Case CueKind.Music : Return "♪ " & If(String.IsNullOrWhiteSpace(tlClip.CueText), "音乐", tlClip.CueText)
                Case CueKind.ClearBullets : Return "清除子弹"
                Case CueKind.Shake : Return "屏幕震动"
                Case CueKind.EndStage : Return "结束关卡"
            End Select
        End If
        If track.Kind = TrackKind.Background Then
            If tlClip.IsLoopEvent Then Return "循环 " & tlClip.LoopStartTick & "→" & tlClip.LoopEndTick
            Dim layer = App.Session.Stage.FindBackgroundLayer(track.BackgroundLayerId)
            Return If(layer Is Nothing, "关键帧 " & tlClip.StartTick & "f", tlClip.StartTick & "f")
        End If
        Dim tmpl = App.Session.Stage.FindTemplate(track.TemplateId)
        Return If(tmpl Is Nothing, track.Name, tmpl.Name)
    End Function

    Private Function ClipSubtitle(track As TimelineTrack, tlClip As TimelineClip) As String
        If track.Kind = TrackKind.Cue Then
            Return TimeSpan.FromSeconds(tlClip.StartTick / 60.0).ToString("m\:ss") & " · Tick " & tlClip.StartTick
        End If
        If track.Kind = TrackKind.Background Then
            If tlClip.IsLoopEvent Then Return "循环长度 " & tlClip.LoopLength & "f · 事件 " & tlClip.StartTick & "f"
            Return TimeSpan.FromSeconds(tlClip.StartTick / 60.0).ToString("m\:ss\.ff") & " · 关键帧"
        End If
        Dim parts As New List(Of String)
        If tlClip.Count > 1 Then
            parts.Add("×" & tlClip.Count)
            parts.Add("间隔 " & tlClip.IntervalTicks & "f")
        Else
            parts.Add("×1")
        End If
        parts.Add(FormationName(tlClip.Formation))
        Return String.Join(" · ", parts)
    End Function

    Private Function FormationName(formation As FormationKind) As String
        Select Case formation
            Case FormationKind.SinglePoint : Return "单点"
            Case FormationKind.LineHorizontal : Return "横向"
            Case FormationKind.LineVertical : Return "纵向"
            Case FormationKind.Ring : Return "环形"
            Case FormationKind.Arc : Return "弧形"
            Case FormationKind.RandomBox : Return "随机"
            Case Else : Return "阵型"
        End Select
    End Function

    Private Function ClipTooltip(track As TimelineTrack, tlClip As TimelineClip) As String
        If track.Kind = TrackKind.Cue Then
            Return "事件：" & ClipTitle(track, tlClip) & vbCrLf &
                   "Tick " & tlClip.StartTick & " · " & TimeSpan.FromSeconds(tlClip.StartTick / 60.0).ToString("m\:ss\.ff") & vbCrLf &
                   "双击编辑属性"
        End If
        If track.Kind = TrackKind.Background Then
            If tlClip.IsLoopEvent Then
                Return "背景循环事件" & vbCrLf &
                       "事件 Tick " & tlClip.StartTick & " · 循环 " & tlClip.LoopStartTick & " → " & tlClip.LoopEndTick & vbCrLf &
                       "循环长度 " & tlClip.LoopLength & " 帧" & vbCrLf &
                       "双击编辑属性"
            End If
            Dim layer = App.Session.Stage.FindBackgroundLayer(track.BackgroundLayerId)
            Dim detail As String
            If layer IsNot Nothing AndAlso layer.Kind = BackgroundLayerKind.Layer3D Then
                detail = "摄像机 (" & tlClip.CameraX.ToString("0.##") & ", " & tlClip.CameraY.ToString("0.##") & ", " & tlClip.CameraZ.ToString("0.##") & ")"
            Else
                detail = "位置 (" & tlClip.TargetX.ToString("0.#") & ", " & tlClip.TargetY.ToString("0.#") & ") · 缩放 " & tlClip.TargetScale.ToString("0.##")
            End If
            Return "背景关键帧：" & If(layer Is Nothing, track.Name, layer.Name) & vbCrLf &
                   "Tick " & tlClip.StartTick & " · " & detail & vbCrLf &
                   "双击编辑属性"
        End If
        Dim tmpl = App.Session.Stage.FindTemplate(track.TemplateId)
        Return If(tmpl Is Nothing, "未绑定敌机模板", "敌机：" & tmpl.Name) & vbCrLf &
               "开始：" & tlClip.StartTick & " 帧（" & TimeSpan.FromSeconds(tlClip.StartTick / 60.0).ToString("m\:ss\.ff") & "）" & vbCrLf &
               "数量 " & tlClip.Count & "，间隔 " & tlClip.IntervalTicks & " 帧，结束于 " & tlClip.EndTick & " 帧" & vbCrLf &
               "阵型 " & FormationName(tlClip.Formation) & "，间距 " & tlClip.Spacing & vbCrLf &
               "位置 (" & tlClip.X.ToString("0.#") & ", " & tlClip.Y.ToString("0.#") & ")，抖动 ±" & tlClip.JitterX.ToString("0.#") & " / ±" & tlClip.JitterY.ToString("0.#")
    End Function

    Private Function AccentFill(hex As String, alpha As Byte) As String
        Try
            Dim color = DirectCast(ColorConverter.ConvertFromString(hex), Color)
            Return String.Format("#{0:X2}{1:X2}{2:X2}{3:X2}", alpha, color.R, color.G, color.B)
        Catch
            Return "#55444444"
        End Try
    End Function

    ' ============================================================
    ' 选择与高亮
    ' ============================================================

    Private Sub UpdateSelectionVisuals()
        For Each pair In _clipVisuals
            If pair.Value.IsKeyframe Then Continue For
            Dim selected = (pair.Key Is _selectedClip)
            Dim accent = pair.Value.Track.Accent
            pair.Value.Border.BorderBrush = Ui.Brush(If(selected, "#FFFFFF", accent))
            pair.Value.Border.BorderThickness = New Thickness(If(selected, 2.0, 1.0))
            pair.Value.Border.Background = Ui.Brush(AccentFill(accent, If(selected, 120, 70)))
        Next
        For Each pair In _trackHeaders
            Dim selected = (pair.Key Is _selectedTrack)
            pair.Value.Background = Ui.Brush(If(selected, "#2D4253", "#252526"))
        Next
        UpdateToolbarState()
    End Sub

    Private Sub UpdateToolbarState()
        DeleteClipButton.IsEnabled = _selectedClip IsNot Nothing
        DuplicateButton.IsEnabled = _selectedClip IsNot Nothing
    End Sub

    Private Sub SelectClip(tlClip As TimelineClip, track As TimelineTrack)
        _selectedClip = tlClip
        _selectedTrack = track
        App.Session.SelectItem(tlClip)
        UpdateSelectionVisuals()
    End Sub

    Private Sub SelectTrackOnly(track As TimelineTrack)
        _selectedClip = Nothing
        _selectedTrack = track
        App.Session.SelectItem(track)
        UpdateSelectionVisuals()
    End Sub

    ' ============================================================
    ' 时间轴鼠标交互
    ' ============================================================

    Private Sub Clip_MouseDown(clipUi As ClipUi, e As MouseButtonEventArgs)
        If clipUi.Track.IsLocked Then
            e.Handled = True
            Return
        End If
        Focus()
        If e.ClickCount = 2 Then
            SelectClip(clipUi.Clip, clipUi.Track)
            If clipUi.Track.Kind = TrackKind.Enemy Then
                Dim tmpl = App.Session.Stage.FindTemplate(clipUi.Track.TemplateId)
                If tmpl IsNot Nothing Then App.Session.EditTemplateBehavior(tmpl)
            ElseIf clipUi.Track.Kind = TrackKind.Background Then
                App.Session.SelectItem(clipUi.Clip)
                Dim main = TryCast(Window.GetWindow(Me), MainWindow)
                If clipUi.Clip.IsLoopEvent Then
                    If main IsNot Nothing Then
                        App.Session.SetView("timeline")
                        main.FocusProperties()
                    End If
                ElseIf main IsNot Nothing Then
                    App.Session.SetView("background")
                    main.BackgroundEditorView.SelectLayer(clipUi.Track.BackgroundLayerId)
                End If
            Else
                App.Session.SelectItem(clipUi.Clip)
                App.Session.SetView("timeline")
                Dim main = TryCast(Window.GetWindow(Me), MainWindow)
                If main IsNot Nothing Then main.FocusProperties()
            End If
            e.Handled = True
            Return
        End If
        Dim pos = e.GetPosition(clipUi.Border)
        Dim mode = "move"
        Dim canResize = clipUi.Track.Kind <> TrackKind.Background AndAlso
                        (clipUi.Clip.Count > 1 AndAlso clipUi.Clip.IntervalTicks > 0)
        If pos.X > clipUi.Border.ActualWidth - 8 AndAlso canResize Then
            mode = "resize"
        End If
        _drag = New DragState With {
            .Ui = clipUi,
            .Clip = clipUi.Clip,
            .Track = clipUi.Track,
            .Mode = mode,
            .StartMouseX = e.GetPosition(LaneCanvas).X,
            .StartTick = clipUi.Clip.StartTick,
            .StartCount = clipUi.Clip.Count,
            .StartInterval = clipUi.Clip.IntervalTicks,
            .StartAnimationLength = clipUi.Clip.AnimationLength,
            .Moved = False,
            .UndoStarted = False
        }
        SelectClip(clipUi.Clip, clipUi.Track)
        clipUi.Border.CaptureMouse()
        e.Handled = True
    End Sub

    Private Sub Clip_MouseMove(clipUi As ClipUi, e As MouseEventArgs)
        If _drag Is Nothing OrElse _drag.Ui IsNot clipUi Then
            If clipUi.Border.IsMouseOver Then
                Dim pos = e.GetPosition(clipUi.Border)
                clipUi.Border.Cursor = If(pos.X > clipUi.Border.ActualWidth - 8 AndAlso
                                          clipUi.Track.Kind <> TrackKind.Background AndAlso
                                          (clipUi.Clip.Count > 1 AndAlso clipUi.Clip.IntervalTicks > 0),
                                          Cursors.SizeWE, Cursors.Hand)
            End If
            Return
        End If
        Dim x = e.GetPosition(LaneCanvas).X
        If Not _drag.Moved AndAlso Math.Abs(x - _drag.StartMouseX) < 2 Then Return
        If Not _drag.UndoStarted Then
            _drag.UndoStarted = True
            App.Session.BeginEdit()
        End If
        _drag.Moved = True
        If _drag.Mode = "move" Then
            Dim delta = (x - _drag.StartMouseX) / _px
            Dim target = SnapTick(_drag.StartTick + delta)
            target = Math.Max(0, Math.Min(App.Session.Stage.LengthTicks, target))
            _drag.Clip.StartTick = target
            Canvas.SetLeft(clipUi.Border, target * _px + clipUi.OffsetX)
            UpdateClipText(clipUi)
        Else
            If _drag.Track.Kind = TrackKind.Background Then
                Dim endTick = SnapTick(Math.Max(0, x / _px))
                _drag.Clip.AnimationLength = Math.Max(1, endTick - _drag.Clip.StartTick)
            Else
                Dim endTick = SnapTick(Math.Max(0, x / _px))
                Dim interval = Math.Max(1, _drag.Clip.IntervalTicks)
                Dim newCount = CInt(Math.Floor((endTick - _drag.Clip.StartTick) / interval)) + 1
                newCount = Math.Max(1, Math.Min(64, newCount))
                _drag.Clip.Count = newCount
            End If
            clipUi.Border.Width = ClipWidth(clipUi.Track, _drag.Clip)
            UpdateClipText(clipUi)
        End If
        e.Handled = True
    End Sub

    Private Sub Clip_MouseUp(clipUi As ClipUi, e As MouseButtonEventArgs)
        If _drag Is Nothing OrElse _drag.Ui IsNot clipUi Then Return
        clipUi.Border.ReleaseMouseCapture()
        If _drag.UndoStarted Then
            If _drag.Moved Then
                App.Session.CommitEdit()
            Else
                App.Session.CancelEdit()
            End If
        End If
        _drag = Nothing
        Refresh()
        e.Handled = True
    End Sub

    Private Sub UpdateClipText(clipUi As ClipUi)
        If clipUi.Title IsNot Nothing Then clipUi.Title.Text = ClipTitle(clipUi.Track, clipUi.Clip)
        If clipUi.Subtitle IsNot Nothing Then clipUi.Subtitle.Text = ClipSubtitle(clipUi.Track, clipUi.Clip)
        clipUi.Border.ToolTip = ClipTooltip(clipUi.Track, clipUi.Clip)
    End Sub

    Private Function SnapTick(raw As Double) As Integer
        If SnapCheck IsNot Nothing AndAlso SnapCheck.IsChecked = False Then Return CInt(Math.Round(raw))
        If (Keyboard.Modifiers And ModifierKeys.Alt) = ModifierKeys.Alt Then Return CInt(Math.Round(raw))
        Dim snapStep = SnapGridStep()
        If (Keyboard.Modifiers And ModifierKeys.Shift) = ModifierKeys.Shift Then snapStep = TickStep()
        Return CInt(Math.Round(raw / snapStep) * snapStep)
    End Function

    Private Sub Clip_RightClick(clipUi As ClipUi, e As MouseButtonEventArgs)
        SelectClip(clipUi.Clip, clipUi.Track)
        Dim menu As New ContextMenu()
        menu.Items.Add(MakeMenuItem("编辑属性", Sub() FocusProperties(clipUi.Clip)))
        menu.Items.Add(MakeMenuItem("对齐到播放头", Sub()
                                                              App.Session.BeginEdit()
                                                              clipUi.Clip.StartTick = CInt(_playhead)
                                                              App.Session.CommitEdit()
                                                              Refresh()
                                                          End Sub))
        menu.Items.Add(MakeMenuItem("复制片段", Sub() DuplicateClip(clipUi.Clip, clipUi.Track)))
        menu.Items.Add(New Separator())
        menu.Items.Add(MakeMenuItem("删除片段", Sub() DeleteClip(clipUi.Clip, clipUi.Track)))
        If clipUi.Track.Kind = TrackKind.Enemy Then
            Dim tmpl = App.Session.Stage.FindTemplate(clipUi.Track.TemplateId)
            If tmpl IsNot Nothing Then
                menu.Items.Add(New Separator())
                menu.Items.Add(MakeMenuItem("编辑该敌机的积木行为", Sub() App.Session.EditTemplateBehavior(tmpl)))
            End If
        End If
        menu.PlacementTarget = clipUi.Border
        menu.IsOpen = True
        e.Handled = True
    End Sub

    Private Sub Lane_MouseLeftButtonDown(sender As Object, e As MouseButtonEventArgs)
        If App.Session.Stage Is Nothing Then Return
        Dim pos = e.GetPosition(LaneCanvas)
        Dim index = CInt(Math.Floor(pos.Y / RowHeight))
        If index < 0 OrElse index >= App.Session.Stage.Tracks.Count Then Return
        Dim track = App.Session.Stage.Tracks(index)
        Dim tick = SnapTick(Math.Max(0, pos.X / _px))
        If e.ClickCount = 2 Then
            AddClipAt(track, tick)
            e.Handled = True
            Return
        End If
        SelectTrackOnly(track)
        SetPlayhead(tick)
        SeekPreviewThrottled(tick)
        Focus()
        e.Handled = True
    End Sub

    Private Sub Lane_MouseRightButtonDown(sender As Object, e As MouseButtonEventArgs)
        If App.Session.Stage Is Nothing Then Return
        Dim pos = e.GetPosition(LaneCanvas)
        Dim index = CInt(Math.Floor(pos.Y / RowHeight))
        If index < 0 OrElse index >= App.Session.Stage.Tracks.Count Then Return
        Dim track = App.Session.Stage.Tracks(index)
        Dim tick = SnapTick(Math.Max(0, pos.X / _px))
        SelectTrackOnly(track)
        SetPlayhead(tick)
        Dim menu As New ContextMenu()
        menu.Items.Add(MakeMenuItem("在此添加片段", Sub() AddClipAt(track, tick)))
        If track.Kind = TrackKind.Background Then
            menu.Items.Add(MakeMenuItem("在此添加循环事件", Sub() AddLoopEventAt(track, tick)))
        End If
        menu.Items.Add(MakeMenuItem("添加敌机轨道…", Sub() ShowAddTrackMenu(AddTrackButton)))
        menu.Items.Add(MakeMenuItem("添加事件轨道", Sub() AddEventTrack()))
        If App.Session.Stage.BackgroundLayers.Count > 0 Then
            menu.Items.Add(New Separator())
            For Each layer In App.Session.Stage.BackgroundLayers
                Dim captured = layer
                menu.Items.Add(MakeMenuItem("添加背景动画 · " & layer.Name, Sub() AddBackgroundTrack(captured)))
            Next
        End If
        menu.PlacementTarget = LaneCanvas
        menu.IsOpen = True
        e.Handled = True
    End Sub

    Private Sub TrackHeader_Click(track As TimelineTrack, e As MouseButtonEventArgs)
        If e.ClickCount = 2 Then
            RenameTrack(track)
        Else
            SelectTrackOnly(track)
            ScrollToTrack(track)
        End If
        e.Handled = True
    End Sub

    Private Sub TrackHeader_RightClick(track As TimelineTrack, e As MouseButtonEventArgs)
        SelectTrackOnly(track)
        Dim menu As New ContextMenu()
        menu.Items.Add(MakeMenuItem("重命名轨道…", Sub() RenameTrack(track)))
        If track.Kind = TrackKind.Enemy Then
            menu.Items.Add(New Separator())
            For Each tmpl In App.Session.Stage.EnemyTemplates
                Dim captured = tmpl
                Dim item = MakeMenuItem("使用模板：" & tmpl.Name, Sub()
                                                                           App.Session.BeginEdit()
                                                                           track.TemplateId = captured.Id
                                                                           App.Session.CommitEdit()
                                                                           Refresh()
                                                                       End Sub)
                If track.TemplateId = tmpl.Id Then item.IsChecked = True
                menu.Items.Add(item)
            Next
        ElseIf track.Kind = TrackKind.Background Then
            menu.Items.Add(New Separator())
            menu.Items.Add(MakeMenuItem("打开背景编辑", Sub()
                                                           App.Session.SetView("background")
                                                           Dim main = TryCast(Window.GetWindow(Me), MainWindow)
                                                           If main IsNot Nothing Then main.BackgroundEditorView.SelectLayer(track.BackgroundLayerId)
                                                       End Sub))
            menu.Items.Add(MakeMenuItem("在播放头添加循环事件", Sub() AddLoopEventAt(track, CInt(_playhead))))
        End If
        menu.Items.Add(New Separator())
        menu.Items.Add(MakeMenuItem(If(track.IsMuted, "取消静音", "静音轨道"), Sub() ToggleTrackFlag(track, True)))
        menu.Items.Add(MakeMenuItem(If(track.IsLocked, "解锁轨道", "锁定轨道"), Sub() ToggleTrackFlag(track, False)))
        menu.Items.Add(New Separator())
        menu.Items.Add(MakeMenuItem("删除轨道", Sub() DeleteTrack(track)))
        menu.PlacementTarget = e.OriginalSource
        menu.IsOpen = True
        e.Handled = True
    End Sub

    ' ============================================================
    ' 标尺与预览跳转
    ' ============================================================

    Private Sub Ruler_MouseLeftButtonDown(sender As Object, e As MouseButtonEventArgs)
        _seeking = True
        RulerCanvas.CaptureMouse()
        SeekAt(e.GetPosition(RulerCanvas).X)
        e.Handled = True
    End Sub

    Private Sub Ruler_MouseMove(sender As Object, e As MouseEventArgs)
        If Not _seeking Then Return
        If (DateTime.Now - _lastSeek).TotalMilliseconds < 45 Then Return
        _lastSeek = DateTime.Now
        SeekAt(e.GetPosition(RulerCanvas).X)
    End Sub

    Private Sub Ruler_MouseLeftButtonUp(sender As Object, e As MouseButtonEventArgs)
        _seeking = False
        RulerCanvas.ReleaseMouseCapture()
        e.Handled = True
    End Sub

    Private Sub Ruler_MouseLeave(sender As Object, e As MouseEventArgs)
        If _seeking AndAlso e.LeftButton = MouseButtonState.Released Then
            _seeking = False
            RulerCanvas.ReleaseMouseCapture()
        End If
    End Sub

    Private Sub SeekAt(x As Double)
        Dim stage = App.Session.Stage
        If stage Is Nothing Then Return
        Dim tick = SnapTick(Math.Max(0, x / _px))
        tick = Math.Max(0, Math.Min(stage.LengthTicks, tick))
        SetPlayhead(tick)
        SeekPreviewThrottled(tick)
    End Sub

    Private Sub SeekPreviewThrottled(tick As Long)
        If App.Session.World Is Nothing Then Return
        If (DateTime.Now - _lastSeek).TotalMilliseconds < 45 Then Return
        _lastSeek = DateTime.Now
        App.Session.SeekPreview(tick)
    End Sub

    ' ============================================================
    ' 命令
    ' ============================================================

    Private Sub AddClip_Click(sender As Object, e As RoutedEventArgs)
        AddClipAt(_selectedTrack, CInt(_playhead))
    End Sub

    Private Sub AddClipAt(track As TimelineTrack, tick As Integer)
        Dim stage = App.Session.Stage
        If stage Is Nothing Then Return
        tick = SnapTick(Math.Max(0, tick))
        If track Is Nothing Then
            track = stage.Tracks.FirstOrDefault(Function(t) t.Kind = TrackKind.Enemy)
            If track Is Nothing Then track = stage.Tracks.FirstOrDefault()
        End If
        If track Is Nothing Then
            App.Session.AddLog(IssueSeverity.Warning, "请先添加一条轨道。")
            Return
        End If
        Dim tlClip As New TimelineClip With {
            .StartTick = Math.Max(0, tick),
            .Count = 1,
            .IntervalTicks = 0,
            .X = 192,
            .Y = -16,
            .Formation = FormationKind.SinglePoint
        }
        If track.Kind = TrackKind.Cue Then
            tlClip.Cue = CueKind.Music
            tlClip.CueText = ""
        ElseIf track.Kind = TrackKind.Background Then
            Dim existing = track.Clips.FirstOrDefault(Function(c) c.StartTick = tlClip.StartTick)
            If existing IsNot Nothing Then
                SelectClip(existing, track)
                Refresh()
                Return
            End If
            Dim layer = stage.FindBackgroundLayer(track.BackgroundLayerId)
            If layer IsNot Nothing Then
                Dim previous = track.Clips.Where(Function(c) c.StartTick <= tlClip.StartTick).
                    OrderByDescending(Function(c) c.StartTick).FirstOrDefault()
                If layer.Kind = BackgroundLayerKind.Layer3D Then
                    tlClip.CameraX = If(previous Is Nothing, layer.CameraX, previous.CameraX)
                    tlClip.CameraY = If(previous Is Nothing, layer.CameraY, previous.CameraY)
                    tlClip.CameraZ = If(previous Is Nothing, layer.CameraZ, previous.CameraZ)
                    tlClip.CameraLookX = If(previous Is Nothing, layer.LookX, previous.CameraLookX)
                    tlClip.CameraLookY = If(previous Is Nothing, layer.LookY, previous.CameraLookY)
                    tlClip.CameraLookZ = If(previous Is Nothing, layer.LookZ, previous.CameraLookZ)
                    tlClip.CameraFieldOfView = If(previous Is Nothing, layer.FieldOfView, previous.CameraFieldOfView)
                ElseIf layer.Shape = BackgroundShape2D.ParticleField Then
                    tlClip.ParticleSpeedX = If(previous Is Nothing, layer.ParticleSpeedX, previous.ParticleSpeedX)
                    tlClip.ParticleSpeedY = If(previous Is Nothing, layer.ParticleSpeedY, previous.ParticleSpeedY)
                    tlClip.TargetOpacity = If(previous Is Nothing, layer.Opacity, previous.TargetOpacity)
                Else
                    tlClip.TargetX = If(previous Is Nothing, layer.X, previous.TargetX)
                    tlClip.TargetY = If(previous Is Nothing, layer.Y, previous.TargetY)
                    tlClip.TargetScale = If(previous Is Nothing, 1.0, previous.TargetScale)
                    tlClip.TargetRotation = If(previous Is Nothing, 0.0, previous.TargetRotation)
                    tlClip.TargetOpacity = If(previous Is Nothing, layer.Opacity, previous.TargetOpacity)
                End If
            End If
        End If
        App.Session.BeginEdit()
        track.Clips.Add(tlClip)
        track.Clips.Sort(Function(a, b) a.StartTick.CompareTo(b.StartTick))
        App.Session.CommitEdit()
        _selectedTrack = track
        _selectedClip = tlClip
        App.Session.SelectItem(tlClip)
        Refresh()
        UpdateSelectionVisuals()
    End Sub

    Private Sub AddLoopEventAt(track As TimelineTrack, tick As Integer)
        If track Is Nothing OrElse track.Kind <> TrackKind.Background Then Return
        If tick < 2 Then
            MessageBox.Show(Window.GetWindow(Me), "循环事件前至少要留出 1 帧的循环区间。", "循环事件", MessageBoxButton.OK, MessageBoxImage.Information)
            Return
        End If
        tick = SnapTick(Math.Max(2, tick))
        Dim loopStart = Math.Max(0, tick - 120)
        Dim loopEnd = tick - 1
        For Each otherTrack In App.Session.Stage.Tracks
            If otherTrack Is Nothing OrElse otherTrack.Kind <> TrackKind.Background Then Continue For
            For Each other In otherTrack.Clips
                If other IsNot Nothing AndAlso other.IsLoopEvent AndAlso other.StartTick >= loopStart AndAlso other.StartTick <= loopEnd Then
                    loopEnd = Math.Min(loopEnd, other.StartTick - 1)
                End If
            Next
        Next
        If loopEnd <= loopStart Then
            MessageBox.Show(Window.GetWindow(Me), "这个位置之前没有可用的循环区间；请先移动或删除区间内的循环事件。", "循环事件", MessageBoxButton.OK, MessageBoxImage.Warning)
            Return
        End If
        Dim tlClip As New TimelineClip With {
            .StartTick = tick,
            .IsLoopEvent = True,
            .LoopStartTick = loopStart,
            .LoopEndTick = loopEnd,
            .LoopLength = Math.Max(1, Math.Min(120, loopEnd - loopStart + 1))
        }
        App.Session.BeginEdit()
        track.Clips.Add(tlClip)
        track.Clips.Sort(Function(a, b) a.StartTick.CompareTo(b.StartTick))
        App.Session.CommitEdit()
        _selectedTrack = track
        _selectedClip = tlClip
        App.Session.SelectItem(tlClip)
        Refresh()
        UpdateSelectionVisuals()
        Dim main = TryCast(Window.GetWindow(Me), MainWindow)
        If main IsNot Nothing Then main.FocusProperties()
    End Sub

    Private Sub AddTrack_Click(sender As Object, e As RoutedEventArgs)
        ShowAddTrackMenu(AddTrackButton)
    End Sub

    Private Sub ShowAddTrackMenu(target As FrameworkElement)
        Dim menu As New ContextMenu()
        For Each tmpl In App.Session.Stage.EnemyTemplates
            Dim captured = tmpl
            menu.Items.Add(MakeMenuItem("敌机轨道 · " & tmpl.Name, Sub() AddEnemyTrack(captured)))
        Next
        If App.Session.Stage.EnemyTemplates.Count = 0 Then
            Dim disabled As New MenuItem With {.Header = "（先创建敌机模板）", .IsEnabled = False}
            menu.Items.Add(disabled)
        End If
        menu.Items.Add(New Separator())
        menu.Items.Add(MakeMenuItem("事件轨道 · 音乐/演出", Sub() AddEventTrack()))
        menu.Items.Add(New Separator())
        If App.Session.Stage.BackgroundLayers.Count = 0 Then
            menu.Items.Add(New MenuItem With {.Header = "背景动画轨道（请先添加背景图层）", .IsEnabled = False})
        Else
            For Each layer In App.Session.Stage.BackgroundLayers
                Dim captured = layer
                menu.Items.Add(MakeMenuItem("背景动画 · " & layer.Name, Sub() AddBackgroundTrack(captured)))
            Next
        End If
        menu.PlacementTarget = target
        menu.Placement = PlacementMode.Bottom
        menu.IsOpen = True
    End Sub

    Private Sub AddEnemyTrack(tmpl As EnemyTemplate)
        Dim stage = App.Session.Stage
        Dim palette = {"#D7BA7D", "#CE9178", "#F48771", "#9CDCFE", "#C586C0", "#4EC9B0", "#DCDCAA"}
        Dim accent = palette(stage.Tracks.Count Mod palette.Length)
        Dim track As New TimelineTrack With {
            .Name = tmpl.Name & " 轨道",
            .Kind = TrackKind.Enemy,
            .TemplateId = tmpl.Id,
            .Accent = accent
        }
        App.Session.BeginEdit()
        stage.Tracks.Add(track)
        App.Session.CommitEdit()
        SelectTrackOnly(track)
        Refresh()
        ScrollToTrack(track)
    End Sub

    Private Sub AddEventTrack()
        Dim stage = App.Session.Stage
        Dim track As New TimelineTrack With {
            .Name = "事件轨道",
            .Kind = TrackKind.Cue,
            .Accent = "#C792EA"
        }
        App.Session.BeginEdit()
        stage.Tracks.Add(track)
        App.Session.CommitEdit()
        SelectTrackOnly(track)
        Refresh()
    End Sub

    Private Sub AddBackgroundTrack(layer As BackgroundLayer)
        If layer Is Nothing Then Return
        Dim stage = App.Session.Stage
        Dim track As New TimelineTrack With {
            .Name = "背景动画 · " & layer.Name,
            .Kind = TrackKind.Background,
            .BackgroundLayerId = layer.Id,
            .Accent = If(layer.Kind = BackgroundLayerKind.Layer3D, "#4EC9B0", "#4FC1FF")
        }
        App.Session.BeginEdit()
        stage.Tracks.Add(track)
        App.Session.CommitEdit()
        SelectTrackOnly(track)
        Refresh()
        ScrollToTrack(track)
    End Sub

    Private Sub Delete_Click(sender As Object, e As RoutedEventArgs)
        DeleteSelected()
    End Sub

    Public Sub DeleteSelected()
        If _selectedClip IsNot Nothing Then
            DeleteClip(_selectedClip, App.Session.Stage.TrackOfClip(_selectedClip))
        End If
    End Sub

    Public Sub DuplicateSelected()
        If _selectedClip IsNot Nothing Then
            DuplicateClip(_selectedClip, App.Session.Stage.TrackOfClip(_selectedClip))
        End If
    End Sub

    Public Sub ShowAddTrackMenuForToolbar()
        ShowAddTrackMenu(AddTrackButton)
    End Sub

    Private Sub DeleteClip(tlClip As TimelineClip, track As TimelineTrack)
        If tlClip Is Nothing OrElse track Is Nothing Then Return
        App.Session.BeginEdit()
        track.Clips.Remove(tlClip)
        App.Session.CommitEdit()
        _selectedClip = Nothing
        App.Session.SelectItem(track)
        _selectedTrack = track
        Refresh()
    End Sub

    Private Sub Duplicate_Click(sender As Object, e As RoutedEventArgs)
        If _selectedClip IsNot Nothing Then
            DuplicateClip(_selectedClip, App.Session.Stage.TrackOfClip(_selectedClip))
        End If
    End Sub

    Private Sub DuplicateClip(tlClip As TimelineClip, track As TimelineTrack)
        If tlClip Is Nothing OrElse track Is Nothing Then Return
        Dim copy = tlClip.Clone()
        copy.Id = Ids.NewId("tlClip")
        Dim nextStart = If(track.Kind = TrackKind.Background,
                           tlClip.StartTick + Math.Max(1, tlClip.AnimationLength) + 30,
                           copy.EndTick + 30)
        copy.StartTick = Math.Min(App.Session.Stage.LengthTicks, Math.Max(nextStart, CInt(_playhead)))
        copy.StartTick = Math.Min(App.Session.Stage.LengthTicks, SnapTick(copy.StartTick))
        App.Session.BeginEdit()
        track.Clips.Add(copy)
        track.Clips.Sort(Function(a, b) a.StartTick.CompareTo(b.StartTick))
        App.Session.CommitEdit()
        _selectedClip = copy
        _selectedTrack = track
        App.Session.SelectItem(copy)
        Refresh()
    End Sub

    Private Sub DeleteTrack(track As TimelineTrack)
        If track Is Nothing Then Return
        App.Session.BeginEdit()
        App.Session.Stage.Tracks.Remove(track)
        App.Session.CommitEdit()
        _selectedTrack = Nothing
        _selectedClip = Nothing
        App.Session.SelectItem(App.Session.Stage)
        Refresh()
    End Sub

    Private Sub ToggleTrackFlag(track As TimelineTrack, mute As Boolean)
        App.Session.BeginEdit()
        If mute Then
            track.IsMuted = Not track.IsMuted
        Else
            track.IsLocked = Not track.IsLocked
        End If
        App.Session.CommitEdit()
        Refresh()
    End Sub

    Private Sub RenameTrack(track As TimelineTrack)
        Dim value = InputDialog.ShowInput(Window.GetWindow(Me), "重命名轨道", "轨道名称：", track.Name,
                                          Function(text) If(String.IsNullOrWhiteSpace(text), "名称不能为空。", Nothing))
        If value Is Nothing Then Return
        App.Session.BeginEdit()
        track.Name = value.Trim()
        App.Session.CommitEdit()
        Refresh()
    End Sub

    Private Sub FocusProperties(item As Object)
        App.Session.SelectItem(item)
        App.Session.SetView("timeline")
        Dim main = TryCast(Window.GetWindow(Me), MainWindow)
        If main IsNot Nothing Then main.FocusProperties()
    End Sub

    Private Sub ZoomIn_Click(sender As Object, e As RoutedEventArgs)
        ZoomBy(1.25)
    End Sub

    Private Sub ZoomOut_Click(sender As Object, e As RoutedEventArgs)
        ZoomBy(0.8)
    End Sub

    Private Sub ZoomFit_Click(sender As Object, e As RoutedEventArgs)
        Dim stage = App.Session.Stage
        If stage Is Nothing Then Return
        _px = Math.Max(0.06, Math.Min(3.0, (LaneScroll.ViewportWidth - 40) / Math.Max(1, stage.LengthTicks)))
        Refresh()
    End Sub

    Private Sub ZoomBy(factor As Double)
        _px = Math.Max(0.06, Math.Min(3.0, _px * factor))
        Refresh()
    End Sub

    Private Sub ViewPreview_Click(sender As Object, e As RoutedEventArgs)
        App.Session.SetView("preview")
    End Sub

    Private Sub LaneScroll_PreviewMouseWheel(sender As Object, e As MouseWheelEventArgs)
        If (Keyboard.Modifiers And ModifierKeys.Control) = ModifierKeys.Control Then
            ZoomBy(If(e.Delta > 0, 1.15, 0.87))
            e.Handled = True
        End If
    End Sub

    Private Sub LaneScroll_ScrollChanged(sender As Object, e As ScrollChangedEventArgs)
        If _suppressScrollSync Then Return
        _suppressScrollSync = True
        Try
            RulerScroll.ScrollToHorizontalOffset(LaneScroll.HorizontalOffset)
            HeaderScroll.ScrollToVerticalOffset(LaneScroll.VerticalOffset)
        Finally
            _suppressScrollSync = False
        End Try
    End Sub

    Private Sub Timeline_KeyDown(sender As Object, e As KeyEventArgs)
        If TypeOf Keyboard.FocusedElement Is TextBox Then Return
        Select Case e.Key
            Case Key.Delete
                DeleteSelected()
                e.Handled = True
            Case Key.Insert
                AddClipAt(_selectedTrack, CInt(_playhead))
                e.Handled = True
            Case Key.D
                If (Keyboard.Modifiers And ModifierKeys.Control) = ModifierKeys.Control Then
                    Duplicate_Click(Nothing, Nothing)
                    e.Handled = True
                End If
            Case Key.Add, Key.OemPlus
                If (Keyboard.Modifiers And ModifierKeys.Control) = ModifierKeys.Control Then
                    ZoomBy(1.25)
                    e.Handled = True
                End If
            Case Key.Subtract, Key.OemMinus
                If (Keyboard.Modifiers And ModifierKeys.Control) = ModifierKeys.Control Then
                    ZoomBy(0.8)
                    e.Handled = True
                End If
            Case Key.Home
                SetPlayhead(0)
                SeekPreviewThrottled(0)
                e.Handled = True
        End Select
    End Sub

    Private Function MakeMenuItem(header As String, action As Action) As MenuItem
        Dim item As New MenuItem With {.Header = header}
        AddHandler item.Click, Sub(sender As Object, e As RoutedEventArgs) action.Invoke()
        Return item
    End Function

    Private Class ClipUi
        Public Property Clip As TimelineClip
        Public Property Track As TimelineTrack
        Public Property Border As Border
        Public Property Title As TextBlock
        Public Property Subtitle As TextBlock
        Public Property OffsetX As Double
        Public Property IsKeyframe As Boolean
    End Class

    Private Class DragState
        Public Property Ui As ClipUi
        Public Property Clip As TimelineClip
        Public Property Track As TimelineTrack
        Public Property Mode As String
        Public Property StartMouseX As Double
        Public Property StartTick As Integer
        Public Property StartCount As Integer
        Public Property StartInterval As Integer
        Public Property StartAnimationLength As Integer
        Public Property Moved As Boolean
        Public Property UndoStarted As Boolean
    End Class

End Class
