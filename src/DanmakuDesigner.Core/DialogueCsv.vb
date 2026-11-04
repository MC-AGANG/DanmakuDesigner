Option Strict On
Option Infer On

Imports System.Text

Namespace Global.DanmakuDesigner.Core

    ''' <summary>AgSTG DialogArea.LoadDialog 使用的 CSV 格式。</summary>
    Public Module DialogueCsv

        Public Function ToCsv(dialogue As DialogueDefinition) As String
            Dim sb As New StringBuilder()
            sb.AppendLine("player,enemy,,,")
            sb.AppendLine("0,0,,,")
            sb.AppendLine("speaker,content,illustration,cd,auto")
            If dialogue IsNot Nothing AndAlso dialogue.Lines IsNot Nothing Then
                For Each line In dialogue.Lines
                    If line Is Nothing Then Continue For
                    Dim content = If(line.Content, "").Replace(vbCrLf, "<br/>").Replace(vbCr, "<br/>").Replace(vbLf, "<br/>")
                    content = content.Replace(",", "，")
                    sb.Append(If(line.Speaker = DialogueSpeaker.Enemy, "1", "0")).Append(","c)
                    sb.Append(content).Append(","c)
                    sb.Append(Math.Max(0, line.Illustration)).Append(","c)
                    sb.Append(Math.Max(0, line.CD)).Append(","c)
                    sb.AppendLine(If(line.Auto, "1", "0"))
                Next
            End If
            Return sb.ToString()
        End Function

        Public Function Parse(csv As String, name As String) As DialogueDefinition
            Dim result As New DialogueDefinition With {.Name = If(String.IsNullOrWhiteSpace(name), "导入对话", name)}
            If String.IsNullOrWhiteSpace(csv) Then Return result
            Dim rows = csv.Replace(vbCrLf, vbLf).Replace(vbCr, vbLf).Split(ControlChars.Lf)
            Dim dataStarted = False
            For Each row In rows
                If String.IsNullOrWhiteSpace(row) Then Continue For
                If Not dataStarted Then
                    If row.StartsWith("speaker,", StringComparison.OrdinalIgnoreCase) Then dataStarted = True
                    Continue For
                End If
                Dim parts = row.Split(","c)
                If parts.Length < 5 Then Continue For
                Dim speaker As DialogueSpeaker = DialogueSpeaker.Player
                If CInt(Val(parts(0))) = 1 Then speaker = DialogueSpeaker.Enemy
                Dim content = parts(1).Replace("<br/>", vbCrLf).Replace("<br>", vbCrLf)
                Dim illustration = CInt(Val(parts(2)))
                Dim cd = CInt(Val(parts(3)))
                Dim isAuto = CInt(Val(parts(4))) <> 0
                result.Lines.Add(New DialogueLine With {
                    .Speaker = speaker,
                    .Content = content,
                    .Illustration = Math.Max(0, illustration),
                    .CD = Math.Max(0, cd),
                    .Auto = isAuto
                })
            Next
            Return result
        End Function
    End Module
End Namespace
