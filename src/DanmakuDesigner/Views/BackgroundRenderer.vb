Imports System.IO
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Media
Imports System.Windows.Media.Imaging
Imports System.Windows.Media.Media3D
Imports System.Windows.Shapes
Imports DanmakuDesigner.Core

''' <summary>把背景图层模型转换为编辑器预览用的 WPF 元素。</summary>
Public Module BackgroundRenderer

    Public Const PlayfieldWidth As Double = 384
    Public Const PlayfieldHeight As Double = 448

    Public Function BuildPreview(stage As StageDocument, Optional tick As Long = 0,
                                 Optional width As Double = PlayfieldWidth,
                                 Optional height As Double = PlayfieldHeight) As Canvas
        Dim canvas As New Canvas With {
            .Width = width,
            .Height = height,
            .ClipToBounds = True,
            .Background = Brushes.Black
        }
        If stage Is Nothing Then Return canvas

        For Each layer In stage.BackgroundLayers
            If layer Is Nothing OrElse Not layer.Visible Then Continue For
            Dim anim = ComputeAnimation(stage, layer, tick)
            If layer.Kind = BackgroundLayerKind.Layer3D Then
                Dim view = BuildViewport(layer, Nothing, width, height, anim)
                view.Opacity = anim.Opacity
                canvas.Children.Add(view)
            Else
                canvas.Children.Add(Build2D(layer, width, height, anim))
            End If
        Next
        Return canvas
    End Function

    Public Class LayerAnimation
        Public Opacity As Double = 1
        Public MoveX As Double
        Public MoveY As Double
        Public Scale As Double = 1
        Public Rotation As Double
        Public CameraX As Double = Double.NaN
        Public CameraY As Double
        Public CameraZ As Double
        Public LookX As Double
        Public LookY As Double
        Public LookZ As Double
        Public FieldOfView As Double
    End Class

    Private Function ComputeAnimation(stage As StageDocument, layer As BackgroundLayer, tick As Long) As LayerAnimation
        Dim anim As New LayerAnimation With {
            .Opacity = layer.Opacity,
            .Scale = 1,
            .CameraX = layer.CameraX,
            .CameraY = layer.CameraY,
            .CameraZ = layer.CameraZ,
            .LookX = layer.LookX,
            .LookY = layer.LookY,
            .LookZ = layer.LookZ,
            .FieldOfView = layer.FieldOfView
        }
        For Each track In stage.Tracks
            If track Is Nothing OrElse track.Kind <> TrackKind.Background OrElse track.IsMuted Then Continue For
            If Not String.Equals(track.BackgroundLayerId, layer.Id, StringComparison.Ordinal) Then Continue For
            Dim keys = track.Clips.OrderBy(Function(c) c.StartTick).ToList()
            If keys.Count = 0 Then Continue For
            Dim first = keys(0)
            Dim current = first
            Dim nextKey As TimelineClip = Nothing
            For i = 0 To keys.Count - 1
                If tick >= keys(i).StartTick Then
                    current = keys(i)
                    If i + 1 < keys.Count Then nextKey = keys(i + 1) Else nextKey = Nothing
                Else
                    If nextKey Is Nothing Then nextKey = keys(i)
                    Exit For
                End If
            Next
            Dim p = 1.0
            If nextKey IsNot Nothing AndAlso nextKey.StartTick > current.StartTick Then
                p = Math.Max(0, Math.Min(1, CDbl(tick - current.StartTick) / (nextKey.StartTick - current.StartTick)))
            End If
            If layer.Kind = BackgroundLayerKind.Layer3D Then
                anim.CameraX = If(nextKey Is Nothing, current.CameraX, current.CameraX + (nextKey.CameraX - current.CameraX) * p)
                anim.CameraY = If(nextKey Is Nothing, current.CameraY, current.CameraY + (nextKey.CameraY - current.CameraY) * p)
                anim.CameraZ = If(nextKey Is Nothing, current.CameraZ, current.CameraZ + (nextKey.CameraZ - current.CameraZ) * p)
                anim.LookX = If(nextKey Is Nothing, current.CameraLookX, current.CameraLookX + (nextKey.CameraLookX - current.CameraLookX) * p)
                anim.LookY = If(nextKey Is Nothing, current.CameraLookY, current.CameraLookY + (nextKey.CameraLookY - current.CameraLookY) * p)
                anim.LookZ = If(nextKey Is Nothing, current.CameraLookZ, current.CameraLookZ + (nextKey.CameraLookZ - current.CameraLookZ) * p)
                anim.FieldOfView = If(nextKey Is Nothing, current.CameraFieldOfView, current.CameraFieldOfView + (nextKey.CameraFieldOfView - current.CameraFieldOfView) * p)
            Else
                If tick < first.StartTick Then
                    current = first
                End If
                If layer.Shape = BackgroundShape2D.ParticleField Then
                    anim.Opacity = current.TargetOpacity
                Else
                    anim.Opacity = If(nextKey Is Nothing, current.TargetOpacity, current.TargetOpacity + (nextKey.TargetOpacity - current.TargetOpacity) * p)
                    anim.MoveX = If(nextKey Is Nothing, current.TargetX, current.TargetX + (nextKey.TargetX - current.TargetX) * p) - layer.X
                    anim.MoveY = If(nextKey Is Nothing, current.TargetY, current.TargetY + (nextKey.TargetY - current.TargetY) * p) - layer.Y
                    anim.Scale = If(nextKey Is Nothing, current.TargetScale, current.TargetScale + (nextKey.TargetScale - current.TargetScale) * p)
                    anim.Rotation = If(nextKey Is Nothing, current.TargetRotation, current.TargetRotation + (nextKey.TargetRotation - current.TargetRotation) * p)
                End If
            End If
        Next
        Return anim
    End Function

    Public Function BuildViewport(layer As BackgroundLayer, selectedMeshId As String,
                                  Optional width As Double = PlayfieldWidth,
                                  Optional height As Double = PlayfieldHeight,
                                  Optional anim As LayerAnimation = Nothing) As Viewport3D
        Dim view As New Viewport3D With {
            .Width = width,
            .Height = height,
            .ClipToBounds = True,
            .IsHitTestVisible = False
        }
        If layer Is Nothing Then Return view
        Dim camera As New PerspectiveCamera With {
            .Position = New Point3D(If(anim Is Nothing, layer.CameraX, anim.CameraX),
                                    If(anim Is Nothing, layer.CameraY, anim.CameraY),
                                    If(anim Is Nothing, layer.CameraZ, anim.CameraZ)),
            .LookDirection = New Vector3D(If(anim Is Nothing, layer.LookX, anim.LookX),
                                          If(anim Is Nothing, layer.LookY, anim.LookY),
                                          If(anim Is Nothing, layer.LookZ, anim.LookZ)),
            .UpDirection = New Vector3D(0, 1, 0),
            .FieldOfView = Math.Max(1, Math.Min(170, If(anim Is Nothing, layer.FieldOfView, anim.FieldOfView)))
        }
        view.Camera = camera

        Dim group As New Model3DGroup()
        group.Children.Add(New AmbientLight(Ui.Brush(layer.AmbientColor).Color))
        group.Children.Add(New DirectionalLight(Ui.Brush(layer.LightColor).Color,
                                                New Vector3D(layer.LightX, layer.LightY, layer.LightZ)))
        For Each mesh In layer.Meshes
            If mesh Is Nothing Then Continue For
            group.Children.Add(BuildModel(mesh, String.Equals(mesh.Id, selectedMeshId, StringComparison.Ordinal)))
        Next
        view.Children.Add(New ModelVisual3D With {.Content = group})
        Return view
    End Function

    Private Function Build2D(layer As BackgroundLayer, width As Double, height As Double, anim As LayerAnimation) As FrameworkElement
        If layer.Shape = BackgroundShape2D.ParticleField Then
            Dim canvas As New Canvas With {
                .Width = width,
                .Height = height,
                .Opacity = anim.Opacity,
                .IsHitTestVisible = False
            }
            Dim count = Math.Min(400, Math.Max(0, layer.ParticleCount))
            Dim rnd As New Random(Math.Abs(layer.Id.GetHashCode()))
            Dim brush = Ui.Brush(layer.ParticleColor)
            For i = 1 To count
                Dim size = layer.ParticleMinSize + rnd.NextDouble() * Math.Max(0, layer.ParticleMaxSize - layer.ParticleMinSize)
                Dim dot As New Ellipse With {.Width = size, .Height = size, .Fill = brush}
                Canvas.SetLeft(dot, rnd.NextDouble() * width)
                Canvas.SetTop(dot, rnd.NextDouble() * height)
                canvas.Children.Add(dot)
            Next
            Return canvas
        End If

        Dim element As Shape
        If layer.Shape = BackgroundShape2D.Ellipse Then
            element = New Ellipse()
        Else
            element = New Rectangle With {
                .RadiusX = Math.Max(0, layer.CornerRadius),
                .RadiusY = Math.Max(0, layer.CornerRadius)
            }
        End If
        element.Width = Math.Max(0, layer.Width)
        element.Height = Math.Max(0, layer.Height)
        Dim image = TryLoadResourceImage(layer.ImageResourceId)
        If image IsNot Nothing Then
            element.Fill = New ImageBrush(image) With {.Stretch = Stretch.Fill}
        Else
            element.Fill = Ui.Brush(layer.Fill)
        End If
        element.Stroke = Ui.Brush(layer.Stroke)
        element.StrokeThickness = Math.Max(0, layer.StrokeThickness)
        element.Opacity = anim.Opacity
        element.IsHitTestVisible = False
        element.RenderTransformOrigin = New Point(0.5, 0.5)
        Dim transforms As New TransformGroup()
        transforms.Children.Add(New ScaleTransform(anim.Scale, anim.Scale))
        transforms.Children.Add(New RotateTransform(anim.Rotation))
        element.RenderTransform = transforms
        Canvas.SetLeft(element, layer.X + anim.MoveX)
        Canvas.SetTop(element, layer.Y + anim.MoveY)
        Return element
    End Function

    Private Function BuildModel(mesh As BackgroundMesh, selected As Boolean) As GeometryModel3D
        Dim data = BackgroundGeometry.Build(mesh)
        Dim geometry As New MeshGeometry3D()
        For Each p In data.Positions
            geometry.Positions.Add(New Point3D(p.X, p.Y, p.Z))
        Next
        For Each n In data.Normals
            geometry.Normals.Add(New Vector3D(n.X, n.Y, n.Z))
        Next
        For Each uv In data.TextureCoordinates
            geometry.TextureCoordinates.Add(New Point(uv.X, uv.Y))
        Next
        For Each index In data.Indices
            geometry.TriangleIndices.Add(index)
        Next
        Dim color = If(selected, "#FFFFD166", mesh.Color)
        Dim image = If(selected, Nothing, TryLoadResourceImage(mesh.ImageResourceId))
        Dim material As Material
        If image IsNot Nothing Then
            material = New DiffuseMaterial(New ImageBrush(image) With {.Stretch = Stretch.Fill})
        Else
            material = New DiffuseMaterial(Ui.Brush(color))
        End If
        Dim backMaterial As Material
        If image IsNot Nothing Then
            backMaterial = New DiffuseMaterial(New ImageBrush(image) With {.Stretch = Stretch.Fill})
        Else
            backMaterial = New DiffuseMaterial(Ui.Brush(color))
        End If
        Dim model As New GeometryModel3D(geometry, material) With {
            .BackMaterial = backMaterial
        }
        Dim transforms As New Transform3DGroup()
        transforms.Children.Add(New ScaleTransform3D(mesh.ScaleX, mesh.ScaleY, mesh.ScaleZ))
        If Math.Abs(mesh.RotationX) > 0.0001 Then transforms.Children.Add(New RotateTransform3D(New AxisAngleRotation3D(New Vector3D(1, 0, 0), mesh.RotationX)))
        If Math.Abs(mesh.RotationY) > 0.0001 Then transforms.Children.Add(New RotateTransform3D(New AxisAngleRotation3D(New Vector3D(0, 1, 0), mesh.RotationY)))
        If Math.Abs(mesh.RotationZ) > 0.0001 Then transforms.Children.Add(New RotateTransform3D(New AxisAngleRotation3D(New Vector3D(0, 0, 1), mesh.RotationZ)))
        transforms.Children.Add(New TranslateTransform3D(mesh.X, mesh.Y, mesh.Z))
        model.Transform = transforms
        Return model
    End Function

    Public Function TryLoadResourceImage(resourceId As String) As ImageSource
        If String.IsNullOrWhiteSpace(resourceId) Then Return Nothing
        If App.Session Is Nothing OrElse App.Session.Project Is Nothing Then Return Nothing
        Dim resource = App.Session.Project.Resources.FirstOrDefault(Function(r) String.Equals(r.Id, resourceId, StringComparison.Ordinal))
        If resource Is Nothing OrElse resource.Kind <> GameResourceKind.Image Then Return Nothing
        Dim path = App.Session.ResourceFullPath(resource)
        If String.IsNullOrWhiteSpace(path) OrElse Not File.Exists(path) Then Return Nothing
        Try
            Dim image As New BitmapImage()
            image.BeginInit()
            image.CacheOption = BitmapCacheOption.OnLoad
            image.UriSource = New Uri(path, UriKind.Absolute)
            image.EndInit()
            image.Freeze()
            Return image
        Catch
            Return Nothing
        End Try
    End Function

End Module
