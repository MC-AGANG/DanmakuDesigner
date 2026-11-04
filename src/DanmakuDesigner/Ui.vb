Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Media
Imports DanmakuDesigner.Core

''' <summary>一组用于在代码后置中构建 VS 风格小部件的辅助函数。</summary>
Public Module Ui

    Private ReadOnly BrushCache As New Dictionary(Of String, SolidColorBrush)(StringComparer.OrdinalIgnoreCase)

    Public Function Brush(hex As String) As SolidColorBrush
        If String.IsNullOrWhiteSpace(hex) Then hex = "#FF000000"
        Dim cached As SolidColorBrush = Nothing
        If BrushCache.TryGetValue(hex, cached) Then Return cached
        Try
            Dim color = DirectCast(ColorConverter.ConvertFromString(hex), Color)
            Dim solid As New SolidColorBrush(color)
            solid.Freeze()
            BrushCache(hex) = solid
            Return solid
        Catch
            Return Brushes.Transparent
        End Try
    End Function

    Public Function Icon(name As String, size As Double) As Image
        Return IconLibrary.Get(name, size)
    End Function

    Public Function ToolButton(iconName As String, tooltip As String, handler As RoutedEventHandler, Optional text As String = Nothing) As Button
        Dim button As New Button With {
            .Style = TryCast(Application.Current.FindResource("VS.ToolButton"), Style),
            .ToolTip = tooltip,
            .Height = 30
        }
        Dim panel As New StackPanel With {.Orientation = Orientation.Horizontal}
        Dim image = Icon(iconName, 16)
        If image IsNot Nothing Then panel.Children.Add(image)
        If Not String.IsNullOrEmpty(text) Then
            panel.Children.Add(New TextBlock With {
                .Text = text,
                .Margin = New Thickness(6, 0, 1, 0),
                .VerticalAlignment = VerticalAlignment.Center,
                .FontSize = 12
            })
        End If
        button.Content = panel
        AddHandler button.Click, handler
        Return button
    End Function

    Public Function IconButton(iconName As String, tooltip As String, handler As RoutedEventHandler) As Button
        Dim button As New Button With {
            .Style = TryCast(Application.Current.FindResource("VS.IconButton"), Style),
            .ToolTip = tooltip
        }
        Dim image = Icon(iconName, 16)
        If image IsNot Nothing Then
            button.Content = image
        Else
            button.Content = New TextBlock With {.Text = "?", .FontSize = 11}
        End If
        AddHandler button.Click, handler
        Return button
    End Function

    Public Function TextButton(text As String, tooltip As String, handler As RoutedEventHandler) As Button
        Dim button As New Button With {
            .Style = TryCast(Application.Current.FindResource("VS.Button"), Style),
            .Content = text,
            .ToolTip = tooltip
        }
        AddHandler button.Click, handler
        Return button
    End Function

    Public Function Label(text As String, Optional size As Double = 12, Optional color As String = "#CCCCCC") As TextBlock
        Return New TextBlock With {
            .Text = text,
            .FontSize = size,
            .Foreground = Brush(color),
            .FontWeight = FontWeights.Normal,
            .VerticalAlignment = VerticalAlignment.Center,
            .TextTrimming = TextTrimming.CharacterEllipsis
        }
    End Function

    Public Function FieldLabel(text As String) As TextBlock
        Return New TextBlock With {
            .Text = text,
            .FontSize = 11,
            .Foreground = Brush("#9A9A9A"),
            .VerticalAlignment = VerticalAlignment.Center,
            .TextWrapping = TextWrapping.Wrap
        }
    End Function

    Public Function MakeTextBox(value As String, Optional width As Double = Double.NaN) As TextBox
        Dim box As New TextBox With {
            .Text = If(value, ""),
            .Height = 26,
            .VerticalContentAlignment = VerticalAlignment.Center
        }
        If Not Double.IsNaN(width) Then box.Width = width
        Return box
    End Function

    Public Function MakeCombo(options As IEnumerable(Of ChoiceInfo), selected As String) As ComboBox
        Dim combo As New ComboBox With {.Height = 26}
        FillCombo(combo, options, selected)
        Return combo
    End Function

    Public Sub FillCombo(combo As ComboBox, options As IEnumerable(Of ChoiceInfo), selected As String)
        combo.Items.Clear()
        Dim selectedItem As ComboBoxItem = Nothing
        For Each opt In options
            Dim item As New ComboBoxItem With {
                .Content = opt.Display,
                .Tag = opt.Value
            }
            combo.Items.Add(item)
            If String.Equals(opt.Value, If(selected, ""), StringComparison.OrdinalIgnoreCase) Then selectedItem = item
        Next
        combo.SelectedItem = selectedItem
        If combo.SelectedItem Is Nothing AndAlso combo.Items.Count > 0 Then combo.SelectedIndex = 0
    End Sub

    Public Function ComboValue(combo As ComboBox) As String
        Dim item = TryCast(combo.SelectedItem, ComboBoxItem)
        If item IsNot Nothing Then Return If(CStr(item.Tag), "")
        Return ""
    End Function

    Public Sub SelectComboValue(combo As ComboBox, value As String)
        For Each item In combo.Items
            Dim ci = TryCast(item, ComboBoxItem)
            If ci IsNot Nothing AndAlso String.Equals(CStr(ci.Tag), If(value, ""), StringComparison.OrdinalIgnoreCase) Then
                combo.SelectedItem = ci
                Return
            End If
        Next
    End Sub

    ''' <summary>创建一个属性行：左侧标签，右侧编辑控件。</summary>
    Public Function PropertyRow(label As String, editor As FrameworkElement) As Grid
        Dim grid As New Grid With {.Margin = New Thickness(0, 0, 0, 7)}
        grid.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(96)})
        grid.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})
        Dim text = FieldLabel(label)
        Grid.SetColumn(text, 0)
        editor.Margin = New Thickness(0)
        Grid.SetColumn(editor, 1)
        grid.Children.Add(text)
        grid.Children.Add(editor)
        Return grid
    End Function

    Public Function PropertyRow(label As String, editor As FrameworkElement, hint As String) As Grid
        Dim grid = PropertyRow(label, editor)
        If Not String.IsNullOrEmpty(hint) Then
            grid.ToolTip = hint
        End If
        Return grid
    End Function

    Public Function SectionTitle(text As String) As Border
        Dim border As New Border With {
            .Background = Brush("#2A2A2B"),
            .Padding = New Thickness(10, 6, 10, 6),
            .Margin = New Thickness(0, 6, 0, 8),
            .CornerRadius = New CornerRadius(2)
        }
        Dim titleText = Label(text, 11, "#CCCCCC")
        titleText.FontWeight = FontWeights.SemiBold
        border.Child = titleText
        Return border
    End Function

    Public Function Badge(text As String, background As String, Optional foreground As String = "#FFFFFF") As Border
        Dim border As New Border With {
            .Background = Brush(background),
            .CornerRadius = New CornerRadius(2),
            .Padding = New Thickness(5, 1, 5, 1),
            .Margin = New Thickness(4, 0, 0, 0),
            .VerticalAlignment = VerticalAlignment.Center
        }
        border.Child = New TextBlock With {
            .Text = text,
            .FontSize = 10,
            .Foreground = Brush(foreground),
            .VerticalAlignment = VerticalAlignment.Center
        }
        Return border
    End Function

    Public Function AccentLine(hex As String, Optional width As Double = 3) As Border
        Return New Border With {
            .Background = Brush(hex),
            .Width = width,
            .HorizontalAlignment = HorizontalAlignment.Left,
            .CornerRadius = New CornerRadius(1)
        }
    End Function

    Public Function Separator() As Border
        Return New Border With {
            .Width = 1,
            .Background = Brush("#464649"),
            .Margin = New Thickness(5, 6, 5, 6)
        }
    End Function

End Module
