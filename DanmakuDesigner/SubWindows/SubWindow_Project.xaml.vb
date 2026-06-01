Imports System.Collections
Imports System.Collections.ObjectModel
Imports DanmakuDesigner.PublicArea
Imports ResourcePack.DD
Public Class SubWindow_Project

    Public Property Nodes As New ObservableCollection(Of TreeNode)

    Public Sub New()
        DataContext = Me
        SW_Project = Me
        InitializeComponent()
    End Sub
    Private Sub UserControl_Loaded(sender As Object, e As RoutedEventArgs)
        LoadProject("")
    End Sub
    Public Sub LoadProject(Path As String)
        Nodes.Clear()
        Nodes.Add(New TreeNode.Node_Project("NewProject"))
        Dim n As New TreeNode.Node_Stage("Stage1")
        Nodes(0).Children.Add(n)
        n.ShowProperty()
    End Sub
    Private Sub TreeViewItem_MouseDoubleClick(sender As Object, e As MouseButtonEventArgs)
        Dim source As TreeNode = CType(sender, TreeViewItem).DataContext
        source.ShowProperty()
        e.Handled = True
    End Sub

    Private Sub MI_Project_AddStage_Click(sender As Object, e As RoutedEventArgs)
        Dim n As Integer = 0
        Dim f As Boolean = False
        Do Until f
            f = True
            n += 1
            For Each c In Nodes(0).Children
                If String.Equals("Stage" + CStr(n), c.Name, StringComparison.OrdinalIgnoreCase) Then
                    f = False
                    Exit For
                End If
            Next
        Loop
        Nodes(0).Children.Add(New TreeNode.Node_Stage("Stage" + CStr(n)))
    End Sub

    Private Sub MI_Stage_Delete_Click(sender As Object, e As RoutedEventArgs)
        Dim menuItem = CType(sender, MenuItem)
        Dim menu = CType(menuItem.Parent, ContextMenu)
        Dim treeItem = CType(menu.PlacementTarget, TreeViewItem)
        Dim node = CType(treeItem.DataContext, TreeNode)
        Nodes(0).Children.Remove(node)
    End Sub
End Class
Public MustInherit Class TreeNode
    Public Property Name As String
    Public Property Type As NodeType
    Public Property Children As New ObservableCollection(Of TreeNode)
    Public ReadOnly Property Icon As ImageBrush
        Get
            Select Case Type
                Case 0
                    Return Textures.Icon_Project
                Case 1
                    Return Textures.Icon_Stage
                Case Else
                    Return Textures.Icon_Project
            End Select
        End Get
    End Property
    Public MustOverride Sub ShowProperty()

    Public Class Node_Project
        Inherits TreeNode
        Public Sub New(name As String)
            Type = 0
            Me.Name = name
        End Sub
        Public Overrides Sub ShowProperty()

        End Sub
    End Class
    Public Class Node_Stage
        Inherits TreeNode
        Public Events As New List(Of EventPoint)
        Public Property Length As ULong
            Set(value As ULong)
                _Length = value
                If PublicArea.Events Is Events Then
                    SW_Timeline.Length = value
                End If
            End Set
            Get
                Return _Length
            End Get
        End Property
        Private _Length As ULong = 1000
        Public Sub New(name As String)
            Type = 1
            Me.Name = name

        End Sub
        Public Overrides Sub ShowProperty()
            For Each e In PublicArea.Events
                If SW_Timeline.CV_Main.Children.Contains(e) Then
                    SW_Timeline.CV_Main.Children.Remove(e)
                End If
            Next
            PublicArea.Events = Events
            For Each e In Events
                SW_Timeline.CV_Main.Children.Add(e)
            Next
            If SW_Property IsNot Nothing Then
                SW_Property.Load(Me)
            End If
            SW_Timeline.Length = Length
        End Sub
    End Class
End Class
Public Enum NodeType
    Project = 0
    Stage = 1
End Enum