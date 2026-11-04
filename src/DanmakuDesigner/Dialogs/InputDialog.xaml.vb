Imports System.Windows
Imports System.Windows.Input

Public Class InputDialog

    Public Property ResultValue As String = ""
    Private _validator As Func(Of String, String) = Nothing

    Public Sub New()
        InitializeComponent()
        TitleIcon.Source = IconLibrary.Get("Property_16x.png")
        AddHandler PreviewKeyDown, AddressOf Dialog_PreviewKeyDown
        AddHandler Loaded, Sub()
                      ValueBox.Focus()
                      ValueBox.SelectAll()
                  End Sub
    End Sub

    Private Sub Dialog_PreviewKeyDown(sender As Object, e As KeyEventArgs)
        If e.Key = Key.Escape Then
            DialogResult = False
            e.Handled = True
        End If
    End Sub

    Private Sub Ok_Click(sender As Object, e As RoutedEventArgs)
        Dim value = ValueBox.Text
        If _validator IsNot Nothing Then
            Dim message = _validator(value)
            If Not String.IsNullOrEmpty(message) Then
                ErrorText.Text = message
                ErrorText.Visibility = Visibility.Visible
                ValueBox.Focus()
                Return
            End If
        End If
        ResultValue = value
        DialogResult = True
    End Sub

    Private Sub Cancel_Click(sender As Object, e As RoutedEventArgs)
        DialogResult = False
    End Sub

    Public Shared Function ShowInput(owner As Window, title As String, prompt As String, initial As String, Optional validator As Func(Of String, String) = Nothing) As String
        Dim dialog As New InputDialog()
        dialog.Owner = owner
        dialog.TitleText.Text = title
        dialog.PromptText.Text = prompt
        dialog.ValueBox.Text = If(initial, "")
        dialog._validator = validator
        If dialog.ShowDialog() = True Then
            Return dialog.ResultValue
        End If
        Return Nothing
    End Function

End Class
