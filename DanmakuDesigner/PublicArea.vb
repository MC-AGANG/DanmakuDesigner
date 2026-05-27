Public Class PublicArea
    Public Shared MW As MainWindow
    Public Shared SW_Stage As SubWindow_Stage
    Public Shared SW_Performance As SubWindow_Performance
    Public Shared SW_Timeline As SubWindow_TimeLine
    Public Shared SW_Property As SubWindow_Property
    Public Shared Events As New List(Of EventPoint)
    Public Shared FocusedTextBox As TextBox
    Public Shared UndoMgr As New UndoManager

    Public Shared Function IsChildOf(child As DependencyObject, parent As DependencyObject) As Boolean
        While child IsNot Nothing
            If child Is parent Then
                Return True
            End If
            child = VisualTreeHelper.GetParent(child)
        End While
        Return False
    End Function
End Class
Module PublicFunctions

End Module