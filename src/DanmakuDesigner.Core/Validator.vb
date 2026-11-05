Option Strict On
Option Infer On

Imports System.Linq
Imports System.Text.RegularExpressions

Namespace Global.DanmakuDesigner.Core

    Public Enum IssueSeverity
        Info = 0
        Warning = 1
        [Error] = 2
    End Enum

    Public Class ValidationIssue
        Public Property Severity As IssueSeverity = IssueSeverity.Info
        Public Property Message As String = ""
        Public Property TargetKind As String = ""
        Public Property TargetId As String = ""
        Public Property ObjectName As String = ""
        Public Property Tick As Integer = -1

        Public ReadOnly Property SeverityText As String
            Get
                Select Case Severity
                    Case IssueSeverity.[Error] : Return "错误"
                    Case IssueSeverity.Warning : Return "警告"
                    Case Else : Return "信息"
                End Select
            End Get
        End Property

        Public ReadOnly Property LocationText As String
            Get
                If Tick >= 0 Then Return "Tick " & Tick.ToString()
                Return If(ObjectName, "")
            End Get
        End Property
    End Class

    Public Module Validator

        Private ReadOnly IdentifierPattern As New Regex("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled)

        Public Function Validate(project As StageProject) As List(Of ValidationIssue)
            Dim issues As New List(Of ValidationIssue)
            If project Is Nothing OrElse project.Stage Is Nothing Then
                issues.Add(Make(IssueSeverity.[Error], "工程为空。", "stage", "", ""))
                Return issues
            End If
            Dim stage = project.Stage
            stage.Normalize()

            If String.IsNullOrWhiteSpace(stage.Name) Then
                issues.Add(Make(IssueSeverity.Warning, "关卡名称为空。", "stage", "", ""))
            End If
            If Not IdentifierPattern.IsMatch(If(stage.CodeName, "")) Then
                issues.Add(Make(IssueSeverity.[Error], "代码类名必须是 ASCII 标识符（字母/数字/下划线，且不能以数字开头）：" & If(stage.CodeName, ""), "stage", "", stage.CodeName))
            End If
            If stage.LengthTicks < 60 Then
                issues.Add(Make(IssueSeverity.Warning, "关卡长度少于 1 秒。", "stage", "", stage.Name))
            End If
            If stage.LengthTicks > 36000 Then
                issues.Add(Make(IssueSeverity.Warning, "关卡长度超过 10 分钟，时间轴会比较难操作。", "stage", "", stage.Name))
            End If

            If stage.EnemyTemplates.Count = 0 Then
                issues.Add(Make(IssueSeverity.Warning, "还没有任何敌机模板。", "template", "", ""))
            End If
            If stage.BulletPatterns.Count = 0 Then
                issues.Add(Make(IssueSeverity.Info, "还没有子弹样式；发射积木将无法导出。", "pattern", "", ""))
            End If

            Dim names As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
            For Each t In stage.EnemyTemplates
                If Not names.Add(t.Name) Then
                    issues.Add(Make(IssueSeverity.Warning, "敌机模板重名：" & t.Name, "template", t.Id, t.Name))
                End If
                If t.HP <= 0 Then issues.Add(Make(IssueSeverity.[Error], "敌机血量必须大于 0。", "template", t.Id, t.Name))
                If t.Life <= 0 Then issues.Add(Make(IssueSeverity.[Error], "敌机存在时间必须大于 0。", "template", t.Id, t.Name))
                If t.Defence < 0 OrElse t.Defence >= 1 Then issues.Add(Make(IssueSeverity.Warning, "防御值应在 0 ~ 1 之间。", "template", t.Id, t.Name))
                For Each ch In t.DropItems
                    If ch < "0"c OrElse ch > "6"c Then
                        issues.Add(Make(IssueSeverity.Warning, "掉落物字符串只能包含 0~6：" & t.DropItems, "template", t.Id, t.Name))
                        Exit For
                    End If
                Next
                If t.Speed < 0 Then issues.Add(Make(IssueSeverity.Warning, "速度不能为负数。", "template", t.Id, t.Name))
                If t.IsBoss Then
                    Dim selectedSpells = t.SpellCardIds.Select(Function(id) stage.FindSpell(id)).Where(Function(s) s IsNot Nothing).ToList()
                    If selectedSpells.Count = 0 Then
                        issues.Add(Make(IssueSeverity.Warning, "Boss 模板还没有选择符卡。", "template", t.Id, t.Name))
                    End If
                    For Each spellId In t.SpellCardIds
                        If stage.FindSpell(spellId) Is Nothing Then
                            issues.Add(Make(IssueSeverity.Warning, "Boss 引用了不存在的符卡：" & spellId, "template", t.Id, t.Name))
                        End If
                    Next
                    If t.UseResourceBossTextures AndAlso t.BossNormalTextureIds.Count = 0 Then
                        issues.Add(Make(IssueSeverity.Warning, "Boss 还没有选择普通贴图。", "template", t.Id, t.Name))
                    End If
                End If
                ValidateProgram(t.Behavior, ProgramScope.Enemy, stage, t.Id, t.Name, issues)
            Next

            For Each dialogue In stage.Dialogues
                If dialogue Is Nothing Then Continue For
                If dialogue.Lines.Count = 0 Then
                    issues.Add(Make(IssueSeverity.Warning, "对话没有任何内容。", "dialogue", dialogue.Id, dialogue.Name))
                End If
                If dialogue.AfterAction = DialogueAfterAction.StartBoss Then
                    Dim bossTemplate = stage.FindTemplate(dialogue.BossTemplateId)
                    If bossTemplate Is Nothing OrElse Not bossTemplate.IsBoss Then
                        issues.Add(Make(IssueSeverity.Warning, "对话结束后要 Boss 开打，但没有选择有效的 Boss 模板。", "dialogue", dialogue.Id, dialogue.Name))
                    End If
                End If
            Next

            names.Clear()
            For Each p In stage.BulletPatterns
                If Not names.Add(p.Name) Then
                    issues.Add(Make(IssueSeverity.Warning, "子弹样式重名：" & p.Name, "pattern", p.Id, p.Name))
                End If
                If p.BulletType = 0 Then
                    issues.Add(Make(IssueSeverity.[Error], "不能使用 0 号（激光）作为普通子弹样式。", "pattern", p.Id, p.Name))
                End If
                ValidateProgram(p.Behavior, ProgramScope.Bullet, stage, p.Id, p.Name, issues)
            Next

            names.Clear()
            For Each spell In stage.SpellCards
                If spell Is Nothing Then Continue For
                If Not names.Add(spell.Name) Then
                    issues.Add(Make(IssueSeverity.Warning, "符卡重名：" & spell.Name, "spell", spell.Id, spell.Name))
                End If
                ValidateProgram(spell.Behavior, ProgramScope.Enemy, stage, spell.Id, spell.Name, issues)
            Next

            If stage.Tracks.Count = 0 Then
                issues.Add(Make(IssueSeverity.Warning, "时间轴上还没有轨道。", "track", "", ""))
            End If
            names.Clear()
            For Each t In stage.Tracks
                If Not names.Add(t.Name) Then
                    issues.Add(Make(IssueSeverity.Warning, "轨道重名：" & t.Name, "track", t.Id, t.Name))
                End If
                If t.Kind = TrackKind.Enemy Then
                    If String.IsNullOrEmpty(t.TemplateId) Then
                        issues.Add(Make(IssueSeverity.[Error], "敌机轨道没有绑定敌机模板。", "track", t.Id, t.Name))
                    ElseIf stage.FindTemplate(t.TemplateId) Is Nothing Then
                        issues.Add(Make(IssueSeverity.[Error], "轨道引用的敌机模板不存在。", "track", t.Id, t.Name))
                    End If
                End If
                If t.Kind = TrackKind.Background Then
                    If String.IsNullOrWhiteSpace(t.BackgroundLayerId) OrElse stage.FindBackgroundLayer(t.BackgroundLayerId) Is Nothing Then
                        issues.Add(Make(IssueSeverity.[Error], "背景动画轨道没有绑定有效的背景图层。", "track", t.Id, t.Name))
                    End If
                End If
                For Each c In t.Clips
                    If c.IsLoopEvent Then
                        If t.Kind <> TrackKind.Background Then
                            issues.Add(Make(IssueSeverity.[Error], "循环事件只能放在背景动画轨道上。", "clip", c.Id, t.Name, c.StartTick))
                        End If
                        If c.LoopStartTick > c.LoopEndTick Then
                            issues.Add(Make(IssueSeverity.[Error], "循环起始帧不能大于循环结束帧。", "clip", c.Id, t.Name, c.StartTick))
                        End If
                        If c.StartTick <= 0 Then
                            issues.Add(Make(IssueSeverity.[Error], "循环事件帧必须大于 0。", "clip", c.Id, t.Name, c.StartTick))
                        End If
                        If c.LoopStartTick >= c.StartTick OrElse c.LoopEndTick >= c.StartTick Then
                            issues.Add(Make(IssueSeverity.[Error], "循环起始帧和结束帧都必须早于循环事件帧。", "clip", c.Id, t.Name, c.StartTick))
                        End If
                        If c.LoopLength < 1 Then
                            issues.Add(Make(IssueSeverity.[Error], "循环长度必须至少为 1 帧。", "clip", c.Id, t.Name, c.StartTick))
                        End If
                        For Each otherTrack In stage.Tracks
                            If otherTrack Is Nothing OrElse otherTrack.Kind <> TrackKind.Background Then Continue For
                            For Each other In otherTrack.Clips
                                If other Is Nothing OrElse other Is c OrElse Not other.IsLoopEvent Then Continue For
                                If other.StartTick >= c.LoopStartTick AndAlso other.StartTick <= c.LoopEndTick Then
                                    issues.Add(Make(IssueSeverity.[Error], "循环区间内不能包含其他循环事件。", "clip", c.Id, t.Name, c.StartTick))
                                    Exit For
                                End If
                            Next
                        Next
                        Continue For
                    End If
                    If c.StartTick > stage.LengthTicks Then
                        issues.Add(Make(IssueSeverity.Warning, "片段起点超过了关卡长度。", "clip", c.Id, t.Name, c.StartTick))
                    End If
                    If c.Count < 1 Then
                        issues.Add(Make(IssueSeverity.Warning, "片段数量至少为 1。", "clip", c.Id, t.Name, c.StartTick))
                    End If
                    If c.Count > 64 Then
                        issues.Add(Make(IssueSeverity.Warning, "单次生成超过 64 个敌机容易卡顿。", "clip", c.Id, t.Name, c.StartTick))
                    End If
                    If c.IntervalTicks = 0 AndAlso c.Count > 1 Then
                        issues.Add(Make(IssueSeverity.Info, "间隔为 0，同一帧将生成多个敌机。", "clip", c.Id, t.Name, c.StartTick))
                    End If
                    If t.Kind = TrackKind.Cue Then
                        If c.Cue = CueKind.Music AndAlso String.IsNullOrWhiteSpace(c.CueText) Then
                            issues.Add(Make(IssueSeverity.Warning, "音乐事件缺少曲名。", "clip", c.Id, t.Name, c.StartTick))
                        End If
                    End If
                Next
            Next

            Dim hasEnd = stage.AllClips().Any(Function(c) c.Cue = CueKind.EndStage)
            If Not hasEnd Then
                issues.Add(Make(IssueSeverity.Info, "没有【结束关卡】事件，游戏不会自动进入下一关。", "stage", "", stage.Name))
            End If

            Return issues.OrderByDescending(Function(i) CInt(i.Severity)).ToList()
        End Function

        Private Sub ValidateProgram(program As Program, scope As ProgramScope, stage As StageDocument, ownerId As String, ownerName As String, issues As List(Of ValidationIssue))
            If program Is Nothing OrElse program.Scripts.Count = 0 Then
                issues.Add(Make(IssueSeverity.Info, ownerName & " 还没有行为脚本。", If(scope = ProgramScope.Enemy, "template", "pattern"), ownerId, ownerName))
                Return
            End If
            For Each script In program.Scripts
                If script Is Nothing Then Continue For
                Dim def = script.Definition
                If def Is Nothing Then
                    issues.Add(Make(IssueSeverity.[Error], "未知的积木类型：" & script.TypeId, "block", ownerId, ownerName))
                    Continue For
                End If
                If Not def.IsHat Then
                    issues.Add(Make(IssueSeverity.[Error], "脚本必须以事件积木开始。", "block", ownerId, ownerName))
                End If
                For Each child In script.Children
                    ValidateBlock(child, scope, stage, ownerId, ownerName, issues, topLevelOfScript:=True, inLoop:=False)
                Next
            Next
        End Sub

        Private Sub ValidateBlock(block As BlockNode, scope As ProgramScope, stage As StageDocument, ownerId As String, ownerName As String, issues As List(Of ValidationIssue), topLevelOfScript As Boolean, inLoop As Boolean)
            If block Is Nothing Then Return
            Dim def = block.Definition
            If def Is Nothing Then
                issues.Add(Make(IssueSeverity.[Error], "未知的积木类型：" & block.TypeId, "block", ownerId, ownerName))
                Return
            End If
            If Not def.Allows(scope) Then
                issues.Add(Make(IssueSeverity.[Error], def.Title & " 不能用在" & If(scope = ProgramScope.Enemy, "敌机", "子弹") & "行为中。", "block", ownerId, ownerName))
            End If
            If block.TypeId = "wait" AndAlso Not topLevelOfScript Then
                issues.Add(Make(IssueSeverity.[Error], "【等待】只能放在脚本顶层，不能放进如果/重复积木里。", "block", ownerId, ownerName))
            End If
            If block.TypeId = "wait" AndAlso inLoop Then
                issues.Add(Make(IssueSeverity.[Error], "【等待】不能放在重复积木里。", "block", ownerId, ownerName))
            End If
            If block.TypeId = "repeat" Then
                Dim times = block.GetDouble("times", 3)
                If times <= 0 Then issues.Add(Make(IssueSeverity.Warning, "重复次数为 0，主体不会执行。", "block", ownerId, ownerName))
                If times > 500 Then issues.Add(Make(IssueSeverity.Warning, "重复次数过大，可能造成卡顿。", "block", ownerId, ownerName))
            End If
            If block.TypeId = "if" AndAlso block.GetField("subj") = "var" AndAlso String.IsNullOrWhiteSpace(block.GetField("varName")) Then
                issues.Add(Make(IssueSeverity.Warning, "条件使用了【变量】，但没有填写变量名。", "block", ownerId, ownerName))
            End If
            If block.TypeId.StartsWith("fire_", StringComparison.Ordinal) Then
                If block.TypeId <> "fire_laser" Then
                    Dim patternId = block.GetField("pattern")
                    If String.IsNullOrEmpty(patternId) Then
                        issues.Add(Make(IssueSeverity.Warning, "发射积木没有选择子弹样式，导出时会被忽略。", "block", ownerId, ownerName))
                    ElseIf stage.FindPattern(patternId) Is Nothing Then
                        issues.Add(Make(IssueSeverity.[Error], "发射积木引用的子弹样式已不存在。", "block", ownerId, ownerName))
                    End If
                    Dim count = block.GetDouble("count", 1)
                    If count < 1 Then issues.Add(Make(IssueSeverity.Warning, "子弹数量至少为 1。", "block", ownerId, ownerName))
                    If count > 200 Then issues.Add(Make(IssueSeverity.Warning, "单次发射子弹过多（>200）。", "block", ownerId, ownerName))
                End If
            End If

            For Each f In def.Fields
                If f.Kind = FieldKind.Number Then
                    Dim raw = block.GetField(f.Key)
                    If Not ValueExpression.IsReporter(raw) AndAlso Not String.IsNullOrWhiteSpace(raw) Then
                        Dim dummy As Double
                        If Not Double.TryParse(raw, Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, dummy) Then
                            issues.Add(Make(IssueSeverity.Warning, "字段【" & f.Key & "】不是有效数字：" & raw, "block", ownerId, ownerName))
                        End If
                    End If
                End If
            Next

            If def.IsContainer Then
                For Each child In block.Children
                    ValidateBlock(child, scope, stage, ownerId, ownerName, issues, topLevelOfScript:=False, inLoop:=(block.TypeId = "repeat"))
                Next
                For Each child In block.ElseChildren
                    ValidateBlock(child, scope, stage, ownerId, ownerName, issues, topLevelOfScript:=False, inLoop:=inLoop)
                Next
            End If
        End Sub

        Private Function Make(severity As IssueSeverity, message As String, kind As String, id As String, name As String, Optional tick As Integer = -1) As ValidationIssue
            Return New ValidationIssue With {
                .Severity = severity,
                .Message = message,
                .TargetKind = kind,
                .TargetId = id,
                .ObjectName = name,
                .Tick = tick
            }
        End Function

    End Module

End Namespace
