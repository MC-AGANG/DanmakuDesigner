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
        Height = 16
        Width = 16
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
    Public Property EnemyType As Byte = 0
    Public Property EnemyColor As Byte = 0
    Public Property X As Double = 192
    Public Property Y As Double = 224
    Public Property HP As Double = 10
    Public Property Items As String = ""
    Public Property Life As Integer = 256
    Public Property Speed As Double = 0
    Public Property Direction As Double = 180
    Public Property Defence As Double = 0
    Public Property Random_X As Double = 0
    Public Property Random_Y As Double = 0
    Public Property Random_Speed As Double = 0
    Public Property Random_Direction As Double = 0
    Public Sub New()
        _Type = 0
    End Sub
    Public Overrides Sub Run()
        STG.Add(New Enemy(EnemyType,
                          EnemyColor,
                          X + Random_X * 2 * Rnd() - Random_X,
                          Y + Random_Y * 2 * Rnd() - Random_Y,
                          HP,
                          Items,
                          Life,
                          Speed + Random_Speed * 2 * Rnd() - Random_Speed,
                          Direction + Random_Direction * 2 * Rnd() - Random_Direction,
                          Defence) With {
                          .Tag = Tag
         })
    End Sub
    Public Overrides Sub UpdateIcon()
        LB_Main.Foreground = Brushes.Lime
        LB_Main.Content = "▲"
    End Sub
    Public Overrides Sub ShowProperty()
        SW_Property.Load(Me)
        SW_Property.PB_EnemyGroup.Visibility = Visibility.Hidden
        SW_Property.PB_EnemyClear.Visibility = Visibility.Hidden
    End Sub
End Class
Public Class EnemyGroupEvent
    Inherits EventPoint
    Public Property EnemyType As Byte = 0
    Public Property EnemyColor As Byte = 0
    Public Property X As Double = 192
    Public Property Y As Double = 224
    Public Property HP As Double = 10
    Public Property Items As String = ""
    Public Property Life As Integer = 256
    Public Property Speed As Double = 0
    Public Property Direction As Double = 180
    Public Property Defence As Double = 0
    Public Property X_End As Double = 192
    Public Property Y_End As Double = 224
    Public Property Speed_End As Double = 0
    Public Property Direction_End As Double = 180
    Public Property Count As UInteger
        Set(value As UInteger)
            _Count = value
            UpdateIcon()
        End Set
        Get
            Return _Count
        End Get
    End Property
    Private _Count As UInteger = 1
    Public Sub New()
        Type = EventType.EnemyGroup
    End Sub
    Public Overrides Sub Run()
        For i As UInteger = 1 To Count
            STG.Add(GetInterpolation(i))
        Next
    End Sub
    Private Function GetInterpolation(i As UInteger) As Enemy
        Dim e As New Enemy(EnemyType,
                           EnemyColor,
                           X + (X_End - X) / Count * (i - 1),
                           Y + (Y_End - Y) / Count * (i - 1),
                           HP,
                           Items,
                           Life,
                           Speed + (Speed_End - Speed) / Count * (i - 1),
                           Direction + (Direction_End - Direction) / Count * (i - 1),
                           Defence) With {
                           .Tag = Tag
        }
        Return e
    End Function
    Public Overrides Sub UpdateIcon()
        LB_Main.Foreground = Brushes.Lime
        LB_Main.Content = CStr(Count)
    End Sub
    Public Overrides Sub ShowProperty()
        SW_Property.Load(Me)
        SW_Property.PB_Enemy.Visibility = Visibility.Hidden
        SW_Property.PB_EnemyClear.Visibility = Visibility.Hidden
    End Sub
End Class
Public Class EnemyClearEvent
    Inherits EventPoint
    Public Property UseTag As Boolean = False
    Public Sub New()
        Type = EventType.ClearEnemy
    End Sub
    Public Overrides Sub Run()
        If UseTag Then
            For Each e In STG.SearchEnemy
                If Tag.Equals(e.Tag) Then
                    e.Life = 1
                End If
            Next
        Else
            For Each e In STG.SearchEnemy
                e.Life = 1
            Next
        End If
    End Sub

    Public Overrides Sub UpdateIcon()
        LB_Main.Foreground = Brushes.Red
        LB_Main.Content = "▲"
    End Sub
    Public Overrides Sub ShowProperty()
        SW_Property.Load(Me)
        SW_Property.PB_Enemy.Visibility = Visibility.Hidden
        SW_Property.PB_EnemyGroup.Visibility = Visibility.Hidden
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
    Public Target_End As Bullet
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
        Target_End = New Bullet(0, 0, 0, 0, 0)
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
                                Target_From.X + (Target_End.X - Target_From.X) / Count * (i - 1),
                                Target_From.Y + (Target_End.Y - Target_From.Y) / Count * (i - 1),
                                Target_From.Speed + (Target_End.Speed - Target_From.Speed) / Count * (i - 1),
                                Target_From.Direction + (Target_End.Direction - Target_From.Direction) / Count * (i - 1),
                                0)
        Else
            e = New Bullet(Target_From.BulletType,
                                Target_From.BulletColor,
                                Target_From.X + (Target_End.X - Target_From.X) / Count * (i - 1),
                                Target_From.Y + (Target_End.Y - Target_From.Y) / Count * (i - 1),
                                Target_From.Speed + (Target_End.Speed - Target_From.Speed) / Count * (i - 1),
                                Target_From.Direction + (Target_End.Direction - Target_From.Direction) / Count * (i - 1))
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