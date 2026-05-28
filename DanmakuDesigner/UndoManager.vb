Imports DanmakuDesigner.PublicArea
Public Interface IUndoableCommand
    Function Execute() As Boolean
    Sub Undo()
End Interface
Public Class UndoManager
    Private UndoStack As New Stack(Of IUndoableCommand)
    Private RedoStack As New Stack(Of IUndoableCommand)

    Public Sub Execute(cmd As IUndoableCommand)
        If cmd.Execute() Then
            UndoStack.Push(cmd)
            RedoStack.Clear()
        End If
        UpdateButton()
    End Sub

    Public Sub Undo()
        If UndoStack.Count = 0 Then Return
        Dim cmd = UndoStack.Pop()
        cmd.Undo()
        RedoStack.Push(cmd)
        UpdateButton()
    End Sub

    Public Sub Redo()
        If RedoStack.Count = 0 Then Return
        Dim cmd = RedoStack.Pop()
        cmd.Execute()
        UndoStack.Push(cmd)
        UpdateButton()
    End Sub
    Public Sub UpdateButton()
        If UndoStack.Count > 0 Then
            MW.BT_Undo.IsEnabled = True
        Else
            MW.BT_Undo.IsEnabled = False
        End If
        If RedoStack.Count > 0 Then
            MW.BT_Redo.IsEnabled = True
        Else
            MW.BT_Redo.IsEnabled = False
        End If
    End Sub
End Class
Public Class PropertyChangeCommand(Of T)
    Implements IUndoableCommand
    Private Target As Object
    Private [Property] As Reflection.PropertyInfo
    Private OldValue As T
    Private NewValue As T

    Public Sub New(
        targetObj As Object,
        propertyName As String,
        oldVal As T,
        newVal As T)
        Target = targetObj
        [Property] = targetObj.GetType().GetProperty(propertyName)
        OldValue = oldVal
        NewValue = newVal
    End Sub

    Public Function Execute() As Boolean Implements IUndoableCommand.Execute
        If OldValue IsNot Nothing AndAlso NewValue IsNot Nothing Then
            If Not OldValue.Equals(NewValue) Then
                [Property].SetValue(Target, NewValue)
                Return True
            Else
                Return False
            End If
        Else
            If Not (OldValue Is Nothing AndAlso NewValue Is Nothing) Then
                [Property].SetValue(Target, NewValue)
                Return True
            Else
                Return False
            End If
        End If
    End Function

    Public Sub Undo() Implements IUndoableCommand.Undo
        [Property].SetValue(Target, OldValue)
    End Sub

End Class

Public Class EventAddCommand
    Implements IUndoableCommand
    Private Target As EventPoint
    Public Sub New(Target As EventPoint)
        Me.Target = Target
    End Sub
    Public Function Execute() As Boolean Implements IUndoableCommand.Execute
        Events.Add(Target)
        SW_Timeline.CV_Main.Children.Add(Target)
        Return True
    End Function

    Public Sub Undo() Implements IUndoableCommand.Undo
        Events.Remove(Target)
        SW_Timeline.CV_Main.Children.Remove(Target)
    End Sub
End Class
Public Class EventDeleteCommand
    Implements IUndoableCommand
    Private Targets As List(Of EventPoint)
    Public Sub New(Targets As List(Of EventPoint))
        Me.Targets = Targets.Distinct.ToList
    End Sub
    Public Function Execute() As Boolean Implements IUndoableCommand.Execute
        If Targets.Count > 0 Then
            For Each target In Targets
                If Events.Contains(target) Then
                    target.Remove()
                End If
            Next
            Return True
        Else
            Return False
        End If
    End Function

    Public Sub Undo() Implements IUndoableCommand.Undo
        For Each target In Targets
            Events.Add(Target)
            SW_Timeline.CV_Main.Children.Add(target)
        Next
    End Sub
End Class