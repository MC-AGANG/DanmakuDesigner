Option Strict On
Option Infer On

Imports System.Linq

Namespace Global.DanmakuDesigner.Core

    Public Enum ActorKind
        Enemy = 0
        Bullet = 1
        Laser = 2
    End Enum

    Public Class PreviewActor
        Public Property Kind As ActorKind = ActorKind.Enemy
        Public Property X As Double
        Public Property Y As Double
        Public Property Speed As Double
        Public Property Direction As Double
        Public Property Ticks As Integer = -1
        Public Property HP As Double
        Public Property MaxHP As Double
        Public Property Defence As Double
        Public Property EnemyType As Integer
        Public Property EnemyColor As Integer
        Public Property BulletType As Integer
        Public Property BulletColor As Integer
        Public Property TemplateId As String = ""
        Public Property PatternId As String = ""
        Public Property Behavior As BehaviorRuntime = Nothing
        Public Property Variables As New Dictionary(Of String, Double)(StringComparer.OrdinalIgnoreCase)
        Public Property IsEnabled As Boolean = False
        Public Property IsDying As Boolean = False
        Public Property FadeFrame As Integer = 0
        Public Property Life As Integer = 300
        Public Property LaserLength As Double
        Public Property LaserWidth As Double
        Public Property Opacity As Double = 1
        Public Property Scale As Double = 1
        Public Property Angle As Double = 0
        Public Property Grazed As Boolean = False
        Public Property Breakable As Boolean = True
        Public Property RemoveMe As Boolean = False
        Public Property Hitbox As Double = 8
        Public Property SpriteWidth As Double = 32
        Public Property SpriteHeight As Double = 32
        Public Property BossSpellBehavior As BehaviorRuntime = Nothing
        Public Property BossSpellTicks As Long = 0
        Public Property BossSpellIndex As Integer = -1
    End Class

    Public Class PendingAction
        Public Property Frames As Integer
        Public Property Action As Action
        Public Property Script As ScriptRuntime
    End Class

    Public Class ScriptRuntime
        Public Property Hat As BlockNode
        Public Property Fired As Boolean = False
        Public Property PendingCount As Integer = 0
    End Class

    ''' <summary>解释执行一段积木程序。与代码生成器保持同一套语义。</summary>
    Public Class BehaviorRuntime
        Public Property Owner As PreviewActor
        Public Property World As PreviewWorld
        Public Property Program As Program
        Public Property Halted As Boolean = False
        Private ReadOnly _scripts As New List(Of ScriptRuntime)
        Private ReadOnly _pending As New List(Of PendingAction)
        Private ReadOnly _scope As ProgramScope
        Private ReadOnly _timeProvider As Func(Of Long)

        Private ReadOnly Property CurrentTicks As Long
            Get
                Return If(_timeProvider IsNot Nothing, _timeProvider(), Owner.Ticks)
            End Get
        End Property

        Public Sub New(world As PreviewWorld, owner As PreviewActor, program As Program, scope As ProgramScope, Optional timeProvider As Func(Of Long) = Nothing)
            Me.World = world
            Me.Owner = owner
            Me.Program = program
            _scope = scope
            _timeProvider = timeProvider
            If program IsNot Nothing Then
                For Each s In program.Scripts
                    _scripts.Add(New ScriptRuntime With {.Hat = s})
                Next
            End If
        End Sub

        Public Sub Tick()
            If Halted Then Return
            RunPending()
            For Each s In _scripts
                If Halted Then Return
                If s.Hat Is Nothing Then Continue For
                If s.PendingCount > 0 Then Continue For
                Dim def = s.Hat.Definition
                If def Is Nothing OrElse Not def.IsHat Then Continue For
                If Not HatFires(s, def) Then Continue For
                If OnceHat(def.HatKind) Then s.Fired = True
                ExecuteStack(s.Hat.Children, 0, s)
            Next
        End Sub

        Private Shared Function OnceHat(kind As HatKind) As Boolean
            Select Case kind
                Case HatKind.Spawn, HatKind.AtTime, HatKind.HpBelow, HatKind.Killed, HatKind.SpellStart
                    Return True
                Case Else
                    Return False
            End Select
        End Function

        Private Function HatFires(s As ScriptRuntime, def As BlockDefinition) As Boolean
            If OnceHat(def.HatKind) AndAlso s.Fired Then Return False
            Dim t = If(_timeProvider IsNot Nothing, _timeProvider(), Owner.Ticks)
            Select Case def.HatKind
                Case HatKind.Spawn
                    If _scope = ProgramScope.Bullet Then Return t = 11
                    Return t = 0
                Case HatKind.EveryFrame
                    Return True
                Case HatKind.EveryN
                    Dim n = Math.Max(1, CInt(s.Hat.GetDouble("n", 10)))
                    Return t Mod n = 0
                Case HatKind.AtTime
                    Return t = CInt(s.Hat.GetDouble("t", 0))
                Case HatKind.TimeBetween
                    Dim a = s.Hat.GetDouble("a", 0)
                    Dim b = s.Hat.GetDouble("b", 60)
                    If a > b Then
                        Dim tmp = a
                        a = b
                        b = tmp
                    End If
                    Return t >= a AndAlso t <= b
                Case HatKind.HpBelow
                    Dim pct = Owner.MaxHP * s.Hat.GetDouble("pct", 50) / 100.0
                    Return Owner.HP <= pct
                Case HatKind.PlayerNear
                    Return OwnerDistanceToPlayer() < s.Hat.GetDouble("dist", 120)
                Case HatKind.Killed
                    Return Owner.HP <= 0
                Case HatKind.BossInit
                    Return t = 0
                Case HatKind.SpellStart
                    Return t >= 65
                Case HatKind.SpellEveryFrame
                    Return t >= 65
                Case Else
                    Return False
            End Select
        End Function

        Private Function OwnerDistanceToPlayer() As Double
            Dim dx = Owner.X - World.PlayerX
            Dim dy = Owner.Y - World.PlayerY
            Return Math.Sqrt(dx * dx + dy * dy)
        End Function

        Private Sub RunPending()
            If _pending.Count = 0 Then Return
            Dim due As New List(Of PendingAction)
            For i = _pending.Count - 1 To 0 Step -1
                _pending(i).Frames -= 1
                If _pending(i).Frames <= 0 Then
                    due.Add(_pending(i))
                    _pending.RemoveAt(i)
                End If
            Next
            For Each p In due
                If Halted Then Exit For
                If p.Script IsNot Nothing Then p.Script.PendingCount = Math.Max(0, p.Script.PendingCount - 1)
                If p.Action IsNot Nothing Then p.Action.Invoke()
            Next
        End Sub

        Private Sub Schedule(frames As Integer, script As ScriptRuntime, action As Action)
            If script IsNot Nothing Then script.PendingCount += 1
            _pending.Add(New PendingAction With {
                .Frames = Math.Max(0, frames),
                .Action = action,
                .Script = script
            })
        End Sub

        ''' <summary>执行积木栈。返回 False 表示脚本已被暂停（等待/停止）。</summary>
        Private Function ExecuteStack(blocks As List(Of BlockNode), startIndex As Integer, script As ScriptRuntime) As Boolean
            If blocks Is Nothing Then Return True
            Dim i = startIndex
            While i < blocks.Count
                If Halted Then Return False
                Dim block = blocks(i)
                If block Is Nothing Then
                    i += 1
                    Continue While
                End If
                If block.TypeId = "wait" Then
                    Dim frames = Math.Max(0, CInt(EvalValue(block.GetField("frames"))))
                    Dim nextIndex = i + 1
                    Dim captured = blocks
                    Schedule(frames, script, Sub() ExecuteStack(captured, nextIndex, script))
                    Return False
                End If
                If Not ExecuteBlock(block, script) Then Return False
                i += 1
            End While
            Return True
        End Function

        Private Function ExecuteBlock(block As BlockNode, script As ScriptRuntime) As Boolean
            Dim def = block.Definition
            If def Is Nothing Then Return True
            Select Case block.TypeId
                Case "repeat"
                    Dim times = Math.Max(0, Math.Min(1000, CInt(EvalValue(block.GetField("times")))))
                    For n = 1 To times
                        If Halted Then Return False
                        If Not ExecuteStack(block.Children, 0, script) Then Return False
                    Next
                    Return True
                Case "if"
                    Dim ok = EvalCondition(block)
                    Dim branch = If(ok, block.Children, block.ElseChildren)
                    Return ExecuteStack(branch, 0, script)
                Case "stop_script"
                    Halted = True
                    Return False
                Case "boss_next_spell"
                    Return True
                Case "next_stage"
                    World.NextStage()
                    Return True
                Case "play_dialogue"
                    Dim dialogue = World.Stage.FindDialogue(block.GetField("dialogue"))
                    If dialogue IsNot Nothing Then
                        World.ActiveMessage = "对话 · " & dialogue.Name
                        World.MessageTimer = 120
                        If dialogue.AfterAction = DialogueAfterAction.NextStage Then World.NextStage()
                    End If
                    Return True
                Case "play_music"
                    Dim musicTitle = If(block.GetField("title"), "").Trim()
                    World.ActiveMessage = If(String.IsNullOrWhiteSpace(musicTitle), "音乐播放", "音乐 · " & musicTitle)
                    World.MessageTimer = 180
                    Return True
                Case "set_var"
                    Dim name = NormalizeVarName(block.GetField("name"))
                    Owner.Variables(name) = EvalValue(block.GetField("value"))
                Case "add_var"
                    Dim name = NormalizeVarName(block.GetField("name"))
                    Dim current As Double = 0
                    Owner.Variables.TryGetValue(name, current)
                    Owner.Variables(name) = current + EvalValue(block.GetField("value"))
                Case "set_speed"
                    Owner.Speed = EvalValue(block.GetField("speed"))
                Case "add_speed"
                    Owner.Speed += EvalValue(block.GetField("delta"))
                Case "set_direction"
                    Owner.Direction = EvalValue(block.GetField("direction"))
                Case "add_direction"
                    Owner.Direction += EvalValue(block.GetField("delta"))
                Case "random_direction"
                    Dim a = EvalValue(block.GetField("min"))
                    Dim b = EvalValue(block.GetField("max"))
                    Owner.Direction = a + World.Rng.NextDouble() * (b - a)
                Case "aim_player"
                    Owner.Direction = AngleToPlayer() + EvalValue(block.GetField("offset"))
                Case "move_to"
                    Dim tx = EvalValue(block.GetField("x"))
                    Dim ty = EvalValue(block.GetField("y"))
                    Dim frames = Math.Max(1, EvalValue(block.GetField("frames")))
                    MoveToward(tx, ty, frames)
                Case "move_center"
                    Dim frames = Math.Max(1, EvalValue(block.GetField("frames")))
                    MoveToward(192, 128, frames)
                Case "set_position"
                    Owner.X = EvalValue(block.GetField("x"))
                    Owner.Y = EvalValue(block.GetField("y"))
                Case "stop_move"
                    Owner.Speed = 0
                Case "fire_ring"
                    FireRing(block)
                Case "fire_aimed"
                    FireAimed(block)
                Case "fire_single"
                    FireSingle(block)
                Case "fire_laser"
                    FireLaser(block)
                Case "clear_bullets"
                    World.BreakAllBullets()
                Case "play_sound"
                    World.PlaySound(block.GetField("sound"))
                Case "shake_screen"
                    World.ShakeFrames = Math.Max(World.ShakeFrames, CInt(EvalValue(block.GetField("frames"))))
            End Select
            Return True
        End Function

        Private Function AngleToPlayer() As Double
            Dim dx = World.PlayerX - Owner.X
            Dim dy = World.PlayerY - Owner.Y
            Return Math.Atan2(dx, -dy) * 180.0 / Math.PI
        End Function

        Private Sub MoveToward(tx As Double, ty As Double, frames As Double)
            Dim dx = tx - Owner.X
            Dim dy = ty - Owner.Y
            Dim dist = Math.Sqrt(dx * dx + dy * dy)
            Owner.Direction = Math.Atan2(dx, -dy) * 180.0 / Math.PI
            Owner.Speed = dist / Math.Max(1, frames)
        End Sub

        Public Function EvalValue(text As String) As Double
            If String.IsNullOrWhiteSpace(text) Then Return 0
            If Not ValueExpression.IsReporter(text) Then
                Return ValueExpression.ParseNumber(text, 0)
            End If
            Dim kind = ValueExpression.ReporterKind(text)
            Dim arg = ValueExpression.ReporterArg(text)
            Select Case kind
                Case "ticks" : Return CurrentTicks
                Case "hp" : Return If(Owner.MaxHP > 0, Owner.HP / Owner.MaxHP * 100, 0)
                Case "px" : Return World.PlayerX
                Case "py" : Return World.PlayerY
                Case "dist" : Return OwnerDistanceToPlayer()
                Case "x" : Return Owner.X
                Case "y" : Return Owner.Y
                Case "rand"
                    Dim a = 0.0
                    Dim b = 1.0
                    Dim parts = If(arg, "").Split(","c)
                    If parts.Length >= 2 Then
                        Double.TryParse(parts(0), Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, a)
                        Double.TryParse(parts(1), Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, b)
                    End If
                    Return a + World.Rng.NextDouble() * (b - a)
                Case "var"
                    Dim name = NormalizeVarName(arg)
                    Dim value As Double = 0
                    Owner.Variables.TryGetValue(name, value)
                    Return value
                Case "bullets" : Return World.Bullets.Count
                Case "enemies" : Return World.Enemies.Count
                Case Else : Return 0
            End Select
        End Function

        Private Function EvalCondition(block As BlockNode) As Boolean
            Dim subj = block.GetField("subj")
            Dim left As Double
            Dim right = EvalValue(block.GetField("value"))
            Select Case subj
                Case "ticks" : left = CurrentTicks
                Case "hp" : left = If(Owner.MaxHP > 0, Owner.HP / Owner.MaxHP * 100, 0)
                Case "dist" : left = OwnerDistanceToPlayer()
                Case "px" : left = World.PlayerX
                Case "py" : left = World.PlayerY
                Case "x" : left = Owner.X
                Case "y" : left = Owner.Y
                Case "var"
                    Dim name = NormalizeVarName(block.GetField("varName"))
                    Dim value As Double = 0
                    Owner.Variables.TryGetValue(name, value)
                    left = value
                Case "random" : left = World.Rng.NextDouble()
                Case Else : left = 0
            End Select
            Select Case block.GetField("op")
                Case ">" : Return left > right
                Case "<" : Return left < right
                Case ">=" : Return left >= right
                Case "<=" : Return left <= right
                Case "==" : Return Math.Abs(left - right) < 0.000001
                Case "!=" : Return Math.Abs(left - right) >= 0.000001
                Case Else : Return False
            End Select
        End Function

        Private Sub FireRing(block As BlockNode)
            Dim pattern = World.FindPattern(block.GetField("pattern"))
            If pattern Is Nothing Then Return
            Dim count = Math.Max(1, Math.Min(200, CInt(EvalValue(block.GetField("count")))))
            Dim speed = Math.Max(0, EvalValue(block.GetField("speed")))
            Dim startAngle = EvalValue(block.GetField("start"))
            Dim arc = EvalValue(block.GetField("arc"))
            Dim stepAngle As Double
            If Math.Abs(arc) >= 359.9 Then
                stepAngle = arc / count
            Else
                stepAngle = If(count > 1, arc / (count - 1), 0)
            End If
            For i = 0 To count - 1
                World.SpawnBullet(pattern, Owner.X, Owner.Y, speed, startAngle + stepAngle * i)
            Next
        End Sub

        Private Sub FireAimed(block As BlockNode)
            Dim pattern = World.FindPattern(block.GetField("pattern"))
            If pattern Is Nothing Then Return
            Dim count = Math.Max(1, Math.Min(200, CInt(EvalValue(block.GetField("count")))))
            Dim speed = Math.Max(0, EvalValue(block.GetField("speed")))
            Dim spread = EvalValue(block.GetField("spread"))
            Dim baseAngle = AngleToPlayer()
            Dim stepAngle = If(count > 1, spread / (count - 1), 0)
            For i = 0 To count - 1
                Dim offset = (i - (count - 1) / 2.0) * stepAngle
                World.SpawnBullet(pattern, Owner.X, Owner.Y, speed, baseAngle + offset)
            Next
        End Sub

        Private Sub FireSingle(block As BlockNode)
            Dim pattern = World.FindPattern(block.GetField("pattern"))
            If pattern Is Nothing Then Return
            Dim speed = Math.Max(0, EvalValue(block.GetField("speed")))
            Dim direction = EvalValue(block.GetField("direction"))
            World.SpawnBullet(pattern, Owner.X, Owner.Y, speed, direction)
        End Sub

        Private Sub FireLaser(block As BlockNode)
            Dim color = CInt(EvalValue(block.GetField("color")))
            Dim direction = EvalValue(block.GetField("direction"))
            Dim width = Math.Max(1, EvalValue(block.GetField("width")))
            Dim length = Math.Max(1, EvalValue(block.GetField("length")))
            Dim life = Math.Max(1, CInt(EvalValue(block.GetField("life"))))
            World.SpawnLaser(Owner.X, Owner.Y, direction, width, length, life, color)
        End Sub

        Private Shared Function NormalizeVarName(name As String) As String
            If String.IsNullOrWhiteSpace(name) Then Return "n"
            Return name.Trim()
        End Function
    End Class

    ''' <summary>编辑器内置的轻量运行时，用于实时预览关卡。</summary>
    Public Class PreviewWorld
        Public Const ViewWidth As Double = 384
        Public Const ViewHeight As Double = 448
        Public Const PlayerHitbox As Double = 2.5

        Public Property Project As StageProject
        Public Property Stage As StageDocument
        Public Property Tick As Long = -1
        Public Property PlayerX As Double = 192
        Public Property PlayerY As Double = 384
        Public Property PlayerLives As Integer = 2
        Public Property PlayerInvin As Integer = 0
        Public Property Graze As Long = 0
        Public Property Rng As New Random(1337)
        Public Property ShakeFrames As Integer = 0
        Public Property IsGameOver As Boolean = False
        Public Property IsCleared As Boolean = False
        Public Property MusicName As String = ""
        Public Property ActiveMessage As String = ""
        Public Property MessageTimer As Integer = 0
        Public Property GodMode As Boolean = False
        ''' <summary>调试预览：不计残机，被弹不会结束游戏。</summary>
        Public Property DebugInfiniteLives As Boolean = True
        Public Property Overflowed As Boolean = False
        Public Property InputX As Integer = 0
        Public Property InputY As Integer = 0
        Public Property InputSlow As Boolean = False
        Public Property Seeking As Boolean = False

        Public ReadOnly Enemies As New List(Of PreviewActor)
        Public ReadOnly Bullets As New List(Of PreviewActor)
        Public ReadOnly Lasers As New List(Of PreviewActor)
        Public ReadOnly Log As New List(Of String)
        Public ReadOnly RecentSounds As New List(Of String)

        Public Sub New()
        End Sub

        Public Sub New(project As StageProject)
            Me.Project = project
            Me.Stage = project.Stage
            Reset()
        End Sub

        Public Sub Reset()
            Enemies.Clear()
            Bullets.Clear()
            Lasers.Clear()
            Log.Clear()
            RecentSounds.Clear()
            Tick = -1
            PlayerX = 192
            PlayerY = 384
            PlayerLives = 2
            PlayerInvin = 0
            Graze = 0
            ShakeFrames = 0
            IsGameOver = False
            IsCleared = False
            MusicName = ""
            ActiveMessage = ""
            MessageTimer = 0
            Overflowed = False
            Rng = New Random(1337)
            InputX = 0
            InputY = 0
            InputSlow = False
        End Sub

        Public Sub Advance()
            If Stage Is Nothing Then Return
            Tick += 1
            If PlayerInvin > 0 Then PlayerInvin -= 1
            If MessageTimer > 0 Then MessageTimer -= 1
            If ShakeFrames > 0 Then ShakeFrames -= 1

            MovePlayer()
            UpdateActors()
            If Not IsCleared AndAlso Not IsGameOver Then
                ProcessSpawns()
                ProcessCues()
            End If
            Cleanup()
        End Sub

        Public Sub Seek(targetTick As Long)
            If Stage Is Nothing Then Return
            Dim wasGod = GodMode
            Reset()
            Dim target = Math.Max(0L, Math.Min(targetTick, CLng(Stage.LengthTicks)))
            GodMode = True
            Seeking = True
            Try
                While Tick < target
                    Advance()
                End While
            Finally
                Seeking = False
                GodMode = wasGod
            End Try
        End Sub

        Private Sub MovePlayer()
            If IsGameOver OrElse IsCleared Then Return
            Dim speed = 4.0
            If InputSlow Then speed *= 0.4
            PlayerX += InputX * speed
            PlayerY += InputY * speed
            PlayerX = Math.Max(12, Math.Min(ViewWidth - 12, PlayerX))
            PlayerY = Math.Max(24, Math.Min(ViewHeight - 16, PlayerY))
        End Sub

        Private Sub UpdateActors()
            For Each a In Enemies
                UpdateEnemy(a)
            Next
            For Each b In Bullets
                UpdateBullet(b)
            Next
            For Each l In Lasers
                UpdateLaser(l)
            Next
            If Not GodMode AndAlso Not IsGameOver Then
                CheckPlayerCollisions()
            End If
            Enemies.RemoveAll(Function(a) a.RemoveMe)
            Bullets.RemoveAll(Function(b) b.RemoveMe)
            Lasers.RemoveAll(Function(l) l.RemoveMe)
        End Sub

        Private Sub UpdateEnemy(a As PreviewActor)
            a.Ticks += 1
            If a.IsDying Then
                a.FadeFrame += 1
                a.Opacity = Math.Max(0, 1 - a.FadeFrame / 16.0)
                a.Scale = 1 + a.FadeFrame / 8.0
                If a.FadeFrame >= 16 Then a.RemoveMe = True
                Return
            End If

            a.Opacity = Math.Min(1, a.Ticks / 30.0)
            If a.Ticks >= 30 Then a.IsEnabled = True
            If a.Behavior IsNot Nothing Then a.Behavior.Tick()
            If a.BossSpellBehavior IsNot Nothing AndAlso a.Ticks >= 65 Then
                a.BossSpellBehavior.Tick()
                a.BossSpellTicks += 1
            End If

            a.X += a.Speed * Math.Sin(a.Direction / 180.0 * Math.PI)
            a.Y -= a.Speed * Math.Cos(a.Direction / 180.0 * Math.PI)

            If (a.Ticks > a.Life AndAlso a.IsEnabled) OrElse (a.HP <= 0 AndAlso a.IsEnabled) Then
                a.IsDying = True
                a.IsEnabled = False
                a.FadeFrame = 0
            End If
        End Sub

        Private Sub UpdateBullet(b As PreviewActor)
            b.Ticks += 1
            If b.Ticks <= 10 Then
                b.Opacity = (10 - b.Ticks) / 10.0
            ElseIf b.Ticks = 11 Then
                b.Opacity = 1
                b.IsEnabled = True
            End If

            If b.FadeFrame > 0 Then
                b.Opacity = Math.Max(0, (32 - b.FadeFrame) / 32.0)
                b.Scale = 1 + b.FadeFrame / 16.0
                b.FadeFrame += 1
                If b.FadeFrame >= 32 Then b.RemoveMe = True
                Return
            End If

            If b.IsEnabled AndAlso b.Behavior IsNot Nothing Then b.Behavior.Tick()

            b.X += b.Speed * Math.Sin(b.Direction / 180.0 * Math.PI)
            b.Y -= b.Speed * Math.Cos(b.Direction / 180.0 * Math.PI)

            If b.IsEnabled Then
                If b.Y < -32 OrElse b.X > ViewWidth + 16 OrElse b.X < -16 OrElse b.Y > ViewHeight + 32 Then
                    b.RemoveMe = True
                End If
            End If
        End Sub

        Private Sub UpdateLaser(l As PreviewActor)
            l.Ticks += 1
            If l.Ticks <= 30 Then
                l.Opacity = 0.15
            ElseIf l.Ticks < 60 Then
                l.Opacity = 0.8 * ((l.Ticks - 30) / 30.0)
            Else
                l.Opacity = 0.8 + 0.2 * Math.Sin(l.Ticks)
                l.IsEnabled = True
            End If
            If l.Ticks > l.Life AndAlso l.FadeFrame = 0 Then
                l.FadeFrame = 1
                l.IsEnabled = False
            End If
            If l.FadeFrame > 0 Then
                l.FadeFrame += 1
                l.Opacity = Math.Max(0, (30 - l.FadeFrame) / 30.0)
                If l.FadeFrame >= 30 Then l.RemoveMe = True
            End If
        End Sub

        Private Sub CheckPlayerCollisions()
            If PlayerInvin > 0 Then Return
            For Each b In Bullets
                If Not b.IsEnabled OrElse b.FadeFrame > 0 Then Continue For
                Dim dx = b.X - PlayerX
                Dim dy = b.Y - PlayerY
                Dim dist = Math.Sqrt(dx * dx + dy * dy)
                If dist < b.Hitbox + PlayerHitbox Then
                    PlayerMiss()
                    Return
                End If
                If Not b.Grazed AndAlso dist < 32 + b.Hitbox Then
                    b.Grazed = True
                    Graze += 1
                End If
            Next
            For Each e In Enemies
                If Not e.IsEnabled OrElse e.IsDying Then Continue For
                Dim dx = e.X - PlayerX
                Dim dy = e.Y - PlayerY
                If Math.Sqrt(dx * dx + dy * dy) < e.Hitbox + PlayerHitbox Then
                    PlayerMiss()
                    Return
                End If
            Next
            For Each l In Lasers
                If Not l.IsEnabled Then Continue For
                If LaserHits(l, PlayerX, PlayerY, l.LaserWidth * 0.5) Then
                    PlayerMiss()
                    Return
                End If
            Next
        End Sub

        Public Function LaserHits(l As PreviewActor, px As Double, py As Double, halfWidth As Double) As Boolean
            Dim ax = l.X
            Dim ay = l.Y
            Dim bx = ax + l.LaserLength * Math.Sin(l.Direction / 180.0 * Math.PI)
            Dim by = ay - l.LaserLength * Math.Cos(l.Direction / 180.0 * Math.PI)
            Dim abx = bx - ax
            Dim aby = by - ay
            Dim ab2 = abx * abx + aby * aby
            If ab2 <= 0.000001 Then
                Dim ddx = px - ax
                Dim ddy = py - ay
                Return Math.Sqrt(ddx * ddx + ddy * ddy) <= halfWidth + PlayerHitbox
            End If
            Dim t = ((px - ax) * abx + (py - ay) * aby) / ab2
            t = Math.Max(0, Math.Min(1, t))
            Dim cx = ax + t * abx
            Dim cy = ay + t * aby
            Dim dx = px - cx
            Dim dy = py - cy
            Return Math.Sqrt(dx * dx + dy * dy) <= halfWidth + PlayerHitbox
        End Function

        Private Sub PlayerMiss()
            If GodMode Then Return
            PlayerInvin = 120
            BreakAllBullets()
            If DebugInfiniteLives Then
                ActiveMessage = "Miss · 调试模式不消耗残机"
                MessageTimer = 90
                Return
            End If
            PlayerLives -= 1
            If PlayerLives < 0 Then
                IsGameOver = True
                ActiveMessage = "被弹了…… 按 R 重新开始"
                MessageTimer = 600
            Else
                ActiveMessage = "Miss · 剩余 " & PlayerLives & " 条命"
                MessageTimer = 90
            End If
        End Sub

        Public Sub BreakAllBullets()
            For Each b In Bullets
                If b.FadeFrame = 0 Then
                    b.FadeFrame = 1
                    b.IsEnabled = False
                End If
            Next
        End Sub

        ''' <summary>由积木显式结束当前关卡；时间轴自然结束不会调用这里。</summary>
        Public Sub NextStage()
            If IsCleared Then Return
            IsCleared = True
            BreakAllBullets()
            ActiveMessage = "关卡结束 · 进入下一关"
            MessageTimer = 180
        End Sub

        Public Sub PlaySound(name As String)
            If String.IsNullOrWhiteSpace(name) Then Return
            RecentSounds.Add(name)
            While RecentSounds.Count > 8
                RecentSounds.RemoveAt(0)
            End While
        End Sub

        Public Function FindPattern(id As String) As BulletPattern
            If Stage Is Nothing OrElse String.IsNullOrEmpty(id) Then Return Nothing
            Return Stage.FindPattern(id)
        End Function

        Public Function SpawnBullet(pattern As BulletPattern, x As Double, y As Double, speed As Double, direction As Double) As PreviewActor
            If pattern Is Nothing Then Return Nothing
            If Bullets.Count >= 1800 Then
                Overflowed = True
                Return Nothing
            End If
            Dim size = EngineCatalog.BulletSpriteSize(pattern.BulletType)
            Dim actor As New PreviewActor With {
                .Kind = ActorKind.Bullet,
                .X = x,
                .Y = y,
                .Speed = speed,
                .Direction = direction,
                .BulletType = pattern.BulletType,
                .BulletColor = pattern.Color,
                .PatternId = pattern.Id,
                .Life = 100000,
                .Hitbox = size.Hitbox,
                .SpriteWidth = size.Width,
                .SpriteHeight = size.Height,
                .Ticks = -1,
                .Opacity = 0
            }
            Dim info = EngineCatalog.BulletType(pattern.BulletType)
            actor.Angle = If(info.Spin, 0, direction)
            If pattern.Behavior IsNot Nothing AndAlso pattern.Behavior.Scripts.Count > 0 Then
                actor.Behavior = New BehaviorRuntime(Me, actor, pattern.Behavior, ProgramScope.Bullet)
            End If
            Bullets.Add(actor)
            Return actor
        End Function

        Public Function SpawnLaser(x As Double, y As Double, direction As Double, width As Double, length As Double, life As Integer, color As Integer) As PreviewActor
            Dim actor As New PreviewActor With {
                .Kind = ActorKind.Laser,
                .X = x,
                .Y = y,
                .Direction = direction,
                .LaserWidth = width,
                .LaserLength = length,
                .Life = life,
                .BulletColor = color,
                .Hitbox = width / 2,
                .Ticks = -1,
                .Opacity = 0
            }
            Lasers.Add(actor)
            Return actor
        End Function

        Private Sub ProcessSpawns()
            If Stage Is Nothing Then Return
            For Each track In Stage.Tracks
                If track.Kind <> TrackKind.Enemy OrElse track.IsMuted Then Continue For
                Dim template = Stage.FindTemplate(track.TemplateId)
                If template Is Nothing Then Continue For
                For Each clip In track.Clips
                    If clip.Count > 1 AndAlso clip.IntervalTicks = 0 Then
                        If Tick = clip.StartTick Then
                            For i = 0 To Math.Max(1, clip.Count) - 1
                                SpawnEnemy(template, clip, i)
                            Next
                        End If
                    Else
                        Dim index = SpawnIndexAt(clip, Tick)
                        If index >= 0 Then SpawnEnemy(template, clip, index)
                    End If
                Next
            Next
        End Sub

        Private Function SpawnIndexAt(clip As TimelineClip, tick As Long) As Integer
            Dim startTick = clip.StartTick
            If tick < startTick Then Return -1
            Dim count = Math.Max(1, clip.Count)
            If count = 1 Then
                Return If(tick = startTick, 0, -1)
            End If
            If clip.IntervalTicks <= 0 Then Return If(tick = startTick, 0, -1)
            Dim interval = Math.Max(1, clip.IntervalTicks)
            Dim delta = tick - startTick
            If delta Mod interval <> 0 Then Return -1
            Dim index = CInt(delta \ interval)
            If index >= count Then Return -1
            Return index
        End Function

        Private Sub SpawnEnemy(template As EnemyTemplate, clip As TimelineClip, index As Integer)
            Dim pos = FormationPosition(clip, index)
            Dim size = EngineCatalog.EnemySpriteSize(If(template.IsBoss, 1, template.EnemyType))
            Dim firstSpell = template.SpellCardIds.Select(Function(id) Stage.FindSpell(id)).FirstOrDefault(Function(s) s IsNot Nothing)
            Dim maxHp = If(template.IsBoss AndAlso firstSpell IsNot Nothing, firstSpell.SpellHP, template.HP)
            Dim actor As New PreviewActor With {
                .Kind = ActorKind.Enemy,
                .X = pos.X,
                .Y = pos.Y,
                .Speed = template.Speed,
                .Direction = template.Direction,
                .HP = maxHp,
                .MaxHP = maxHp,
                .Defence = template.Defence,
                .EnemyType = If(template.IsBoss, 100, template.EnemyType),
                .EnemyColor = If(template.IsBoss, template.BossId, template.Color),
                .TemplateId = template.Id,
                .Life = template.Life,
                .Hitbox = If(template.IsBoss, 24.0, size.Hitbox),
                .SpriteWidth = If(template.IsBoss, 96.0, size.Width),
                .SpriteHeight = If(template.IsBoss, 96.0, size.Height),
                .Ticks = -1,
                .Opacity = 0
            }
            If template.Behavior IsNot Nothing AndAlso template.Behavior.Scripts.Count > 0 Then
                actor.Behavior = New BehaviorRuntime(Me, actor, template.Behavior, ProgramScope.Enemy)
            End If
            If template.IsBoss AndAlso firstSpell IsNot Nothing AndAlso firstSpell.Behavior IsNot Nothing Then
                actor.BossSpellIndex = 0
                actor.BossSpellTicks = 0
                If firstSpell.Behavior.Scripts.Count > 0 Then
                    actor.BossSpellBehavior = New BehaviorRuntime(Me, actor, firstSpell.Behavior, ProgramScope.Enemy, Function() actor.BossSpellTicks)
                End If
            End If
            Enemies.Add(actor)
        End Sub

        Public Function FormationPosition(clip As TimelineClip, index As Integer) As PointD
            Dim count = Math.Max(1, clip.Count)
            Dim x = clip.X
            Dim y = clip.Y
            Select Case clip.Formation
                Case FormationKind.SinglePoint
                Case FormationKind.LineHorizontal
                    x += (index - (count - 1) / 2.0) * clip.Spacing
                Case FormationKind.LineVertical
                    y += index * clip.Spacing
                Case FormationKind.Ring
                    Dim angle = (clip.StartAngle + index * If(count > 0, clip.FormationAngle / count, 0)) / 180.0 * Math.PI
                    x += clip.Spacing * Math.Sin(angle)
                    y -= clip.Spacing * Math.Cos(angle)
                Case FormationKind.Arc
                    Dim stepAngle = If(count > 1, clip.FormationAngle / (count - 1), 0)
                    Dim angle = (clip.StartAngle + index * stepAngle) / 180.0 * Math.PI
                    x += clip.Spacing * Math.Sin(angle)
                    y -= clip.Spacing * Math.Cos(angle)
                Case FormationKind.RandomBox
                    x += (Rng.NextDouble() * 2 - 1) * clip.Spacing
                    y += (Rng.NextDouble() * 2 - 1) * clip.Spacing
            End Select
            If clip.JitterX > 0 Then x += (Rng.NextDouble() * 2 - 1) * clip.JitterX
            If clip.JitterY > 0 Then y += (Rng.NextDouble() * 2 - 1) * clip.JitterY
            Return New PointD(x, y)
        End Function

        Private Sub ProcessCues()
            If Stage Is Nothing Then Return
            For Each track In Stage.Tracks
                If track.Kind <> TrackKind.Cue OrElse track.IsMuted Then Continue For
                For Each clip In track.Clips
                    If clip.StartTick <> Tick Then Continue For
                    Select Case clip.Cue
                        Case CueKind.Music
                            MusicName = clip.CueText
                            ActiveMessage = "♪ " & clip.CueText
                            MessageTimer = 180
                        Case CueKind.ClearBullets
                            BreakAllBullets()
                            ActiveMessage = "弹幕消除"
                            MessageTimer = 90
                        Case CueKind.Shake
                            ShakeFrames = Math.Max(ShakeFrames, Math.Max(6, clip.CueValue))
                        Case CueKind.EndStage
                            IsCleared = True
                            ActiveMessage = "关卡完成 · " & Stage.Name
                            MessageTimer = 180
                            BreakAllBullets()
                    End Select
                Next
            Next
        End Sub

        Private Sub Cleanup()
            If Enemies.Count > 400 Then Enemies.RemoveRange(0, Enemies.Count - 400)
        End Sub

        Public ReadOnly Property TotalBulletCount As Integer
            Get
                Return Bullets.Count + Lasers.Count
            End Get
        End Property

        Public ReadOnly Property TimeText As String
            Get
                Dim t = Math.Max(0L, Tick) / 60.0
                Return TimeSpan.FromSeconds(t).ToString("mm\:ss\.ff")
            End Get
        End Property

        Public Function StageSignature() As String
            If Stage Is Nothing Then Return ""
            Return Stage.Name & "|" & Stage.LengthTicks
        End Function
    End Class

    Public Structure PointD
        Public Property X As Double
        Public Property Y As Double
        Public Sub New(x As Double, y As Double)
            Me.X = x
            Me.Y = y
        End Sub
    End Structure

End Namespace
