Imports DanmakuDesigner.Core

''' <summary>背景填充/3D 面片可选的图片资源。</summary>
Public Module ResourceChoices

    Public Function ImageFillChoices() As List(Of ChoiceInfo)
        Dim choices As New List(Of ChoiceInfo) From {
            New ChoiceInfo("", "纯色填充")
        }
        If App.Session Is Nothing OrElse App.Session.Project Is Nothing Then Return choices
        For Each resource In App.Session.Project.Resources
            If resource Is Nothing OrElse resource.Kind <> GameResourceKind.Image Then Continue For
            choices.Add(New ChoiceInfo(resource.Id, resource.Name))
        Next
        Return choices
    End Function

    Public Function HasImageResources() As Boolean
        If App.Session Is Nothing OrElse App.Session.Project Is Nothing Then Return False
        Return App.Session.Project.Resources.Any(Function(r) r IsNot Nothing AndAlso r.Kind = GameResourceKind.Image)
    End Function

    Public Function AudioChoices() As List(Of ChoiceInfo)
        Dim choices As New List(Of ChoiceInfo)
        If App.Session Is Nothing OrElse App.Session.Project Is Nothing Then Return choices
        For Each resource In App.Session.Project.Resources
            If resource Is Nothing OrElse resource.Kind <> GameResourceKind.Audio Then Continue For
            If String.IsNullOrWhiteSpace(resource.Name) Then Continue For
            choices.Add(New ChoiceInfo(resource.Id, resource.Name))
        Next
        Return choices
    End Function

    Public Function ImageResourceChoices() As List(Of ChoiceInfo)
        Dim choices As New List(Of ChoiceInfo)
        If App.Session Is Nothing OrElse App.Session.Project Is Nothing Then Return choices
        For Each resource In App.Session.Project.Resources
            If resource Is Nothing OrElse resource.Kind <> GameResourceKind.Image Then Continue For
            If String.IsNullOrWhiteSpace(resource.Name) Then Continue For
            choices.Add(New ChoiceInfo(resource.Id, resource.Name))
        Next
        Return choices
    End Function

End Module
