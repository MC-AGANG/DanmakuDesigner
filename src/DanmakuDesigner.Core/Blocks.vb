Option Strict On
Option Infer On

Imports System.Linq
Imports System.Text

Namespace Global.DanmakuDesigner.Core

    Public Enum BlockScope
        Both = 0
        EnemyOnly = 1
        BulletOnly = 2
        ParticleOnly = 3
    End Enum

    Public Enum HatKind
        None = 0
        Spawn = 1
        EveryFrame = 2
        EveryN = 3
        AtTime = 4
        TimeBetween = 5
        HpBelow = 6
        PlayerNear = 7
        Killed = 8
        BossInit = 9
        SpellStart = 10
        SpellEveryFrame = 11
        ParticleSpawn = 12
        ParticleEveryFrame = 13
        ParticleWhenPosition = 14
        SpellUsualStart = 15
        SpellUsualEveryFrame = 16
        SpellFormalStart = 17
        SpellFormalEveryFrame = 18
    End Enum

    Public NotInheritable Class FieldOption
        Public ReadOnly Property Value As String
        Public ReadOnly Property Display As String
        Public Sub New(value As String, display As String)
            Me.Value = value
            Me.Display = display
        End Sub
        Public Overrides Function ToString() As String
            Return Display
        End Function
    End Class

    Public Class FieldDef
        Public Property Key As String = ""
        Public Property Kind As FieldKind = FieldKind.Number
        Public Property DefaultValue As String = ""
        Public Property Width As Double = 48
        Public Property MinValue As Double = -100000
        Public Property MaxValue As Double = 100000
        Public Property OptionsSource As String = ""
        Public Property StaticOptions As List(Of FieldOption) = Nothing
        Public Property Tooltip As String = ""

        Public Function Options(stage As StageDocument, Optional resources As IEnumerable(Of GameResource) = Nothing) As List(Of FieldOption)
            If StaticOptions IsNot Nothing Then Return StaticOptions
            Return BlockRegistry.ResolveOptions(OptionsSource, stage, resources)
        End Function
    End Class

    Public Class BlockPart
        Public Property Text As String = ""
        Public Property FieldKey As String = ""

        Public ReadOnly Property IsField As Boolean
            Get
                Return Not String.IsNullOrEmpty(FieldKey)
            End Get
        End Property

        Public Shared Function Label(text As String) As BlockPart
            Return New BlockPart With {.Text = text}
        End Function

        Public Shared Function Field(key As String) As BlockPart
            Return New BlockPart With {.FieldKey = key}
        End Function
    End Class

    Public Class BlockDefinition
        Public Property TypeId As String = ""
        Public Property Title As String = ""
        Public Property Category As BlockCategory = BlockCategory.Motion
        Public Property Kind As BlockKind = BlockKind.Stack
        Public Property Scope As BlockScope = BlockScope.Both
        ''' <summary>只有 Boss 模板才显示/可用的积木。</summary>
        Public Property RequiresBoss As Boolean = False
        ''' <summary>只在 2D 粒子行为中显示。</summary>
        Public Property Requires2DParticle As Boolean = False
        ''' <summary>只在 3D 粒子行为中显示。</summary>
        Public Property Requires3DParticle As Boolean = False
        ''' <summary>只在符卡自身的积木行为中显示。</summary>
        Public Property SpellBehaviorOnly As Boolean = False
        ''' <summary>只在 Boss 模板行为中显示（按符卡序号控制）。</summary>
        Public Property BossBehaviorOnly As Boolean = False
        Public Property Tooltip As String = ""
        Public Property HatKind As HatKind = HatKind.None
        Public Property Fields As New List(Of FieldDef)
        Public Property Parts As New List(Of BlockPart)

        Public ReadOnly Property IsHat As Boolean
            Get
                Return Kind = BlockKind.Hat
            End Get
        End Property

        Public ReadOnly Property IsContainer As Boolean
            Get
                Return Kind = BlockKind.CBlock OrElse Kind = BlockKind.CBlockElse
            End Get
        End Property

        Public ReadOnly Property CategoryColor As String
            Get
                Select Case Category
                    Case BlockCategory.Trigger : Return "#C792EA"
                    Case BlockCategory.Motion : Return "#4FC1FF"
                    Case BlockCategory.Attack : Return "#F48771"
                    Case BlockCategory.Control : Return "#E2C08D"
                    Case BlockCategory.Particle : Return "#9CDCFE"
                    Case Else : Return "#9CDCFE"
                End Select
            End Get
        End Property

        Public Function FindField(key As String) As FieldDef
            Return Fields.FirstOrDefault(Function(f) String.Equals(f.Key, key, StringComparison.OrdinalIgnoreCase))
        End Function

        Public Function Allows(programScope As ProgramScope, Optional particle3D As Boolean = False, Optional isSpellBehavior As Boolean = False) As Boolean
            If Requires2DParticle AndAlso particle3D Then Return False
            If Requires3DParticle AndAlso Not particle3D Then Return False
            If SpellBehaviorOnly AndAlso Not isSpellBehavior Then Return False
            If BossBehaviorOnly AndAlso isSpellBehavior Then Return False
            If programScope = ProgramScope.Particle Then Return Me.Scope = BlockScope.ParticleOnly
            If Me.Scope = BlockScope.ParticleOnly Then Return False
            If Me.Scope = BlockScope.Both Then Return True
            If programScope = ProgramScope.Enemy Then Return Me.Scope = BlockScope.EnemyOnly
            Return Me.Scope = BlockScope.BulletOnly
        End Function

        Public Function Summary(block As BlockNode, stage As StageDocument) As String
            Dim sb As New StringBuilder()
            For Each part In Parts
                If part.IsField Then
                    Dim fd = FindField(part.FieldKey)
                    Dim raw = block.GetField(part.FieldKey)
                    If fd IsNot Nothing AndAlso fd.Kind = FieldKind.Choice Then
                        Dim match = fd.Options(stage).FirstOrDefault(Function(o) o.Value = raw)
                        sb.Append(If(match IsNot Nothing, match.Display, raw))
                    Else
                        sb.Append(raw)
                    End If
                Else
                    sb.Append(part.Text)
                End If
            Next
            Return sb.ToString()
        End Function
    End Class

    Public Module BlockRegistry

        Private ReadOnly _all As New List(Of BlockDefinition)()
        Private ReadOnly _byId As New Dictionary(Of String, BlockDefinition)(StringComparer.OrdinalIgnoreCase)
        Private _initialized As Boolean = False
        Private ReadOnly _initLock As New Object()

        Private Sub EnsureBuilt()
            If _initialized Then Return
            SyncLock _initLock
                If _initialized Then Return
                BuildHats()
                BuildMotion()
                BuildAttack()
                BuildControl()
                BuildParticle()
                _initialized = True
            End SyncLock
        End Sub

        Public ReadOnly ConditionSubjects As IReadOnlyList(Of FieldOption) = New List(Of FieldOption) From {
            New FieldOption("ticks", "存在时间"),
            New FieldOption("hp", "血量%"),
            New FieldOption("dist", "与自机距离"),
            New FieldOption("px", "自机 X"),
            New FieldOption("py", "自机 Y"),
            New FieldOption("x", "自身 X"),
            New FieldOption("y", "自身 Y"),
            New FieldOption("var", "变量"),
            New FieldOption("random", "随机数"),
            New FieldOption("atspell", "处于符卡阶段")
        }

        Public ReadOnly Comparisons As IReadOnlyList(Of FieldOption) = New List(Of FieldOption) From {
            New FieldOption(">", ">"),
            New FieldOption("<", "<"),
            New FieldOption(">=", "≥"),
            New FieldOption("<=", "≤"),
            New FieldOption("==", "="),
            New FieldOption("!=", "≠")
        }

        Public ReadOnly ParticleAxes2D As IReadOnlyList(Of FieldOption) = New List(Of FieldOption) From {
            New FieldOption("X", "X"),
            New FieldOption("Y", "Y")
        }

        Public ReadOnly ParticleAxes3D As IReadOnlyList(Of FieldOption) = New List(Of FieldOption) From {
            New FieldOption("X", "X"),
            New FieldOption("Y", "Y"),
            New FieldOption("Z", "Z")
        }

        Public ReadOnly ParticleComparisons As IReadOnlyList(Of FieldOption) = New List(Of FieldOption) From {
            New FieldOption(">", ">"),
            New FieldOption("=", "="),
            New FieldOption("<", "<")
        }

        Public ReadOnly SpellPhases As IReadOnlyList(Of FieldOption) = New List(Of FieldOption) From {
            New FieldOption("all", "全部阶段"),
            New FieldOption("usual", "非符阶段"),
            New FieldOption("spell", "正式符卡阶段")
        }

        Public Sub Register(def As BlockDefinition)
            _all.Add(def)
            _byId(def.TypeId) = def
        End Sub

        Public Function Find(typeId As String) As BlockDefinition
            EnsureBuilt()
            If String.IsNullOrEmpty(typeId) Then Return Nothing
            Dim def As BlockDefinition = Nothing
            If _byId.TryGetValue(typeId, def) Then Return def
            Return Nothing
        End Function

        Public Function All() As IReadOnlyList(Of BlockDefinition)
            EnsureBuilt()
            Return _all
        End Function

        Public Function ForScope(scope As ProgramScope, Optional isBoss As Boolean = False, Optional particle3D As Boolean = False, Optional isSpellBehavior As Boolean = False) As IEnumerable(Of BlockDefinition)
            EnsureBuilt()
            Return _all.Where(Function(d) d.Allows(scope, particle3D, isSpellBehavior) AndAlso (Not d.RequiresBoss OrElse isBoss))
        End Function

        Public Function Palette(category As BlockCategory, scope As ProgramScope, includeHats As Boolean, Optional isBoss As Boolean = False, Optional particle3D As Boolean = False, Optional isSpellBehavior As Boolean = False) As IEnumerable(Of BlockDefinition)
            EnsureBuilt()
            Return _all.Where(Function(d) d.Category = category AndAlso d.Allows(scope, particle3D, isSpellBehavior) AndAlso (d.IsHat = includeHats) AndAlso (Not d.RequiresBoss OrElse isBoss))
        End Function

        Public Function ResolveOptions(source As String, stage As StageDocument, Optional resources As IEnumerable(Of GameResource) = Nothing) As List(Of FieldOption)
            Dim result As New List(Of FieldOption)
            Select Case source
                Case "patterns"
                    If stage IsNot Nothing Then
                        For Each p In stage.BulletPatterns
                            result.Add(New FieldOption(p.Id, p.Name))
                        Next
                    End If
                Case "audio"
                    If resources IsNot Nothing Then
                        For Each resource In resources
                            If resource IsNot Nothing AndAlso resource.Kind = GameResourceKind.Audio Then
                                result.Add(New FieldOption(resource.Id, resource.Name))
                            End If
                        Next
                    End If
                Case "dialogues"
                    If stage IsNot Nothing Then
                        For Each d In stage.Dialogues
                            result.Add(New FieldOption(d.Id, d.Name))
                        Next
                    End If
                Case "bulletColors"
                    For i = 0 To BulletColors.Count - 1
                        result.Add(New FieldOption(i.ToString(), BulletColors(i)))
                    Next
                Case "sounds"
                    For Each s In Sounds
                        result.Add(New FieldOption(s.Value, s.Display))
                    Next
                Case "conditionSubjects"
                    result.AddRange(ConditionSubjects)
                Case "comparisons"
                    result.AddRange(Comparisons)
                Case "particleComparisons"
                    result.AddRange(ParticleComparisons)
                Case "particleAxes2D"
                    result.AddRange(ParticleAxes2D)
                Case "particleAxes3D"
                    result.AddRange(ParticleAxes3D)
                Case "spellPhases"
                    result.AddRange(SpellPhases)
            End Select
            Return result
        End Function

        Public Function CreateBlock(typeId As String, stage As StageDocument) As BlockNode
            Dim def = Find(typeId)
            Dim node As New BlockNode With {.TypeId = typeId}
            If def Is Nothing Then Return node
            For Each f In def.Fields
                node.SetField(f.Key, f.DefaultValue)
            Next
            For Each f In def.Fields
                If f.OptionsSource = "patterns" Then
                    If stage IsNot Nothing AndAlso stage.BulletPatterns.Count > 0 Then
                        node.SetField(f.Key, stage.BulletPatterns(0).Id)
                    Else
                        node.SetField(f.Key, "")
                    End If
                ElseIf f.OptionsSource = "dialogues" Then
                    If stage IsNot Nothing AndAlso stage.Dialogues.Count > 0 Then
                        node.SetField(f.Key, stage.Dialogues(0).Id)
                    Else
                        node.SetField(f.Key, "")
                    End If
                End If
            Next
            Return node
        End Function

        Private Function Def(typeId As String, title As String, category As BlockCategory, kind As BlockKind, scope As BlockScope, hat As HatKind, tooltip As String) As BlockDefinition
            Dim d As New BlockDefinition With {
                .TypeId = typeId,
                .Title = title,
                .Category = category,
                .Kind = kind,
                .Scope = scope,
                .HatKind = hat,
                .Tooltip = tooltip
            }
            Register(d)
            Return d
        End Function

        Private Sub AddField(def As BlockDefinition, key As String, kind As FieldKind, defaultValue As String, width As Double, Optional source As String = "", Optional tooltip As String = "", Optional minValue As Double = -100000, Optional maxValue As Double = 100000)
            def.Fields.Add(New FieldDef With {
                .Key = key,
                .Kind = kind,
                .DefaultValue = defaultValue,
                .Width = width,
                .OptionsSource = source,
                .Tooltip = tooltip,
                .MinValue = minValue,
                .MaxValue = maxValue
            })
        End Sub

        Private Sub AddLabel(def As BlockDefinition, text As String)
            def.Parts.Add(BlockPart.Label(text))
        End Sub

        Private Sub AddFieldPart(def As BlockDefinition, key As String)
            def.Parts.Add(BlockPart.Field(key))
        End Sub

        Private Sub BuildHats()
            Dim d As BlockDefinition

            d = Def("when_spawn", "当生成时", BlockCategory.Trigger, BlockKind.Hat, BlockScope.Both, HatKind.Spawn,
                    "对象生成时执行一次。子弹的行为从第 11 帧（可碰撞时）开始。")
            AddLabel(d, "当生成时")

            d = Def("every_frame", "每帧", BlockCategory.Trigger, BlockKind.Hat, BlockScope.Both, HatKind.EveryFrame,
                    "每一帧都执行。适合持续移动、转向或每帧检测。")
            AddLabel(d, "每帧")

            d = Def("every_n_frame", "每 N 帧", BlockCategory.Trigger, BlockKind.Hat, BlockScope.Both, HatKind.EveryN,
                    "按固定间隔执行。常用作射击节奏，例如每 10 帧发射一轮。")
            AddLabel(d, "每 ")
            AddFieldPart(d, "n")
            AddLabel(d, " 帧")
            AddField(d, "n", FieldKind.Number, "10", 40, "", "间隔帧数（60 帧 = 1 秒）", 1, 100000)

            d = Def("at_time", "当存在时间 = N", BlockCategory.Trigger, BlockKind.Hat, BlockScope.Both, HatKind.AtTime,
                    "当对象存在的帧数等于指定值时执行一次。")
            AddLabel(d, "当存在时间 = ")
            AddFieldPart(d, "t")
            AddField(d, "t", FieldKind.Number, "60", 44, "", "触发帧", 0, 1000000)

            d = Def("time_between", "当存在时间在 A ~ B", BlockCategory.Trigger, BlockKind.Hat, BlockScope.Both, HatKind.TimeBetween,
                    "在指定帧区间内每帧执行。可用于『前 60 帧减速』等持续行为。")
            AddLabel(d, "当存在时间在 ")
            AddFieldPart(d, "a")
            AddLabel(d, " ~ ")
            AddFieldPart(d, "b")
            AddLabel(d, " 帧")
            AddField(d, "a", FieldKind.Number, "0", 40, "", "起始帧", 0, 1000000)
            AddField(d, "b", FieldKind.Number, "60", 40, "", "结束帧", 0, 1000000)

            d = Def("when_hp_below", "当血量 ≤ P%", BlockCategory.Trigger, BlockKind.Hat, BlockScope.EnemyOnly, HatKind.HpBelow,
                    "敌机血量低于百分比时执行一次。可用于进入第二阶段。")
            AddLabel(d, "当血量 ≤ ")
            AddFieldPart(d, "pct")
            AddLabel(d, " %")
            AddField(d, "pct", FieldKind.Number, "50", 40, "", "血量百分比", 0, 100)

            d = Def("when_player_near", "当自机距离 < D", BlockCategory.Trigger, BlockKind.Hat, BlockScope.Both, HatKind.PlayerNear,
                    "自机进入指定半径时，每帧执行。")
            AddLabel(d, "当自机距离 < ")
            AddFieldPart(d, "dist")
            AddLabel(d, " px")
            AddField(d, "dist", FieldKind.Number, "120", 44, "", "距离（像素）", 0, 2000)

            d = Def("when_killed", "当被击破时", BlockCategory.Trigger, BlockKind.Hat, BlockScope.EnemyOnly, HatKind.Killed,
                    "敌机血量归零的那一帧执行一次。")
            AddLabel(d, "当被击破时")

            d = Def("boss_init", "Boss 初始化", BlockCategory.Trigger, BlockKind.Hat, BlockScope.EnemyOnly, HatKind.BossInit,
                    "Boss 被初始化时执行一次；只在 Boss 模板中有效。")
            AddLabel(d, "Boss 初始化时")
            d.RequiresBoss = True

            d = Def("spell_start", "当第 N 张符卡开始", BlockCategory.Trigger, BlockKind.Hat, BlockScope.EnemyOnly, HatKind.SpellStart,
                    "指定符卡开始或进入指定阶段时执行一次；只在 Boss 模板行为中有效。")
            AddLabel(d, "当第 ")
            AddFieldPart(d, "spell")
            AddLabel(d, " 张符卡 ")
            AddFieldPart(d, "phase")
            AddLabel(d, " 开始")
            AddField(d, "spell", FieldKind.Number, "1", 34, "", "符卡序号（从 1 开始）", 1, 64)
            AddField(d, "phase", FieldKind.Choice, "all", 76, "spellPhases", "全部阶段 / 非符阶段 / 正式符卡阶段")
            d.RequiresBoss = True
            d.BossBehaviorOnly = True

            d = Def("spell_every_frame", "第 N 张符卡每帧", BlockCategory.Trigger, BlockKind.Hat, BlockScope.EnemyOnly, HatKind.SpellEveryFrame,
                    "指定符卡的指定阶段内每帧执行；只在 Boss 模板行为中有效。")
            AddLabel(d, "第 ")
            AddFieldPart(d, "spell")
            AddLabel(d, " 张符卡 ")
            AddFieldPart(d, "phase")
            AddLabel(d, " 每帧")
            AddField(d, "spell", FieldKind.Number, "1", 34, "", "符卡序号（从 1 开始）", 1, 64)
            AddField(d, "phase", FieldKind.Choice, "all", 76, "spellPhases", "全部阶段 / 非符阶段 / 正式符卡阶段")
            d.RequiresBoss = True
            d.BossBehaviorOnly = True

            d = Def("spell_usual_start", "非符阶段开始", BlockCategory.Trigger, BlockKind.Hat, BlockScope.EnemyOnly, HatKind.SpellUsualStart,
                    "这张符卡的非符阶段开始时执行一次；只在符卡自身的积木行为中有效。")
            AddLabel(d, "非符阶段开始时")
            d.RequiresBoss = True
            d.SpellBehaviorOnly = True

            d = Def("spell_usual_every_frame", "非符阶段每帧", BlockCategory.Trigger, BlockKind.Hat, BlockScope.EnemyOnly, HatKind.SpellUsualEveryFrame,
                    "这张符卡的非符阶段内每帧执行；只在符卡自身的积木行为中有效。")
            AddLabel(d, "非符阶段每帧")
            d.RequiresBoss = True
            d.SpellBehaviorOnly = True

            d = Def("spell_formal_start", "正式符卡阶段开始", BlockCategory.Trigger, BlockKind.Hat, BlockScope.EnemyOnly, HatKind.SpellFormalStart,
                    "这张符卡进入正式符卡阶段时执行一次；只在符卡自身的积木行为中有效。")
            AddLabel(d, "正式符卡阶段开始时")
            d.RequiresBoss = True
            d.SpellBehaviorOnly = True

            d = Def("spell_formal_every_frame", "正式符卡阶段每帧", BlockCategory.Trigger, BlockKind.Hat, BlockScope.EnemyOnly, HatKind.SpellFormalEveryFrame,
                    "这张符卡的正式符卡阶段内每帧执行；只在符卡自身的积木行为中有效。")
            AddLabel(d, "正式符卡阶段每帧")
            d.RequiresBoss = True
            d.SpellBehaviorOnly = True
        End Sub

        Private Sub BuildMotion()
            Dim d As BlockDefinition

            d = Def("set_speed", "将速度设为", BlockCategory.Motion, BlockKind.Stack, BlockScope.Both, HatKind.None,
                    "设置对象沿当前方向移动的速度。")
            AddLabel(d, "将速度设为 ")
            AddFieldPart(d, "speed")
            AddField(d, "speed", FieldKind.Number, "2", 48, "", "速度（像素/帧）", 0, 1000)

            d = Def("add_speed", "速度增加", BlockCategory.Motion, BlockKind.Stack, BlockScope.Both, HatKind.None,
                    "在现有速度上增加数值，可为负数（减速）。")
            AddLabel(d, "速度增加 ")
            AddFieldPart(d, "delta")
            AddField(d, "delta", FieldKind.Number, "-0.05", 48, "", "速度增量", -1000, 1000)

            d = Def("set_direction", "将方向设为", BlockCategory.Motion, BlockKind.Stack, BlockScope.Both, HatKind.None,
                    "方向以屏幕向上为 0°，顺时针递增。")
            AddLabel(d, "将方向设为 ")
            AddFieldPart(d, "direction")
            AddLabel(d, " °")
            AddField(d, "direction", FieldKind.Number, "180", 48, "", "角度（0 = 上）", -36000, 36000)

            d = Def("add_direction", "方向增加", BlockCategory.Motion, BlockKind.Stack, BlockScope.Both, HatKind.None,
                    "旋转对象的方向。可用于弹幕的螺旋运动。")
            AddLabel(d, "方向增加 ")
            AddFieldPart(d, "delta")
            AddLabel(d, " °")
            AddField(d, "delta", FieldKind.Number, "15", 48, "", "角度增量", -36000, 36000)

            d = Def("random_direction", "方向设为随机范围", BlockCategory.Motion, BlockKind.Stack, BlockScope.Both, HatKind.None,
                    "在指定角度范围内随机取一个方向。")
            AddLabel(d, "方向设为 ")
            AddFieldPart(d, "min")
            AddLabel(d, "° ~ ")
            AddFieldPart(d, "max")
            AddLabel(d, "° 随机")
            AddField(d, "min", FieldKind.Number, "180", 44, "", "最小角度", -36000, 36000)
            AddField(d, "max", FieldKind.Number, "360", 44, "", "最大角度", -36000, 36000)

            d = Def("aim_player", "朝向自机", BlockCategory.Motion, BlockKind.Stack, BlockScope.Both, HatKind.None,
                    "把方向指向自机，可附加角度偏移。配合『每帧』可实现追踪弹。")
            AddLabel(d, "朝向自机，偏移 ")
            AddFieldPart(d, "offset")
            AddLabel(d, " °")
            AddField(d, "offset", FieldKind.Number, "0", 44, "", "角度偏移", -36000, 36000)

            d = Def("move_to", "移动到坐标", BlockCategory.Motion, BlockKind.Stack, BlockScope.EnemyOnly, HatKind.None,
                    "在指定帧数内匀速移动到目标坐标（会同时修改速度与方向）。")
            AddLabel(d, "移动到 (")
            AddFieldPart(d, "x")
            AddLabel(d, ", ")
            AddFieldPart(d, "y")
            AddLabel(d, ")，用时 ")
            AddFieldPart(d, "frames")
            AddLabel(d, " 帧")
            AddField(d, "x", FieldKind.Number, "192", 44, "", "目标 X", -1000, 2000)
            AddField(d, "y", FieldKind.Number, "128", 44, "", "目标 Y", -1000, 2000)
            AddField(d, "frames", FieldKind.Number, "60", 40, "", "移动帧数", 1, 100000)

            d = Def("move_center", "移动到屏幕中央", BlockCategory.Motion, BlockKind.Stack, BlockScope.EnemyOnly, HatKind.None,
                    "在指定帧数内移动到 (192, 128)。")
            AddLabel(d, "移动到屏幕中央，用时 ")
            AddFieldPart(d, "frames")
            AddLabel(d, " 帧")
            AddField(d, "frames", FieldKind.Number, "60", 40, "", "移动帧数", 1, 100000)

            d = Def("set_position", "设置位置", BlockCategory.Motion, BlockKind.Stack, BlockScope.Both, HatKind.None,
                    "直接把对象传送到指定坐标。")
            AddLabel(d, "设置位置 (")
            AddFieldPart(d, "x")
            AddLabel(d, ", ")
            AddFieldPart(d, "y")
            AddLabel(d, ")")
            AddField(d, "x", FieldKind.Number, "192", 44, "", "X 坐标", -1000, 2000)
            AddField(d, "y", FieldKind.Number, "224", 44, "", "Y 坐标", -1000, 2000)

            d = Def("stop_move", "停止移动", BlockCategory.Motion, BlockKind.Stack, BlockScope.Both, HatKind.None,
                    "把速度设为 0，对象会停在原地。")
            AddLabel(d, "停止移动")
        End Sub

        Private Sub BuildAttack()
            Dim d As BlockDefinition

            d = Def("fire_ring", "发射环形弹幕", BlockCategory.Attack, BlockKind.Stack, BlockScope.EnemyOnly, HatKind.None,
                    "以对象中心为圆心，按总角度均匀发射一轮子弹。子弹外观与行为来自所选『子弹样式』。")
            AddLabel(d, "发射 ")
            AddFieldPart(d, "count")
            AddLabel(d, " 发 ")
            AddFieldPart(d, "pattern")
            AddLabel(d, "，速度 ")
            AddFieldPart(d, "speed")
            AddLabel(d, "，起始角 ")
            AddFieldPart(d, "start")
            AddLabel(d, "°，总角度 ")
            AddFieldPart(d, "arc")
            AddLabel(d, "°")
            AddField(d, "count", FieldKind.Number, "12", 40, "", "子弹数量", 1, 200)
            AddField(d, "pattern", FieldKind.Choice, "", 146, "patterns", "子弹样式")
            AddField(d, "speed", FieldKind.Number, "1.5", 44, "", "子弹速度", 0, 100)
            AddField(d, "start", FieldKind.Number, "0", 40, "", "起始角度", -36000, 36000)
            AddField(d, "arc", FieldKind.Number, "360", 44, "", "总角度（360 = 一整圈）", 0, 36000)

            d = Def("fire_aimed", "发射自机狙", BlockCategory.Attack, BlockKind.Stack, BlockScope.EnemyOnly, HatKind.None,
                    "朝自机所在方向发射扇形弹幕。")
            AddLabel(d, "发射 ")
            AddFieldPart(d, "count")
            AddLabel(d, " 发 ")
            AddFieldPart(d, "pattern")
            AddLabel(d, " 自机狙，速度 ")
            AddFieldPart(d, "speed")
            AddLabel(d, "，扩散 ")
            AddFieldPart(d, "spread")
            AddLabel(d, "°")
            AddField(d, "count", FieldKind.Number, "3", 40, "", "子弹数量", 1, 200)
            AddField(d, "pattern", FieldKind.Choice, "", 146, "patterns", "子弹样式")
            AddField(d, "speed", FieldKind.Number, "2", 44, "", "子弹速度", 0, 100)
            AddField(d, "spread", FieldKind.Number, "30", 44, "", "扇形总角度", 0, 3600)

            d = Def("fire_single", "发射单发子弹", BlockCategory.Attack, BlockKind.Stack, BlockScope.EnemyOnly, HatKind.None,
                    "按固定方向发射一颗子弹。")
            AddLabel(d, "发射 ")
            AddFieldPart(d, "pattern")
            AddLabel(d, "，速度 ")
            AddFieldPart(d, "speed")
            AddLabel(d, "，方向 ")
            AddFieldPart(d, "direction")
            AddLabel(d, "°")
            AddField(d, "pattern", FieldKind.Choice, "", 146, "patterns", "子弹样式")
            AddField(d, "speed", FieldKind.Number, "2", 44, "", "子弹速度", 0, 100)
            AddField(d, "direction", FieldKind.Number, "180", 44, "", "发射角度", -36000, 36000)

            d = Def("fire_laser", "发射激光", BlockCategory.Attack, BlockKind.Stack, BlockScope.EnemyOnly, HatKind.None,
                    "发射一条激光。激光会先展开、再造成伤害，最后消失。")
            AddLabel(d, "发射激光 ")
            AddFieldPart(d, "color")
            AddLabel(d, "，方向 ")
            AddFieldPart(d, "direction")
            AddLabel(d, "°，宽度 ")
            AddFieldPart(d, "width")
            AddLabel(d, "，长度 ")
            AddFieldPart(d, "length")
            AddLabel(d, "，持续 ")
            AddFieldPart(d, "life")
            AddLabel(d, " 帧")
            AddField(d, "color", FieldKind.Choice, "2", 60, "bulletColors", "激光颜色")
            AddField(d, "direction", FieldKind.Number, "180", 44, "", "方向", -36000, 36000)
            AddField(d, "width", FieldKind.Number, "12", 40, "", "宽度", 1, 200)
            AddField(d, "length", FieldKind.Number, "420", 44, "", "长度", 1, 2000)
            AddField(d, "life", FieldKind.Number, "120", 44, "", "存在帧数", 1, 100000)

            d = Def("clear_bullets", "消除场上所有子弹", BlockCategory.Attack, BlockKind.Stack, BlockScope.EnemyOnly, HatKind.None,
                    "调用 STG.ClearBullet()，清除所有敌弹（会产生消弹特效）。")
            AddLabel(d, "消除场上所有子弹")

            d = Def("play_sound", "播放音效", BlockCategory.Attack, BlockKind.Stack, BlockScope.Both, HatKind.None,
                    "播放 ResourcePack.Sounds 中的一个音效。")
            AddLabel(d, "播放音效 ")
            AddFieldPart(d, "sound")
            AddField(d, "sound", FieldKind.Choice, "tan00", 150, "sounds", "音效")

            d = Def("shake_screen", "屏幕震动", BlockCategory.Attack, BlockKind.Stack, BlockScope.Both, HatKind.None,
                    "设置 STG.ShakeFrame，让画面抖动指定帧数。")
            AddLabel(d, "屏幕震动 ")
            AddFieldPart(d, "frames")
            AddLabel(d, " 帧")
            AddField(d, "frames", FieldKind.Number, "12", 40, "", "震动帧数", 1, 1000)
        End Sub

        Private Sub BuildControl()
            Dim d As BlockDefinition

            d = Def("wait", "等待", BlockCategory.Control, BlockKind.Stack, BlockScope.Both, HatKind.None,
                    "暂停这个脚本指定的帧数，然后继续执行后续积木。为避免行为不可预测，只能放在脚本顶层。")
            AddLabel(d, "等待 ")
            AddFieldPart(d, "frames")
            AddLabel(d, " 帧")
            AddField(d, "frames", FieldKind.Number, "30", 44, "", "等待帧数", 0, 100000)

            d = Def("repeat", "重复", BlockCategory.Control, BlockKind.CBlock, BlockScope.Both, HatKind.None,
                    "把主体内的积木立即重复执行 N 次（同一帧内）。需要按帧重复请用『每 N 帧』事件。")
            AddLabel(d, "重复 ")
            AddFieldPart(d, "times")
            AddLabel(d, " 次")
            AddField(d, "times", FieldKind.Number, "3", 40, "", "重复次数", 0, 1000)

            d = Def("if", "如果", BlockCategory.Control, BlockKind.CBlockElse, BlockScope.Both, HatKind.None,
                    "根据条件执行主体；可以添加『否则』分支。条件在每帧触发时求值。")
            AddLabel(d, "如果 ")
            AddFieldPart(d, "subj")
            AddLabel(d, " ")
            AddFieldPart(d, "varName")
            AddFieldPart(d, "op")
            AddLabel(d, " ")
            AddFieldPart(d, "value")
            AddLabel(d, " 那么")
            AddField(d, "subj", FieldKind.Choice, "ticks", 64, "conditionSubjects", "条件对象")
            AddField(d, "varName", FieldKind.Text, "n", 54, "", "变量名（条件对象为『变量』时使用）")
            AddField(d, "op", FieldKind.Choice, ">", 44, "comparisons", "比较方式")
            AddField(d, "value", FieldKind.Number, "60", 50, "", "比较值")

            d = Def("set_var", "设置变量", BlockCategory.Control, BlockKind.Stack, BlockScope.Both, HatKind.None,
                    "把自定义变量设为指定值。变量随对象存在，可用于计数、状态机。")
            AddLabel(d, "将变量 ")
            AddFieldPart(d, "name")
            AddLabel(d, " 设为 ")
            AddFieldPart(d, "value")
            AddField(d, "name", FieldKind.Text, "n", 60, "", "变量名")
            AddField(d, "value", FieldKind.Number, "0", 50, "", "数值或取值器")

            d = Def("add_var", "变量增加", BlockCategory.Control, BlockKind.Stack, BlockScope.Both, HatKind.None,
                    "让变量增加指定数值（可为负数）。")
            AddLabel(d, "变量 ")
            AddFieldPart(d, "name")
            AddLabel(d, " 增加 ")
            AddFieldPart(d, "value")
            AddField(d, "name", FieldKind.Text, "n", 60, "", "变量名")
            AddField(d, "value", FieldKind.Number, "1", 50, "", "数值或取值器")

            d = Def("stop_script", "停止这个脚本", BlockCategory.Control, BlockKind.Stack, BlockScope.Both, HatKind.None,
                    "停止当前对象的所有积木脚本，不再响应事件。")
            AddLabel(d, "停止这个脚本")

            d = Def("boss_next_spell", "进入下一张符卡", BlockCategory.Control, BlockKind.Stack, BlockScope.EnemyOnly, HatKind.None,
                    "让 Boss 进入下一张符卡；只在 Boss 模板中有效。通常放在『Boss 初始化』或符卡效果中。")
            AddLabel(d, "进入下一张符卡")
            d.RequiresBoss = True

            d = Def("next_stage", "关卡结束，进入下一关", BlockCategory.Control, BlockKind.Stack, BlockScope.EnemyOnly, HatKind.None,
                    "结束当前关卡并进入下一关；适合在 Boss 被击破后使用。")
            AddLabel(d, "关卡结束，进入下一关")

            d = Def("play_dialogue", "播放对话", BlockCategory.Control, BlockKind.Stack, BlockScope.EnemyOnly, HatKind.None,
                    "播放一个 AgSTG CSV 对话；对话结束后可按对话属性选择让 Boss 开打或进入下一关。")
            AddLabel(d, "播放对话 ")
            AddFieldPart(d, "dialogue")
            AddField(d, "dialogue", FieldKind.Choice, "", 150, "dialogues", "选择对话")

            d = Def("play_music", "播放音乐", BlockCategory.Control, BlockKind.Stack, BlockScope.Both, HatKind.None,
                    "播放一首游戏资源中的歌曲；曲名可以与资源名称不同。")
            AddLabel(d, "播放音乐 ")
            AddFieldPart(d, "music")
            AddLabel(d, " 曲名 ")
            AddFieldPart(d, "title")
            AddField(d, "music", FieldKind.Choice, "", 160, "audio", "选择游戏资源中的音频")
            AddField(d, "title", FieldKind.Text, "", 150, "", "显示曲名；留空时使用音频资源名称")
        End Sub

        Private Sub BuildParticle()
            Dim d As BlockDefinition

            d = Def("particle_spawn", "粒子生成时", BlockCategory.Trigger, BlockKind.Hat, BlockScope.ParticleOnly, HatKind.ParticleSpawn,
                    "每个粒子创建时执行一次；用于初始化速度、位置和外观。")
            AddLabel(d, "当粒子生成时")

            d = Def("particle_every_frame", "粒子每帧", BlockCategory.Trigger, BlockKind.Hat, BlockScope.ParticleOnly, HatKind.ParticleEveryFrame,
                    "每个粒子每一帧执行；用于持续运动和变化。")
            AddLabel(d, "粒子每帧")

            d = Def("particle_when_position_2d", "当 2D 粒子位置满足条件", BlockCategory.Trigger, BlockKind.Hat, BlockScope.ParticleOnly, HatKind.ParticleWhenPosition,
                    "当 2D 粒子的 X 或 Y 坐标满足比较条件时，每一帧执行内部积木。")
            AddLabel(d, "当粒子 ")
            AddFieldPart(d, "axis")
            AddLabel(d, " ")
            AddFieldPart(d, "op")
            AddLabel(d, " ")
            AddFieldPart(d, "value")
            AddField(d, "axis", FieldKind.Choice, "X", 48, "particleAxes2D", "粒子坐标轴")
            AddField(d, "op", FieldKind.Choice, ">", 38, "particleComparisons", "比较方式")
            AddField(d, "value", FieldKind.Number, "0", 52, "", "比较数值")
            d.Requires2DParticle = True

            d = Def("particle_when_position_3d", "当 3D 粒子位置满足条件", BlockCategory.Trigger, BlockKind.Hat, BlockScope.ParticleOnly, HatKind.ParticleWhenPosition,
                    "当 3D 粒子的 X、Y 或 Z 坐标满足比较条件时，每一帧执行内部积木。")
            AddLabel(d, "当粒子 ")
            AddFieldPart(d, "axis")
            AddLabel(d, " ")
            AddFieldPart(d, "op")
            AddLabel(d, " ")
            AddFieldPart(d, "value")
            AddField(d, "axis", FieldKind.Choice, "X", 48, "particleAxes3D", "粒子坐标轴")
            AddField(d, "op", FieldKind.Choice, ">", 38, "particleComparisons", "比较方式")
            AddField(d, "value", FieldKind.Number, "0", 52, "", "比较数值")
            d.Requires3DParticle = True

            d = Def("particle_set_velocity", "设置粒子速度", BlockCategory.Particle, BlockKind.Stack, BlockScope.ParticleOnly, HatKind.None,
                    "设置粒子在三维空间中的速度。2D 粒子忽略 Z。")
            AddLabel(d, "设置粒子速度 (")
            AddFieldPart(d, "x")
            AddLabel(d, ", ")
            AddFieldPart(d, "y")
            AddLabel(d, ", ")
            AddFieldPart(d, "z")
            AddLabel(d, ")")
            AddField(d, "x", FieldKind.Number, "0", 44, "", "X 速度", -1000, 1000)
            AddField(d, "y", FieldKind.Number, "-2", 44, "", "Y 速度", -1000, 1000)
            AddField(d, "z", FieldKind.Number, "0", 44, "", "Z 速度", -1000, 1000)

            d = Def("particle_add_velocity", "粒子速度增加", BlockCategory.Particle, BlockKind.Stack, BlockScope.ParticleOnly, HatKind.None,
                    "在粒子当前速度上增加数值。2D 粒子忽略 Z。")
            AddLabel(d, "粒子速度增加 (")
            AddFieldPart(d, "x")
            AddLabel(d, ", ")
            AddFieldPart(d, "y")
            AddLabel(d, ", ")
            AddFieldPart(d, "z")
            AddLabel(d, ")")
            AddField(d, "x", FieldKind.Number, "0", 44, "", "X 增量", -1000, 1000)
            AddField(d, "y", FieldKind.Number, "0", 44, "", "Y 增量", -1000, 1000)
            AddField(d, "z", FieldKind.Number, "0", 44, "", "Z 增量", -1000, 1000)

            d = Def("particle_set_position", "设置粒子位置", BlockCategory.Particle, BlockKind.Stack, BlockScope.ParticleOnly, HatKind.None,
                    "直接设置粒子在背景图层中的位置。")
            AddLabel(d, "设置粒子位置 (")
            AddFieldPart(d, "x")
            AddLabel(d, ", ")
            AddFieldPart(d, "y")
            AddLabel(d, ", ")
            AddFieldPart(d, "z")
            AddLabel(d, ")")
            AddField(d, "x", FieldKind.Number, "0", 44, "", "X 坐标")
            AddField(d, "y", FieldKind.Number, "0", 44, "", "Y 坐标")
            AddField(d, "z", FieldKind.Number, "0", 44, "", "Z 坐标")

            d = Def("particle_set_size", "设置粒子大小", BlockCategory.Particle, BlockKind.Stack, BlockScope.ParticleOnly, HatKind.None,
                    "设置粒子的屏幕尺寸（像素）。")
            AddLabel(d, "设置粒子大小 ")
            AddFieldPart(d, "size")
            AddField(d, "size", FieldKind.Number, "3", 44, "", "粒子尺寸", 0.2, 1000)

            d = Def("particle_set_life", "设置粒子寿命", BlockCategory.Particle, BlockKind.Stack, BlockScope.ParticleOnly, HatKind.None,
                    "设置粒子存在多少帧后消失。")
            AddLabel(d, "设置粒子寿命 ")
            AddFieldPart(d, "frames")
            AddLabel(d, " 帧")
            AddField(d, "frames", FieldKind.Number, "180", 48, "", "寿命（帧）", 1, 100000)

            d = Def("particle_set_opacity", "设置粒子透明度", BlockCategory.Particle, BlockKind.Stack, BlockScope.ParticleOnly, HatKind.None,
                    "设置粒子透明度，0 为完全透明，1 为不透明。")
            AddLabel(d, "设置粒子透明度 ")
            AddFieldPart(d, "opacity")
            AddField(d, "opacity", FieldKind.Number, "1", 44, "", "透明度", 0, 1)

            d = Def("particle_set_color", "设置粒子颜色", BlockCategory.Particle, BlockKind.Stack, BlockScope.ParticleOnly, HatKind.None,
                    "设置粒子颜色，支持 #AARRGGBB 或 #RRGGBB。")
            AddLabel(d, "设置粒子颜色 ")
            AddFieldPart(d, "color")
            AddField(d, "color", FieldKind.Text, "#FFFFFFFF", 100, "", "颜色")
        End Sub

    End Module

End Namespace
