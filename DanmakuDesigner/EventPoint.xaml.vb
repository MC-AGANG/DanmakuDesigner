Imports AgSTG
Imports DanmakuDesigner.PublicArea
Public MustInherit Class EventPoint
    Public Property Selected As Boolean
        Set(value As Boolean)
            _Selected = value
            If value Then
                BD_Selection.Visibility = Visibility.Visible
                For Each e In Events
                    If e.Selected AndAlso e IsNot Me Then
                        e.Selected = False
                    End If
                Next
                ShowProperty()
            Else
                BD_Selection.Visibility = Visibility.Hidden
            End If
        End Set
        Get
            Return _Selected
        End Get
    End Property
    Private _Selected As Boolean = False
    Public Property Tick As Long
        Set(value As Long)
            _Ticks = value
            UpdatePosition()
        End Set
        Get
            Return _Ticks
        End Get
    End Property
    Protected Property _Ticks As Long
    Public Property Type As EventType
        Set(value As EventType)
            _Type = value
        End Set
        Get
            Return _Type
        End Get
    End Property
    Protected _Type As EventType
    Private Sub UserControl_Loaded(sender As Object, e As RoutedEventArgs)
        UpdateIcon()
        UpdatePosition()
        Selected = True
    End Sub
    Public Enum EventType As Byte
        Enemy = 0
        EnemyGroup = 1
        ClearEnemy = 2
        Bullet = 3
        BulletGroup = 4
        ClearBullet = 5
    End Enum
    Public Sub UpdatePosition()
        Canvas.SetLeft(Me, Tick * 2)
        Dim n As Integer = 0
        For i = 0 To Events.IndexOf(Me) - 1
            If Events(i).Tick = Tick Then
                n += 1
            End If
        Next
        Canvas.SetTop(Me, n * 16)
    End Sub
    Public MustOverride Sub Run()
    Public MustOverride Sub UpdateIcon()
    Public MustOverride Sub ShowProperty()

    Private Sub UserControl_MouseDown(sender As Object, e As MouseButtonEventArgs)
        Selected = True
    End Sub
    Public Sub Remove()
        Events.Remove(Me)
        SW_Timeline.CV_Main.Children.Remove(Me)
        For Each e In Events
            If e.Tick = Tick Then
                e.UpdatePosition()
            End If
        Next
    End Sub
End Class
Public Class EnemyAddEvent
    Inherits EventPoint
    Public Target As Enemy
    Public Sub New()
        _Type = 0
        Target = New Enemy(0, 0, 192, 224, 10, "", 200)
    End Sub
    Public Overrides Sub Run()
        STG.Add(New Enemy(Target.EnemyType, Target.Color, Target.X, Target.Y, Target.HP, Target.DropItems, Target.Life, Target.Speed, Target.Direction, Target.Defence))
    End Sub
    Public Overrides Sub UpdateIcon()
        LB_Main.Foreground = Brushes.Lime
        LB_Main.Content = "▲"
    End Sub
    Public Overrides Sub ShowProperty()
        SW_Property.Load(Me)
    End Sub
End Class
Public Class EnemyGroupEvent
    Inherits EventPoint
    Public Target_From As Enemy
    Public Target_To As Enemy
    Public Property Count As UInteger
        Set(value As UInteger)
            _Count = value
            UpdateIcon()
        End Set
        Get
            Return _Count
        End Get
    End Property
    Private _Count As UInteger
    Public Sub New()
        Type = EventType.EnemyGroup
        Target_From = New Enemy(0, 0, 0, 0, 10, "", 64)
        Target_To = New Enemy(0, 0, 0, 0, 10, "", 64)
    End Sub
    Public Overrides Sub Run()
        For i As UInteger = 1 To Count
            STG.Add(GetInterpolation(i))
        Next
    End Sub
    Private Function GetInterpolation(i As UInteger) As Enemy
        Dim e As New Enemy(Target_From.EnemyType,
                           Target_From.Color,
                           Target_From.X + (Target_To.X - Target_From.X) / Count * (i - 1),
                           Target_From.Y + (Target_To.Y - Target_From.Y) / Count * (i - 1),
                           Target_From.HP,
                           Target_From.DropItems,
                           Target_From.Life,
                           Target_From.Speed + (Target_To.Speed - Target_From.Speed) / Count * (i - 1),
                           Target_From.Direction + (Target_To.Direction - Target_From.Direction) / Count * (i - 1),
                           Target_From.Defence)
        Return e
    End Function
    Public Overrides Sub UpdateIcon()
        LB_Main.Foreground = Brushes.Lime
        LB_Main.Content = CStr(Count)
    End Sub
    Public Overrides Sub ShowProperty()

    End Sub
End Class
Public Class EnemyClearEvent
    Inherits EventPoint
    Public Sub New()
        Type = EventType.ClearEnemy
    End Sub
    Public Overrides Sub Run()
        For Each e In STG.SearchEnemy
            e.Life = 1
        Next
    End Sub

    Public Overrides Sub UpdateIcon()
        LB_Main.Foreground = Brushes.Red
        LB_Main.Content = "▲"
    End Sub
    Public Overrides Sub ShowProperty()

    End Sub
End Class
Public Class BulletAddEvent
    Inherits EventPoint
    Public Target As Bullet
    Public Property BulletType As BulletType
        Set(value As BulletType)
            _BulletType = value
        End Set
        Get
            Return _BulletType
        End Get
    End Property
    Private _BulletType As BulletType = 0
    Public Sub New()
        Type = EventType.Bullet
        Target = New Bullet(0, 0, 0, 0, 0, 0, 0)
    End Sub
    Public Overrides Sub Run()
        If BulletType = BulletType.Default Then
            STG.Add(Target)
        Else
            STG.Add(New Bullet(Target.BulletType, Target.BulletColor, Target.X, Target.Y, Target.Speed, Target.Direction))
        End If
    End Sub

    Public Overrides Sub UpdateIcon()
        LB_Main.Foreground = Brushes.Cyan
        LB_Main.Content = "⬛"
    End Sub
    Public Overrides Sub ShowProperty()

    End Sub
End Class
Public Class BulletGroupEvent
    Inherits EventPoint
    Public Target_From As Bullet
    Public Target_To As Bullet
    Public Property BulletType As BulletType
        Set(value As BulletType)
            _BulletType = value
        End Set
        Get
            Return _BulletType
        End Get
    End Property
    Private _BulletType As BulletType = 0
    Public Property Count As UInteger
        Set(value As UInteger)
            _Count = value
            UpdateIcon()
        End Set
        Get
            Return _Count
        End Get
    End Property
    Private _Count As UInteger
    Public Sub New()
        Type = EventType.Bullet
        Target_From = New Bullet(0, 0, 0, 0, 0)
        Target_To = New Bullet(0, 0, 0, 0, 0)
    End Sub
    Public Overrides Sub Run()
        For i As UInteger = 1 To Count
            STG.Add(GetInterpolation(i))
        Next
    End Sub
    Private Function GetInterpolation(i As UInteger) As Bullet
        Dim e As Bullet
        If BulletType = BulletType.Default Then
            e = New Bullet(Target_From.BulletType,
                                Target_From.BulletColor,
                                Target_From.X + (Target_To.X - Target_From.X) / Count * (i - 1),
                                Target_From.Y + (Target_To.Y - Target_From.Y) / Count * (i - 1),
                                Target_From.Speed + (Target_To.Speed - Target_From.Speed) / Count * (i - 1),
                                Target_From.Direction + (Target_To.Direction - Target_From.Direction) / Count * (i - 1),
                                0)
        Else
            e = New Bullet(Target_From.BulletType,
                                Target_From.BulletColor,
                                Target_From.X + (Target_To.X - Target_From.X) / Count * (i - 1),
                                Target_From.Y + (Target_To.Y - Target_From.Y) / Count * (i - 1),
                                Target_From.Speed + (Target_To.Speed - Target_From.Speed) / Count * (i - 1),
                                Target_From.Direction + (Target_To.Direction - Target_From.Direction) / Count * (i - 1))
        End If
        Return e
    End Function
    Public Overrides Sub UpdateIcon()
        LB_Main.Foreground = Brushes.Cyan
        LB_Main.Content = CStr(Count)
    End Sub
    Public Overrides Sub ShowProperty()

    End Sub
End Class
Public Class BulletClearEvent
    Inherits EventPoint
    Public Sub New()
        Type = EventType.ClearBullet
    End Sub
    Public Overrides Sub Run()
        STG.ClearBullet()
    End Sub

    Public Overrides Sub UpdateIcon()
        LB_Main.Foreground = Brushes.Red
        LB_Main.Content = "⬛"
    End Sub
    Public Overrides Sub ShowProperty()

    End Sub
End Class
Public Enum BulletType As Byte
    [Default] = 0
    Sniper = 1
End Enum