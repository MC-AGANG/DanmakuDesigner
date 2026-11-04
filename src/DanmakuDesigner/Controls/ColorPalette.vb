Imports System.Globalization
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Controls.Primitives
Imports System.Windows.Input
Imports System.Windows.Media
Imports System.Windows.Media.Effects

''' <summary>点击颜色方块时弹出的调色盘：预设色、透明色和自定义色号。</summary>
Public Module ColorPalette

    Private _active As Popup

    Private ReadOnly PaletteColors As String() = {
        "#00000000", "#FF000000", "#FF1E1E1E", "#FF3F3F46", "#FF707070", "#FFA0A0A0", "#FFD0D0D0", "#FFFFFFFF",
        "#FF7F0000", "#FFFF3B30", "#FFFF9500", "#FFFFCC00", "#FF34C759", "#FF00C7BE", "#FF32ADE6", "#FF5856D6",
        "#FFFF2D55", "#FFFF6B81", "#FFFFB86B", "#FFFFE66D", "#FF7ED957", "#FF66D9D9", "#FF6EA8FF", "#FFB08CFF",
        "#FFB71C1C", "#FFC62828", "#FFEF6C00", "#FFF9A825", "#FF2E7D32", "#FF00838F", "#FF1565C0", "#FF4527A0",
        "#FF880E4F", "#FFAD1457", "#FFD81B60", "#FFE65100", "#FF827717", "#FF33691E", "#FF006064", "#FF0D47A1",
        "#33FFFFFF", "#66FFFFFF", "#B0FFFFFF", "#33FFD166", "#66FFD166", "#B0FFD166", "#33FF6EA8", "#66FF6EA8"
    }

    Public Sub Show(anchor As FrameworkElement, initial As String, apply As Action(Of String))
        If anchor Is Nothing OrElse apply Is Nothing Then Return
        If _active IsNot Nothing Then
            _active.IsOpen = False
            _active = Nothing
        End If

        Dim popup As New Popup With {
            .PlacementTarget = anchor,
            .Placement = PlacementMode.Bottom,
            .StaysOpen = False,
            .AllowsTransparency = True,
            .Focusable = True,
            .VerticalOffset = 4,
            .HorizontalOffset = -220
        }
        _active = popup

        Dim root As New StackPanel With {.Width = 268}
        Dim title As New TextBlock With {
            .Text = "选择颜色",
            .FontSize = 12,
            .FontWeight = FontWeights.SemiBold,
            .Foreground = Ui.Brush("#E6E6E6"),
            .Margin = New Thickness(0, 0, 0, 8)
        }
        root.Children.Add(title)

        Dim previewRow As New Grid With {.Margin = New Thickness(0, 0, 0, 8)}
        previewRow.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})
        previewRow.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(38)})
        Dim currentBox = Ui.MakeTextBox(Normalize(initial))
        currentBox.Tag = currentBox.Text
        Dim currentSwatch As New Border With {
            .Width = 30,
            .Height = 24,
            .CornerRadius = New CornerRadius(3),
            .BorderBrush = Ui.Brush("#555555"),
            .BorderThickness = New Thickness(1),
            .Background = Ui.Brush(currentBox.Text),
            .Margin = New Thickness(6, 0, 0, 0)
        }
        AddHandler currentBox.TextChanged, Sub()
                                               If TryNormalize(currentBox.Text) IsNot Nothing Then
                                                   currentSwatch.Background = Ui.Brush(currentBox.Text)
                                               End If
                                           End Sub
        Grid.SetColumn(currentBox, 0)
        Grid.SetColumn(currentSwatch, 1)
        previewRow.Children.Add(currentBox)
        previewRow.Children.Add(currentSwatch)
        root.Children.Add(previewRow)

        Dim paletteGrid As New UniformGrid With {.Columns = 8, .Rows = 6}
        For Each color In PaletteColors
            Dim captured = color
            Dim swatch As New Border With {
                .Width = 27,
                .Height = 22,
                .Margin = New Thickness(2),
                .CornerRadius = New CornerRadius(3),
                .BorderBrush = Ui.Brush(If(String.Equals(Normalize(color), Normalize(initial), StringComparison.OrdinalIgnoreCase), "#FFDCDCDC", "#3F3F46")),
                .BorderThickness = New Thickness(1),
                .Background = Ui.Brush(captured),
                .Cursor = Cursors.Hand,
                .ToolTip = Normalize(captured)
            }
            AddHandler swatch.MouseLeftButtonUp, Sub(sender As Object, e As MouseButtonEventArgs)
                                                        Dim value = Normalize(captured)
                                                        If value Is Nothing Then Return
                                                        apply.Invoke(value)
                                                        popup.IsOpen = False
                                                        e.Handled = True
                                                    End Sub
            AddHandler swatch.MouseEnter, Sub() swatch.BorderBrush = Ui.Brush("#FFDCDCDC")
            AddHandler swatch.MouseLeave, Sub()
                                              If Not String.Equals(Normalize(captured), Normalize(initial), StringComparison.OrdinalIgnoreCase) Then
                                                  swatch.BorderBrush = Ui.Brush("#3F3F46")
                                              End If
                                          End Sub
            paletteGrid.Children.Add(swatch)
        Next
        root.Children.Add(paletteGrid)

        Dim footer As New Grid With {.Margin = New Thickness(0, 8, 0, 0)}
        footer.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})
        footer.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})
        Dim applyButton As New Button With {
            .Content = "应用",
            .Style = TryCast(Application.Current.FindResource("VS.AccentButton"), Style),
            .Padding = New Thickness(12, 3, 12, 3),
            .Margin = New Thickness(6, 0, 0, 0)
        }
        AddHandler applyButton.Click, Sub()
                                          Dim value = TryNormalize(currentBox.Text)
                                          If value Is Nothing Then
                                              currentBox.BorderBrush = Ui.Brush("#F14C4C")
                                              Return
                                          End If
                                          apply.Invoke(value)
                                          popup.IsOpen = False
                                      End Sub
        AddHandler currentBox.KeyDown, Sub(sender As Object, e As KeyEventArgs)
                                           If e.Key = Key.Enter Then
                                               applyButton.RaiseEvent(New RoutedEventArgs(Button.ClickEvent))
                                               e.Handled = True
                                           End If
                                       End Sub
        Dim hint As New TextBlock With {
            .Text = "支持 #RRGGBB、#AARRGGBB",
            .FontSize = 9.5,
            .Foreground = Ui.Brush("#7A7A7A"),
            .VerticalAlignment = VerticalAlignment.Center
        }
        Grid.SetColumn(hint, 0)
        Grid.SetColumn(applyButton, 1)
        footer.Children.Add(hint)
        footer.Children.Add(applyButton)
        root.Children.Add(footer)

        Dim border As New Border With {
            .Background = Ui.Brush("#252526"),
            .BorderBrush = Ui.Brush("#4A4A4D"),
            .BorderThickness = New Thickness(1),
            .CornerRadius = New CornerRadius(4),
            .Padding = New Thickness(10),
            .Child = root
        }
        border.Effect = New DropShadowEffect With {
            .BlurRadius = 12,
            .ShadowDepth = 2,
            .Opacity = 0.55,
            .Color = System.Windows.Media.Colors.Black
        }
        Dim host As New ContentControl With {
            .Content = border,
            .FontFamily = New FontFamily("Microsoft YaHei UI, Microsoft YaHei"),
            .Foreground = Ui.Brush("#E6E6E6")
        }
        popup.Child = host
        AddHandler popup.Closed, Sub()
                                     If _active Is popup Then _active = Nothing
                                 End Sub
        popup.IsOpen = True
    End Sub

    Private Function Normalize(text As String) As String
        Return If(TryNormalize(text), "")
    End Function

    Private Function TryNormalize(text As String) As String
        Dim value = If(text, "").Trim()
        If String.IsNullOrWhiteSpace(value) Then Return Nothing
        Try
            Dim color = DirectCast(ColorConverter.ConvertFromString(value), Color)
            Return String.Format(CultureInfo.InvariantCulture, "#{0:X2}{1:X2}{2:X2}{3:X2}", color.A, color.R, color.G, color.B)
        Catch
            Return Nothing
        End Try
    End Function

End Module
