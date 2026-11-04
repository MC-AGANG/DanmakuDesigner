Option Strict On
Option Infer On

Imports System.Linq

Namespace Global.DanmakuDesigner.Core

    Public Module ProjectFactory

        Public Function CreateBlankProject(Optional name As String = "未命名关卡") As StageProject
            Dim stage As New StageDocument With {
                .Name = name,
                .CodeName = "StageCustom",
                .LengthTicks = 3600,
                .Title = name
            }

            Dim orb As New BulletPattern With {
                .Id = "bp_default_orb",
                .Name = "小玉 · 白",
                .BulletType = 3,
                .Color = 15
            }
            Dim ring As New BulletPattern With {
                .Id = "bp_default_ring",
                .Name = "环弹 · 蓝",
                .BulletType = 2,
                .Color = 6
            }
            stage.BulletPatterns.Add(orb)
            stage.BulletPatterns.Add(ring)

            Dim fairy As New EnemyTemplate With {
                .Id = "et_default_fairy",
                .Name = "小妖精 · 蓝",
                .EnemyType = 1,
                .Color = 2,
                .HP = 20,
                .DropItems = "1",
                .Life = 360,
                .Speed = 2,
                .Direction = 180
            }
            stage.EnemyTemplates.Add(fairy)

            stage.Tracks.Add(New TimelineTrack With {
                .Id = "trk_enemy_1",
                .Name = "敌机轨道",
                .Kind = TrackKind.Enemy,
                .TemplateId = fairy.Id,
                .Accent = "#D7BA7D"
            })
            stage.Tracks.Add(New TimelineTrack With {
                .Id = "trk_cue_1",
                .Name = "演出 / 音乐",
                .Kind = TrackKind.Cue,
                .Accent = "#C792EA"
            })

            Dim project As New StageProject With {
                .Name = name,
                .Stage = stage
            }
            project.Normalize()
            Return project
        End Function

        ''' <summary>示例关卡：妖精编队、侧翼阴阳玉、幽灵与蝴蝶，覆盖大部分积木类型。</summary>
        Public Function CreateBlankGame(Optional name As String = "未命名游戏") As GameProject
            Dim game As New GameProject With {.Name = name}
            game.Stages.Add(CreateBlankProject("第一关"))
            game.Normalize()
            Return game
        End Function

        Public Function CreateDemoGame() As GameProject
            Dim game As New GameProject With {.Name = "幻想乡 · 夜巡", .Author = "DanmakuDesigner"}
            game.Stages.Add(CreateDemoProject())

            Dim second = CreateBlankProject("第二关 · 月下")
            second.Stage.CodeName = "StageDemo2"
            second.Stage.Difficulty = DifficultyMode.Hard
            second.Stage.LengthTicks = 2400
            game.Stages.Add(second)
            game.Normalize()
            Return game
        End Function

        Public Function CreateDemoProject() As StageProject
            Dim stage As New StageDocument With {
                .Name = "幻想乡 · 夜巡",
                .CodeName = "StageDemo",
                .Title = "幻想乡 · 夜巡",
                .Difficulty = DifficultyMode.Normal,
                .LengthTicks = 3600
            }

            ' ---------------- 子弹行为 ----------------
            Dim blueRing As New BulletPattern With {
                .Id = "bp_blue_ring",
                .Name = "环弹 · 蓝",
                .BulletType = 2,
                .Color = 6
            }
            blueRing.Behavior.Scripts.Add(Script("every_n_frame",
                BodyAlone(B("add_direction", "delta", "8")), "n", "30"))

            Dim redOrb As New BulletPattern With {
                .Id = "bp_red_orb",
                .Name = "小玉 · 红",
                .BulletType = 3,
                .Color = 2
            }
            redOrb.Behavior.Scripts.Add(Script("when_spawn",
                BodyAlone(B("wait", "frames", "24"),
                          B("add_speed", "delta", "0.4"))))

            Dim purpleOrb As New BulletPattern With {
                .Id = "bp_purple_orb",
                .Name = "小玉 · 紫",
                .BulletType = 3,
                .Color = 3
            }

            Dim yellowStar As New BulletPattern With {
                .Id = "bp_yellow_star",
                .Name = "星弹 · 黄",
                .BulletType = 11,
                .Color = 6
            }
            yellowStar.Behavior.Scripts.Add(Script("every_frame",
                BodyAlone(B("add_direction", "delta", "1.5"))))

            Dim redBig As New BulletPattern With {
                .Id = "bp_red_big",
                .Name = "大玉 · 红",
                .BulletType = 20,
                .Color = 0
            }
            redBig.Behavior.Scripts.Add(Script("every_frame",
                BodyAlone(B("add_speed", "delta", "-0.01"))))

            Dim whiteMid As New BulletPattern With {
                .Id = "bp_white_mid",
                .Name = "中玉 · 白",
                .BulletType = 12,
                .Color = 7
            }
            whiteMid.Behavior.Scripts.Add(Script("every_n_frame",
                BodyAlone(B("aim_player", "offset", "0")), "n", "8"))

            stage.BulletPatterns.AddRange({blueRing, redOrb, purpleOrb, yellowStar, redBig, whiteMid})

            ' ---------------- 敌机模板 ----------------
            Dim fairy As New EnemyTemplate With {
                .Id = "et_fairy",
                .Name = "小妖精 · 蓝",
                .EnemyType = 1,
                .Color = 2,
                .HP = 18,
                .DropItems = "1",
                .Life = 420,
                .Speed = 3,
                .Direction = 180
            }
            fairy.Behavior.Scripts.Add(Script("when_spawn", BodyAlone(B("set_speed", "speed", "3"))))
            fairy.Behavior.Scripts.Add(Script("time_between",
                BodyAlone(B("add_speed", "delta", "-0.035")), "a", "0", "b", "50"))
            fairy.Behavior.Scripts.Add(Script("at_time",
                BodyAlone(B("set_direction", "direction", "180"), B("set_speed", "speed", "0.8")), "t", "50"))
            fairy.Behavior.Scripts.Add(Script("at_time",
                BodyAlone(FireRing("8", "bp_blue_ring", "1.4", "0", "360")), "t", "90"))
            fairy.Behavior.Scripts.Add(Script("at_time",
                BodyAlone(FireAimed("3", "bp_red_orb", "2", "30")), "t", "170"))

            Dim yinyang As New EnemyTemplate With {
                .Id = "et_yinyang",
                .Name = "阴阳玉 · 红",
                .EnemyType = 0,
                .Color = 0,
                .HP = 46,
                .DropItems = "01",
                .Life = 520,
                .Speed = 2.6,
                .Direction = 90
            }
            yinyang.Behavior.Scripts.Add(Script("when_spawn",
                BodyAlone(B("play_sound", "sound", "tan01"))))
            yinyang.Behavior.Scripts.Add(Script("every_n_frame",
                BodyAlone(B("add_direction", "delta", "3")), "n", "12"))
            yinyang.Behavior.Scripts.Add(Script("every_n_frame",
                BodyAlone(FireAimed("4", "bp_purple_orb", "1.7", "40")), "n", "44"))

            Dim ghost As New EnemyTemplate With {
                .Id = "et_ghost",
                .Name = "幽灵 · 幽蓝",
                .EnemyType = 5,
                .Color = 2,
                .HP = 30,
                .DropItems = "0",
                .Life = 480,
                .Speed = 1.4,
                .Direction = 180
            }
            ghost.Behavior.Scripts.Add(Script("when_spawn",
                BodyAlone(B("set_direction", "direction", "180"), B("set_speed", "speed", "1.4"))))
            ghost.Behavior.Scripts.Add(Script("every_n_frame",
                BodyAlone(B("random_direction", "min", "150", "max", "210")), "n", "45"))
            ghost.Behavior.Scripts.Add(Script("every_n_frame",
                BodyAlone(FireAimed("5", "bp_white_mid", "1.8", "52")), "n", "75"))

            Dim butterfly As New EnemyTemplate With {
                .Id = "et_butterfly",
                .Name = "大蝴蝶 · 正常",
                .EnemyType = 4,
                .Color = 0,
                .HP = 130,
                .DropItems = "0011",
                .Life = 700,
                .Speed = 1.6,
                .Direction = 270
            }
            butterfly.Behavior.Scripts.Add(Script("when_spawn",
                BodyAlone(B("set_direction", "direction", "270"), B("set_speed", "speed", "1.6"))))
            butterfly.Behavior.Scripts.Add(Script("every_n_frame",
                BodyAlone(B("add_direction", "delta", "2")), "n", "30"))
            butterfly.Behavior.Scripts.Add(Script("every_n_frame",
                BodyAlone(FireRing("3", "bp_yellow_star", "2.2", "200", "140")), "n", "8"))
            butterfly.Behavior.Scripts.Add(Script("when_hp_below",
                BodyAlone(FireRing("24", "bp_red_big", "1.6", "0", "360"), B("shake_screen", "frames", "18")), "pct", "50"))

            stage.EnemyTemplates.AddRange({fairy, yinyang, ghost, butterfly})

            Dim boss As New EnemyTemplate With {
                .Id = "boss_demo",
                .Name = "Boss · 测试",
                .IsBoss = True,
                .BossId = 0,
                .BossName = "Demo Boss",
                .BossNormalFrames = 4,
                .BossMoveFrames = 0,
                .HP = 99999,
                .Life = 99999,
                .Speed = 0,
                .Direction = 180,
                .DropItems = ""
            }
            Dim bossSpell As New BossSpellCard With {
                .Id = "spell_demo_1",
                .Name = "符卡 · 测试",
                .Type = BossSpellType.Life,
                .HaveUsual = True,
                .UsualHP = 1200,
                .UsualTime = 1200,
                .SpellHP = 1800,
                .SpellTime = 1800,
                .Score = 1000000
            }
            Dim spellStartHat = BlockRegistry.CreateBlock("when_spawn", stage)
            spellStartHat.Children.Add(BlockRegistry.CreateBlock("clear_bullets", stage))
            bossSpell.Behavior.Scripts.Add(spellStartHat)
            Dim spellEveryHat = BlockRegistry.CreateBlock("every_frame", stage)
            Dim bossRing = BlockRegistry.CreateBlock("fire_ring", stage)
            bossRing.SetField("count", "8")
            bossRing.SetField("pattern", "bp_blue_ring")
            bossRing.SetField("speed", "1.3")
            bossRing.SetField("start", "0")
            bossRing.SetField("arc", "360")
            spellEveryHat.Children.Add(bossRing)
            bossSpell.Behavior.Scripts.Add(spellEveryHat)
            stage.SpellCards.Add(bossSpell)
            Dim demoDialogue As New DialogueDefinition With {.Name = "Boss 登场对话"}
            demoDialogue.Lines.Add(New DialogueLine With {.Speaker = DialogueSpeaker.Player, .Content = "有奇怪的妖气……", .CD = 20})
            demoDialogue.Lines.Add(New DialogueLine With {.Speaker = DialogueSpeaker.Enemy, .Content = "来得正好。", .CD = 20})
            stage.Dialogues.Add(demoDialogue)

            boss.SpellCardIds.Add(bossSpell.Id)
            Dim bossInitHat = BlockRegistry.CreateBlock("boss_init", stage)
            Dim bossDialogueBlock = BlockRegistry.CreateBlock("play_dialogue", stage)
            If bossDialogueBlock IsNot Nothing Then bossInitHat.Children.Add(bossDialogueBlock)
            Dim bossMove = BlockRegistry.CreateBlock("move_center", stage)
            bossMove.SetField("frames", "45")
            bossInitHat.Children.Add(bossMove)
            boss.Behavior.Scripts.Add(bossInitHat)
            stage.EnemyTemplates.Add(boss)

            Dim bossTrack As New TimelineTrack With {
                .Id = "trk_boss",
                .Name = "Boss · 测试",
                .Kind = TrackKind.Enemy,
                .TemplateId = boss.Id,
                .Accent = "#F48771"
            }
            bossTrack.Clips.Add(New TimelineClip With {
                .Id = "boss_clip_1",
                .StartTick = 2400,
                .Count = 1,
                .X = 192,
                .Y = -48,
                .Formation = FormationKind.SinglePoint
            })
            stage.Tracks.Add(bossTrack)

            ' ---------------- 时间轴 ----------------
            Dim tFairyWave As New TimelineTrack With {
                .Id = "trk_fairy_wave",
                .Name = "妖精 · 横向突入",
                .Kind = TrackKind.Enemy,
                .TemplateId = fairy.Id,
                .Accent = "#D7BA7D"
            }
            tFairyWave.Clips.Add(Clip(120, 8, 6, FormationKind.LineHorizontal, 38, 192, -16))
            tFairyWave.Clips.Add(Clip(300, 8, 6, FormationKind.LineHorizontal, 38, 192, -16))
            tFairyWave.Clips.Add(Clip(3180, 6, 8, FormationKind.LineHorizontal, 44, 192, -16))
            tFairyWave.Clips(1).JitterY = 10

            Dim tFairyColumn As New TimelineTrack With {
                .Id = "trk_fairy_column",
                .Name = "妖精 · 纵向编队",
                .Kind = TrackKind.Enemy,
                .TemplateId = fairy.Id,
                .Accent = "#CE9178"
            }
            tFairyColumn.Clips.Add(Clip(480, 5, 12, FormationKind.LineVertical, 44, 92, -20))
            tFairyColumn.Clips.Add(Clip(480, 5, 12, FormationKind.LineVertical, 44, 292, -20))

            Dim tYinyang As New TimelineTrack With {
                .Id = "trk_yinyang",
                .Name = "阴阳玉 · 两侧夹击",
                .Kind = TrackKind.Enemy,
                .TemplateId = yinyang.Id,
                .Accent = "#F48771"
            }
            tYinyang.Clips.Add(Clip(660, 6, 14, FormationKind.LineVertical, 50, -32, 80))
            tYinyang.Clips.Add(Clip(780, 6, 14, FormationKind.LineVertical, 50, 416, 120))

            Dim tGhost As New TimelineTrack With {
                .Id = "trk_ghost",
                .Name = "幽灵 · 随机游走",
                .Kind = TrackKind.Enemy,
                .TemplateId = ghost.Id,
                .Accent = "#9CDCFE"
            }
            Dim ghostClip1 = Clip(1260, 4, 30, FormationKind.SinglePoint, 0, 192, -16)
            ghostClip1.JitterX = 260
            Dim ghostClip2 = Clip(1500, 4, 30, FormationKind.SinglePoint, 0, 192, -16)
            ghostClip2.JitterX = 260
            tGhost.Clips.Add(ghostClip1)
            tGhost.Clips.Add(ghostClip2)

            Dim tButterfly As New TimelineTrack With {
                .Id = "trk_butterfly",
                .Name = "大蝴蝶 · 压制",
                .Kind = TrackKind.Enemy,
                .TemplateId = butterfly.Id,
                .Accent = "#C586C0"
            }
            tButterfly.Clips.Add(Clip(1840, 1, 0, FormationKind.SinglePoint, 0, 352, 100))
            tButterfly.Clips.Add(Clip(2400, 1, 0, FormationKind.SinglePoint, 0, 32, 140))
            tButterfly.Clips.Add(Clip(2900, 1, 0, FormationKind.SinglePoint, 0, 336, 90))

            Dim tCue As New TimelineTrack With {
                .Id = "trk_cue",
                .Name = "演出 / 音乐",
                .Kind = TrackKind.Cue,
                .Accent = "#C792EA"
            }
            tCue.Clips.Add(Cue(0, CueKind.Music, "無何有の郷 ～ Deep Mountain"))
            tCue.Clips.Add(Cue(3000, CueKind.ClearBullets, ""))

            Dim bg2d As New BackgroundLayer With {
                .Id = "bg_demo_2d",
                .Name = "2D 流光",
                .Kind = BackgroundLayerKind.Layer2D,
                .Shape = BackgroundShape2D.Ellipse,
                .X = 42,
                .Y = 34,
                .Width = 300,
                .Height = 300,
                .Fill = "#222A9EFF",
                .Stroke = "#604FC1FF",
                .StrokeThickness = 1
            }
            Dim bg3d As New BackgroundLayer With {
                .Id = "bg_demo_3d",
                .Name = "3D 星空遗迹",
                .Kind = BackgroundLayerKind.Layer3D,
                .CameraX = 0,
                .CameraY = 0,
                .CameraZ = -7,
                .LookZ = 1,
                .FieldOfView = 45,
                .AmbientColor = "#FF606060",
                .LightColor = "#FFFFFFFF",
                .LightX = -0.4,
                .LightY = -0.8,
                .LightZ = -1
            }
            bg3d.Meshes.Add(New BackgroundMesh With {
                .Id = "mesh_demo_box",
                .Name = "遗迹方块",
                .Kind = BackgroundMeshKind.Box,
                .Color = "#FF4FC1FF",
                .X = 0,
                .Y = 0,
                .Z = 3,
                .RotationX = 18,
                .RotationY = 28,
                .SizeX = 2,
                .SizeY = 2,
                .SizeZ = 2
            })
            bg3d.Meshes.Add(New BackgroundMesh With {
                .Id = "mesh_demo_cylinder",
                .Name = "光柱",
                .Kind = BackgroundMeshKind.Cylinder,
                .Color = "#FFCE9178",
                .X = -2.6,
                .Y = -0.6,
                .Z = 5,
                .RotationZ = 8,
                .Radius = 0.55,
                .Height = 3.2,
                .Segments = 20
            })
            bg3d.Meshes.Add(New BackgroundMesh With {
                .Id = "mesh_demo_sphere",
                .Name = "悬浮光球",
                .Kind = BackgroundMeshKind.Sphere,
                .Color = "#FFDCDCAA",
                .X = 2.6,
                .Y = 0.7,
                .Z = 4,
                .Radius = 0.65,
                .Segments = 20
            })
            stage.BackgroundLayers.Add(bg2d)
            stage.BackgroundLayers.Add(bg3d)

            Dim bg2dTrack As New TimelineTrack With {
                .Id = "trk_bg_2d",
                .Name = "背景 · 2D 流光",
                .Kind = TrackKind.Background,
                .BackgroundLayerId = bg2d.Id,
                .Accent = "#4FC1FF"
            }
            bg2dTrack.Clips.Add(New TimelineClip With {
                .StartTick = 0,
                .TargetX = 42,
                .TargetY = 34,
                .TargetScale = 1,
                .TargetRotation = 0,
                .TargetOpacity = 1
            })
            bg2dTrack.Clips.Add(New TimelineClip With {
                .StartTick = 600,
                .TargetX = 122,
                .TargetY = 174,
                .TargetScale = 1.35,
                .TargetRotation = 35,
                .TargetOpacity = 0.55
            })
            Dim bg3dTrack As New TimelineTrack With {
                .Id = "trk_bg_3d",
                .Name = "背景 · 3D 视角",
                .Kind = TrackKind.Background,
                .BackgroundLayerId = bg3d.Id,
                .Accent = "#4EC9B0"
            }
            bg3dTrack.Clips.Add(New TimelineClip With {
                .StartTick = 0,
                .CameraX = 0,
                .CameraY = 0,
                .CameraZ = -7,
                .CameraLookX = 0,
                .CameraLookY = 0,
                .CameraLookZ = 1,
                .CameraFieldOfView = 45
            })
            bg3dTrack.Clips.Add(New TimelineClip With {
                .StartTick = 720,
                .CameraX = 1.8,
                .CameraY = 0.35,
                .CameraZ = -5.8,
                .CameraLookX = -0.25,
                .CameraLookY = 0.1,
                .CameraLookZ = 1,
                .CameraFieldOfView = 52
            })
            stage.Tracks.AddRange({tFairyWave, tFairyColumn, tYinyang, tGhost, tButterfly, bg2dTrack, bg3dTrack, tCue})

            Dim project As New StageProject With {
                .Name = stage.Name,
                .Author = "DanmakuDesigner",
                .Stage = stage
            }
            project.Normalize()
            Return project
        End Function

        ' ---------------- 构造辅助 ----------------

        Private Function B(typeId As String, ParamArray kv As String()) As BlockNode
            Dim node = BlockRegistry.CreateBlock(typeId, Nothing)
            Dim i = 0
            While i + 1 < kv.Length
                node.SetField(kv(i), kv(i + 1))
                i += 2
            End While
            Return node
        End Function

        Private Function BodyAlone(ParamArray blocks As BlockNode()) As List(Of BlockNode)
            Return blocks.ToList()
        End Function

        Private Function Script(hatTypeId As String, body As List(Of BlockNode), ParamArray kv As String()) As BlockNode
            Dim hat = BlockRegistry.CreateBlock(hatTypeId, Nothing)
            Dim i = 0
            While i + 1 < kv.Length
                hat.SetField(kv(i), kv(i + 1))
                i += 2
            End While
            If body IsNot Nothing Then hat.Children.AddRange(body)
            Return hat
        End Function

        Private Function FireRing(count As String, pattern As String, speed As String, startAngle As String, arc As String) As BlockNode
            Return B("fire_ring", "count", count, "pattern", pattern, "speed", speed, "start", startAngle, "arc", arc)
        End Function

        Private Function FireAimed(count As String, pattern As String, speed As String, spread As String) As BlockNode
            Return B("fire_aimed", "count", count, "pattern", pattern, "speed", speed, "spread", spread)
        End Function

        Private Function Clip(startTick As Integer, count As Integer, interval As Integer, formation As FormationKind, spacing As Double, x As Double, y As Double) As TimelineClip
            Return New TimelineClip With {
                .StartTick = startTick,
                .Count = count,
                .IntervalTicks = interval,
                .Formation = formation,
                .Spacing = spacing,
                .X = x,
                .Y = y
            }
        End Function

        Private Function Cue(tick As Integer, kind As CueKind, text As String) As TimelineClip
            Return New TimelineClip With {
                .StartTick = tick,
                .Count = 1,
                .Cue = kind,
                .CueText = text
            }
        End Function

    End Module

End Namespace
