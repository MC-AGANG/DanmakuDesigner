Option Strict On
Option Infer On

Imports System.Text.Json.Serialization

Namespace Global.DanmakuDesigner.Core

    Public Enum DifficultyMode
        Easy = 0
        Normal = 1
        Hard = 2
        Lunatic = 3
        Extra = 4
    End Enum

    Public Enum TrackKind
        Enemy = 0
        Cue = 1
        Background = 2
    End Enum

    Public Enum BackgroundLayerKind
        Layer2D = 0
        Layer3D = 1
    End Enum

    Public Enum BackgroundShape2D
        Rectangle = 0
        Ellipse = 1
        ParticleField = 2
    End Enum

    Public Enum BackgroundParticleMode
        None = 0
        TwoD = 1
        ThreeD = 2
    End Enum

    Public Enum BackgroundMeshKind
        Box = 0
        Cylinder = 1
        Sphere = 2
        Rectangle = 3
        Triangle = 4
    End Enum

    Public Enum BossSpellType
        None = 0
        Life = 1
        Time = 2
    End Enum

    Public Enum FormationKind
        SinglePoint = 0
        LineHorizontal = 1
        LineVertical = 2
        Ring = 3
        Arc = 4
        RandomBox = 5
    End Enum

    Public Enum CueKind
        Music = 0
        ClearBullets = 1
        Shake = 2
        ''' <summary>已移除的提示文字事件；仅用于兼容旧工程解析，载入后会被清理。</summary>
        Message = 3
        EndStage = 4
    End Enum

    Public Enum BlockCategory
        Trigger = 0
        Motion = 1
        Attack = 2
        Control = 3
        Particle = 4
    End Enum

    Public Enum BlockKind
        Hat = 0
        Stack = 1
        CBlock = 2
        CBlockElse = 3
    End Enum

    Public Enum ProgramScope
        Enemy = 0
        Bullet = 1
        Particle = 2
    End Enum

    Public Enum FieldKind
        Number = 0
        Text = 1
        Choice = 2
        Color = 3
    End Enum

    Public NotInheritable Class Ids
        Private Sub New()
        End Sub
        Public Shared Function NewId(prefix As String) As String
            Return prefix & Guid.NewGuid().ToString("N").Substring(0, 8)
        End Function
    End Class

    ''' <summary>一个编辑器工程对应一个游戏，可包含多个关卡和共享资源。</summary>
    Public Class StageProject
        Public Property FormatVersion As Integer = 1
        Public Property Id As String = Ids.NewId("stage")
        Public Property Name As String = "未命名关卡"
        Public Property Author As String = ""
        Public Property Stage As StageDocument = New StageDocument()

        Public Function Clone() As StageProject
            Return ProjectJson.Clone(Me)
        End Function

        Public Sub Normalize()
            If String.IsNullOrWhiteSpace(Id) Then Id = Ids.NewId("stage")
            If Stage Is Nothing Then Stage = New StageDocument()
            If String.IsNullOrWhiteSpace(Name) Then Name = "未命名关卡"
            Stage.Normalize()
            If String.IsNullOrWhiteSpace(Stage.Name) Then Stage.Name = Name
        End Sub
    End Class

    Public Enum GameResourceKind
        Image = 0
        Audio = 1
        Other = 2
    End Enum

    ''' <summary>游戏级共享资源，路径相对于工程文件所在目录。</summary>
    Public Class GameResource
        Public Property Id As String = Ids.NewId("res")
        Public Property Name As String = "新资源"
        Public Property Kind As GameResourceKind = GameResourceKind.Image
        Public Property RelativePath As String = ""
        Public Property Notes As String = ""

        Public Sub Normalize()
            If String.IsNullOrWhiteSpace(Id) Then Id = Ids.NewId("res")
            If String.IsNullOrWhiteSpace(Name) Then Name = "新资源"
            If RelativePath Is Nothing Then RelativePath = ""
            RelativePath = RelativePath.Replace("\"c, "/"c)
            If Notes Is Nothing Then Notes = ""
        End Sub
    End Class

    Public Class GameProject
        Public Property FormatVersion As Integer = 2
        Public Property Name As String = "未命名游戏"
        Public Property Author As String = ""
        ''' <summary>启动页面背景图片资源 Id。</summary>
        Public Property StartupBackgroundId As String = ""
        ''' <summary>标题页面背景图片资源 Id。</summary>
        Public Property TitleBackgroundId As String = ""
        ''' <summary>游戏页面背景图片资源 Id。</summary>
        Public Property GameBackgroundId As String = ""
        ''' <summary>标题页面 BGM 音频资源 Id。</summary>
        Public Property TitleBgmId As String = ""
        Public Property Stages As New List(Of StageProject)
        Public Property Resources As New List(Of GameResource)

        Public Function Clone() As GameProject
            Return ProjectJson.Clone(Me)
        End Function

        Public Sub Normalize()
            If Stages Is Nothing Then Stages = New List(Of StageProject)
            If Resources Is Nothing Then Resources = New List(Of GameResource)
            If StartupBackgroundId Is Nothing Then StartupBackgroundId = ""
            If TitleBackgroundId Is Nothing Then TitleBackgroundId = ""
            If GameBackgroundId Is Nothing Then GameBackgroundId = ""
            If TitleBgmId Is Nothing Then TitleBgmId = ""
            Stages.RemoveAll(Function(s) s Is Nothing)
            Resources.RemoveAll(Function(r) r Is Nothing)
            For Each stage In Stages
                stage.Normalize()
            Next
            For Each resource In Resources
                resource.Normalize()
            Next
            If Stages.Count = 0 Then Stages.Add(New StageProject())
            If String.IsNullOrWhiteSpace(Name) Then Name = "未命名游戏"
            If Author Is Nothing Then Author = ""
        End Sub

        Public Function FindStage(stageId As String) As StageProject
            Return Stages.FirstOrDefault(Function(s) String.Equals(s.Id, stageId, StringComparison.Ordinal))
        End Function
    End Class

    Public Enum DialogueSpeaker
        Player = 0
        Enemy = 1
    End Enum

    Public Enum DialogueAfterAction
        None = 0
        StartBoss = 1
        NextStage = 2
    End Enum

    ''' <summary>AgSTG CSV 对话中的一句话。</summary>
    Public Class DialogueLine
        Public Property Speaker As DialogueSpeaker = DialogueSpeaker.Player
        Public Property Content As String = ""
        ''' <summary>本句使用的立绘图片资源；留空时沿用对话默认立绘/旧索引。</summary>
        Public Property IllustrationId As String = ""
        Public Property Illustration As Integer = 0
        Public Property CD As Integer = 15
        Public Property Auto As Boolean = False

        Public Sub Normalize()
            If Content Is Nothing Then Content = ""
            If IllustrationId Is Nothing Then IllustrationId = ""
            If Illustration < 0 Then Illustration = 0
            If CD < 0 Then CD = 0
        End Sub
    End Class

    ''' <summary>一个可在方块中播放的 AgSTG CSV 对话。</summary>
    Public Class DialogueDefinition
        Public Property Id As String = Ids.NewId("dialog")
        Public Property Name As String = "新对话"
        Public Property Lines As New List(Of DialogueLine)
        Public Property AfterAction As DialogueAfterAction = DialogueAfterAction.None
        ''' <summary>旧字段；Boss 开打现在作用于当前场上 Boss，不再创建模板。</summary>
        Public Property BossTemplateId As String = ""
        ''' <summary>对话中玩家一侧的立绘图片资源 Id。</summary>
        Public Property PlayerIllustrationId As String = ""
        ''' <summary>对话中敌方一侧的立绘图片资源 Id。</summary>
        Public Property EnemyIllustrationId As String = ""

        Public Sub Normalize()
            If String.IsNullOrWhiteSpace(Id) Then Id = Ids.NewId("dialog")
            If String.IsNullOrWhiteSpace(Name) Then Name = "新对话"
            If Lines Is Nothing Then Lines = New List(Of DialogueLine)
            For Each line In Lines
                If line Is Nothing Then Continue For
                line.Normalize()
            Next
            Lines.RemoveAll(Function(l) l Is Nothing)
            If BossTemplateId Is Nothing Then BossTemplateId = ""
            If PlayerIllustrationId Is Nothing Then PlayerIllustrationId = ""
            If EnemyIllustrationId Is Nothing Then EnemyIllustrationId = ""
        End Sub
    End Class

    Public Class StageDocument
        Public Property Name As String = "新关卡"
        Public Property CodeName As String = "StageCustom"
        Public Property Difficulty As DifficultyMode = DifficultyMode.Normal
        Public Property LengthTicks As Integer = 3600
        Public Property Title As String = ""
        Public Property BackgroundLayers As New List(Of BackgroundLayer)
        Public Property Tracks As New List(Of TimelineTrack)
        Public Property EnemyTemplates As New List(Of EnemyTemplate)
        Public Property BulletPatterns As New List(Of BulletPattern)
        ''' <summary>本关卡的符卡池；Boss 模板通过 SpellCardIds 选择其中若干张。</summary>
        Public Property SpellCards As New List(Of BossSpellCard)
        ''' <summary>本关卡的对话资源池。</summary>
        Public Property Dialogues As New List(Of DialogueDefinition)

        <JsonIgnore>
        Public ReadOnly Property LengthSeconds As Double
            Get
                Return LengthTicks / 60.0
            End Get
        End Property

        Public Function FindTrack(id As String) As TimelineTrack
            Return Tracks.FirstOrDefault(Function(t) String.Equals(t.Id, id, StringComparison.Ordinal))
        End Function

        Public Function FindClip(id As String) As TimelineClip
            For Each t In Tracks
                For Each c In t.Clips
                    If String.Equals(c.Id, id, StringComparison.Ordinal) Then Return c
                Next
            Next
            Return Nothing
        End Function

        Public Function FindTemplate(id As String) As EnemyTemplate
            Return EnemyTemplates.FirstOrDefault(Function(t) String.Equals(t.Id, id, StringComparison.Ordinal))
        End Function

        Public Function FindPattern(id As String) As BulletPattern
            Return BulletPatterns.FirstOrDefault(Function(p) String.Equals(p.Id, id, StringComparison.Ordinal))
        End Function

        Public Function FindSpell(id As String) As BossSpellCard
            Return SpellCards.FirstOrDefault(Function(s) String.Equals(s.Id, id, StringComparison.Ordinal))
        End Function

        Public Function FindDialogue(id As String) As DialogueDefinition
            Return Dialogues.FirstOrDefault(Function(d) String.Equals(d.Id, id, StringComparison.Ordinal))
        End Function

        Public Function FindBackgroundLayer(id As String) As BackgroundLayer
            Return BackgroundLayers.FirstOrDefault(Function(l) String.Equals(l.Id, id, StringComparison.Ordinal))
        End Function

        Public Function TrackOfClip(clip As TimelineClip) As TimelineTrack
            For Each t In Tracks
                If t.Clips.Contains(clip) Then Return t
            Next
            Return Nothing
        End Function

        Public Function AllClips() As IEnumerable(Of TimelineClip)
            Return Tracks.SelectMany(Function(t) t.Clips)
        End Function

        Public Sub Normalize()
            If Tracks Is Nothing Then Tracks = New List(Of TimelineTrack)
            If BackgroundLayers Is Nothing Then BackgroundLayers = New List(Of BackgroundLayer)
            If EnemyTemplates Is Nothing Then EnemyTemplates = New List(Of EnemyTemplate)
            If BulletPatterns Is Nothing Then BulletPatterns = New List(Of BulletPattern)
            If SpellCards Is Nothing Then SpellCards = New List(Of BossSpellCard)
            If Dialogues Is Nothing Then Dialogues = New List(Of DialogueDefinition)
            If LengthTicks < 60 Then LengthTicks = 3600
            If String.IsNullOrWhiteSpace(CodeName) Then CodeName = "StageCustom"
            If Name Is Nothing Then Name = "新关卡"
            If Title Is Nothing Then Title = ""

            For Each t In Tracks
                If t Is Nothing Then Continue For
                t.Normalize()
            Next
            Tracks.RemoveAll(Function(t) t Is Nothing)

            For Each layer In BackgroundLayers
                If layer Is Nothing Then Continue For
                layer.Normalize()
            Next
            BackgroundLayers.RemoveAll(Function(l) l Is Nothing)

            For Each e In EnemyTemplates
                If e Is Nothing Then Continue For
                e.Normalize()
            Next
            EnemyTemplates.RemoveAll(Function(e) e Is Nothing)

            For Each p In BulletPatterns
                If p Is Nothing Then Continue For
                p.Normalize()
            Next
            BulletPatterns.RemoveAll(Function(p) p Is Nothing)

            For Each spell In SpellCards
                If spell Is Nothing Then Continue For
                spell.Normalize()
            Next
            SpellCards.RemoveAll(Function(s) s Is Nothing)

            For Each dialogue In Dialogues
                If dialogue Is Nothing Then Continue For
                dialogue.Normalize()
            Next
            Dialogues.RemoveAll(Function(d) d Is Nothing)

            ' 旧版把符卡直接嵌在 Boss 模板里；载入时迁移到关卡的符卡池。
            For Each template In EnemyTemplates
                If template Is Nothing Then Continue For
                If template.SpellCards Is Nothing OrElse template.SpellCards.Count = 0 Then Continue For
                For Each legacy In template.SpellCards
                    If legacy Is Nothing Then Continue For
                    legacy.Normalize()
                    Dim existing = FindSpell(legacy.Id)
                    If existing Is Nothing Then
                        SpellCards.Add(legacy)
                    Else
                        legacy = existing
                    End If
                    If Not template.SpellCardIds.Contains(legacy.Id) Then template.SpellCardIds.Add(legacy.Id)
                Next
                template.SpellCards.Clear()
            Next
        End Sub
    End Class

    Public Class TimelineTrack
        Public Property Id As String = Ids.NewId("trk")
        Public Property Name As String = "敌机轨道"
        Public Property Kind As TrackKind = TrackKind.Enemy
        Public Property TemplateId As String = ""
        Public Property BackgroundLayerId As String = ""
        Public Property Accent As String = "#D7BA7D"
        Public Property IsMuted As Boolean = False
        Public Property IsLocked As Boolean = False
        Public Property Clips As New List(Of TimelineClip)

        <JsonIgnore>
        Public ReadOnly Property IsEnemy As Boolean
            Get
                Return Kind = TrackKind.Enemy
            End Get
        End Property

        Public Sub Normalize()
            If Clips Is Nothing Then Clips = New List(Of TimelineClip)
            If String.IsNullOrWhiteSpace(Id) Then Id = Ids.NewId("trk")
            If String.IsNullOrWhiteSpace(Name) Then
                Name = If(Kind = TrackKind.Cue, "事件轨道", If(Kind = TrackKind.Background, "背景动画轨道", "敌机轨道"))
            End If
            If BackgroundLayerId Is Nothing Then BackgroundLayerId = ""
            If Kind = TrackKind.Background AndAlso BackgroundLayerId = "" AndAlso Not String.IsNullOrWhiteSpace(TemplateId) Then
                BackgroundLayerId = TemplateId
                TemplateId = ""
            End If
            If Accent Is Nothing Then Accent = "#D7BA7D"
            For Each c In Clips
                If c Is Nothing Then Continue For
                c.Normalize()
            Next
            Clips.RemoveAll(Function(c) c Is Nothing)
            ' 提示文字事件已移除；旧工程载入时直接丢弃，避免残留不可编辑的事件。
            If Kind = TrackKind.Cue Then Clips.RemoveAll(Function(c) c.Cue = CueKind.Message)
            Clips.Sort(Function(a, b) a.StartTick.CompareTo(b.StartTick))
        End Sub
    End Class

    Public Class TimelineClip
        Public Property Id As String = Ids.NewId("clip")
        Public Property StartTick As Integer = 0
        Public Property Count As Integer = 1
        Public Property IntervalTicks As Integer = 0
        Public Property Formation As FormationKind = FormationKind.SinglePoint
        Public Property Spacing As Double = 40
        Public Property FormationAngle As Double = 360
        Public Property StartAngle As Double = 0
        Public Property X As Double = 192
        Public Property Y As Double = -16
        Public Property JitterX As Double = 0
        Public Property JitterY As Double = 0
        Public Property Notes As String = ""
        ''' <summary>仅事件轨道使用。</summary>
        Public Property Cue As CueKind = CueKind.Music
        Public Property CueText As String = ""
        ''' <summary>音乐事件关联的游戏音频资源 Id；曲名仍由 CueText 单独保存。</summary>
        Public Property CueResourceId As String = ""
        Public Property CueValue As Integer = 0

        ''' <summary>背景动画轨道上的循环事件：从 LoopStartTick 到 LoopEndTick 循环 LoopLength 帧。</summary>
        Public Property IsLoopEvent As Boolean = False
        Public Property LoopStartTick As Integer = 0
        Public Property LoopEndTick As Integer = 60
        Public Property LoopLength As Integer = 60

        ''' <summary>背景动画片段持续时间。</summary>
        Public Property AnimationLength As Integer = 60
        Public Property CameraDeltaX As Double = 0
        Public Property CameraDeltaY As Double = 0
        Public Property CameraDeltaZ As Double = 0
        Public Property CameraLookDeltaX As Double = 0
        Public Property CameraLookDeltaY As Double = 0
        Public Property CameraLookDeltaZ As Double = 0
        Public Property CameraFieldOfViewTo As Double = 45
        Public Property MoveX As Double = 0
        Public Property MoveY As Double = 0
        Public Property ScaleTo As Double = 1
        Public Property RotationTo As Double = 0
        Public Property OpacityTo As Double = 1
        Public Property ParticleSpeedX As Double = 0
        Public Property ParticleSpeedY As Double = 0

        ''' <summary>3D 背景关键帧的摄像机绝对位置/朝向。</summary>
        Public Property CameraX As Double = 0
        Public Property CameraY As Double = 0
        Public Property CameraZ As Double = -7
        Public Property CameraLookX As Double = 0
        Public Property CameraLookY As Double = 0
        Public Property CameraLookZ As Double = 1
        Public Property CameraFieldOfView As Double = 45

        ''' <summary>2D 背景关键帧的绝对位置/缩放/旋转/透明度。</summary>
        Public Property TargetX As Double = 0
        Public Property TargetY As Double = 0
        Public Property TargetScale As Double = 1
        Public Property TargetRotation As Double = 0
        Public Property TargetOpacity As Double = 1

        <JsonIgnore>
        Public ReadOnly Property EndTick As Integer
            Get
                If Count <= 1 Then Return StartTick
                Return StartTick + Math.Max(0, Count - 1) * Math.Max(0, IntervalTicks)
            End Get
        End Property

        <JsonIgnore>
        Public ReadOnly Property DurationTicks As Integer
            Get
                Return Math.Max(0, EndTick - StartTick)
            End Get
        End Property

        <JsonIgnore>
        Public ReadOnly Property StartSeconds As Double
            Get
                Return StartTick / 60.0
            End Get
        End Property

        Public Function Clone() As TimelineClip
            Return New TimelineClip With {
                .Id = Id,
                .StartTick = StartTick,
                .Count = Count,
                .IntervalTicks = IntervalTicks,
                .Formation = Formation,
                .Spacing = Spacing,
                .FormationAngle = FormationAngle,
                .StartAngle = StartAngle,
                .X = X,
                .Y = Y,
                .JitterX = JitterX,
                .JitterY = JitterY,
                .Notes = Notes,
                .Cue = Cue,
                .CueText = CueText,
                .CueResourceId = CueResourceId,
                .CueValue = CueValue,
                .IsLoopEvent = IsLoopEvent,
                .LoopStartTick = LoopStartTick,
                .LoopEndTick = LoopEndTick,
                .LoopLength = LoopLength,
                .AnimationLength = AnimationLength,
                .CameraDeltaX = CameraDeltaX,
                .CameraDeltaY = CameraDeltaY,
                .CameraDeltaZ = CameraDeltaZ,
                .CameraLookDeltaX = CameraLookDeltaX,
                .CameraLookDeltaY = CameraLookDeltaY,
                .CameraLookDeltaZ = CameraLookDeltaZ,
                .CameraFieldOfViewTo = CameraFieldOfViewTo,
                .MoveX = MoveX,
                .MoveY = MoveY,
                .ScaleTo = ScaleTo,
                .RotationTo = RotationTo,
                .OpacityTo = OpacityTo,
                .ParticleSpeedX = ParticleSpeedX,
                .ParticleSpeedY = ParticleSpeedY,
                .CameraX = CameraX,
                .CameraY = CameraY,
                .CameraZ = CameraZ,
                .CameraLookX = CameraLookX,
                .CameraLookY = CameraLookY,
                .CameraLookZ = CameraLookZ,
                .CameraFieldOfView = CameraFieldOfView,
                .TargetX = TargetX,
                .TargetY = TargetY,
                .TargetScale = TargetScale,
                .TargetRotation = TargetRotation,
                .TargetOpacity = TargetOpacity
            }
        End Function

        Public Sub Normalize()
            If String.IsNullOrWhiteSpace(Id) Then Id = Ids.NewId("clip")
            If StartTick < 0 Then StartTick = 0
            If Count < 1 Then Count = 1
            If IntervalTicks < 0 Then IntervalTicks = 0
            If Spacing < 0 Then Spacing = 0
            If Notes Is Nothing Then Notes = ""
            If CueText Is Nothing Then CueText = ""
            If CueResourceId Is Nothing Then CueResourceId = ""
            If AnimationLength < 1 Then AnimationLength = 1
            If LoopStartTick < 0 Then LoopStartTick = 0
            If LoopEndTick < 0 Then LoopEndTick = 0
            If LoopLength < 1 Then LoopLength = 1
            If CameraFieldOfViewTo <= 0 Then CameraFieldOfViewTo = 45
            If ScaleTo <= 0 Then ScaleTo = 1
            If OpacityTo < 0 Then OpacityTo = 0
            If OpacityTo > 1 Then OpacityTo = 1
            If TargetScale <= 0 Then TargetScale = 1
            If TargetOpacity < 0 Then TargetOpacity = 0
            If TargetOpacity > 1 Then TargetOpacity = 1
            If CameraFieldOfView < 1 Then CameraFieldOfView = 45
            If CameraFieldOfView > 170 Then CameraFieldOfView = 170
        End Sub
    End Class

    ''' <summary>关卡背景中的一个 2D 图层或 3D 图层。</summary>
    Public Class BackgroundLayer
        Public Property Id As String = Ids.NewId("bg")
        Public Property Name As String = "新背景图层"
        Public Property Kind As BackgroundLayerKind = BackgroundLayerKind.Layer2D
        Public Property Visible As Boolean = True
        Public Property Opacity As Double = 1

        ' 2D 图层
        Public Property Shape As BackgroundShape2D = BackgroundShape2D.Rectangle
        Public Property X As Double = 0
        Public Property Y As Double = 0
        Public Property Width As Double = 384
        Public Property Height As Double = 448
        Public Property Fill As String = "#331E88E5"
        Public Property ImageResourceId As String = ""
        Public Property Stroke As String = "#00000000"
        Public Property StrokeThickness As Double = 0
        Public Property CornerRadius As Double = 0
        Public Property ParticleCount As Integer = 80
        Public Property ParticleColor As String = "#B0FFFFFF"
        ''' <summary>粒子填充图片资源；留空时使用 ParticleColor 纯色填充。</summary>
        Public Property ParticleImageResourceId As String = ""
        Public Property ParticleMinSize As Double = 1
        Public Property ParticleMaxSize As Double = 3
        Public Property ParticleSpeedX As Double = 0
        Public Property ParticleSpeedY As Double = 18
        Public Property ParticleSpread As Double = 1
        ''' <summary>粒子效果模式；2D 图层默认 2D，3D 图层可切换为 3D。</summary>
        Public Property ParticleMode As BackgroundParticleMode = BackgroundParticleMode.None
        Public Property ParticleZSpeed As Double = 0
        Public Property ParticleLifetime As Integer = 240
        ''' <summary>粒子效果的积木行为。</summary>
        Public Property ParticleBehavior As Program
        ''' <summary>整个背景图层的模糊半径，0 表示不模糊。</summary>
        Public Property BlurRadius As Double = 0

        ' 3D 图层
        Public Property CameraX As Double = 0
        Public Property CameraY As Double = 0
        Public Property CameraZ As Double = -7
        Public Property LookX As Double = 0
        Public Property LookY As Double = 0
        Public Property LookZ As Double = 1
        Public Property FieldOfView As Double = 45
        Public Property AmbientColor As String = "#FF404040"
        Public Property LightColor As String = "#FFFFFFFF"
        Public Property LightX As Double = -0.4
        Public Property LightY As Double = -0.8
        Public Property LightZ As Double = -1
        Public Property Meshes As New List(Of BackgroundMesh)

        Public Function FindMesh(id As String) As BackgroundMesh
            Return Meshes.FirstOrDefault(Function(m) String.Equals(m.Id, id, StringComparison.Ordinal))
        End Function

        Public Sub Normalize()
            If String.IsNullOrWhiteSpace(Id) Then Id = Ids.NewId("bg")
            If Name Is Nothing Then Name = "背景图层"
            If Opacity < 0 Then Opacity = 0
            If Opacity > 1 Then Opacity = 1
            If Width <= 0 Then Width = 384
            If Height <= 0 Then Height = 448
            If Stroke Is Nothing Then Stroke = "#00000000"
            If Fill Is Nothing Then Fill = "#331E88E5"
            If ImageResourceId Is Nothing Then ImageResourceId = ""
            If ParticleColor Is Nothing Then ParticleColor = "#B0FFFFFF"
            If ParticleImageResourceId Is Nothing Then ParticleImageResourceId = ""
            If ParticleCount < 0 Then ParticleCount = 0
            If ParticleCount > 5000 Then ParticleCount = 5000
            If ParticleMinSize < 0.2 Then ParticleMinSize = 0.2
            If ParticleMaxSize < ParticleMinSize Then ParticleMaxSize = ParticleMinSize
            If ParticleSpread < 0 Then ParticleSpread = 0
            If ParticleSpread > 10 Then ParticleSpread = 10
            If ParticleBehavior Is Nothing Then ParticleBehavior = Program.CreateEmpty()
            ParticleBehavior.Normalize()
            If Shape = BackgroundShape2D.ParticleField AndAlso ParticleMode = BackgroundParticleMode.None Then ParticleMode = BackgroundParticleMode.TwoD
            If ParticleMode <> BackgroundParticleMode.None Then ParticleBehavior.Normalize()
            If ParticleLifetime < 1 Then ParticleLifetime = 240
            If BlurRadius < 0 Then BlurRadius = 0
            If BlurRadius > 100 Then BlurRadius = 100
            If FieldOfView < 1 Then FieldOfView = 45
            If FieldOfView > 170 Then FieldOfView = 170
            If LookX = 0 AndAlso LookY = 0 AndAlso LookZ = 0 Then LookZ = 1
            If AmbientColor Is Nothing Then AmbientColor = "#FF404040"
            If LightColor Is Nothing Then LightColor = "#FFFFFFFF"
            If Meshes Is Nothing Then Meshes = New List(Of BackgroundMesh)
            For Each mesh In Meshes
                If mesh Is Nothing Then Continue For
                mesh.Normalize()
            Next
            Meshes.RemoveAll(Function(m) m Is Nothing)
        End Sub

        Public Function Clone() As BackgroundLayer
            Return ProjectJson.Clone(Me)
        End Function
    End Class

    ''' <summary>3D 背景中的一个基础几何体。</summary>
    Public Class BackgroundMesh
        Public Property Id As String = Ids.NewId("mesh")
        Public Property Name As String = "几何体"
        Public Property Kind As BackgroundMeshKind = BackgroundMeshKind.Box
        Public Property Color As String = "#FF5AA9E6"
        Public Property ImageResourceId As String = ""
        Public Property Opacity As Double = 1
        Public Property X As Double = 0
        Public Property Y As Double = 0
        Public Property Z As Double = 0
        Public Property RotationX As Double = 0
        Public Property RotationY As Double = 0
        Public Property RotationZ As Double = 0
        Public Property ScaleX As Double = 1
        Public Property ScaleY As Double = 1
        Public Property ScaleZ As Double = 1
        Public Property SizeX As Double = 1
        Public Property SizeY As Double = 1
        Public Property SizeZ As Double = 1
        Public Property Radius As Double = 1
        Public Property Height As Double = 2
        Public Property Segments As Integer = 20

        Public Sub Normalize()
            If String.IsNullOrWhiteSpace(Id) Then Id = Ids.NewId("mesh")
            If String.IsNullOrWhiteSpace(Name) Then Name = Kind.ToString()
            If Color Is Nothing Then Color = "#FF5AA9E6"
            If ImageResourceId Is Nothing Then ImageResourceId = ""
            If Opacity < 0 Then Opacity = 0
            If Opacity > 1 Then Opacity = 1
            If ScaleX = 0 Then ScaleX = 0.001
            If ScaleY = 0 Then ScaleY = 0.001
            If ScaleZ = 0 Then ScaleZ = 0.001
            If SizeX <= 0 Then SizeX = 1
            If SizeY <= 0 Then SizeY = 1
            If SizeZ <= 0 Then SizeZ = 1
            If Radius <= 0 Then Radius = 1
            If Height <= 0 Then Height = 2
            If Segments < 3 Then Segments = 3
            If Segments > 64 Then Segments = 64
        End Sub
    End Class

    Public Class BossSpellCard
        Public Property Id As String = Ids.NewId("spell")
        Public Property Name As String = "新符卡"
        Public Property Type As BossSpellType = BossSpellType.Life
        Public Property HaveUsual As Boolean = True
        Public Property UsualHP As Double = 1800
        Public Property UsualTime As Integer = 1800
        Public Property SpellHP As Double = 2400
        Public Property SpellTime As Integer = 2400
        Public Property Score As Long = 1000000
        Public Property Items As String = ""
        Public Property StageCount As Integer = 1
        Public Property Behavior As Program = Program.CreateEmpty()

        Public Sub Normalize()
            If String.IsNullOrWhiteSpace(Id) Then Id = Ids.NewId("spell")
            If String.IsNullOrWhiteSpace(Name) Then Name = "新符卡"
            If UsualHP < 1 Then UsualHP = 1
            If UsualTime < 1 Then UsualTime = 1
            If SpellHP < 1 Then SpellHP = 1
            If SpellTime < 1 Then SpellTime = 1
            If Score < 0 Then Score = 0
            If Items Is Nothing Then Items = ""
            If StageCount < 1 Then StageCount = 1
            If StageCount > 8 Then StageCount = 8
            If Behavior Is Nothing Then Behavior = Program.CreateEmpty()
            Behavior.Normalize()
        End Sub
    End Class

    Public Class EnemyTemplate
        Public Property Id As String = Ids.NewId("enemy")
        Public Property Name As String = "新敌机"
        Public Property EnemyType As Integer = 1
        Public Property Color As Integer = 0
        Public Property HP As Double = 20
        Public Property Defence As Double = 0
        Public Property DropItems As String = "1"
        Public Property Life As Integer = 300
        Public Property Speed As Double = 2
        Public Property Direction As Double = 180
        Public Property Behavior As Program = Program.CreateEmpty()

        ''' <summary>Boss 模板：使用 Enemy.Boss，而不是普通 Enemy。</summary>
        Public Property IsBoss As Boolean = False
        Public Property BossId As Integer = 0
        Public Property BossName As String = ""
        Public Property BossNormalFrames As Integer = 4
        Public Property BossMoveFrames As Integer = 0
        ''' <summary>Boss 普通贴图的游戏资源 Id，顺序即动画帧顺序。</summary>
        Public Property BossNormalTextureIds As New List(Of String)
        ''' <summary>Boss 移动贴图的游戏资源 Id，顺序即动画帧顺序。</summary>
        Public Property BossMoveTextureIds As New List(Of String)
        ''' <summary>是否使用游戏资源贴图；false 时兼容旧工程的 TH07 Boss 贴图设置。</summary>
        Public Property UseResourceBossTextures As Boolean = False
        ''' <summary>Boss 当前选择的符卡 Id，顺序即符卡顺序。</summary>
        Public Property SpellCardIds As New List(Of String)
        ''' <summary>旧工程内嵌符卡；载入时迁移到 StageDocument.SpellCards。</summary>
        Public Property SpellCards As New List(Of BossSpellCard)

        Public Sub Normalize()
            If String.IsNullOrWhiteSpace(Id) Then Id = Ids.NewId("enemy")
            If String.IsNullOrWhiteSpace(Name) Then Name = "新敌机"
            If EnemyType < 0 OrElse EnemyType > 6 Then EnemyType = 1
            Color = EngineCatalog.NormalizeEnemyColor(EnemyType, Color)
            If HP < 1 Then HP = 1
            If Defence < 0 Then Defence = 0
            If Defence > 0.95 Then Defence = 0.95
            If Life < 1 Then Life = 1
            If DropItems Is Nothing Then DropItems = ""
            If Behavior Is Nothing Then Behavior = Program.CreateEmpty()
            Behavior.Normalize()
            If BossId < 0 Then BossId = 0
            If BossId > 8 Then BossId = 8
            If BossNormalTextureIds Is Nothing Then BossNormalTextureIds = New List(Of String)
            If BossMoveTextureIds Is Nothing Then BossMoveTextureIds = New List(Of String)
            BossNormalTextureIds.RemoveAll(Function(id) String.IsNullOrWhiteSpace(id))
            BossMoveTextureIds.RemoveAll(Function(id) String.IsNullOrWhiteSpace(id))
            If BossNormalTextureIds.Count > 0 OrElse BossMoveTextureIds.Count > 0 Then UseResourceBossTextures = True
            If BossName Is Nothing Then BossName = ""
            If IsBoss AndAlso String.IsNullOrWhiteSpace(BossName) Then BossName = Name
            If BossNormalFrames < 0 Then BossNormalFrames = 0
            If BossNormalFrames > 16 Then BossNormalFrames = 16
            If BossMoveFrames < 0 Then BossMoveFrames = 0
            If BossMoveFrames > 16 Then BossMoveFrames = 16
            If SpellCardIds Is Nothing Then SpellCardIds = New List(Of String)
            SpellCardIds.RemoveAll(Function(id) String.IsNullOrWhiteSpace(id))
            If SpellCards Is Nothing Then SpellCards = New List(Of BossSpellCard)
            For Each spell In SpellCards
                If spell Is Nothing Then Continue For
                spell.Normalize()
            Next
            SpellCards.RemoveAll(Function(s) s Is Nothing)
        End Sub
    End Class

    Public Class BulletPattern
        Public Property Id As String = Ids.NewId("bullet")
        Public Property Name As String = "新子弹"
        Public Property BulletType As Integer = 2
        Public Property Color As Integer = 6
        Public Property Behavior As Program = Program.CreateEmpty()

        Public Sub Normalize()
            If String.IsNullOrWhiteSpace(Id) Then Id = Ids.NewId("bullet")
            If String.IsNullOrWhiteSpace(Name) Then Name = "新子弹"
            Select Case BulletType
                Case 1 To 20
                Case Else
                    BulletType = 2
            End Select
            Color = EngineCatalog.NormalizeBulletColor(BulletType, Color)
            If Behavior Is Nothing Then Behavior = Program.CreateEmpty()
            Behavior.Normalize()
        End Sub
    End Class

    ''' <summary>一段积木程序（由一个或多个『事件积木』脚本组成）。</summary>
    Public Class Program
        Public Property Scripts As New List(Of BlockNode)

        <JsonIgnore>
        Public ReadOnly Property IsEmpty As Boolean
            Get
                Return Scripts Is Nothing OrElse Scripts.Count = 0
            End Get
        End Property

        <JsonIgnore>
        Public ReadOnly Property BlockCount As Integer
            Get
                Dim total = 0
                If Scripts Is Nothing Then Return 0
                For Each s In Scripts
                    total += 1 + s.CountAllChildren()
                Next
                Return total
            End Get
        End Property

        Public Shared Function CreateEmpty() As Program
            Return New Program()
        End Function

        Public Function Clone() As Program
            Return ProjectJson.Clone(Me)
        End Function

        Public Sub Normalize()
            If Scripts Is Nothing Then Scripts = New List(Of BlockNode)
            Scripts.RemoveAll(Function(s) s Is Nothing)
            For Each s In Scripts
                s.Normalize(ProgramScope.Enemy)
            Next
        End Sub
    End Class

    ''' <summary>积木节点。Fields 保存字段字符串，Children 是 C 形积木的主体。</summary>
    Public Class BlockNode
        Public Property TypeId As String = ""
        Public Property Fields As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
        Public Property Children As New List(Of BlockNode)
        Public Property ElseChildren As New List(Of BlockNode)

        <JsonIgnore>
        Public ReadOnly Property Definition As BlockDefinition
            Get
                Return BlockRegistry.Find(TypeId)
            End Get
        End Property

        Public Function GetField(key As String) As String
            Dim value As String = Nothing
            If Fields IsNot Nothing AndAlso Fields.TryGetValue(key, value) Then Return If(value, "")
            Return ""
        End Function

        Public Sub SetField(key As String, value As String)
            If Fields Is Nothing Then Fields = New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
            Fields(key) = If(value, "")
        End Sub

        Public Function GetDouble(key As String, fallback As Double) As Double
            Return ValueExpression.ParseNumber(GetField(key), fallback)
        End Function

        Public Function GetBool(key As String, fallback As Boolean) As Boolean
            Dim text = GetField(key)
            If String.IsNullOrEmpty(text) Then Return fallback
            Return String.Equals(text, "true", StringComparison.OrdinalIgnoreCase) OrElse text = "1"
        End Function

        Public Function CountAllChildren() As Integer
            Dim total = 0
            If Children IsNot Nothing Then
                For Each c In Children
                    total += 1 + c.CountAllChildren()
                Next
            End If
            If ElseChildren IsNot Nothing Then
                For Each c In ElseChildren
                    total += 1 + c.CountAllChildren()
                Next
            End If
            Return total
        End Function

        Public Function CloneNode() As BlockNode
            Return ProjectJson.Clone(Me)
        End Function

        ''' <summary>补齐默认字段，并清理无效积木。</summary>
        Public Sub Normalize(scope As ProgramScope)
            If Fields Is Nothing Then
                Fields = New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
            Else
                Dim copy As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
                For Each kv In Fields
                    copy(kv.Key) = kv.Value
                Next
                Fields = copy
            End If
            If Children Is Nothing Then Children = New List(Of BlockNode)
            If ElseChildren Is Nothing Then ElseChildren = New List(Of BlockNode)
            If String.IsNullOrWhiteSpace(TypeId) Then TypeId = ""
            Dim def = BlockRegistry.Find(TypeId)
            If def IsNot Nothing Then
                For Each f In def.Fields
                    If Not Fields.ContainsKey(f.Key) Then Fields(f.Key) = f.DefaultValue
                Next
            End If
            Children.RemoveAll(Function(c) c Is Nothing)
            ElseChildren.RemoveAll(Function(c) c Is Nothing)
            For Each c In Children
                c.Normalize(scope)
            Next
            For Each c In ElseChildren
                c.Normalize(scope)
            Next
        End Sub
    End Class

    ''' <summary>数值字段表达式：普通数字或以 $ 开头的取值器。</summary>
    Public NotInheritable Class ValueExpression
        Private Sub New()
        End Sub

        Public Const ReporterPrefix As String = "$"

        Public Shared Function IsReporter(text As String) As Boolean
            Return Not String.IsNullOrWhiteSpace(text) AndAlso text.StartsWith(ReporterPrefix, StringComparison.Ordinal)
        End Function

        Public Shared Function ReporterKind(text As String) As String
            If Not IsReporter(text) Then Return ""
            Dim body = text.Substring(ReporterPrefix.Length)
            Dim colon = body.IndexOf(":"c)
            If colon >= 0 Then Return body.Substring(0, colon)
            Return body
        End Function

        Public Shared Function ReporterArg(text As String) As String
            If Not IsReporter(text) Then Return ""
            Dim body = text.Substring(ReporterPrefix.Length)
            Dim colon = body.IndexOf(":"c)
            If colon < 0 Then Return ""
            Return body.Substring(colon + 1)
        End Function

        Public Shared Function ParseNumber(text As String, fallback As Double) As Double
            If String.IsNullOrWhiteSpace(text) Then Return fallback
            Dim value As Double
            If Double.TryParse(text.Trim(), Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, value) Then
                Return value
            End If
            Return fallback
        End Function

        Public Shared Function FormatNumber(value As Double) As String
            If Math.Abs(value - Math.Round(value)) < 0.0000001 Then
                Return CInt(Math.Round(value)).ToString(Globalization.CultureInfo.InvariantCulture)
            End If
            Return value.ToString("0.###", Globalization.CultureInfo.InvariantCulture)
        End Function

        Public Shared Function Describe(text As String) As String
            If Not IsReporter(text) Then Return If(text, "0")
            Select Case ReporterKind(text)
                Case "ticks" : Return "存在时间"
                Case "hp" : Return "血量百分比"
                Case "px" : Return "自机 X"
                Case "py" : Return "自机 Y"
                Case "dist" : Return "与自机距离"
                Case "x" : Return "自身 X"
                Case "y" : Return "自身 Y"
                Case "rand"
                    Dim arg = ReporterArg(text)
                    Return "随机 " & If(arg, "0,1")
                Case "var" : Return "变量 " & ReporterArg(text)
                Case "bullets" : Return "场上子弹数"
                Case "enemies" : Return "场上敌机数"
                Case Else : Return text
            End Select
        End Function
    End Class

End Namespace
