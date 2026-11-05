Option Strict On
Option Infer On

Namespace Global.DanmakuDesigner.Core

    Public NotInheritable Class DropItemInfo
        Public ReadOnly Property Id As Integer
        Public ReadOnly Property Name As String
        Public ReadOnly Property Effect As String
        Public ReadOnly Property Color As String

        Public Sub New(id As Integer, name As String, effect As String, color As String)
            Me.Id = id
            Me.Name = name
            Me.Effect = effect
            Me.Color = color
        End Sub
    End Class

    ''' <summary>AgSTG DropItems 字符串的解析与生成。每个字符代表一个掉落物。</summary>
    Public Module DropItemCatalog

        Public ReadOnly Items As IReadOnlyList(Of DropItemInfo) = New List(Of DropItemInfo) From {
            New DropItemInfo(0, "P 点", "增加火力", "#E06C75"),
            New DropItemInfo(1, "蓝点", "增加分数", "#61AFEF"),
            New DropItemInfo(2, "信仰点", "增加信仰值", "#ABFF00"),
            New DropItemInfo(3, "残机", "增加一条生命", "#FF0078"),
            New DropItemInfo(4, "残机碎片", "收集碎片", "#FF64AD"),
            New DropItemInfo(5, "符卡（B）", "增加符卡数量", "#12FF00"),
            New DropItemInfo(6, "符卡碎片", "收集符卡碎片", "#0DBE00")
        }

        Public Function Find(id As Integer) As DropItemInfo
            Return Items.FirstOrDefault(Function(i) i.Id = id)
        End Function

        Public Function Counts(value As String) As Integer()
            Dim result(Items.Count - 1) As Integer
            If String.IsNullOrEmpty(value) Then Return result
            For Each c In value
                Dim id = AscW(c) - AscW("0"c)
                If id >= 0 AndAlso id < Items.Count Then result(id) += 1
            Next
            Return result
        End Function

        Public Function Build(counts As Integer()) As String
            Dim sb As New Text.StringBuilder()
            If counts Is Nothing Then Return ""
            For i = 0 To Math.Min(Items.Count - 1, counts.Length - 1)
                Dim count = Math.Max(0, Math.Min(999, counts(i)))
                If count > 0 Then sb.Append(ChrW(AscW("0"c) + i), count)
            Next
            Return sb.ToString()
        End Function

        Public Function Total(value As String) As Integer
            Return Counts(value).Sum()
        End Function

        Public Function Describe(value As String) As String
            Dim itemCounts = Counts(value)
            Dim parts As New List(Of String)
            For i = 0 To itemCounts.Length - 1
                If itemCounts(i) > 0 Then parts.Add(Items(i).Name & " ×" & itemCounts(i))
            Next
            If parts.Count = 0 Then Return "无掉落物"
            Return String.Join("、", parts) & "（共 " & itemCounts.Sum() & " 个）"
        End Function

        Public Function Normalize(value As String) As String
            Return Build(Counts(value))
        End Function
    End Module
End Namespace
