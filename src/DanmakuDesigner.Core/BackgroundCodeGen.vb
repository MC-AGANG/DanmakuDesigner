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
                sb.AppendLine("        <Canvas x:Name=""" & ParticleCanvasName(layer) & """ Canvas.Left=""0"" Canvas.Top=""0"" Width=""384"" Height=""448"" Opacity=""" & N(layer.Opacity) & """ Visibility=""" & visibility & """ IsHitTestVisible=""False""/>")
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
                sb.AppendLine("        </" & shapeTag & ">")
                Return
            End If
            sb.AppendLine("        <" & shapeTag & " x:Name=""" & LayerName(layer) & """ Canvas.Left=""" & N(layer.X) & """ Canvas.Top=""" & N(layer.Y) &
                          """ Width=""" & N(layer.Width) & """ Height=""" & N(layer.Height) & """ Fill=""" & VBCodeGenerator.NormalizeColor(layer.Fill) &
                          """ Stroke=""" & VBCodeGenerator.NormalizeColor(layer.Stroke) & """ StrokeThickness=""" & N(layer.StrokeThickness) &
                          """" & radius & " Opacity=""" & N(layer.Opacity) & """ Visibility=""" & visibility & """ IsHitTestVisible=""False""/>")
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
            w.Line()
            w.Open("Public Class " & _className & "bg")
            w.Line("Inherits UserControl")
            w.Line("Private _ticks As Long = 0")
            For Each layer In _stage.BackgroundLayers
                If layer IsNot Nothing AndAlso layer.Kind = BackgroundLayerKind.Layer2D AndAlso layer.Shape = BackgroundShape2D.ParticleField Then
                    w.Line("Private ReadOnly _particles_" & SafeId(layer.Id) & " As New List(Of BgParticle)")
                    w.Line("Private _particleSpeedX_" & SafeId(layer.Id) & " As Double = " & N(layer.ParticleSpeedX))
                    w.Line("Private _particleSpeedY_" & SafeId(layer.Id) & " As Double = " & N(layer.ParticleSpeedY))
                End If
            Next
            w.Line()
            w.Open("Public Sub New()")
            w.Line("InitializeComponent()")
            For Each layer In _stage.BackgroundLayers
                If layer IsNot Nothing AndAlso layer.Kind = BackgroundLayerKind.Layer2D AndAlso layer.Shape = BackgroundShape2D.ParticleField Then
                    w.Line("InitParticles_" & SafeId(layer.Id) & "()")
                End If
            Next
            w.Close("End Sub")
            w.Line()

            For Each layer In _stage.BackgroundLayers
                If layer IsNot Nothing AndAlso layer.Kind = BackgroundLayerKind.Layer2D AndAlso layer.Shape = BackgroundShape2D.ParticleField Then
                    EmitParticleMethods(w, layer)
                End If
            Next

            w.Open("Public Sub Render()")
            w.Line("AnimateBackground()")
            For Each layer In _stage.BackgroundLayers
                If layer IsNot Nothing AndAlso layer.Kind = BackgroundLayerKind.Layer2D AndAlso layer.Shape = BackgroundShape2D.ParticleField Then
                    w.Line("UpdateParticles_" & SafeId(layer.Id) & "()")
                End If
            Next
            w.Line("_ticks += 1")
            w.Close("End Sub")
            w.Line()
            EmitAnimation(w)
            If AnyParticleLayer() Then
                w.Line()
                w.Open("Private Class BgParticle")
                w.Line("Public X As Double")
                w.Line("Public Y As Double")
                w.Line("Public SpeedX As Double")
                w.Line("Public SpeedY As Double")
                w.Line("Public Shape As Ellipse")
                w.Close("End Class")
            End If
            w.Close("End Class")
            Return w.ToString()
        End Function

        Private Sub EmitParticleMethods(w As CodeWriter, layer As BackgroundLayer)
            Dim id = SafeId(layer.Id)
            Dim canvasName = ParticleCanvasName(layer)
            w.Open("Private Sub InitParticles_" & id & "()")
            w.Line("Dim rnd As New Random(" & Math.Abs(layer.Id.GetHashCode()) Mod 100000 & ")")
            w.Line("Dim brush As New SolidColorBrush(DirectCast(ColorConverter.ConvertFromString(" & VBCodeGenerator.Quote(layer.ParticleColor) & "), Color))")
            w.Open("For i As Integer = 1 To " & Math.Max(0, layer.ParticleCount).ToString(CultureInfo.InvariantCulture))
            w.Line("Dim p As New BgParticle()")
            w.Line("p.X = rnd.NextDouble() * " & N(layer.Width))
            w.Line("p.Y = rnd.NextDouble() * " & N(layer.Height))
            w.Line("p.SpeedX = (rnd.NextDouble() * 2.0 - 1.0) * " & N(layer.ParticleSpread * 8))
            w.Line("p.SpeedY = (rnd.NextDouble() * 2.0 - 1.0) * " & N(layer.ParticleSpread * 8))
            w.Line("Dim size = " & N(layer.ParticleMinSize) & " + rnd.NextDouble() * " & N(Math.Max(0, layer.ParticleMaxSize - layer.ParticleMinSize)))
            w.Line("p.Shape = New Ellipse()")
            w.Line("p.Shape.Width = size : p.Shape.Height = size")
            w.Line("p.Shape.Fill = brush")
            w.Line("Canvas.SetLeft(p.Shape, p.X) : Canvas.SetTop(p.Shape, p.Y)")
            w.Line(canvasName & ".Children.Add(p.Shape)")
            w.Line("_particles_" & id & ".Add(p)")
            w.Close("Next")
            w.Close("End Sub")
            w.Line()
            w.Open("Private Sub UpdateParticles_" & id & "()")
            w.Open("For Each p In _particles_" & id)
            w.Line("p.X += p.SpeedX + _particleSpeedX_" & id)
            w.Line("p.Y += p.SpeedY + _particleSpeedY_" & id)
            w.Open("If p.Y > " & N(layer.Height + 8) & " Then")
            w.Line("p.Y = -8 : p.X = Rnd() * " & N(layer.Width))
            w.Close("End If")
            w.Open("If p.X < -8 Then")
            w.Line("p.X = " & N(layer.Width + 8))
            w.Close("End If")
            w.Open("If p.X > " & N(layer.Width + 8) & " Then")
            w.Line("p.X = -8")
            w.Close("End If")
            w.Line("Canvas.SetLeft(p.Shape, p.X) : Canvas.SetTop(p.Shape, p.Y)")
            w.Close("Next")
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
            Return _stage.BackgroundLayers.Any(Function(l) l IsNot Nothing AndAlso l.Kind = BackgroundLayerKind.Layer2D AndAlso l.Shape = BackgroundShape2D.ParticleField)
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

        Private Shared Function N(value As Double) As String
            If Double.IsNaN(value) OrElse Double.IsInfinity(value) Then Return "0"
            Return value.ToString("0.######", CultureInfo.InvariantCulture)
        End Function

        Private Shared Function XmlEscape(text As String) As String
            Return If(text, "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("""", "&quot;")
        End Function

    End Class

End Namespace
