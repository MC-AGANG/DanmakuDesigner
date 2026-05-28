Public Class Button
    Public Property Icon As ImageBrush
        Set(value As ImageBrush)
            If value.ImageSource.Width > Width OrElse value.ImageSource.Height > Height Then
                RC_Icon.HorizontalAlignment = HorizontalAlignment.Stretch
                RC_Icon.VerticalAlignment = VerticalAlignment.Stretch
            Else
                RC_Icon.HorizontalAlignment = HorizontalAlignment.Center
                RC_Icon.VerticalAlignment = VerticalAlignment.Center
                RC_Icon.Width = value.ImageSource.Width
                RC_Icon.Height = value.ImageSource.Height
            End If
            RC_Icon.Fill = value
        End Set
        Get
            Return RC_Icon.Fill
        End Get
    End Property

    Public Event Clicked()
    Private LeftButtonPressed As Boolean = False
    Public Property Text As String
        Set(value As String)
            LB_Text.Content = value
        End Set
        Get
            Return LB_Text.Content
        End Get
    End Property
    Private Sub UserControl_MouseUp(sender As Object, e As MouseButtonEventArgs)
        If LeftButtonPressed AndAlso e.LeftButton = MouseButtonState.Released Then
            RaiseEvent Clicked()
            If IsEnabled Then
                RC_Icon.Opacity = 1
                LB_Text.Opacity = 1
            End If
        End If
        LeftButtonPressed = False

    End Sub

    Private Sub UserControl_MouseDown(sender As Object, e As MouseButtonEventArgs)
        If e.LeftButton = MouseButtonState.Pressed Then
            RC_Icon.Opacity = 0.5
            LB_Text.Opacity = 0.5
            LeftButtonPressed = True
        End If
    End Sub

    Private Sub UserControl_MouseEnter(sender As Object, e As MouseEventArgs)
        Background = New SolidColorBrush(Color.FromArgb(32, 255, 255, 255))
    End Sub

    Private Sub UserControl_MouseLeave(sender As Object, e As MouseEventArgs)
        Background = New SolidColorBrush(Color.FromArgb(1, 255, 255, 255))
        If IsEnabled Then
            RC_Icon.Opacity = 1
            LB_Text.Opacity = 1
        End If
    End Sub

    Private Sub UserControl_IsEnabledChanged(sender As Object, e As DependencyPropertyChangedEventArgs)
        If IsEnabled Then
            RC_Icon.Opacity = 1
            LB_Text.Opacity = 1
        Else
            RC_Icon.Opacity = 0.5
            LB_Text.Opacity = 0.5
        End If
    End Sub
End Class
