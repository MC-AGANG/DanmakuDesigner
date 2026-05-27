Imports AgSTG
Public Class ToolWindow_ItemEditor
    Public Items As String
    Private Sub BT_Apply_Click(sender As Object, e As RoutedEventArgs)
        Dim s As String = ""
        For i = 1 To Val(TB_Power.Text)
            s += "0"
        Next
        For i = 1 To Val(TB_Point.Text)
            s += "1"
        Next
        For i = 1 To Val(TB_Life.Text)
            s += "3"
        Next
        For i = 1 To Val(TB_LifePiece.Text)
            s += "4"
        Next
        For i = 1 To Val(TB_Spell.Text)
            s += "5"
        Next
        For i = 1 To Val(TB_SpellPiece.Text)
            s += "6"
        Next
        Items = s
        DialogResult = True
        Close()
    End Sub

    Private Sub BT_Cancel_Click(sender As Object, e As RoutedEventArgs)
        DialogResult = False
        Close()
    End Sub
    Private Sub Window_Loaded(sender As Object, e As RoutedEventArgs)
        TB_Power.Text = 0
        TB_Point.Text = 0
        TB_Life.Text = 0
        TB_LifePiece.Text = 0
        TB_Spell.Text = 0
        TB_SpellPiece.Text = 0
        For Each c In Items
            Select Case c
                Case "0"
                    TB_Power.Text = Val(TB_Power.Text) + 1
                Case "1"
                    TB_Point.Text = Val(TB_Point.Text) + 1
                Case "3"
                    TB_Life.Text = Val(TB_Life.Text) + 1
                Case "4"
                    TB_LifePiece.Text = Val(TB_LifePiece.Text) + 1
                Case "5"
                    TB_Spell.Text = Val(TB_Spell.Text) + 1
                Case "6"
                    TB_SpellPiece.Text = Val(TB_SpellPiece.Text) + 1
            End Select
        Next
    End Sub
End Class
