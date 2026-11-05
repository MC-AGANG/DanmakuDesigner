Option Strict On
Option Infer On

Imports System.Collections.Generic
Imports System.Globalization
Imports System.Text

Namespace Global.DanmakuDesigner.Core

    ''' <summary>把 StageDocument.BackgroundLayers 编译成背景 UserControl 的 XAML 与动画代码。</summary>
    Friend Class BackgroundSceneGenerator

        Private ReadOnly _stage As StageDocument
        Private ReadOnly _className As String
        Private ReadOnly _resourceUris As IDictionary(Of String, String)

        Public Sub New(stage As StageDocument, className As String,
                       Optional resourceUris As IDictionary(Of String, String) = Nothing)
            _stage = stage
            _className = className
            _resourceUris = If(resourceUris, New Dictionary(Of String, String)())
        End Sub

        Public Function BuildXaml() As String
            Dim sb As New StringBuilder()
            sb.AppendLine("<UserControl x:Class=""" & _className & "bg""")
            sb.AppendLine("             xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""")
            sb.AppendLine("             xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""")
            sb.AppendLine("             xmlns:mc=""http://schemas.openxmlformats.org/markup-compatibility/2006""")
            sb.AppendLine("             xmlns:d=""http://schemas.microsoft.com/expression/blend/2008""")
            sb.AppendLine("             mc:Ignorable=""d"" d:DesignHeight=""448"" d:DesignWidth=""384"">")
            Dim particleResources = _stage.BackgroundLayers.Where(Function(l) l IsNot Nothing AndAlso HasCustomParticles(l) AndAlso ResourceUri(l.ParticleImageResourceId) IsNot Nothing).ToList()
            If particleResources.Count > 0 Then
                sb.AppendLine("    <UserControl.Resources>")
                For Each layer In particleResources
                    sb.AppendLine("        <ImageBrush x:Key=""" & ParticleBrushKey(layer) & """ ImageSource=""" & XmlEscape(ResourceUri(layer.ParticleImageResourceId)) & """ Stretch=""Fill""/>")
                Next
                sb.AppendLine("    </UserControl.Resources>")
            End If
            sb.AppendLine("    <Canvas x:Name=""Root"" ClipToBounds=""True"" Width=""384"" Height=""448"">")
            For Each layer In _stage.BackgroundLayers
                If layer Is Nothing Then Continue For
                If layer.Kind = BackgroundLayerKind.Layer2D Then
                    Append2DLayer(sb, layer)
                Else
                    Append3DLayer(sb, layer)
                End If
            Next
            sb.AppendLine("    </Canvas>")
            sb.AppendLine("</UserControl>")
            Return sb.ToString()
        End Function

        Private Sub Append2DLayer(sb As StringBuilder, layer As BackgroundLayer)
            Dim visibility = If(layer.Visible, "Visible", "Collapsed")
            If layer.Shape = BackgroundShape2D.ParticleField Then
                AppendCustomParticleCanvas(sb, layer)
                Return
            End If

            Dim shapeTag = If(layer.Shape = BackgroundShape2D.Ellipse, "Ellipse", "Rectangle")
            Dim radius = If(layer.Shape = BackgroundShape2D.Rectangle,
                             " RadiusX=""" & N(Math.Max(0, layer.CornerRadius)) & """ RadiusY=""" & N(Math.Max(0, layer.CornerRadius)) & """", "")
            Dim imageUri = ResourceUri(layer.ImageResourceId)
            If imageUri IsNot Nothing Then
                sb.AppendLine("        <" & shapeTag & " x:Name=""" & LayerName(layer) & """ Canvas.Left=""" & N(layer.X) & """ Canvas.Top=""" & N(layer.Y) &
                              """ Width=""" & N(layer.Width) & """ Height=""" & N(layer.Height) & """" & radius &
                              " Stroke=""" & VBCodeGenerator.NormalizeColor(layer.Stroke) & """ StrokeThickness=""" & N(layer.StrokeThickness) &
                              """ Opacity=""" & N(layer.Opacity) & """ Visibility=""" & visibility & """ IsHitTestVisible=""False"">")
                sb.AppendLine("            <" & shapeTag & ".Fill>")
                sb.AppendLine("                <ImageBrush ImageSource=""" & XmlEscape(imageUri) & """ Stretch=""Fill""/>")
                sb.AppendLine("            </" & shapeTag & ".Fill>")
                If layer.BlurRadius > 0 Then
                    sb.AppendLine("            <" & shapeTag & ".Effect>")
                    sb.AppendLine("                <BlurEffect Radius=""" & N(layer.BlurRadius) & """/>")
                    sb.AppendLine("            </" & shapeTag & ".Effect>")
                End If
                sb.AppendLine("        </" & shapeTag & ">")
                Return
            End If
            Dim shapeOpen = "        <" & shapeTag & " x:Name=""" & LayerName(layer) & """ Canvas.Left=""" & N(layer.X) & """ Canvas.Top=""" & N(layer.Y) &
                            """ Width=""" & N(layer.Width) & """ Height=""" & N(layer.Height) & """ Fill=""" & VBCodeGenerator.NormalizeColor(layer.Fill) &
                            """ Stroke=""" & VBCodeGenerator.NormalizeColor(layer.Stroke) & """ StrokeThickness=""" & N(layer.StrokeThickness) &
                            """" & radius & " Opacity=""" & N(layer.Opacity) & """ Visibility=""" & visibility & """ IsHitTestVisible=""False"""
            If layer.BlurRadius > 0 Then
                sb.AppendLine(shapeOpen & ">")
                sb.AppendLine("            <" & shapeTag & ".Effect>")
                sb.AppendLine("                <BlurEffect Radius=""" & N(layer.BlurRadius) & """/>")
                sb.AppendLine("            </" & shapeTag & ".Effect>")
                sb.AppendLine("        </" & shapeTag & ">")
            Else
                sb.AppendLine(shapeOpen & "/>")
            End If
            AppendCustomParticleCanvas(sb, layer)
        End Sub

        Private Sub AppendCustomParticleCanvas(sb As StringBuilder, layer As BackgroundLayer)
            If Not HasCustomParticles(layer) OrElse layer.Kind <> BackgroundLayerKind.Layer2D Then Return
            Dim visibility = If(layer.Visible, "Visible", "Collapsed")
            Dim canvasOpen = "        <Canvas x:Name=""" & CustomParticleCanvasName(layer) & """ Canvas.Left=""0"" Canvas.Top=""0"" Width=""384"" Height=""448"" Opacity=""" & N(layer.Opacity) & """ Visibility=""" & visibility & """ IsHitTestVisible=""False"""
            If layer.BlurRadius > 0 Then
                sb.AppendLine(canvasOpen & ">")
                sb.AppendLine("            <Canvas.Effect>")
                sb.AppendLine("                <BlurEffect Radius=""" & N(layer.BlurRadius) & """/>")
                sb.AppendLine("            </Canvas.Effect>")
                sb.AppendLine("        </Canvas>")
            Else
                sb.AppendLine(canvasOpen & "/>")
            End If
        End Sub

        Private Sub AppendCustomParticleGroup(sb As StringBuilder, layer As BackgroundLayer)
            If Not HasCustomParticles(layer) OrElse layer.Kind <> BackgroundLayerKind.Layer3D Then Return
            sb.AppendLine("        <ModelVisual3D>")
            sb.AppendLine("            <ModelVisual3D.Content>")
            sb.AppendLine("                <Model3DGroup x:Name=""" & CustomParticleGroupName(layer) & """/>")
            sb.AppendLine("            </ModelVisual3D.Content>")
            sb.AppendLine("        </ModelVisual3D>")
        End Sub

        Private Sub Append3DLayer(sb As StringBuilder, layer As BackgroundLayer)
            Dim visibility = If(layer.Visible, "Visible", "Collapsed")
            Dim viewName As String = ViewportName(layer)
            sb.AppendLine("        <Viewport3D x:Name=""" & viewName & """ Width=""384"" Height=""448"" ClipToBounds=""True"" Opacity=""" & N(layer.Opacity) & """ Visibility=""" & visibility & """ IsHitTestVisible=""False"">")
            sb.AppendLine("            <Viewport3D.Camera>")
            sb.AppendLine("                <PerspectiveCamera x:Name=""" & CameraName(layer) & """ Position=""" & N(layer.CameraX) & "," & N(layer.CameraY) & "," & N(layer.CameraZ) &
                          """ LookDirection=""" & N(layer.LookX) & "," & N(layer.LookY) & "," & N(layer.LookZ) &
                          """ UpDirection=""0,1,0"" FieldOfView=""" & N(layer.FieldOfView) & """/>")
            sb.AppendLine("            </Viewport3D.Camera>")
            sb.AppendLine("            <ModelVisual3D>")
            sb.AppendLine("                <ModelVisual3D.Content>")
            sb.AppendLine("                    <Model3DGroup>")
            sb.AppendLine("                        <AmbientLight Color=""" & VBCodeGenerator.NormalizeColor(layer.AmbientColor) & """/>")
            sb.AppendLine("                        <DirectionalLight Color=""" & VBCodeGenerator.NormalizeColor(layer.LightColor) &
                          """ Direction=""" & N(layer.LightX) & "," & N(layer.LightY) & "," & N(layer.LightZ) & """/>")
            For Each mesh In layer.Meshes
                If mesh IsNot Nothing Then AppendGeometryModel(sb, mesh)
            Next
            sb.AppendLine("                    </Model3DGroup>")
            sb.AppendLine("                </ModelVisual3D.Content>")
            sb.AppendLine("            </ModelVisual3D>")
            AppendCustomParticleGroup(sb, layer)
            If layer.BlurRadius > 0 Then
                sb.AppendLine("            <Viewport3D.Effect>")
                sb.AppendLine("                <BlurEffect Radius=""" & N(layer.BlurRadius) & """/>")
                sb.AppendLine("            </Viewport3D.Effect>")
            End If
            sb.AppendLine("        </Viewport3D>")
        End Sub

        Private Sub AppendGeometryModel(sb As StringBuilder, mesh As BackgroundMesh)
            Dim data = BackgroundGeometry.Build(mesh)
            If data.Positions.Count = 0 OrElse data.Indices.Count = 0 Then Return
            Dim positions = String.Join(" ", data.Positions.Select(Function(p) N(p.X) & "," & N(p.Y) & "," & N(p.Z)))
            Dim normals = String.Join(" ", data.Normals.Select(Function(p) N(p.X) & "," & N(p.Y) & "," & N(p.Z)))
            Dim indices = String.Join(" ", data.Indices.Select(Function(i) i.ToString(CultureInfo.InvariantCulture)))
            Dim texture = ""
            If data.TextureCoordinates.Count = data.Positions.Count Then
                texture = " TextureCoordinates=""" & String.Join(" ", data.TextureCoordinates.Select(Function(p) N(p.X) & "," & N(p.Y))) & """"
            End If
            Dim color = VBCodeGenerator.NormalizeColor(mesh.Color)
            Dim imageUri = ResourceUri(mesh.ImageResourceId)
            sb.AppendLine("                        <GeometryModel3D>")
            sb.AppendLine("                            <GeometryModel3D.Geometry>")
            sb.AppendLine("                                <MeshGeometry3D Positions=""" & positions & """ Normals=""" & normals & """" & texture & " TriangleIndices=""" & indices & """/>")
            sb.AppendLine("                            </GeometryModel3D.Geometry>")
            If imageUri IsNot Nothing Then
                sb.AppendLine("                            <GeometryModel3D.Material>")
                sb.AppendLine("                                <DiffuseMaterial>")
                sb.AppendLine("                                    <DiffuseMaterial.Brush>")
                sb.AppendLine("                                        <ImageBrush ImageSource=""" & XmlEscape(imageUri) & """ Stretch=""Fill""/>")
                sb.AppendLine("                                    </DiffuseMaterial.Brush>")
                sb.AppendLine("                                </DiffuseMaterial>")
                sb.AppendLine("                            </GeometryModel3D.Material>")
                sb.AppendLine("                            <GeometryModel3D.BackMaterial>")
                sb.AppendLine("                                <DiffuseMaterial>")
                sb.AppendLine("                                    <DiffuseMaterial.Brush>")
                sb.AppendLine("                                        <ImageBrush ImageSource=""" & XmlEscape(imageUri) & """ Stretch=""Fill""/>")
                sb.AppendLine("                                    </DiffuseMaterial.Brush>")
                sb.AppendLine("                                </DiffuseMaterial>")
                sb.AppendLine("                            </GeometryModel3D.BackMaterial>")
            Else
                sb.AppendLine("                            <GeometryModel3D.Material>")
                sb.AppendLine("                                <DiffuseMaterial Brush=""" & color & """/>")
                sb.AppendLine("                            </GeometryModel3D.Material>")
                sb.AppendLine("                            <GeometryModel3D.BackMaterial>")
                sb.AppendLine("                                <DiffuseMaterial Brush=""" & color & """/>")
                sb.AppendLine("                            </GeometryModel3D.BackMaterial>")
            End If
            sb.AppendLine("                            <GeometryModel3D.Transform>")
            sb.AppendLine("                                <Transform3DGroup>")
            sb.AppendLine("                                    <ScaleTransform3D ScaleX=""" & N(mesh.ScaleX) & """ ScaleY=""" & N(mesh.ScaleY) & """ ScaleZ=""" & N(mesh.ScaleZ) & """/>")
            AppendRotation(sb, "1,0,0", mesh.RotationX)
            AppendRotation(sb, "0,1,0", mesh.RotationY)
            AppendRotation(sb, "0,0,1", mesh.RotationZ)
            sb.AppendLine("                                    <TranslateTransform3D OffsetX=""" & N(mesh.X) & """ OffsetY=""" & N(mesh.Y) & """ OffsetZ=""" & N(mesh.Z) & """/>")
            sb.AppendLine("                                </Transform3DGroup>")
            sb.AppendLine("                            </GeometryModel3D.Transform>")
            sb.AppendLine("                        </GeometryModel3D>")
        End Sub

        Private Sub AppendRotation(sb As StringBuilder, axis As String, angle As Double)
            If Math.Abs(angle) < 0.0001 Then Return
            sb.AppendLine("                                    <RotateTransform3D>")
            sb.AppendLine("                                        <RotateTransform3D.Rotation>")
            sb.AppendLine("                                            <AxisAngleRotation3D Axis=""" & axis & """ Angle=""" & N(angle) & """/>")
            sb.AppendLine("                                        </RotateTransform3D.Rotation>")
            sb.AppendLine("                                    </RotateTransform3D>")
        End Sub

        Public Function BuildCode() As String
            Dim w As New CodeWriter()
            w.Line("Imports System")
            w.Line("Imports System.Collections.Generic")
            w.Line("Imports System.Windows")
            w.Line("Imports System.Windows.Controls")
            w.Line("Imports System.Windows.Media")
            w.Line("Imports System.Windows.Media.Media3D")
            w.Line("Imports System.Windows.Shapes")
            w.Line("Imports AgSTG")
            w.Line()
            w.Open("Public Class " & _className & "bg")
            w.Line("Inherits UserControl")
            w.Line("Private _ticks As Long = 0")
            Dim loopEvents = BackgroundLoopClips()
            If loopEvents.Count > 0 Then
                w.Line("Private _bgLoopActive As Boolean = False")
                w.Line("Private _bgLoopRemaining As Integer = 0")
                w.Line("Private _bgLoopStart As Long = 0")
                w.Line("Private _bgLoopEnd As Long = 0")
                w.Line("Private _bgLoopResume As Long = 0")
            End If
            For Each layer In _stage.BackgroundLayers
                If layer IsNot Nothing AndAlso ((layer.Kind = BackgroundLayerKind.Layer2D AndAlso layer.Shape = BackgroundShape2D.ParticleField) OrElse HasCustomParticles(layer)) Then
                    Dim particleType = If(layer.ParticleMode = BackgroundParticleMode.ThreeD, "Particle3D", "Particle")
                    w.Line("Private ReadOnly _particles_" & SafeId(layer.Id) & " As New List(Of " & particleType & ")")
                    w.Line("Private _particleLife_" & SafeId(layer.Id) & " As Double = " & Math.Max(1, layer.ParticleLifetime).ToString(CultureInfo.InvariantCulture))
                End If
            Next
            w.Line()
            w.Open("Public Sub New()")
            w.Line("InitializeComponent()")
            For Each layer In _stage.BackgroundLayers
                If layer IsNot Nothing AndAlso ((layer.Kind = BackgroundLayerKind.Layer2D AndAlso layer.Shape = BackgroundShape2D.ParticleField) OrElse HasCustomParticles(layer)) Then
                    w.Line("InitParticles_" & SafeId(layer.Id) & "()")
                End If
            Next
            w.Close("End Sub")
            w.Line()

            For Each layer In _stage.BackgroundLayers
                If layer IsNot Nothing AndAlso ((layer.Kind = BackgroundLayerKind.Layer2D AndAlso layer.Shape = BackgroundShape2D.ParticleField) OrElse HasCustomParticles(layer)) Then
                    EmitParticleMethods(w, layer)
                End If
            Next

            w.Open("Public Sub Render()")
            w.Line("AnimateBackground()")
            For Each layer In _stage.BackgroundLayers
                If layer IsNot Nothing AndAlso ((layer.Kind = BackgroundLayerKind.Layer2D AndAlso layer.Shape = BackgroundShape2D.ParticleField) OrElse HasCustomParticles(layer)) Then
                    w.Line("UpdateParticles_" & SafeId(layer.Id) & "()")
                End If
            Next
            w.Line(If(loopEvents.Count > 0, "AdvanceBackgroundTick()", "_ticks += 1"))
            w.Close("End Sub")
            w.Line()
            If loopEvents.Count > 0 Then
                EmitBackgroundLoopClock(w, loopEvents)
            End If
            EmitAnimation(w)
            w.Close("End Class")
            Return w.ToString()
        End Function

        Private Sub EmitParticleMethods(w As CodeWriter, layer As BackgroundLayer)
            EmitCustomParticleMethods(w, layer)
        End Sub

        Private Sub EmitCustomParticleMethods(w As CodeWriter, layer As BackgroundLayer)
            Dim id = SafeId(layer.Id)
            Dim is3D = layer.ParticleMode = BackgroundParticleMode.ThreeD
            Dim typeName = If(is3D, "Particle3D", "Particle")
            Dim canvasName = CustomParticleCanvasName(layer)
            Dim groupName = CustomParticleGroupName(layer)

            w.Open("Private Sub InitParticles_" & id & "()")
            w.Line("Dim rnd As New Random(" & Math.Abs(layer.Id.GetHashCode()) Mod 100000 & ")")
            If ResourceUri(layer.ParticleImageResourceId) IsNot Nothing Then
                w.Line("Dim brush As Brush = TryCast(FindResource(" & VBCodeGenerator.Quote(ParticleBrushKey(layer)) & "), Brush)")
                w.Line("If brush Is Nothing Then brush = New SolidColorBrush(DirectCast(ColorConverter.ConvertFromString(" & VBCodeGenerator.Quote(layer.ParticleColor) & "), Color))")
            Else
                w.Line("Dim brush As New SolidColorBrush(DirectCast(ColorConverter.ConvertFromString(" & VBCodeGenerator.Quote(layer.ParticleColor) & "), Color))")
            End If
            w.Open("For i As Integer = 1 To " & Math.Max(0, layer.ParticleCount).ToString(CultureInfo.InvariantCulture))
            w.Line("Dim x = rnd.NextDouble() * " & N(layer.Width))
            w.Line("Dim y = rnd.NextDouble() * " & N(layer.Height))
            w.Line("Dim z = (rnd.NextDouble() * 2 - 1) * " & N(Math.Max(1, layer.ParticleSpread) * 8))
            w.Line("Dim size = " & N(layer.ParticleMinSize) & " + rnd.NextDouble() * " & N(Math.Max(0, layer.ParticleMaxSize - layer.ParticleMinSize)))
            If is3D Then
                w.Line("Dim p As New Particle3D(x, y, z, size, size, New Vector3D(" &
                       N(layer.ParticleSpeedX) & ", " & N(layer.ParticleSpeedY) & ", " & N(layer.ParticleZSpeed) & "), " & groupName & ", brush)")
            Else
                w.Line("Dim p As New Particle(x, y, size, size, " & canvasName & ")")
                w.Line("p.Background = brush")
                w.Line("SetParticleVelocity_" & id & "(p, " & N(layer.ParticleSpeedX) & ", " & N(layer.ParticleSpeedY) & ")")
            End If
            w.Line("p.Act = Sub() ApplyParticleFrame_" & id & "(p)")
            w.Line("ApplyParticleSpawn_" & id & "(p)")
            w.Line("_particles_" & id & ".Add(p)")
            w.Close("Next")
            w.Close("End Sub")
            w.Line()

            w.Open("Private Sub UpdateParticles_" & id & "()")
            w.Open("For Each p In _particles_" & id)
            w.Line("p.Render()")
            w.Open("If p.Ticks >= _particleLife_" & id & " Then")
            w.Line("p.Ticks = 0")
            w.Line("p.X = Rnd() * " & N(layer.Width))
            w.Line("p.Y = Rnd() * " & N(layer.Height))
            If is3D Then w.Line("p.Z = (Rnd() * 2 - 1) * " & N(Math.Max(1, layer.ParticleSpread) * 8))
            w.Line("ApplyParticleSpawn_" & id & "(p)")
            w.Close("End If")
            w.Open("If p.X < -32 Then")
            w.Line("p.X = " & N(layer.Width + 32))
            w.Close("End If")
            w.Open("If p.X > " & N(layer.Width + 32) & " Then")
            w.Line("p.X = -32")
            w.Close("End If")
            w.Open("If p.Y > " & N(layer.Height + 32) & " Then")
            w.Line("p.Y = -32")
            w.Close("End If")
            w.Open("If p.Y < -32 Then")
            w.Line("p.Y = " & N(layer.Height + 32))
            w.Close("End If")
            w.Close("Next")
            w.Close("End Sub")
            w.Line()

            If Not is3D Then
                w.Open("Private Sub SetParticleVelocity_" & id & "(p As Particle, vx As Double, vy As Double)")
                w.Line("p.Speed = Math.Sqrt(vx * vx + vy * vy)")
                w.Line("p.Direction = Math.Atan2(vx, -vy) * 180 / Math.PI")
                w.Close("End Sub")
                w.Line()
            End If

            w.Open("Private Sub ApplyParticleSpawn_" & id & "(p As " & typeName & ")")
            EmitParticleScripts(w, layer, HatKind.ParticleSpawn, is3D, id)
            w.Close("End Sub")
            w.Line()
            w.Open("Private Sub ApplyParticleFrame_" & id & "(p As " & typeName & ")")
            EmitParticleFrameScripts(w, layer, is3D, id)
            w.Close("End Sub")
            w.Line()
        End Sub

        Private Sub EmitParticleFrameScripts(w As CodeWriter, layer As BackgroundLayer, is3D As Boolean, id As String)
            If layer.ParticleBehavior Is Nothing Then Return
            For Each script In layer.ParticleBehavior.Scripts
                If script Is Nothing OrElse script.Definition Is Nothing Then Continue For
                Select Case script.Definition.HatKind
                    Case HatKind.ParticleEveryFrame
                        For Each child In script.Children
                            EmitParticleBlock(w, child, is3D, id)
                        Next
                    Case HatKind.ParticleWhenPosition
                        Dim axis = script.GetField("axis").Trim().ToUpperInvariant()
                        If axis <> "X" AndAlso axis <> "Y" AndAlso (Not is3D OrElse axis <> "Z") Then axis = "X"
                        Dim op = script.GetField("op").Trim()
                        If op <> ">" AndAlso op <> "=" AndAlso op <> "<" Then op = ">"
                        Dim value = script.GetDouble("value", 0)
                        w.Open("If p." & axis & " " & op & " " & N(value) & " Then")
                        For Each child In script.Children
                            EmitParticleBlock(w, child, is3D, id)
                        Next
                        w.Close("End If")
                End Select
            Next
        End Sub

        Private Sub EmitParticleScripts(w As CodeWriter, layer As BackgroundLayer, hat As HatKind, is3D As Boolean, id As String)
            If layer.ParticleBehavior Is Nothing Then Return
            For Each script In layer.ParticleBehavior.Scripts
                If script Is Nothing OrElse script.Definition Is Nothing OrElse script.Definition.HatKind <> hat Then Continue For
                For Each child In script.Children
                    EmitParticleBlock(w, child, is3D, id)
                Next
            Next
        End Sub

        Private Sub EmitParticleBlock(w As CodeWriter, block As BlockNode, is3D As Boolean, id As String)
            If block Is Nothing Then Return
            Select Case block.TypeId
                Case "particle_set_velocity"
                    If is3D Then
                        w.Line("p.Speed = New Vector3D(" & N(block.GetDouble("x", 0)) & ", " & N(block.GetDouble("y", 0)) & ", " & N(block.GetDouble("z", 0)) & ")")
                    Else
                        w.Line("SetParticleVelocity_" & id & "(p, " & N(block.GetDouble("x", 0)) & ", " & N(block.GetDouble("y", 0)) & ")")
                    End If
                Case "particle_add_velocity"
                    If is3D Then
                        w.Line("p.Speed = New Vector3D(p.Speed.X + " & N(block.GetDouble("x", 0)) & ", p.Speed.Y + " & N(block.GetDouble("y", 0)) & ", p.Speed.Z + " & N(block.GetDouble("z", 0)) & ")")
                    Else
                        w.Line("SetParticleVelocity_" & id & "(p, p.Speed * Math.Sin(p.Direction * Math.PI / 180) + " & N(block.GetDouble("x", 0)) & ", -p.Speed * Math.Cos(p.Direction * Math.PI / 180) + " & N(block.GetDouble("y", 0)) & ")")
                    End If
                Case "particle_set_position"
                    w.Line("p.X = " & N(block.GetDouble("x", 0)) & " : p.Y = " & N(block.GetDouble("y", 0)))
                    If is3D Then w.Line("p.Z = " & N(block.GetDouble("z", 0)))
                Case "particle_set_size"
                    w.Line("p.Width = " & N(block.GetDouble("size", 3)) & " : p.Height = " & N(block.GetDouble("size", 3)))
                Case "particle_set_life"
                    w.Line("_particleLife_" & id & " = " & Math.Max(1, CInt(block.GetDouble("frames", 180))).ToString(CultureInfo.InvariantCulture))
                Case "particle_set_opacity"
                    If Not is3D Then w.Line("p.Opacity = " & N(Math.Max(0, Math.Min(1, block.GetDouble("opacity", 1)))))
                Case "particle_set_color"
                    If Not is3D Then w.Line("p.Background = New SolidColorBrush(DirectCast(ColorConverter.ConvertFromString(" & VBCodeGenerator.Quote(block.GetField("color")) & "), Color))")
            End Select
        End Sub

        Private Function BackgroundLoopClips() As List(Of TimelineClip)
            Dim result As New List(Of TimelineClip)
            For Each track In _stage.Tracks
                If track Is Nothing OrElse track.Kind <> TrackKind.Background Then Continue For
                For Each clip In track.Clips
                    If clip Is Nothing OrElse Not clip.IsLoopEvent Then Continue For
                    If clip.StartTick <= 0 OrElse clip.LoopStartTick < 0 OrElse clip.LoopEndTick < clip.LoopStartTick Then Continue For
                    If clip.LoopEndTick >= clip.StartTick Then Continue For
                    result.Add(clip)
                Next
            Next
            Return result.OrderBy(Function(c) c.StartTick).ToList()
        End Function

        Private Sub EmitBackgroundLoopClock(w As CodeWriter, events As List(Of TimelineClip))
            w.Open("Private Sub AdvanceBackgroundTick()")
            w.Open("If _bgLoopActive Then")
            w.Line("_bgLoopRemaining -= 1")
            w.Open("If _bgLoopRemaining <= 0 Then")
            w.Line("_bgLoopActive = False")
            w.Line("_ticks = _bgLoopResume")
            w.Close("Else")
            w.Line("_ticks += 1")
            w.Line("If _ticks > _bgLoopEnd Then _ticks = _bgLoopStart")
            w.Close("End If")
            w.Line("Return")
            w.Close("End If")
            w.Line("_ticks += 1")
            For Each clip In events
                w.Open("If _ticks = " & clip.StartTick.ToString(CultureInfo.InvariantCulture) & " Then")
                w.Line("_bgLoopActive = True")
                w.Line("_bgLoopRemaining = " & Math.Max(1, clip.LoopLength).ToString(CultureInfo.InvariantCulture))
                w.Line("_bgLoopStart = " & clip.LoopStartTick.ToString(CultureInfo.InvariantCulture))
                w.Line("_bgLoopEnd = " & clip.LoopEndTick.ToString(CultureInfo.InvariantCulture))
                w.Line("_bgLoopResume = " & (clip.StartTick + 1).ToString(CultureInfo.InvariantCulture))
                w.Line("_ticks = _bgLoopStart")
                w.Line("Return")
                w.Close("End If")
            Next
            w.Close("End Sub")
            w.Line()
        End Sub

        Private Sub EmitAnimation(w As CodeWriter)
            w.Open("Private Sub AnimateBackground()")
            Dim trackIndex = 0
            For Each track In _stage.Tracks
                If track Is Nothing OrElse track.Kind <> TrackKind.Background OrElse track.IsMuted Then Continue For
                Dim layer = _stage.FindBackgroundLayer(track.BackgroundLayerId)
                If layer Is Nothing Then Continue For
                Dim keys = track.Clips.OrderBy(Function(c) c.StartTick).ToList()
                If keys.Count = 0 Then
                    trackIndex += 1
                    Continue For
                End If
                Dim prefix = "k" & trackIndex.ToString(CultureInfo.InvariantCulture)
                If layer.Kind = BackgroundLayerKind.Layer3D Then
                    EmitCameraKeyframes(w, layer, keys, prefix)
                ElseIf layer.Shape = BackgroundShape2D.ParticleField Then
                    EmitParticleKeyframes(w, layer, keys, prefix)
                Else
                    Emit2DKeyframes(w, layer, keys, prefix)
                End If
                trackIndex += 1
            Next
            w.Close("End Sub")
        End Sub

        Private Sub EmitCameraKeyframes(w As CodeWriter, layer As BackgroundLayer, keys As List(Of TimelineClip), prefix As String)
            Dim vars = {"camX", "camY", "camZ", "lookX", "lookY", "lookZ", "fov"}
            Dim values = Function(key As TimelineClip) New Double() {key.CameraX, key.CameraY, key.CameraZ, key.CameraLookX, key.CameraLookY, key.CameraLookZ, key.CameraFieldOfView}
            EmitVariables(w, prefix, vars, values(keys(0)))
            EmitKeyframeAssignments(w, prefix, vars, keys, values)
            EmitInterpolations(w, prefix, vars, keys, values)
            w.Line(CameraName(layer) & ".Position = New Point3D(" & prefix & "_camX, " & prefix & "_camY, " & prefix & "_camZ)")
            w.Line(CameraName(layer) & ".LookDirection = New Vector3D(" & prefix & "_lookX, " & prefix & "_lookY, " & prefix & "_lookZ)")
            w.Line(CameraName(layer) & ".FieldOfView = " & prefix & "_fov")
        End Sub

        Private Sub Emit2DKeyframes(w As CodeWriter, layer As BackgroundLayer, keys As List(Of TimelineClip), prefix As String)
            Dim vars = {"x", "y", "scale", "rotation", "opacity"}
            Dim values = Function(key As TimelineClip) New Double() {key.TargetX, key.TargetY, key.TargetScale, key.TargetRotation, key.TargetOpacity}
            EmitVariables(w, prefix, vars, values(keys(0)))
            EmitKeyframeAssignments(w, prefix, vars, keys, values)
            EmitInterpolations(w, prefix, vars, keys, values)
            w.Line(LayerName(layer) & ".Opacity = " & prefix & "_opacity")
            w.Line("Dim tg As New TransformGroup()")
            w.Line("tg.Children.Add(New ScaleTransform(" & prefix & "_scale, " & prefix & "_scale))")
            w.Line("tg.Children.Add(New RotateTransform(" & prefix & "_rotation))")
            w.Line(LayerName(layer) & ".RenderTransformOrigin = New Point(0.5, 0.5)")
            w.Line(LayerName(layer) & ".RenderTransform = tg")
            w.Line("Canvas.SetLeft(" & LayerName(layer) & ", " & prefix & "_x)")
            w.Line("Canvas.SetTop(" & LayerName(layer) & ", " & prefix & "_y)")
        End Sub

        Private Sub EmitParticleKeyframes(w As CodeWriter, layer As BackgroundLayer, keys As List(Of TimelineClip), prefix As String)
            Dim vars = {"speedX", "speedY", "opacity"}
            Dim values = Function(key As TimelineClip) New Double() {key.ParticleSpeedX, key.ParticleSpeedY, key.TargetOpacity}
            EmitVariables(w, prefix, vars, values(keys(0)))
            EmitKeyframeAssignments(w, prefix, vars, keys, values)
            EmitInterpolations(w, prefix, vars, keys, values)
            w.Line("_particleSpeedX_" & SafeId(layer.Id) & " = " & prefix & "_speedX")
            w.Line("_particleSpeedY_" & SafeId(layer.Id) & " = " & prefix & "_speedY")
            w.Line(ParticleCanvasName(layer) & ".Opacity = " & prefix & "_opacity")
        End Sub

        Private Sub EmitVariables(w As CodeWriter, prefix As String, vars As String(), values As Double())
            For i = 0 To vars.Length - 1
                w.Line("Dim " & prefix & "_" & vars(i) & " As Double = " & N(values(i)))
            Next
        End Sub

        Private Sub EmitKeyframeAssignments(w As CodeWriter, prefix As String, vars As String(),
                                             keys As List(Of TimelineClip),
                                             values As Func(Of TimelineClip, Double()))
            For i = 1 To keys.Count - 1
                w.Open("If _ticks >= " & Math.Max(0, keys(i).StartTick) & " Then")
                Dim frame = values(keys(i))
                For v = 0 To vars.Length - 1
                    w.Line(prefix & "_" & vars(v) & " = " & N(frame(v)))
                Next
                w.Close("End If")
            Next
        End Sub

        Private Sub EmitInterpolations(w As CodeWriter, prefix As String, vars As String(),
                                       keys As List(Of TimelineClip),
                                       values As Func(Of TimelineClip, Double()))
            For i = 0 To keys.Count - 2
                Dim startTick = Math.Max(0, keys(i).StartTick)
                Dim endTick = Math.Max(0, keys(i + 1).StartTick)
                If endTick <= startTick Then Continue For
                w.Open("If _ticks >= " & startTick & " AndAlso _ticks < " & endTick & " Then")
                w.Line("Dim " & prefix & "_p" & i.ToString(CultureInfo.InvariantCulture) & " = CDbl(_ticks - " & startTick & ") / " & (endTick - startTick).ToString(CultureInfo.InvariantCulture))
                Dim a = values(keys(i))
                Dim b = values(keys(i + 1))
                For v = 0 To vars.Length - 1
                    w.Line(prefix & "_" & vars(v) & " = " & N(a(v)) & " + (" & N(b(v)) & " - " & N(a(v)) & ") * " & prefix & "_p" & i.ToString(CultureInfo.InvariantCulture))
                Next
                w.Close("End If")
            Next
        End Sub

        Private Function AnyParticleLayer() As Boolean
            Return _stage.BackgroundLayers.Any(Function(l) l IsNot Nothing AndAlso ((l.Kind = BackgroundLayerKind.Layer2D AndAlso l.Shape = BackgroundShape2D.ParticleField) OrElse HasCustomParticles(l)))
        End Function

        Private Function HasCustomParticles(layer As BackgroundLayer) As Boolean
            Return layer IsNot Nothing AndAlso (layer.Shape = BackgroundShape2D.ParticleField OrElse layer.ParticleMode <> BackgroundParticleMode.None)
        End Function

        Private Function ResourceUri(resourceId As String) As String
            If String.IsNullOrWhiteSpace(resourceId) OrElse _resourceUris Is Nothing Then Return Nothing
            Dim uri As String = Nothing
            If _resourceUris.TryGetValue(resourceId, uri) AndAlso Not String.IsNullOrWhiteSpace(uri) Then Return uri
            Return Nothing
        End Function

        Private Function SafeId(id As String) As String
            Return VBCodeGenerator.SanitizeIdentifier(id, "bg")
        End Function

        Private Function LayerName(layer As BackgroundLayer) As String
            Return "BgLayer_" & SafeId(layer.Id)
        End Function

        Private Function CameraName(layer As BackgroundLayer) As String
            Return "BgCamera_" & SafeId(layer.Id)
        End Function

        Private Function ViewportName(layer As BackgroundLayer) As String
            Return "BgView_" & SafeId(layer.Id)
        End Function

        Private Function ParticleCanvasName(layer As BackgroundLayer) As String
            Return "BgParticles_" & SafeId(layer.Id)
        End Function

        Private Function CustomParticleCanvasName(layer As BackgroundLayer) As String
            Return "BgParticlesCustom_" & SafeId(layer.Id)
        End Function

        Private Function CustomParticleGroupName(layer As BackgroundLayer) As String
            Return "BgParticles3D_" & SafeId(layer.Id)
        End Function

        Private Function ParticleBrushKey(layer As BackgroundLayer) As String
            Return "BgParticleBrush_" & SafeId(layer.Id)
        End Function

        Private Shared Function N(value As Double) As String
            If Double.IsNaN(value) OrElse Double.IsInfinity(value) Then Return "0"
            Return value.ToString("0.######", CultureInfo.InvariantCulture)
        End Function

        Private Shared Function XmlEscape(text As String) As String
            Return If(text, "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("""", "&quot;")
        End Function

    End Class

End Namespace
