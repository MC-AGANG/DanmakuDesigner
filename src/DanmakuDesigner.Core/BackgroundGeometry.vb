Option Strict On
Option Infer On

Namespace Global.DanmakuDesigner.Core

    Public Structure GeometryPoint
        Public X As Double
        Public Y As Double
        Public Z As Double

        Public Sub New(x As Double, y As Double, z As Double)
            Me.X = x
            Me.Y = y
            Me.Z = z
        End Sub
    End Structure

    Public Class MeshGeometryData
        Public Property Positions As New List(Of GeometryPoint)
        Public Property Normals As New List(Of GeometryPoint)
        Public Property TextureCoordinates As New List(Of GeometryPoint)
        Public Property Indices As New List(Of Integer)
    End Class

    ''' <summary>把简单的背景几何体展开为三角网格，供编辑器预览和 XAML 导出共用。</summary>
    Public Module BackgroundGeometry

        Public Function Build(mesh As BackgroundMesh) As MeshGeometryData
            If mesh Is Nothing Then Return New MeshGeometryData()
            Select Case mesh.Kind
                Case BackgroundMeshKind.Box
                    Return BuildBox(mesh)
                Case BackgroundMeshKind.Cylinder
                    Return BuildCylinder(mesh)
                Case BackgroundMeshKind.Sphere
                    Return BuildSphere(mesh)
                Case BackgroundMeshKind.Rectangle
                    Return BuildRectangle(mesh)
                Case BackgroundMeshKind.Triangle
                    Return BuildTriangle(mesh)
                Case Else
                    Return New MeshGeometryData()
            End Select
        End Function

        Private Function BuildBox(mesh As BackgroundMesh) As MeshGeometryData
            Dim result As New MeshGeometryData()
            Dim hx = mesh.SizeX / 2.0
            Dim hy = mesh.SizeY / 2.0
            Dim hz = mesh.SizeZ / 2.0

            AddQuad(result,
                    New GeometryPoint(-hx, -hy, -hz), New GeometryPoint(hx, -hy, -hz),
                    New GeometryPoint(hx, hy, -hz), New GeometryPoint(-hx, hy, -hz),
                    New GeometryPoint(0, 0, -1))
            AddQuad(result,
                    New GeometryPoint(hx, -hy, hz), New GeometryPoint(-hx, -hy, hz),
                    New GeometryPoint(-hx, hy, hz), New GeometryPoint(hx, hy, hz),
                    New GeometryPoint(0, 0, 1))
            AddQuad(result,
                    New GeometryPoint(-hx, -hy, hz), New GeometryPoint(-hx, -hy, -hz),
                    New GeometryPoint(-hx, hy, -hz), New GeometryPoint(-hx, hy, hz),
                    New GeometryPoint(-1, 0, 0))
            AddQuad(result,
                    New GeometryPoint(hx, -hy, -hz), New GeometryPoint(hx, -hy, hz),
                    New GeometryPoint(hx, hy, hz), New GeometryPoint(hx, hy, -hz),
                    New GeometryPoint(1, 0, 0))
            AddQuad(result,
                    New GeometryPoint(-hx, hy, -hz), New GeometryPoint(hx, hy, -hz),
                    New GeometryPoint(hx, hy, hz), New GeometryPoint(-hx, hy, hz),
                    New GeometryPoint(0, 1, 0))
            AddQuad(result,
                    New GeometryPoint(-hx, -hy, hz), New GeometryPoint(hx, -hy, hz),
                    New GeometryPoint(hx, -hy, -hz), New GeometryPoint(-hx, -hy, -hz),
                    New GeometryPoint(0, -1, 0))
            Return result
        End Function

        Private Function BuildRectangle(mesh As BackgroundMesh) As MeshGeometryData
            Dim result As New MeshGeometryData()
            Dim hx = mesh.SizeX / 2.0
            Dim hy = mesh.SizeY / 2.0
            AddQuad(result,
                    New GeometryPoint(-hx, -hy, 0), New GeometryPoint(hx, -hy, 0),
                    New GeometryPoint(hx, hy, 0), New GeometryPoint(-hx, hy, 0),
                    New GeometryPoint(0, 0, -1))
            result.TextureCoordinates.AddRange({
                New GeometryPoint(0, 1, 0),
                New GeometryPoint(1, 1, 0),
                New GeometryPoint(1, 0, 0),
                New GeometryPoint(0, 0, 0)
            })
            Return result
        End Function

        Private Function BuildTriangle(mesh As BackgroundMesh) As MeshGeometryData
            Dim result As New MeshGeometryData()
            Dim hx = mesh.SizeX / 2.0
            Dim hy = mesh.SizeY / 2.0
            Dim normal As New GeometryPoint(0, 0, -1)
            result.Positions.Add(New GeometryPoint(-hx, -hy, 0))
            result.Positions.Add(New GeometryPoint(hx, -hy, 0))
            result.Positions.Add(New GeometryPoint(0, hy, 0))
            result.Normals.Add(normal)
            result.Normals.Add(normal)
            result.Normals.Add(normal)
            result.TextureCoordinates.AddRange({
                New GeometryPoint(0, 1, 0),
                New GeometryPoint(1, 1, 0),
                New GeometryPoint(0.5, 0, 0)
            })
            result.Indices.AddRange({0, 1, 2})
            Return result
        End Function

        Private Function BuildCylinder(mesh As BackgroundMesh) As MeshGeometryData
            Dim result As New MeshGeometryData()
            Dim segments = Math.Max(3, mesh.Segments)
            Dim radius = mesh.Radius
            Dim halfHeight = mesh.Height / 2.0
            For i = 0 To segments - 1
                Dim a0 = Math.PI * 2.0 * i / segments
                Dim a1 = Math.PI * 2.0 * (i + 1) / segments
                Dim n0 As New GeometryPoint(Math.Cos(a0), 0, Math.Sin(a0))
                Dim n1 As New GeometryPoint(Math.Cos(a1), 0, Math.Sin(a1))
                Dim start = result.Positions.Count
                result.Positions.Add(New GeometryPoint(radius * n0.X, -halfHeight, radius * n0.Z))
                result.Positions.Add(New GeometryPoint(radius * n1.X, -halfHeight, radius * n1.Z))
                result.Positions.Add(New GeometryPoint(radius * n1.X, halfHeight, radius * n1.Z))
                result.Positions.Add(New GeometryPoint(radius * n0.X, halfHeight, radius * n0.Z))
                result.Normals.Add(n0)
                result.Normals.Add(n1)
                result.Normals.Add(n1)
                result.Normals.Add(n0)
                result.Indices.AddRange({start, start + 1, start + 2, start, start + 2, start + 3})
            Next

            AddCylinderCap(result, radius, halfHeight, segments, True)
            AddCylinderCap(result, radius, -halfHeight, segments, False)
            Return result
        End Function

        Private Sub AddCylinderCap(result As MeshGeometryData, radius As Double, y As Double, segments As Integer, top As Boolean)
            Dim centerIndex = result.Positions.Count
            result.Positions.Add(New GeometryPoint(0, y, 0))
            Dim normal As New GeometryPoint(0, If(top, 1.0, -1.0), 0)
            result.Normals.Add(normal)
            For i = 0 To segments - 1
                Dim a = Math.PI * 2.0 * i / segments
                result.Positions.Add(New GeometryPoint(radius * Math.Cos(a), y, radius * Math.Sin(a)))
                result.Normals.Add(normal)
            Next
            For i = 0 To segments - 1
                Dim nextIndex = (i + 1) Mod segments
                If top Then
                    result.Indices.AddRange({centerIndex, centerIndex + 1 + i, centerIndex + 1 + nextIndex})
                Else
                    result.Indices.AddRange({centerIndex, centerIndex + 1 + nextIndex, centerIndex + 1 + i})
                End If
            Next
        End Sub

        Private Function BuildSphere(mesh As BackgroundMesh) As MeshGeometryData
            Dim result As New MeshGeometryData()
            Dim segments = Math.Max(3, mesh.Segments)
            Dim rings = Math.Max(2, segments \ 2)
            Dim radius = mesh.Radius
            For ring = 0 To rings
                Dim phi = -Math.PI / 2.0 + Math.PI * ring / rings
                Dim y = Math.Sin(phi)
                Dim r = Math.Cos(phi)
                For segment = 0 To segments
                    Dim theta = Math.PI * 2.0 * segment / segments
                    Dim x = r * Math.Cos(theta)
                    Dim z = r * Math.Sin(theta)
                    result.Positions.Add(New GeometryPoint(x * radius, y * radius, z * radius))
                    result.Normals.Add(New GeometryPoint(x, y, z))
                Next
            Next
            For ring = 0 To rings - 1
                For segment = 0 To segments - 1
                    Dim a = ring * (segments + 1) + segment
                    Dim b = a + segments + 1
                    result.Indices.AddRange({a, a + 1, b, a + 1, b + 1, b})
                Next
            Next
            Return result
        End Function

        Private Sub AddQuad(result As MeshGeometryData, p0 As GeometryPoint, p1 As GeometryPoint,
                            p2 As GeometryPoint, p3 As GeometryPoint, normal As GeometryPoint)
            Dim start = result.Positions.Count
            result.Positions.Add(p0)
            result.Positions.Add(p1)
            result.Positions.Add(p2)
            result.Positions.Add(p3)
            result.Normals.Add(normal)
            result.Normals.Add(normal)
            result.Normals.Add(normal)
            result.Normals.Add(normal)
            result.Indices.AddRange({start, start + 1, start + 2, start, start + 2, start + 3})
        End Sub

    End Module

End Namespace
