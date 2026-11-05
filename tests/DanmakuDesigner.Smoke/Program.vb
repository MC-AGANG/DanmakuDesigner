Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports System.Text
Imports DanmakuDesigner.Core

Module Program
    Sub Main(args As String())
        Console.OutputEncoding = Encoding.UTF8
        Dim failures = 0

        Dim demo = ProjectFactory.CreateDemoProject()
        AssertTrue(demo.Stage.Tracks.Count >= 6, "demo has tracks", failures)
        AssertTrue(demo.Stage.EnemyTemplates.Count = 5, "demo has 5 templates", failures)
        AssertTrue(demo.Stage.EnemyTemplates.Any(Function(t) t.IsBoss), "demo has a boss template", failures)
        AssertTrue(demo.Stage.SpellCards.Count = 1, "demo has a stage spell pool", failures)
        AssertTrue(demo.Stage.Dialogues.Count = 1, "demo has a stage dialogue", failures)
        AssertTrue(demo.Stage.EnemyTemplates.First(Function(t) t.IsBoss).SpellCardIds.Count = 1, "demo boss selects a spell", failures)

        Dim dialogue As New DialogueDefinition With {.Name = "Smoke Dialogue"}
        dialogue.Lines.Add(New DialogueLine With {.Speaker = DialogueSpeaker.Player, .Content = "第一句", .CD = 12})
        dialogue.Lines.Add(New DialogueLine With {.Speaker = DialogueSpeaker.Enemy, .Content = "第二句<br/>换行", .CD = 30, .Auto = True})
        Dim dialogueText As String = DialogueCsv.ToCsv(dialogue)
        AssertTrue(dialogueText.Contains("speaker,content,illustration,cd,auto"), "dialogue csv has header", failures)
        Dim parsedDialogue = DialogueCsv.Parse(dialogueText, "Roundtrip")
        AssertTrue(parsedDialogue.Lines.Count = 2, "dialogue csv roundtrip line count", failures)
        AssertTrue(parsedDialogue.Lines(1).Speaker = DialogueSpeaker.Enemy, "dialogue csv speaker roundtrip", failures)
        AssertTrue(parsedDialogue.Lines(1).Auto, "dialogue csv auto roundtrip", failures)

        Dim dropCounts = DropItemCatalog.Counts("0011")
        AssertTrue(dropCounts(0) = 2 AndAlso dropCounts(1) = 2, "drop item counts", failures)
        AssertTrue(DropItemCatalog.Total("0011") = 4, "drop item total", failures)
        AssertTrue(DropItemCatalog.Build(New Integer() {2, 2, 0, 0, 0, 0, 0}) = "0011", "drop item build", failures)

        Dim legacyBossProject = ProjectFactory.CreateBlankProject("LegacyBoss")
        Dim legacyBoss As New EnemyTemplate With {.Name = "Legacy Boss", .IsBoss = True}
        Dim legacySpell As New BossSpellCard With {.Name = "Legacy Spell"}
        legacyBoss.SpellCards.Add(legacySpell)
        legacyBossProject.Stage.EnemyTemplates.Add(legacyBoss)
        legacyBossProject.Stage.Normalize()
        AssertTrue(legacyBossProject.Stage.SpellCards.Count = 1, "legacy boss spell migrated", failures)
        AssertTrue(legacyBoss.SpellCardIds.Contains(legacySpell.Id), "legacy boss spell selected", failures)
        AssertTrue(legacyBoss.SpellCards.Count = 0, "legacy boss spell cleared", failures)
        AssertTrue(demo.Stage.BulletPatterns.Count = 6, "demo has 6 patterns", failures)

        AssertTrue(EngineCatalog.EnemyColors(0).Count = 4, "enemy 0 has 4 colors", failures)
        AssertTrue(EngineCatalog.EnemyColors(1).Count = 8, "enemy 1 has 8 colors", failures)
        AssertTrue(EngineCatalog.EnemyColors(2).Count = 2, "enemy 2 has 2 colors", failures)
        AssertTrue(EngineCatalog.EnemyColors(4).Count = 2, "enemy 4 has 2 colors", failures)
        AssertTrue(EngineCatalog.EnemyColors(6).Count = 1, "enemy 6 has 1 color", failures)
        AssertTrue(EngineCatalog.BulletColorLabels(1).Count = 16, "normal bullets have 16 colors", failures)
        AssertTrue(EngineCatalog.BulletColorLabels(11).Count = 8, "type 11 bullets have 8 colors", failures)
        AssertTrue(EngineCatalog.BulletColorLabels(20).Count = 4, "type 20 bullets have 4 colors", failures)
        AssertTrue(EngineCatalog.NormalizeEnemyColor(4, 3) = 0, "butterfly invalid color normalizes", failures)
        AssertTrue(EngineCatalog.NormalizeBulletColor(11, 13) = 0, "restricted bullet invalid color normalizes", failures)
        For Each tmpl In demo.Stage.EnemyTemplates
            AssertTrue(tmpl.Color >= 0 AndAlso tmpl.Color < EngineCatalog.EnemyColors(tmpl.EnemyType).Count,
                       "enemy color valid: " & tmpl.Name, failures)
        Next
        For Each pattern In demo.Stage.BulletPatterns
            AssertTrue(pattern.Color >= 0 AndAlso pattern.Color < EngineCatalog.BulletColorLabels(pattern.BulletType).Count,
                       "bullet color valid: " & pattern.Name, failures)
        Next

        Dim json = ProjectJson.ToJson(demo)
        AssertTrue(json.Contains("StageDemo"), "json contains code name", failures)
        Dim round = ProjectJson.FromJson(json)
        AssertTrue(round.Stage.Tracks.Count = demo.Stage.Tracks.Count, "json roundtrip tracks", failures)
        AssertTrue(round.Stage.EnemyTemplates(0).Behavior.Scripts.Count = demo.Stage.EnemyTemplates(0).Behavior.Scripts.Count, "json roundtrip scripts", failures)
        AssertTrue(round.Stage.BulletPatterns(0).Name = demo.Stage.BulletPatterns(0).Name, "json roundtrip Chinese", failures)

        Dim blank = ProjectFactory.CreateBlankProject("Smoke Blank")
        AssertTrue(blank.Stage.Tracks.Count = 2, "blank has two tracks", failures)
        AssertTrue(blank.Stage.BulletPatterns.Count = 2, "blank patterns", failures)
        AssertTrue(blank.Stage.EnemyTemplates(0).Color = 2, "blank fairy is blue (color 2)", failures)

        Dim world As New PreviewWorld(demo)
        Dim maxBullets = 0
        Dim maxEnemies = 0
        Dim sounds = 0
        world.GodMode = True
        For i = 1 To 3600
            world.InputX = 0
            world.InputY = 0
            world.Advance()
            maxBullets = Math.Max(maxBullets, world.TotalBulletCount)
            maxEnemies = Math.Max(maxEnemies, world.Enemies.Count)
        Next
        sounds = world.RecentSounds.Count
        Console.WriteLine("preview ticks=" & world.Tick & " maxBullets=" & maxBullets & " maxEnemies=" & maxEnemies & " cleared=" & world.IsCleared & " gameover=" & world.IsGameOver)
        AssertTrue(maxBullets > 50, "bullets were spawned", failures)
        AssertTrue(maxEnemies > 10, "enemies were spawned", failures)
        AssertTrue(Not world.IsCleared, "timeline end does not stop preview", failures)

        world.Seek(1800)
        AssertTrue(world.Tick = 1800, "seek reaches target", failures)
        AssertTrue(world.Enemies.Count > 0, "seek keeps enemies", failures)

        Dim issues = Validator.Validate(demo)
        Dim errs = 0
        Dim warns = 0
        For Each issue In issues
            If issue.Severity = IssueSeverity.Error Then errs += 1
            If issue.Severity = IssueSeverity.Warning Then warns += 1
            If issue.Severity = IssueSeverity.Error Then Console.WriteLine("ERR " & issue.Message & " @ " & issue.ObjectName)
        Next
        Console.WriteLine("validation issues=" & issues.Count & " errors=" & errs & " warnings=" & warns)
        AssertTrue(errs = 0, "demo has no validation errors", failures)

        Dim generated = VBCodeGenerator.Generate(demo, "F:\code\AgSTG")
        AssertTrue(generated.Code.Contains("Public Class StageDemo"), "generated class", failures)
        AssertTrue(generated.Code.Contains("Class StageDemo_Beh_et_fairy"), "generated enemy behavior class", failures)
        AssertTrue(generated.Code.Contains("Class StageDemo_Beh_bp_red_orb"), "generated bullet behavior class", failures)
        AssertTrue(generated.Code.Contains("Me.After("), "generated wait timer", failures)
        AssertTrue(generated.Code.Contains("BulletType.环弹"), "generated bullet type", failures)
        AssertTrue(generated.Code.Contains("Enemy.Boss"), "generated boss type", failures)
        AssertTrue(generated.Code.Contains("Boss.NextSpell()"), "generated boss spell start", failures)
        AssertTrue(generated.Code.Contains("Boss.SpellCards.Add"), "generated boss spell setup", failures)
        AssertTrue(generated.Code.Contains("Private Sub SP0_0()"), "generated spell behavior method", failures)

        Dim outDir = Path.Combine(Path.GetTempPath(), "dd_smoke_export")
        If Directory.Exists(outDir) Then Directory.Delete(outDir, True)
        Directory.CreateDirectory(outDir)
        File.WriteAllText(Path.Combine(outDir, "StageDemo.vb"), generated.Code, New UTF8Encoding(True))
        File.WriteAllText(Path.Combine(outDir, "StageDemobg.xaml"), generated.BackgroundXaml, New UTF8Encoding(True))
        File.WriteAllText(Path.Combine(outDir, "StageDemobg.xaml.vb"), generated.BackgroundCode, New UTF8Encoding(True))
        File.WriteAllText(Path.Combine(outDir, "StageDemo.vbproj"), generated.ProjectFile, New UTF8Encoding(True))
        Console.WriteLine("exported to " & outDir)

        ' 取值器 / 变量 / 嵌套等待校验
        Dim reporterProject = ProjectFactory.CreateBlankProject("Reporter Test")
        Dim repTemplate = reporterProject.Stage.EnemyTemplates(0)
        Dim repScript = BlockRegistry.CreateBlock("every_frame", reporterProject.Stage)
        Dim repDirection = BlockRegistry.CreateBlock("add_direction", reporterProject.Stage)
        repDirection.SetField("delta", "$rand:-2,2")
        repScript.Children.Add(repDirection)
        Dim repVar = BlockRegistry.CreateBlock("set_var", reporterProject.Stage)
        repVar.SetField("name", "spd")
        repVar.SetField("value", "$hp")
        repScript.Children.Add(repVar)
        Dim repIf = BlockRegistry.CreateBlock("if", reporterProject.Stage)
        repIf.SetField("subj", "var")
        repIf.SetField("varName", "spd")
        repIf.SetField("op", ">")
        repIf.SetField("value", "3")
        repScript.Children.Add(repIf)
        repTemplate.Behavior.Scripts.Add(repScript)
        Dim repGen = VBCodeGenerator.Generate(reporterProject, "F:\code\AgSTG")
        AssertTrue(repGen.Code.Contains("Rnd()"), "reporter rand codegen", failures)
        AssertTrue(repGen.Code.Contains("V_spd"), "variable codegen", failures)
        AssertTrue(repGen.Code.Contains("Host.HP / MaxHP"), "hp reporter codegen", failures)

        Dim badProject = ProjectFactory.CreateBlankProject("Bad")
        Dim badTemplate = badProject.Stage.EnemyTemplates(0)
        Dim badScript = BlockRegistry.CreateBlock("every_frame", badProject.Stage)
        Dim badRepeat = BlockRegistry.CreateBlock("repeat", badProject.Stage)
        badRepeat.Children.Add(BlockRegistry.CreateBlock("wait", badProject.Stage))
        badScript.Children.Add(badRepeat)
        badTemplate.Behavior.Scripts.Add(badScript)
        Dim badIssues = Validator.Validate(badProject)
        AssertTrue(badIssues.Any(Function(i) i.Severity = IssueSeverity.Error), "wait inside repeat is an error", failures)

        Dim burstProject = ProjectFactory.CreateBlankProject("Burst")
        Dim burstTrack = burstProject.Stage.Tracks.First(Function(t) t.Kind = TrackKind.Enemy)
        burstTrack.Clips.Add(New TimelineClip With {.StartTick = 10, .Count = 4, .IntervalTicks = 0, .X = 192, .Y = -16})
        Dim burstWorld As New PreviewWorld(burstProject)
        For i = 1 To 11
            burstWorld.Advance()
        Next
        AssertTrue(burstWorld.Enemies.Count = 4, "interval=0 spawns all enemies in one frame", failures)

        Dim sampleDir = "F:\code\DanmakuDesigner\samples"
        Directory.CreateDirectory(sampleDir)
        Dim demoGame = ProjectFactory.CreateDemoGame()
        AssertTrue(demoGame.Stages.Count = 2, "demo game has two stages", failures)
        AssertTrue(demoGame.Stages(0).Stage.CodeName = "StageDemo", "first stage code name", failures)
        AssertTrue(demoGame.Stages(1).Stage.CodeName = "StageDemo2", "second stage code name", failures)
        AssertTrue(demoGame.Stages(0).Stage.BackgroundLayers.Count = 2, "demo has two background layers", failures)
        AssertTrue(demoGame.Stages(0).Stage.BackgroundLayers.Any(Function(l) l.Kind = BackgroundLayerKind.Layer3D AndAlso l.Meshes.Count >= 3), "demo 3D background has meshes", failures)
        AssertTrue(demoGame.Stages(0).Stage.Tracks.Any(Function(t) t.Kind = TrackKind.Background), "demo has background tracks", failures)
        Dim bgGenerated = VBCodeGenerator.Generate(demoGame.Stages(0), "F:\code\AgSTG")
        AssertTrue(bgGenerated.BackgroundXaml.Contains("<Viewport3D"), "background xaml contains 3D viewport", failures)
        AssertTrue(bgGenerated.BackgroundXaml.Contains("BgLayer_"), "background xaml contains 2D layer", failures)
        AssertTrue(bgGenerated.BackgroundCode.Contains("AnimateBackground"), "background code contains animation", failures)
        Dim particleStage = ProjectFactory.CreateDemoProject()
        particleStage.Stage.BackgroundLayers.Add(New BackgroundLayer With {
            .Id = "bg_test_particles",
            .Name = "测试粒子",
            .Kind = BackgroundLayerKind.Layer2D,
            .Shape = BackgroundShape2D.ParticleField,
            .ParticleCount = 20,
            .ParticleImageResourceId = "res_snow",
            .BlurRadius = 5
        })
        Dim particleUris As New Dictionary(Of String, String) From {
            {"res_snow", "pack://application:,,,/GameResources;component/Resources/res_snow.png"}
        }
        Dim particleGenerated = VBCodeGenerator.Generate(particleStage, "F:\code\AgSTG", particleUris)
        AssertTrue(particleGenerated.BackgroundXaml.Contains("BgParticlesCustom_"), "background xaml contains particle canvas", failures)
        AssertTrue(particleGenerated.BackgroundCode.Contains("List(Of Particle)"), "background code uses engine Particle", failures)
        AssertTrue(particleGenerated.BackgroundCode.Contains("New Particle("), "background code creates engine Particle", failures)
        AssertTrue(particleGenerated.BackgroundXaml.Contains("BgParticleBrush_"), "particle image brush resource generated", failures)
        AssertTrue(particleGenerated.BackgroundCode.Contains("FindResource("), "particle code resolves image brush", failures)
        AssertTrue(particleGenerated.BackgroundXaml.Contains("BlurEffect Radius="""), "background layer blur emitted", failures)
        Dim p3dBehavior = DanmakuDesigner.Core.Program.CreateEmpty()
        Dim p3dCondition = BlockRegistry.CreateBlock("particle_when_position_3d", Nothing)
        p3dCondition.SetField("axis", "Z")
        p3dCondition.SetField("op", "<")
        p3dCondition.SetField("value", "-5")
        Dim p3dSize = BlockRegistry.CreateBlock("particle_set_size", Nothing)
        p3dSize.SetField("size", "4")
        p3dCondition.Children.Add(p3dSize)
        p3dBehavior.Scripts.Add(p3dCondition)
        particleStage.Stage.BackgroundLayers.Add(New BackgroundLayer With {
            .Id = "bg_test_particles3d",
            .Name = "测试3D粒子",
            .Kind = BackgroundLayerKind.Layer3D,
            .ParticleMode = BackgroundParticleMode.ThreeD,
            .ParticleCount = 8,
            .ParticleBehavior = p3dBehavior
        })
        Dim particle3DGenerated = VBCodeGenerator.Generate(particleStage, "F:\code\AgSTG")
        AssertTrue(particle3DGenerated.BackgroundCode.Contains("List(Of Particle3D)"), "background code uses engine Particle3D", failures)
        AssertTrue(particle3DGenerated.BackgroundCode.Contains("New Particle3D("), "background code creates engine Particle3D", failures)
        AssertTrue(particle3DGenerated.BackgroundCode.Contains("If p.Z < -5 Then"), "particle position condition codegen", failures)
        AssertTrue(particle3DGenerated.BackgroundXaml.Contains("BgParticles3D_"), "background xaml contains Particle3D group", failures)
        Dim particle2DDefinitions = BlockRegistry.ForScope(ProgramScope.Particle, False, False).Select(Function(d) d.TypeId).ToList()
        Dim particle3DDefinitions = BlockRegistry.ForScope(ProgramScope.Particle, False, True).Select(Function(d) d.TypeId).ToList()
        AssertTrue(particle2DDefinitions.Contains("particle_when_position_2d"), "2D particle palette has 2D condition", failures)
        AssertTrue(Not particle2DDefinitions.Contains("particle_when_position_3d"), "2D particle palette hides 3D condition", failures)
        AssertTrue(particle3DDefinitions.Contains("particle_when_position_3d"), "3D particle palette has 3D condition", failures)
        AssertTrue(Not particle3DDefinitions.Contains("particle_when_position_2d"), "3D particle palette hides 2D condition", failures)
        Dim normalEnemyBlocks = BlockRegistry.ForScope(ProgramScope.Enemy, False).Select(Function(d) d.TypeId).ToList()
        Dim bossEnemyBlocks = BlockRegistry.ForScope(ProgramScope.Enemy, True).Select(Function(d) d.TypeId).ToList()
        AssertTrue(Not normalEnemyBlocks.Contains("boss_init"), "normal enemy hides boss blocks", failures)
        AssertTrue(bossEnemyBlocks.Contains("boss_init"), "boss shows boss blocks", failures)
        Dim particleBlocks = BlockRegistry.ForScope(ProgramScope.Particle, False).Select(Function(d) d.TypeId).ToList()
        AssertTrue(particleBlocks.Contains("particle_set_velocity"), "particle scope has particle blocks", failures)
        AssertTrue(Not particleBlocks.Contains("fire_ring"), "particle scope hides enemy attack blocks", failures)

        Dim loopStage = ProjectFactory.CreateDemoProject()
        Dim loopTrack = loopStage.Stage.Tracks.First(Function(t) t.Kind = TrackKind.Background)
        Dim loopClip As New TimelineClip With {
            .StartTick = 240,
            .IsLoopEvent = True,
            .LoopStartTick = 60,
            .LoopEndTick = 120,
            .LoopLength = 60
        }
        loopTrack.Clips.Add(loopClip)
        Dim loopGenerated = VBCodeGenerator.Generate(loopStage, "F:\code\AgSTG")
        AssertTrue(loopGenerated.BackgroundCode.Contains("AdvanceBackgroundTick"), "background loop clock codegen", failures)
        AssertTrue(loopGenerated.BackgroundCode.Contains("_bgLoopStart = 60"), "background loop range codegen", failures)
        Dim validLoopIssues = Validator.Validate(loopStage)
        AssertTrue(Not validLoopIssues.Any(Function(i) i.Severity = IssueSeverity.[Error] AndAlso i.TargetId = loopClip.Id), "valid loop event passes validation", failures)
        loopClip.LoopStartTick = 200
        Dim invalidLoopIssues = Validator.Validate(loopStage)
        AssertTrue(invalidLoopIssues.Any(Function(i) i.Severity = IssueSeverity.[Error] AndAlso i.TargetId = loopClip.Id), "loop range must be before loop event", failures)
        For Each meshKind In {BackgroundMeshKind.Box, BackgroundMeshKind.Cylinder, BackgroundMeshKind.Sphere, BackgroundMeshKind.Rectangle, BackgroundMeshKind.Triangle}
            Dim geometry = BackgroundGeometry.Build(New BackgroundMesh With {.Kind = meshKind})
            AssertTrue(geometry.Positions.Count > 0 AndAlso geometry.Indices.Count >= 3, "background geometry " & meshKind.ToString(), failures)
        Next
        Dim backgroundJson = ProjectJson.ToJson(demoGame.Stages(0))
        Dim backgroundRound = ProjectJson.FromJson(backgroundJson)
        AssertTrue(backgroundRound.Stage.BackgroundLayers.Count = 2, "background layers json roundtrip", failures)
        Dim gameJson = ProjectJson.ToGameJson(demoGame)
        Dim gameRound = ProjectJson.FromGameJson(gameJson)
        AssertTrue(gameRound.Stages.Count = 2, "game json roundtrip stages", failures)
        AssertTrue(gameRound.Stages(1).Stage.Name = "第二关 · 月下", "game json roundtrip stage name", failures)
        Dim legacyWrapped = ProjectJson.FromGameJson(ProjectJson.ToJson(demo))
        AssertTrue(legacyWrapped.Stages.Count = 1, "legacy stage wrapped as game", failures)
        ProjectJson.SaveGame(Path.Combine(sampleDir, "DemoStage.dmkproj"), demoGame)
        Console.WriteLine("sample written to " & sampleDir)

        If failures > 0 Then
            Console.WriteLine("FAILED: " & failures)
            Environment.Exit(1)
        End If
        Console.WriteLine("ALL CORE CHECKS PASSED")
    End Sub

    Private Sub AssertTrue(condition As Boolean, name As String, ByRef failures As Integer)
        If condition Then
            Console.WriteLine("OK  " & name)
        Else
            Console.WriteLine("FAIL " & name)
            failures += 1
        End If
    End Sub
End Module
