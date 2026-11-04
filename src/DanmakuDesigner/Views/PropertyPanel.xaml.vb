Imports System.Globalization
Imports System.Linq
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Input
Imports System.Windows.Media
Imports System.Windows.Media.Media3D
Imports DanmakuDesigner.Core

Public Class PropertyPanel

    Private _loaded As Boolean
    Private _updating As Boolean
    Private _committing As Boolean

    Public Sub New()
        InitializeComponent()
        AddHandler Loaded, Sub()
            _loaded = True
            Refresh()
        End Sub
    End Sub

    Public Sub Refresh()
        If Not _loaded Then Return
        If _updating Then Return
        _updating = True
        Try
            Host.Children.Clear()
            Dim selection = App.Session.Selection
            Select Case True
                Case selection Is Nothing
                    BuildStage(App.Session.Stage)
                Case TypeOf selection Is StageDocument
                    KindText.Text = "关卡"
                    BuildStage(DirectCast(selection, StageDocument))
                Case TypeOf selection Is GameProject
                    KindText.Text = "游戏"
                    BuildGame(DirectCast(selection, GameProject))
                Case TypeOf selection Is TimelineTrack
                    KindText.Text = "轨道"
                    BuildTrack(DirectCast(selection, TimelineTrack))
                Case TypeOf selection Is TimelineClip
                    KindText.Text = "片段"
                    BuildClip(DirectCast(selection, TimelineClip))
                Case TypeOf selection Is EnemyTemplate
                    KindText.Text = "敌机模板"
                    BuildTemplate(DirectCast(selection, EnemyTemplate))
                Case TypeOf selection Is BulletPattern
                    KindText.Text = "子弹样式"
                    BuildPattern(DirectCast(selection, BulletPattern))
                Case TypeOf selection Is BossSpellCard
                    KindText.Text = "符卡"
                    BuildSpell(DirectCast(selection, BossSpellCard))
                Case TypeOf selection Is DialogueDefinition
                    KindText.Text = "对话"
                    BuildDialogue(DirectCast(selection, DialogueDefinition))
                Case TypeOf selection Is BlockNode
                    KindText.Text = "积木"
                    BuildBlock(DirectCast(selection, BlockNode))
                Case Else
                    BuildStage(App.Session.Stage)
            End Select
        Finally
            _updating = False
        End Try
    End Sub

    ' ============================================================
    ' 关卡属性
    ' ============================================================

    Private Sub BuildGame(game As GameProject)
        If game Is Nothing Then Return
        Host.Children.Add(SectionHeader(game.Name, "Application_16x.png", game.Stages.Count & " 个关卡"))
        Host.Children.Add(TextRow("游戏名称", game.Name, Sub(value)
                                                           If Not String.IsNullOrWhiteSpace(value) Then game.Name = value.Trim()
                                                       End Sub))
        Host.Children.Add(TextRow("作者", game.Author, Sub(value) game.Author = value))

        Host.Children.Add(Ui.SectionTitle("游戏画面"))
        Dim images = ResourceChoices.ImageResourceChoices()
        images.Insert(0, New ChoiceInfo("", "默认主题图"))
        Host.Children.Add(ComboRow("启动页面背景", images, game.StartupBackgroundId, Sub(value) game.StartupBackgroundId = value))
        Host.Children.Add(ComboRow("标题页面背景", images, game.TitleBackgroundId, Sub(value) game.TitleBackgroundId = value))
        Host.Children.Add(ComboRow("游戏页面背景", images, game.GameBackgroundId, Sub(value) game.GameBackgroundId = value))

        Host.Children.Add(Ui.SectionTitle("标题页面音乐"))
        Dim songs = ResourceChoices.AudioChoices()
        songs.Insert(0, New ChoiceInfo("", "默认标题 BGM"))
        Host.Children.Add(ComboRow("标题 BGM", songs, game.TitleBgmId, Sub(value) game.TitleBgmId = value))
        Host.Children.Add(Ui.Label("背景和 BGM 来自「游戏资源」；默认主题图/默认 BGM 使用导出的内置素材。", 10.5, "#8A8A8A"))

        Host.Children.Add(Ui.SectionTitle("统计"))
        Host.Children.Add(ReadOnlyRow("关卡", game.Stages.Count.ToString()))
        Host.Children.Add(ReadOnlyRow("游戏资源", game.Resources.Count.ToString()))
    End Sub

    Private Sub BuildStage(stage As StageDocument)
        If stage Is Nothing Then
            Host.Children.Add(Ui.Label("没有打开的工程", 12, "#8A8A8A"))
            Return
        End If
        KindText.Text = "关卡"
        Host.Children.Add(SectionHeader(stage.Name, "Application_16x.png", stage.Difficulty.ToString().ToUpperInvariant() & " · " & stage.LengthTicks & " 帧"))

        Host.Children.Add(TextRow("关卡名称", stage.Name, Sub(value)
                                                                 If String.IsNullOrWhiteSpace(value) Then Return
                                                                 If App.Session.ActiveStageProject IsNot Nothing Then App.Session.ActiveStageProject.Name = value.Trim()
                                                                 stage.Name = value.Trim()
                                                             End Sub, "当前关卡显示名称；游戏名称在工程文件中设置"))
        Host.Children.Add(TextRow("代码类名", stage.CodeName, Sub(value)
                                                                 If String.IsNullOrWhiteSpace(value) Then Return
                                                                 stage.CodeName = value.Trim()
                                                             End Sub, "导出 VB 时使用的类名，必须是 ASCII 标识符"))
        Host.Children.Add(ComboRow("难度", EngineCatalog.Difficulties, stage.Difficulty.ToString(), Sub(value)
                                                                                                       Dim mode As DifficultyMode
                                                                                                       If [Enum].TryParse(value, mode) Then stage.Difficulty = mode
                                                                                                   End Sub))
        Host.Children.Add(NumberRow("总时长（帧）", stage.LengthTicks, Sub(value)
                                                                         stage.LengthTicks = Math.Max(60, CInt(value))
                                                                     End Sub, "60 帧 = 1 秒。当前 " & (stage.LengthTicks / 60.0).ToString("0.0") & " 秒"))

        Host.Children.Add(Ui.SectionTitle("统计"))
        Host.Children.Add(ReadOnlyRow("敌机模板", stage.EnemyTemplates.Count.ToString()))
        Host.Children.Add(ReadOnlyRow("子弹样式", stage.BulletPatterns.Count.ToString()))
        Host.Children.Add(ReadOnlyRow("轨道", stage.Tracks.Count.ToString()))
        Host.Children.Add(ReadOnlyRow("片段", stage.AllClips().Count().ToString()))
        Host.Children.Add(ReadOnlyRow("行为积木", stage.EnemyTemplates.Sum(Function(t) t.Behavior.BlockCount).ToString()))

        Dim buttons As New WrapPanel With {.Margin = New Thickness(0, 6, 0, 0)}
        buttons.Children.Add(MakeButton("导出 VB 关卡", "VS.AccentButton", Sub() CallMain().ExportCode()))
        buttons.Children.Add(MakeButton("试玩预览", "VS.Button", Sub() App.Session.SetView("preview")))
        buttons.Children.Add(MakeButton("重置预览", "VS.Button", Sub()
                                                           App.Session.RebuildWorld()
                                                           App.Session.SetView("preview")
                                                       End Sub))
        Host.Children.Add(buttons)
    End Sub

    ' ============================================================
    ' 轨道
    ' ============================================================

    Private Sub BuildTrack(track As TimelineTrack)
        KindText.Text = "轨道"
        Dim subtitle = If(track.Kind = TrackKind.Cue, "事件轨道", If(track.Kind = TrackKind.Background, "背景动画轨道", "敌机轨道"))
        Host.Children.Add(SectionHeader(track.Name, "Timeline_16x.png", subtitle))

        Host.Children.Add(TextRow("轨道名称", track.Name, Sub(value) track.Name = value, "显示在时间轴左侧"))
        If track.Kind = TrackKind.Enemy Then
            Dim options As New List(Of ChoiceInfo)
            For Each tmpl In App.Session.Stage.EnemyTemplates
                options.Add(New ChoiceInfo(tmpl.Id, tmpl.Name))
            Next
            Host.Children.Add(ComboRow("敌机模板", options, track.TemplateId, Sub(value) track.TemplateId = value))
        ElseIf track.Kind = TrackKind.Background Then
            Dim options As New List(Of ChoiceInfo)
            For Each layer In App.Session.Stage.BackgroundLayers
                options.Add(New ChoiceInfo(layer.Id, layer.Name))
            Next
            Host.Children.Add(ComboRow("目标背景图层", options, track.BackgroundLayerId, Sub(value) track.BackgroundLayerId = value))
            Host.Children.Add(ReadOnlyRow("控制内容", "摄像机 / 2D 位移 / 粒子速度"))
        Else
            Host.Children.Add(ReadOnlyRow("类型", "事件 / 演出"))
        End If
        Host.Children.Add(ColorRow("轨道颜色", track.Accent, Sub(value) track.Accent = value))
        Host.Children.Add(CheckRow("静音", track.IsMuted, Sub(value) track.IsMuted = value))
        Host.Children.Add(CheckRow("锁定", track.IsLocked, Sub(value) track.IsLocked = value))
        Host.Children.Add(ReadOnlyRow("片段数量", track.Clips.Count.ToString()))

        Dim buttons As New WrapPanel With {.Margin = New Thickness(0, 6, 0, 0)}
        buttons.Children.Add(MakeButton("在时间轴中查看", "VS.Button", Sub() App.Session.SetView("timeline")))
        buttons.Children.Add(MakeButton("删除轨道", "VS.Button", Sub() DeleteTrack(track)))
        Host.Children.Add(buttons)
    End Sub

    Private Sub DeleteTrack(track As TimelineTrack)
        If MessageBox.Show(Window.GetWindow(Me), "确定删除轨道【" & track.Name & "】及其中的 " & track.Clips.Count & " 个片段吗？", "删除轨道", MessageBoxButton.YesNo, MessageBoxImage.Warning) <> MessageBoxResult.Yes Then Return
        App.Session.BeginEdit()
        App.Session.Stage.Tracks.Remove(track)
        App.Session.CommitEdit()
        App.Session.SelectItem(App.Session.Stage)
        Refresh()
    End Sub

    ' ============================================================
    ' 片段
    ' ============================================================

    Private Sub BuildClip(clip As TimelineClip)
        Dim track = App.Session.Stage.TrackOfClip(clip)
        If track Is Nothing Then Return
        Dim tmpl = If(track.Kind = TrackKind.Enemy, App.Session.Stage.FindTemplate(track.TemplateId), Nothing)
        KindText.Text = "片段"
        Dim clipEnd = If(track.Kind = TrackKind.Background, clip.StartTick, clip.EndTick)
        Host.Children.Add(SectionHeader(If(tmpl Is Nothing, track.Name, tmpl.Name),
                                       If(track.Kind = TrackKind.Cue, "Timer_16x.png", If(track.Kind = TrackKind.Background, "Media_16x.png", "Bug_16x.png")),
                                       TimeSpan.FromSeconds(clip.StartTick / 60.0).ToString("m\:ss\.ff") & " → " & TimeSpan.FromSeconds(clipEnd / 60.0).ToString("m\:ss\.ff")))

        Host.Children.Add(NumberRow("开始（帧）", clip.StartTick, Sub(value)
                                                                   clip.StartTick = Math.Max(0, CInt(value))
                                                                   track.Normalize()
                                                               End Sub, "Tick " & clip.StartTick))
        Host.Children.Add(ReadOnlyRow("开始时间", TimeSpan.FromSeconds(clip.StartTick / 60.0).ToString("m\:ss\.ff")))

        If track.Kind = TrackKind.Cue Then
            Host.Children.Add(ComboRow("事件", EngineCatalog.CueTypes, clip.Cue.ToString(), Sub(value)
                                                                                                 Dim kind As CueKind
                                                                                                 If [Enum].TryParse(value, kind) Then clip.Cue = kind
                                                                                             End Sub))
            If clip.Cue = CueKind.Music Then
                Dim songs = ResourceChoices.AudioChoices()
                If songs.Count > 0 Then
                    Host.Children.Add(ComboRow("音频资源", songs, clip.CueResourceId, Sub(value)
                                                                                            clip.CueResourceId = value
                                                                                            If String.IsNullOrWhiteSpace(clip.CueText) AndAlso App.Session.Project IsNot Nothing Then
                                                                                                Dim resource = App.Session.Project.Resources.FirstOrDefault(Function(r) String.Equals(r.Id, value, StringComparison.Ordinal))
                                                                                                If resource IsNot Nothing Then clip.CueText = resource.Name
                                                                                            End If
                                                                                        End Sub))
                Else
                    Host.Children.Add(Ui.Label("游戏资源中还没有歌曲。请先在「游戏资源」中导入音频。", 10.5, "#8A8A8A"))
                End If
                Host.Children.Add(TextRow("曲名", clip.CueText, Sub(value) clip.CueText = value, "显示在游戏中的曲名，可以不同于文件名"))
            End If
            Host.Children.Add(NumberRow("震动帧数", clip.CueValue, Sub(value) clip.CueValue = Math.Max(0, CInt(value)), "仅屏幕震动事件使用"))
        ElseIf track.Kind = TrackKind.Background Then
            Dim layer = App.Session.Stage.FindBackgroundLayer(track.BackgroundLayerId)
            Host.Children.Add(ReadOnlyRow("目标图层", If(layer Is Nothing, "（未绑定）", layer.Name)))
            If layer IsNot Nothing AndAlso layer.Kind = BackgroundLayerKind.Layer3D Then
                Host.Children.Add(Ui.SectionTitle("摄像机关键帧"))
                Host.Children.Add(MakeButton("选择视角…", "VS.AccentButton", Sub() OpenCameraView(clip, layer)))
                Host.Children.Add(NumberRow("摄像机 X", clip.CameraX, Sub(value) clip.CameraX = value))
                Host.Children.Add(NumberRow("摄像机 Y", clip.CameraY, Sub(value) clip.CameraY = value))
                Host.Children.Add(NumberRow("摄像机 Z", clip.CameraZ, Sub(value) clip.CameraZ = value))
                Host.Children.Add(NumberRow("朝向 X", clip.CameraLookX, Sub(value) clip.CameraLookX = value))
                Host.Children.Add(NumberRow("朝向 Y", clip.CameraLookY, Sub(value) clip.CameraLookY = value))
                Host.Children.Add(NumberRow("朝向 Z", clip.CameraLookZ, Sub(value) clip.CameraLookZ = value))
                Host.Children.Add(NumberRow("视野 FOV", clip.CameraFieldOfView, Sub(value) clip.CameraFieldOfView = Math.Max(1, Math.Min(170, value))))
            Else
                Host.Children.Add(Ui.SectionTitle("2D / 粒子关键帧"))
                If layer IsNot Nothing AndAlso layer.Shape = BackgroundShape2D.ParticleField Then
                    Host.Children.Add(NumberRow("粒子速度 X", clip.ParticleSpeedX, Sub(value) clip.ParticleSpeedX = value))
                    Host.Children.Add(NumberRow("粒子速度 Y", clip.ParticleSpeedY, Sub(value) clip.ParticleSpeedY = value))
                    Host.Children.Add(NumberRow("不透明度", clip.TargetOpacity, Sub(value) clip.TargetOpacity = Math.Max(0, Math.Min(1, value))))
                Else
                    Host.Children.Add(NumberRow("X", clip.TargetX, Sub(value) clip.TargetX = value))
                    Host.Children.Add(NumberRow("Y", clip.TargetY, Sub(value) clip.TargetY = value))
                    Host.Children.Add(NumberRow("缩放", clip.TargetScale, Sub(value) clip.TargetScale = Math.Max(0.01, value)))
                    Host.Children.Add(NumberRow("旋转°", clip.TargetRotation, Sub(value) clip.TargetRotation = value))
                    Host.Children.Add(NumberRow("不透明度", clip.TargetOpacity, Sub(value) clip.TargetOpacity = Math.Max(0, Math.Min(1, value))))
                End If
            End If
        Else
            Host.Children.Add(NumberRow("数量", clip.Count, Sub(value)
                                                               clip.Count = Math.Max(1, CInt(value))
                                                               clip.Count = Math.Min(64, clip.Count)
                                                           End Sub, "本次生成多少个敌机"))
            Host.Children.Add(NumberRow("间隔（帧）", clip.IntervalTicks, Sub(value)
                                                                          clip.IntervalTicks = Math.Max(0, CInt(value))
                                                                      End Sub, "相邻两个敌机的时间间隔；0 表示同一帧"))
            Host.Children.Add(ComboRow("阵型", EngineCatalog.Formations, clip.Formation.ToString(), Sub(value)
                                                                                                               Dim formation As FormationKind
                                                                                                               If [Enum].TryParse(value, formation) Then clip.Formation = formation
                                                                                                           End Sub))
            Host.Children.Add(NumberRow("间距 / 半径", clip.Spacing, Sub(value) clip.Spacing = Math.Max(0, value), "横向、纵向间距或环形半径"))
            Host.Children.Add(NumberRow("阵型总角度", clip.FormationAngle, Sub(value) clip.FormationAngle = value, "环形/弧形阵使用"))
            Host.Children.Add(NumberRow("起始角度", clip.StartAngle, Sub(value) clip.StartAngle = value, "0° = 屏幕上方，顺时针"))
            Host.Children.Add(NumberRow("X", clip.X, Sub(value) clip.X = value))
            Host.Children.Add(NumberRow("Y", clip.Y, Sub(value) clip.Y = value))
            Host.Children.Add(NumberRow("随机 X", clip.JitterX, Sub(value) clip.JitterX = Math.Max(0, value), "生成位置左右随机偏移"))
            Host.Children.Add(NumberRow("随机 Y", clip.JitterY, Sub(value) clip.JitterY = Math.Max(0, value), "生成位置上下随机偏移"))
            Host.Children.Add(NoteRow("备注", clip.Notes, Sub(value) clip.Notes = value))
        End If

        If track.Kind = TrackKind.Background Then
            Host.Children.Add(ReadOnlyRow("关键帧", clip.StartTick.ToString() & " 帧"))
        Else
            Host.Children.Add(ReadOnlyRow("已存在", (clip.EndTick - clip.StartTick).ToString() & " 帧"))
        End If

        Dim buttons As New WrapPanel With {.Margin = New Thickness(0, 6, 0, 0)}
        buttons.Children.Add(MakeButton("在时间轴中查看", "VS.Button", Sub() App.Session.SetView("timeline")))
        buttons.Children.Add(MakeButton("删除片段", "VS.Button", Sub() DeleteClip(clip)))
        Host.Children.Add(buttons)
    End Sub

    Private Sub OpenCameraView(clip As TimelineClip, layer As BackgroundLayer)
        If clip Is Nothing OrElse layer Is Nothing Then Return
        Dim window As New CameraViewWindow(layer,
                                           New Point3D(clip.CameraX, clip.CameraY, clip.CameraZ),
                                           New Vector3D(clip.CameraLookX, clip.CameraLookY, clip.CameraLookZ),
                                           clip.CameraFieldOfView)
        window.Owner = Window.GetWindow(Me)
        If window.ShowDialog() = True AndAlso window.ResultPose IsNot Nothing Then
            _committing = True
            Try
                App.Session.BeginEdit()
                clip.CameraX = window.ResultPose.Position.X
                clip.CameraY = window.ResultPose.Position.Y
                clip.CameraZ = window.ResultPose.Position.Z
                clip.CameraLookX = window.ResultPose.LookDirection.X
                clip.CameraLookY = window.ResultPose.LookDirection.Y
                clip.CameraLookZ = window.ResultPose.LookDirection.Z
                clip.CameraFieldOfView = window.ResultPose.FieldOfView
                App.Session.CommitEdit()
                Refresh()
            Finally
                _committing = False
            End Try
        End If
    End Sub

    Private Sub DeleteClip(clip As TimelineClip)
        Dim track = App.Session.Stage.TrackOfClip(clip)
        If MessageBox.Show(Window.GetWindow(Me), "确定删除这个片段吗？", "删除片段", MessageBoxButton.YesNo, MessageBoxImage.Warning) <> MessageBoxResult.Yes Then Return
        App.Session.BeginEdit()
        If track IsNot Nothing Then track.Clips.Remove(clip)
        App.Session.CommitEdit()
        App.Session.SelectItem(track)
        Refresh()
    End Sub

    ' ============================================================
    ' 敌机模板 / 子弹样式
    ' ============================================================

    Private Sub BuildTemplate(tmpl As EnemyTemplate)
        KindText.Text = If(tmpl.IsBoss, "Boss 模板", "敌机模板")
        Host.Children.Add(SectionHeader(tmpl.Name, If(tmpl.IsBoss, "Rocket_16x.png", "Bug_16x.png"),
                                        If(tmpl.IsBoss, "Boss · " & tmpl.SpellCardIds.Count & " 张符卡 · " & tmpl.BossNormalTextureIds.Count & " 普通帧 / " & tmpl.BossMoveTextureIds.Count & " 移动帧", "HP " & tmpl.HP.ToString("0.#") & " · " & tmpl.Behavior.Scripts.Count & " 脚本")))

        Dim bossPreview As ImageSource = Nothing
        If tmpl.IsBoss Then
            If tmpl.UseResourceBossTextures Then
                If tmpl.BossNormalTextureIds.Count > 0 Then
                    bossPreview = BackgroundRenderer.TryLoadResourceImage(tmpl.BossNormalTextureIds(0))
                End If
            Else
                bossPreview = TextureService.Boss(tmpl.BossId, 0)
            End If
        End If
        Dim preview = If(tmpl.IsBoss,
                         MakeSpritePreview(bossPreview, GetEnemyFallbackColor(tmpl.BossId), 72),
                         MakeSpritePreview(TextureService.Enemy(tmpl.EnemyType, tmpl.Color, 0), GetEnemyFallbackColor(tmpl.Color), 72))
        preview.Margin = New Thickness(0, 0, 0, 10)
        Host.Children.Add(preview)

        Host.Children.Add(TextRow("名称", tmpl.Name, Sub(value) tmpl.Name = value))
        Host.Children.Add(ComboRow("种类", EnemyTypeOptions(), If(tmpl.IsBoss, "boss", tmpl.EnemyType.ToString()), Sub(value)
                                                                                                                         If value = "boss" Then
                                                                                                                             If Not tmpl.IsBoss Then
                                                                                                                                 tmpl.IsBoss = True
                                                                                                                                 If String.IsNullOrWhiteSpace(tmpl.BossName) Then tmpl.BossName = tmpl.Name
                                                                                                                             End If
                                                                                                                         Else
                                                                                                                             tmpl.IsBoss = False
                                                                                                                             Dim id As Integer
                                                                                                                             If Integer.TryParse(value, id) Then tmpl.EnemyType = id
                                                                                                                         End If
                                                                                                                     End Sub))
        If tmpl.IsBoss Then
            BuildBossSettings(tmpl)
        Else
            Host.Children.Add(ComboRow("颜色", EnemyColorOptions(tmpl.EnemyType), tmpl.Color.ToString(), Sub(value)
                                                                                                                 Dim id As Integer
                                                                                                                 If Integer.TryParse(value, id) Then tmpl.Color = id
                                                                                                             End Sub))
        End If
        Host.Children.Add(NumberRow("血量", tmpl.HP, Sub(value) tmpl.HP = Math.Max(1, value)))
        Host.Children.Add(NumberRow("防御 0~1", tmpl.Defence, Sub(value) tmpl.Defence = Math.Max(0, Math.Min(0.95, value))))
        Host.Children.Add(TextRow("掉落物", tmpl.DropItems, Sub(value) tmpl.DropItems = value, "示例：0011 = 两个 P 点、两个蓝点"))
        Host.Children.Add(NumberRow("存在时间", tmpl.Life, Sub(value) tmpl.Life = Math.Max(1, CInt(value)), "帧数；超时后淡出"))
        Host.Children.Add(NumberRow("初始速度", tmpl.Speed, Sub(value) tmpl.Speed = Math.Max(0, value), "像素 / 帧"))
        Host.Children.Add(NumberRow("初始方向", tmpl.Direction, Sub(value) tmpl.Direction = value, "0° 向上，顺时针"))

        Dim behaviorBox = BehaviorCard(tmpl.Behavior, If(tmpl.IsBoss, "Boss 行为 / 初始化 / 符卡效果", "敌机行为"), Sub() App.Session.EditTemplateBehavior(tmpl))
        Host.Children.Add(behaviorBox)

        Dim buttons As New WrapPanel With {.Margin = New Thickness(0, 6, 0, 0)}
        buttons.Children.Add(MakeButton("编辑积木行为", "VS.AccentButton", Sub() App.Session.EditTemplateBehavior(tmpl)))
        buttons.Children.Add(MakeButton("复制模板", "VS.Button", Sub() DuplicateTemplate(tmpl)))
        buttons.Children.Add(MakeButton("删除模板", "VS.Button", Sub() DeleteTemplate(tmpl)))
        Host.Children.Add(buttons)
    End Sub

    Private Sub BuildBossSettings(tmpl As EnemyTemplate)
        Host.Children.Add(Ui.SectionTitle("Boss 属性"))
        Host.Children.Add(TextRow("Boss 名称", tmpl.BossName, Sub(value) tmpl.BossName = value, "显示在屏幕顶部的 Boss 名称"))

        Dim textures As New WrapPanel With {.Margin = New Thickness(0, 2, 0, 4)}
        textures.Children.Add(MakeButton("普通贴图… (" & tmpl.BossNormalTextureIds.Count & " 帧)", "VS.Button",
                                         Sub() OpenBossTextures(tmpl, False)))
        textures.Children.Add(MakeButton("移动贴图… (" & tmpl.BossMoveTextureIds.Count & " 帧)", "VS.Button",
                                         Sub() OpenBossTextures(tmpl, True)))
        Host.Children.Add(textures)
        Host.Children.Add(Ui.Label("从「游戏资源」选择图片；普通贴图用于待机动画，移动贴图用于 Boss 移动动画。", 10.5, "#8A8A8A"))

        Host.Children.Add(Ui.SectionTitle("Boss 符卡"))
        Dim stage = App.Session.Stage
        Host.Children.Add(MakeButton("选择符卡… (" & tmpl.SpellCardIds.Count & " 张)", "VS.Button",
                                     Sub() OpenBossSpells(tmpl)))
        Host.Children.Add(Ui.Label("左侧选择加入，右侧调整 Boss 的符卡战斗顺序；在左侧『符卡』分组中双击可编辑符卡积木。", 10.5, "#8A8A8A"))
        Host.Children.Add(MakeButton("＋ 新建符卡", "VS.AccentButton", Sub()
                                                                              Dim spell As New BossSpellCard With {.Name = "符卡 " & (stage.SpellCards.Count + 1)}
                                                                              App.Session.BeginEdit()
                                                                              stage.SpellCards.Add(spell)
                                                                              If Not tmpl.SpellCardIds.Contains(spell.Id) Then tmpl.SpellCardIds.Add(spell.Id)
                                                                              App.Session.CommitEdit()
                                                                              Refresh()
                                                                          End Sub))
        Host.Children.Add(Ui.Label("在左侧『符卡』中双击符卡即可编辑它的积木。", 10.5, "#8A8A8A"))
    End Sub

    Private Sub BuildSpell(spell As BossSpellCard)
        Host.Children.Add(SectionHeader(spell.Name, "Rocket_16x.png", SpellTypeDisplay(spell.Type) & " · " & spell.Behavior.BlockCount & " 块"))
        Host.Children.Add(TextRow("名称", spell.Name, Sub(value) spell.Name = value))
        Dim types As New List(Of ChoiceInfo) From {
            New ChoiceInfo(BossSpellType.None.ToString(), "仅非符"),
            New ChoiceInfo(BossSpellType.Life.ToString(), "普通符卡"),
            New ChoiceInfo(BossSpellType.Time.ToString(), "时符")
        }
        Host.Children.Add(ComboRow("类型", types, spell.Type.ToString(), Sub(value)
                                                                            Dim kind As BossSpellType
                                                                            If [Enum].TryParse(value, kind) Then spell.Type = kind
                                                                        End Sub))
        Host.Children.Add(CheckRow("拥有非符阶段", spell.HaveUsual, Sub(value) spell.HaveUsual = value))
        Host.Children.Add(NumberRow("非符 HP", spell.UsualHP, Sub(value) spell.UsualHP = Math.Max(1, value)))
        Host.Children.Add(NumberRow("非符时间", spell.UsualTime, Sub(value) spell.UsualTime = Math.Max(1, CInt(value))))
        Host.Children.Add(NumberRow("符卡 HP", spell.SpellHP, Sub(value) spell.SpellHP = Math.Max(1, value)))
        Host.Children.Add(NumberRow("符卡时间", spell.SpellTime, Sub(value) spell.SpellTime = Math.Max(1, CInt(value))))
        Host.Children.Add(NumberRow("分数", spell.Score, Sub(value) spell.Score = Math.Max(0, CLng(value))))
        Host.Children.Add(TextRow("掉落物", spell.Items, Sub(value) spell.Items = value))
        Host.Children.Add(NumberRow("阶段数", spell.StageCount, Sub(value) spell.StageCount = Math.Max(1, Math.Min(8, CInt(value)))))

        Host.Children.Add(BehaviorCard(spell.Behavior, "符卡行为", Sub() App.Session.EditSpellBehavior(spell)))
        Dim buttons As New WrapPanel With {.Margin = New Thickness(0, 6, 0, 0)}
        buttons.Children.Add(MakeButton("编辑符卡积木", "VS.AccentButton", Sub() App.Session.EditSpellBehavior(spell)))
        buttons.Children.Add(MakeButton("复制符卡", "VS.Button", Sub() DuplicateSpell(spell)))
        buttons.Children.Add(MakeButton("删除符卡", "VS.Button", Sub() DeleteSpell(spell)))
        Host.Children.Add(buttons)

        Dim stage = App.Session.Stage
        If stage IsNot Nothing Then
            Dim users = stage.EnemyTemplates.Where(Function(t) t.IsBoss AndAlso t.SpellCardIds.Contains(spell.Id)).Select(Function(t) t.Name).ToList()
            Host.Children.Add(Ui.Label(If(users.Count = 0, "尚未被任何 Boss 使用。", "被以下 Boss 使用：" & String.Join("、", users)), 10.5, "#8A8A8A"))
        End If
    End Sub

    Private Sub BuildDialogue(dialogue As DialogueDefinition)
        Host.Children.Add(SectionHeader(dialogue.Name, "Document_16x.png", dialogue.Lines.Count & " 句对话"))
        Host.Children.Add(TextRow("名称", dialogue.Name, Sub(value) dialogue.Name = value))
        Host.Children.Add(Ui.Label("共 " & dialogue.Lines.Count & " 句对话；句数和内容在对话编辑器中修改。", 10.5, "#8A8A8A"))

        Host.Children.Add(Ui.SectionTitle("结束后"))
        Dim bosses As New List(Of ChoiceInfo)
        Dim stage = App.Session.Stage
        If stage IsNot Nothing Then
            For Each bossTemplate In stage.EnemyTemplates
                If bossTemplate IsNot Nothing AndAlso bossTemplate.IsBoss Then bosses.Add(New ChoiceInfo(bossTemplate.Id, bossTemplate.Name))
            Next
        End If
        Dim afterOptions As New List(Of ChoiceInfo) From {
            New ChoiceInfo(DialogueAfterAction.None.ToString(), "不结束"),
            New ChoiceInfo(DialogueAfterAction.StartBoss.ToString(), "Boss 开打"),
            New ChoiceInfo(DialogueAfterAction.NextStage.ToString(), "进入下一关")
        }
        Host.Children.Add(ComboRow("对话结束之后", afterOptions, dialogue.AfterAction.ToString(), Sub(value)
                                                                                                         Dim kind As DialogueAfterAction
                                                                                                         If [Enum].TryParse(value, kind) Then
                                                                                                             dialogue.AfterAction = kind
                                                                                                             If kind = DialogueAfterAction.StartBoss AndAlso String.IsNullOrWhiteSpace(dialogue.BossTemplateId) AndAlso bosses.Count > 0 Then
                                                                                                                 dialogue.BossTemplateId = bosses(0).Value
                                                                                                             End If
                                                                                                         End If
                                                                                                     End Sub))
        If bosses.Count > 0 Then
            Host.Children.Add(ComboRow("要开打的 Boss", bosses, dialogue.BossTemplateId, Sub(value) dialogue.BossTemplateId = value))
        Else
            Host.Children.Add(Ui.Label("还没有 Boss 模板；先在敌机模板中把某个模板设为 Boss。", 10.5, "#8A8A8A"))
        End If

        Host.Children.Add(Ui.SectionTitle("内容"))
        If dialogue.Lines.Count = 0 Then
            Host.Children.Add(Ui.Label("对话还没有内容。", 10.5, "#8A8A8A"))
        Else
            For i = 0 To Math.Min(3, dialogue.Lines.Count - 1)
                Dim line = dialogue.Lines(i)
                Dim who = If(line.Speaker = DialogueSpeaker.Enemy, "敌", "我")
                Dim text = If(line.Content, "").Replace(vbCrLf, " ").Replace(vbCr, " ").Replace(vbLf, " ")
                If text.Length > 48 Then text = text.Substring(0, 48) & "…"
                Host.Children.Add(Ui.Label(who & "：" & text, 10.5, "#C8C8C8"))
            Next
        End If

        Dim buttons As New WrapPanel With {.Margin = New Thickness(0, 6, 0, 0)}
        buttons.Children.Add(MakeButton("编辑对话…", "VS.AccentButton", Sub() EditDialogue(dialogue)))
        buttons.Children.Add(MakeButton("复制对话", "VS.Button", Sub() DuplicateDialogue(dialogue)))
        buttons.Children.Add(MakeButton("删除对话", "VS.Button", Sub() DeleteDialogue(dialogue)))
        Host.Children.Add(buttons)
    End Sub

    Private Sub EditDialogue(dialogue As DialogueDefinition)
        Dim editor As New DialogueEditorWindow(dialogue)
        editor.Owner = Window.GetWindow(Me)
        If editor.ShowDialog() <> True Then Return
        App.Session.BeginEdit()
        dialogue.Name = editor.ResultDialogue.Name
        dialogue.Lines = editor.ResultDialogue.Lines
        dialogue.AfterAction = editor.ResultDialogue.AfterAction
        dialogue.BossTemplateId = editor.ResultDialogue.BossTemplateId
        App.Session.CommitEdit()
        Refresh()
    End Sub

    Private Sub DuplicateDialogue(dialogue As DialogueDefinition)
        Dim copy = ProjectJson.Clone(dialogue)
        copy.Id = Ids.NewId("dialog")
        copy.Name = dialogue.Name & " 副本"
        App.Session.BeginEdit()
        App.Session.Stage.Dialogues.Add(copy)
        App.Session.CommitEdit()
        App.Session.SelectItem(copy)
        Refresh()
    End Sub

    Private Sub DeleteDialogue(dialogue As DialogueDefinition)
        If MessageBox.Show(Window.GetWindow(Me), "确定删除对话【" & dialogue.Name & "】吗？", "删除对话", MessageBoxButton.YesNo, MessageBoxImage.Warning) <> MessageBoxResult.Yes Then Return
        App.Session.BeginEdit()
        App.Session.Stage.Dialogues.Remove(dialogue)
        App.Session.CommitEdit()
        App.Session.SelectItem(App.Session.Stage)
        Refresh()
    End Sub

    Private Function SpellTypeDisplay(kind As BossSpellType) As String
        Select Case kind
            Case BossSpellType.Life : Return "普通符卡"
            Case BossSpellType.Time : Return "时符"
            Case Else : Return "仅非符"
        End Select
    End Function

    Private Sub BuildPattern(pattern As BulletPattern)
        KindText.Text = "子弹样式"
        Host.Children.Add(SectionHeader(pattern.Name, "Bullet_16x.png", EngineCatalog.BulletType(pattern.BulletType).Display & " · " & pattern.Behavior.BlockCount & " 块"))

        Dim preview = MakeSpritePreview(TextureService.Bullet(pattern.BulletType, pattern.Color), GetBulletFallbackColor(pattern.Color), 56)
        preview.Margin = New Thickness(0, 0, 0, 10)
        Host.Children.Add(preview)

        Host.Children.Add(TextRow("名称", pattern.Name, Sub(value) pattern.Name = value))
        Dim typeOptions As New List(Of ChoiceInfo)
        For Each info In EngineCatalog.BulletTypes
            typeOptions.Add(New ChoiceInfo(info.Id.ToString(), info.Display))
        Next
        Host.Children.Add(ComboRow("子弹种类", typeOptions, pattern.BulletType.ToString(), Sub(value)
                                                                                                             Dim id As Integer
                                                                                                             If Integer.TryParse(value, id) Then pattern.BulletType = id
                                                                                                         End Sub))
        Host.Children.Add(ComboRow("颜色", BulletColorOptions(pattern.BulletType), pattern.Color.ToString(), Sub(value)
                                                                                                          Dim id As Integer
                                                                                                          If Integer.TryParse(value, id) Then pattern.Color = id
                                                                                                      End Sub))

        Host.Children.Add(BehaviorCard(pattern.Behavior, "子弹行为", Sub() App.Session.EditPatternBehavior(pattern)))

        Dim buttons As New WrapPanel With {.Margin = New Thickness(0, 6, 0, 0)}
        buttons.Children.Add(MakeButton("编辑子弹行为", "VS.AccentButton", Sub() App.Session.EditPatternBehavior(pattern)))
        buttons.Children.Add(MakeButton("复制样式", "VS.Button", Sub() DuplicatePattern(pattern)))
        buttons.Children.Add(MakeButton("删除样式", "VS.Button", Sub() DeletePattern(pattern)))
        Host.Children.Add(buttons)
    End Sub

    Private Function BehaviorCard(program As Program, title As String, editAction As Action) As Border
        Dim border As New Border With {
            .Background = Ui.Brush("#2A2A2B"),
            .BorderBrush = Ui.Brush("#3F3F46"),
            .BorderThickness = New Thickness(1),
            .CornerRadius = New CornerRadius(3),
            .Padding = New Thickness(10, 8, 10, 9),
            .Margin = New Thickness(0, 6, 0, 0)
        }
        Dim panel As New StackPanel()
        panel.Children.Add(New TextBlock With {
            .Text = title,
            .FontSize = 11,
            .FontWeight = FontWeights.SemiBold,
            .Foreground = Ui.Brush("#E0E0E0")
        })
        Dim summary = If(program Is Nothing OrElse program.IsEmpty,
                         "还没有事件脚本。点击下方按钮，从积木库拖入「每帧」或「当生成时」。",
                         program.Scripts.Count & " 个事件脚本 · " & program.BlockCount & " 块积木")
        panel.Children.Add(New TextBlock With {
            .Text = summary,
            .FontSize = 10,
            .Foreground = Ui.Brush("#9A9A9A"),
            .TextWrapping = TextWrapping.Wrap,
            .Margin = New Thickness(0, 4, 0, 6)
        })
        If program IsNot Nothing AndAlso Not program.IsEmpty Then
            Dim hats As New List(Of String)
            For Each script In program.Scripts
                Dim def = script.Definition
                If def IsNot Nothing Then hats.Add(def.Title)
            Next
            panel.Children.Add(New TextBlock With {
                .Text = String.Join(" · ", hats.Take(5)) & If(hats.Count > 5, " …", ""),
                .FontSize = 9.5,
                .Foreground = Ui.Brush("#7F9DB9"),
                .TextWrapping = TextWrapping.Wrap
            })
        End If
        Dim editButton = MakeButton("打开积木编辑器", "VS.LinkButton", editAction)
        editButton.HorizontalAlignment = HorizontalAlignment.Left
        editButton.Margin = New Thickness(0, 7, 0, 0)
        panel.Children.Add(editButton)
        border.Child = panel
        Return border
    End Function

    Private Sub BuildBlock(block As BlockNode)
        KindText.Text = "积木"
        Dim def = block.Definition
        Host.Children.Add(SectionHeader(If(def Is Nothing, "未知积木", def.Title), "Code_16x.png", If(def Is Nothing, block.TypeId, def.Category.ToString())))
        If def IsNot Nothing Then
            Host.Children.Add(New TextBlock With {
                .Text = def.Tooltip,
                .FontSize = 11,
                .Foreground = Ui.Brush("#B8B8B8"),
                .TextWrapping = TextWrapping.Wrap,
                .LineHeight = 18,
                .Margin = New Thickness(0, 2, 0, 8)
            })
            Host.Children.Add(ReadOnlyRow("积木类型", def.TypeId))
            Host.Children.Add(ReadOnlyRow("作用域", If(def.Scope = BlockScope.EnemyOnly, "敌机", If(def.Scope = BlockScope.BulletOnly, "子弹", "通用"))))
            Host.Children.Add(ReadOnlyRow("子积木", (block.Children.Count + block.ElseChildren.Count).ToString()))
        End If
        Host.Children.Add(New TextBlock With {
            .Text = "字段请直接在积木画布上编辑；右键数字字段可以插入取值器。",
            .FontSize = 10,
            .Foreground = Ui.Brush("#7A7A7A"),
            .TextWrapping = TextWrapping.Wrap,
            .Margin = New Thickness(0, 8, 0, 0)
        })
        Host.Children.Add(MakeButton("返回积木画布", "VS.Button", Sub() App.Session.SetView("blocks")))
    End Sub

    ' ============================================================
    ' 行构建辅助
    ' ============================================================

    Private Function SectionHeader(title As String, iconName As String, subtitle As String) As Border
        Dim border As New Border With {
            .Background = Ui.Brush("#2A2A2B"),
            .CornerRadius = New CornerRadius(3),
            .Padding = New Thickness(8, 6, 8, 6),
            .Margin = New Thickness(0, 0, 0, 9)
        }
        Dim grid As New Grid()
        grid.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(24)})
        grid.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})
        Dim img = Icon(iconName, 16)
        If img IsNot Nothing Then grid.Children.Add(img)
        Dim panel As New StackPanel With {.VerticalAlignment = VerticalAlignment.Center}
        panel.Children.Add(New TextBlock With {
            .Text = If(title, "未命名"),
            .FontSize = 12,
            .FontWeight = FontWeights.SemiBold,
            .Foreground = Ui.Brush("#F1F1F1"),
            .TextTrimming = TextTrimming.CharacterEllipsis
        })
        If Not String.IsNullOrEmpty(subtitle) Then
            panel.Children.Add(New TextBlock With {
                .Text = subtitle,
                .FontSize = 9.5,
                .Foreground = Ui.Brush("#9A9A9A"),
                .Margin = New Thickness(0, 1, 0, 0),
                .TextTrimming = TextTrimming.CharacterEllipsis
            })
        End If
        Grid.SetColumn(panel, 1)
        panel.Margin = New Thickness(4, 0, 0, 0)
        grid.Children.Add(panel)
        border.Child = grid
        Return border
    End Function

    Private Function TextRow(label As String, value As String, apply As Action(Of String), Optional hint As String = "") As Grid
        Dim box = Ui.MakeTextBox(value)
        box.Tag = If(value, "")
        AddHandler box.LostKeyboardFocus, Sub() CommitText(box, apply)
        AddHandler box.KeyDown, Sub(sender As Object, e As KeyEventArgs)
                                    If e.Key = Key.Enter Then
                                        CommitText(box, apply)
                                        e.Handled = True
                                    ElseIf e.Key = Key.Escape Then
                                        box.Text = CStr(box.Tag)
                                        e.Handled = True
                                    End If
                                End Sub
        Return Ui.PropertyRow(label, box, hint)
    End Function

    Private Sub CommitText(box As TextBox, apply As Action(Of String))
        If _updating OrElse _committing Then Return
        Dim value = box.Text
        If value = CStr(box.Tag) Then Return
        _committing = True
        Try
            App.Session.BeginEdit()
            apply(value)
            App.Session.CommitEdit()
            Refresh()
        Finally
            _committing = False
        End Try
    End Sub

    Private Function NumberRow(label As String, value As Double, apply As Action(Of Double), Optional hint As String = "") As Grid
        Dim box = Ui.MakeTextBox(ValueExpression.FormatNumber(value))
        box.Tag = ValueExpression.FormatNumber(value)
        AddHandler box.LostKeyboardFocus, Sub() CommitNumber(box, apply)
        AddHandler box.KeyDown, Sub(sender As Object, e As KeyEventArgs)
                                    If e.Key = Key.Enter Then
                                        CommitNumber(box, apply)
                                        e.Handled = True
                                    ElseIf e.Key = Key.Escape Then
                                        box.Text = CStr(box.Tag)
                                        box.BorderBrush = Ui.Brush("#3F3F46")
                                        e.Handled = True
                                    End If
                                End Sub
        Return Ui.PropertyRow(label, box, hint)
    End Function

    Private Sub CommitNumber(box As TextBox, apply As Action(Of Double))
        If _updating OrElse _committing Then Return
        Dim text = box.Text.Trim()
        If text = CStr(box.Tag) Then Return
        Dim number As Double
        If Not Double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, number) Then
            box.BorderBrush = Ui.Brush("#F14C4C")
            Return
        End If
        _committing = True
        Try
            App.Session.BeginEdit()
            apply(number)
            App.Session.CommitEdit()
            Refresh()
        Finally
            _committing = False
        End Try
    End Sub

    Private Function ComboRow(label As String, options As IEnumerable(Of ChoiceInfo), selected As String, apply As Action(Of String)) As Grid
        Dim combo = Ui.MakeCombo(options, selected)
        AddHandler combo.SelectionChanged, Sub()
                                              If _updating OrElse _committing Then Return
                                              Dim value = Ui.ComboValue(combo)
                                              _committing = True
                                              Try
                                                  App.Session.BeginEdit()
                                                  apply(value)
                                                  App.Session.CommitEdit()
                                                  Refresh()
                                              Finally
                                                  _committing = False
                                              End Try
                                          End Sub
        Return Ui.PropertyRow(label, combo)
    End Function

    Private Function CheckRow(label As String, value As Boolean, apply As Action(Of Boolean)) As Grid
        Dim check As New CheckBox With {.IsChecked = value, .VerticalAlignment = VerticalAlignment.Center, .Content = ""}
        AddHandler check.Click, Sub()
                                    If _updating OrElse _committing Then Return
                                    _committing = True
                                    Try
                                        App.Session.BeginEdit()
                                        apply(If(check.IsChecked, True, False))
                                        App.Session.CommitEdit()
                                        Refresh()
                                    Finally
                                        _committing = False
                                    End Try
                                End Sub
        Return Ui.PropertyRow(label, check)
    End Function

    Private Function ColorRow(label As String, value As String, apply As Action(Of String)) As Grid
        Dim grid As New Grid()
        grid.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})
        grid.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(28)})
        Dim box = Ui.MakeTextBox(value)
        box.Tag = value
        Dim swatch As New Border With {
            .Width = 22,
            .Height = 22,
            .CornerRadius = New CornerRadius(3),
            .Background = Ui.Brush(value),
            .BorderBrush = Ui.Brush("#555555"),
            .BorderThickness = New Thickness(1),
            .Margin = New Thickness(6, 0, 0, 0),
            .Cursor = Cursors.Hand,
            .ToolTip = "点击打开调色盘"
        }
        AddHandler box.TextChanged, Sub()
                                        Try
                                            swatch.Background = Ui.Brush(box.Text.Trim())
                                            box.BorderBrush = Ui.Brush("#3F3F46")
                                        Catch
                                            box.BorderBrush = Ui.Brush("#F14C4C")
                                        End Try
                                    End Sub
        AddHandler box.LostKeyboardFocus, Sub()
                                              If _updating OrElse _committing Then Return
                                              If box.Text = CStr(box.Tag) Then Return
                                              _committing = True
                                              Try
                                                  App.Session.BeginEdit()
                                                  apply(box.Text.Trim())
                                                  App.Session.CommitEdit()
                                                  Refresh()
                                              Finally
                                                  _committing = False
                                              End Try
                                          End Sub
        AddHandler box.KeyDown, Sub(sender As Object, e As KeyEventArgs)
                                    If e.Key = Key.Enter Then
                                        box.MoveFocus(New TraversalRequest(FocusNavigationDirection.Next))
                                        e.Handled = True
                                    End If
                                End Sub
        Dim commitColor As Action(Of String) =
            Sub(color)
                If _updating OrElse _committing Then Return
                _committing = True
                Try
                    box.Text = color
                    swatch.Background = Ui.Brush(color)
                    App.Session.BeginEdit()
                    apply(color)
                    App.Session.CommitEdit()
                    Refresh()
                Finally
                    _committing = False
                End Try
            End Sub
        AddHandler swatch.MouseLeftButtonUp, Sub(sender As Object, e As MouseButtonEventArgs)
                                                     ColorPalette.Show(swatch, box.Text, commitColor)
                                                     e.Handled = True
                                                 End Sub
        grid.Children.Add(box)
        Grid.SetColumn(swatch, 1)
        grid.Children.Add(swatch)
        Return Ui.PropertyRow(label, grid)
    End Function

    Private Function NoteRow(label As String, value As String, apply As Action(Of String)) As Grid
        Dim box = Ui.MakeTextBox(value)
        box.AcceptsReturn = True
        box.TextWrapping = TextWrapping.Wrap
        box.Height = 58
        box.VerticalContentAlignment = VerticalAlignment.Top
        Dim original = If(value, "")
        AddHandler box.LostKeyboardFocus, Sub()
                                              If _updating OrElse _committing Then Return
                                              If box.Text = original Then Return
                                              _committing = True
                                              Try
                                                  App.Session.BeginEdit()
                                                  apply(box.Text)
                                                  App.Session.CommitEdit()
                                                  Refresh()
                                              Finally
                                                  _committing = False
                                              End Try
                                          End Sub
        Return Ui.PropertyRow(label, box)
    End Function

    Private Function ReadOnlyRow(label As String, value As String) As Grid
        Dim text As New TextBlock With {
            .Text = value,
            .FontSize = 11.5,
            .Foreground = Ui.Brush("#C8C8C8"),
            .VerticalAlignment = VerticalAlignment.Center,
            .TextWrapping = TextWrapping.Wrap
        }
        Return Ui.PropertyRow(label, text)
    End Function

    Private Function MakeButton(text As String, styleKey As String, action As Action) As Button
        Dim button As New Button With {
            .Content = text,
            .Style = TryCast(Application.Current.FindResource(styleKey), Style),
            .Margin = New Thickness(0, 0, 6, 6),
            .Padding = New Thickness(10, 4, 10, 4)
        }
        AddHandler button.Click, Sub() action.Invoke()
        Return button
    End Function

    Private Function MakeSpritePreview(source As ImageSource, fallback As Color, size As Double) As Border
        Dim border As New Border With {
            .Width = size + 30,
            .Height = size + 20,
            .Background = Ui.Brush("#151517"),
            .BorderBrush = Ui.Brush("#3F3F46"),
            .BorderThickness = New Thickness(1),
            .CornerRadius = New CornerRadius(3),
            .HorizontalAlignment = HorizontalAlignment.Left
        }
        If source IsNot Nothing Then
            border.Child = New Image With {
                .Source = source,
                .Width = size,
                .Height = size,
                .Stretch = Stretch.Uniform
            }
        Else
            Dim grid As New Grid()
            grid.Children.Add(New Ellipse With {
                .Width = size * 0.6,
                .Height = size * 0.6,
                .Fill = New SolidColorBrush(fallback),
                .HorizontalAlignment = HorizontalAlignment.Center,
                .VerticalAlignment = VerticalAlignment.Center
            })
            grid.Children.Add(newMissingTextureLabel())
            border.Child = grid
        End If
        Return border
    End Function

    Private Function newMissingTextureLabel() As TextBlock
        Return New TextBlock With {
            .Text = "无贴图",
            .FontSize = 9,
            .Foreground = Ui.Brush("#666666"),
            .HorizontalAlignment = HorizontalAlignment.Right,
            .VerticalAlignment = VerticalAlignment.Bottom,
            .Margin = New Thickness(0, 0, 4, 2)
        }
    End Function

    Private Function GetEnemyFallbackColor(color As Integer) As Color
        Dim palette = {System.Windows.Media.Color.FromRgb(180, 180, 180), System.Windows.Media.Color.FromRgb(160, 50, 60), System.Windows.Media.Color.FromRgb(220, 70, 70), System.Windows.Media.Color.FromRgb(170, 100, 220),
                       System.Windows.Media.Color.FromRgb(220, 90, 190), System.Windows.Media.Color.FromRgb(60, 90, 180), System.Windows.Media.Color.FromRgb(70, 130, 240), System.Windows.Media.Color.FromRgb(80, 200, 220)}
        If color < 0 OrElse color >= palette.Length Then Return palette(0)
        Return palette(color)
    End Function

    Private Function GetBulletFallbackColor(color As Integer) As Color
        Select Case color
            Case 2 : Return System.Windows.Media.Color.FromRgb(235, 70, 70)
            Case 3 : Return System.Windows.Media.Color.FromRgb(175, 90, 220)
            Case 4 : Return System.Windows.Media.Color.FromRgb(235, 90, 190)
            Case 5 : Return System.Windows.Media.Color.FromRgb(40, 70, 170)
            Case 6 : Return System.Windows.Media.Color.FromRgb(60, 120, 240)
            Case 7 : Return System.Windows.Media.Color.FromRgb(60, 200, 220)
            Case 13 : Return System.Windows.Media.Color.FromRgb(240, 220, 90)
            Case 15 : Return System.Windows.Media.Color.FromRgb(245, 245, 245)
            Case Else : Return System.Windows.Media.Color.FromRgb(200, 200, 200)
        End Select
    End Function

    Private Function EnemyTypeOptions() As List(Of ChoiceInfo)
        Dim list As New List(Of ChoiceInfo)
        For Each info In EngineCatalog.EnemyTypes
            list.Add(New ChoiceInfo(info.Id.ToString(), info.Display))
        Next
        list.Add(New ChoiceInfo("boss", "Boss"))
        Return list
    End Function

    Private Function EnemyColorOptions(enemyType As Integer) As List(Of ChoiceInfo)
        Dim list As New List(Of ChoiceInfo)
        Dim colors = EngineCatalog.EnemyColors(enemyType)
        For i = 0 To colors.Count - 1
            list.Add(New ChoiceInfo(i.ToString(), colors(i)))
        Next
        Return list
    End Function

    Private Function BulletColorOptions(bulletType As Integer) As List(Of ChoiceInfo)
        Dim list As New List(Of ChoiceInfo)
        Dim colors = EngineCatalog.BulletColorLabels(bulletType)
        For i = 0 To colors.Count - 1
            list.Add(New ChoiceInfo(i.ToString(), colors(i)))
        Next
        Return list
    End Function

    Private Sub DuplicateTemplate(tmpl As EnemyTemplate)
        Dim copy = ProjectJson.Clone(tmpl)
        copy.Id = Ids.NewId("enemy")
        copy.Name = tmpl.Name & " 副本"
        App.Session.BeginEdit()
        App.Session.Stage.EnemyTemplates.Add(copy)
        App.Session.CommitEdit()
        App.Session.SelectItem(copy)
        Refresh()
    End Sub

    Private Sub DeleteTemplate(tmpl As EnemyTemplate)
        If MessageBox.Show(Window.GetWindow(Me), "确定删除敌机模板【" & tmpl.Name & "】吗？", "删除敌机模板", MessageBoxButton.YesNo, MessageBoxImage.Warning) <> MessageBoxResult.Yes Then Return
        App.Session.BeginEdit()
        App.Session.Stage.EnemyTemplates.Remove(tmpl)
        App.Session.CommitEdit()
        App.Session.SelectItem(App.Session.Stage)
        Refresh()
    End Sub

    Private Sub OpenBossSpells(tmpl As EnemyTemplate)
        Dim stage = App.Session.Stage
        If stage Is Nothing Then Return
        Dim dialog As New BossSpellDialog(stage.SpellCards, tmpl.SpellCardIds)
        dialog.Owner = Window.GetWindow(Me)
        If dialog.ShowDialog() <> True Then Return
        App.Session.BeginEdit()
        tmpl.SpellCardIds = dialog.ResultIds.ToList()
        App.Session.CommitEdit()
        Refresh()
    End Sub

    Private Sub OpenBossTextures(tmpl As EnemyTemplate, move As Boolean)
        If tmpl Is Nothing Then Return
        Dim title = If(move, "Boss 移动贴图", "Boss 普通贴图")
        Dim selectedIds = If(move, tmpl.BossMoveTextureIds, tmpl.BossNormalTextureIds)
        Dim dialog As New BossTextureDialog(title, selectedIds)
        dialog.Owner = Window.GetWindow(Me)
        If dialog.ShowDialog() <> True Then Return
        App.Session.BeginEdit()
        tmpl.UseResourceBossTextures = True
        If move Then
            tmpl.BossMoveTextureIds = dialog.ResultIds.ToList()
        Else
            tmpl.BossNormalTextureIds = dialog.ResultIds.ToList()
        End If
        App.Session.CommitEdit()
        Refresh()
    End Sub

    Private Sub DuplicateSpell(spell As BossSpellCard)
        Dim stage = App.Session.Stage
        If stage Is Nothing Then Return
        Dim copy = ProjectJson.Clone(spell)
        copy.Id = Ids.NewId("spell")
        copy.Name = spell.Name & " 副本"
        App.Session.BeginEdit()
        stage.SpellCards.Add(copy)
        App.Session.CommitEdit()
        App.Session.SelectItem(copy)
    End Sub

    Private Sub DeleteSpell(spell As BossSpellCard)
        If MessageBox.Show(Window.GetWindow(Me), "确定删除符卡【" & spell.Name & "】吗？所有 Boss 中的引用也会移除。", "删除符卡", MessageBoxButton.YesNo, MessageBoxImage.Warning) <> MessageBoxResult.Yes Then Return
        Dim stage = App.Session.Stage
        If stage Is Nothing Then Return
        App.Session.BeginEdit()
        stage.SpellCards.Remove(spell)
        For Each tmpl In stage.EnemyTemplates
            tmpl.SpellCardIds.Remove(spell.Id)
        Next
        App.Session.CommitEdit()
        App.Session.SelectItem(stage)
    End Sub

    Private Sub DuplicatePattern(pattern As BulletPattern)
        Dim copy = ProjectJson.Clone(pattern)
        copy.Id = Ids.NewId("bullet")
        copy.Name = pattern.Name & " 副本"
        App.Session.BeginEdit()
        App.Session.Stage.BulletPatterns.Add(copy)
        App.Session.CommitEdit()
        App.Session.SelectItem(copy)
        Refresh()
    End Sub

    Private Sub DeletePattern(pattern As BulletPattern)
        If MessageBox.Show(Window.GetWindow(Me), "确定删除子弹样式【" & pattern.Name & "】吗？", "删除子弹样式", MessageBoxButton.YesNo, MessageBoxImage.Warning) <> MessageBoxResult.Yes Then Return
        App.Session.BeginEdit()
        App.Session.Stage.BulletPatterns.Remove(pattern)
        App.Session.CommitEdit()
        App.Session.SelectItem(App.Session.Stage)
        Refresh()
    End Sub

    Private Function CallMain() As MainWindow
        Return TryCast(Window.GetWindow(Me), MainWindow)
    End Function

End Class
