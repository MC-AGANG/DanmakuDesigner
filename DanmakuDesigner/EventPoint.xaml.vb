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
    Public Property X_Random As Double = 0
    Public Property Y_Random As Double = 0
    Public Property Speed_Random As Double = 0
    Public Property Direction_Random As Double = 0
    Public Sub New()
        _Type = 0
    End Sub
    Public Overrides Sub Run()
        STG.Add(New Enemy(EnemyType,
                          EnemyColor,
                          X + X_Random * 2 * Rnd() - X_Random,
                          Y + Y_Random * 2 * Rnd() - Y_Random,
                          HP,
                          Items,
                          Life,
                          Speed + Speed_Random * 2 * Rnd() - Speed_Random,
                          Direction + Direction_Random * 2 * Rnd() - Direction_Random,
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
    End Sub
End Class
Public Class BulletAddEvent
    Inherits EventPoint
    Public Property Sniper As Boolean = True
    Public Property BulletType As Byte = 1
    Public Property BulletColor As Byte = 0
    Public Property X As Double = 192
    Public Property Y As Double = 224
    Public Property Speed As Double = 1
    Public Property Direction As Double = 0
    Public Property X_Random As Double = 0
    Public Property Y_Random As Double = 0
    Public Property Speed_Random As Double = 0
    Public Property Direction_Random As Double = 0
    Public Property Breakable As Boolean = True
    Public Sub New()
        Type = EventType.Bullet
    End Sub
    Public Overrides Sub Run()
        If BulletType = 0 Then
            If Sniper Then
                STG.Add(New Bullet.Laser(BulletColor,
                                         X + X_Random * 2 * Rnd() - X_Random,
                                         Y + Y_Random * 2 * Rnd() - Y_Random,
                                         16,
                                         Speed,
                                         Speed_Random) With {
                                         .Tag = Tag,
                                         .Breakable = Breakable,
                                         .Direction = .Direction + Direction + Direction_Random * 2 * Rnd() - Direction_Random})
            Else
                STG.Add(New Bullet.Laser(BulletColor,
                                         X + X_Random * 2 * Rnd() - X_Random,
                                         Y + Y_Random * 2 * Rnd() - Y_Random,
                                         Direction + Direction_Random * 2 * Rnd() - Direction_Random,
                                         16,
                                         Speed,
                                         Speed_Random) With {
                                         .Tag = Tag,
                                         .Breakable = Breakable})
            End If
        Else
            If Sniper Then
                STG.Add(New Bullet(BulletType,
                                   BulletColor,
                                   X + X_Random * 2 * Rnd() - X_Random,
                                   Y + Y_Random * 2 * Rnd() - Y_Random,
                                   Speed + Speed_Random * 2 * Rnd() - Speed_Random,
                                   Direction + Direction_Random * 2 * Rnd() - Direction_Random) With {
                                   .Tag = Tag,
                                   .Breakable = Breakable
                })
            Else
                STG.Add(New Bullet(BulletType,
                                   BulletColor,
                                   X + X_Random * 2 * Rnd() - X_Random,
                                   Y + Y_Random * 2 * Rnd() - Y_Random,
                                   Speed + Speed_Random * 2 * Rnd() - Speed_Random,
                                   Direction + Direction_Random * 2 * Rnd() - Direction_Random,
                                   0) With {
                                   .Tag = Tag,
                                   .Breakable = Breakable
                })
            End If
        End If
    End Sub

    Public Overrides Sub UpdateIcon()
        LB_Main.Foreground = Brushes.Cyan
        LB_Main.Content = "⬛"
    End Sub
    Public Overrides Sub ShowProperty()
        SW_Property.Load(Me)
    End Sub
End Class
Public Class BulletGroupEvent
    Inherits EventPoint
    Public Property Sniper As Boolean = True
    Public Property BulletType As Byte = 1
    Public Property BulletColor As Byte = 0
    Public Property X As Double = 192
    Public Property Y As Double = 224
    Public Property Speed As Double = 1
    Public Property Direction As Double = 0
    Public Property X_End As Double = 192
    Public Property Y_End As Double = 224
    Public Property Speed_End As Double = 1
    Public Property Direction_End As Double = 0
    Public Property Breakable As Boolean = True
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
        Type = EventType.Bullet
    End Sub
    Public Overrides Sub Run()
        For i As UInteger = 1 To Count
            STG.Add(GetInterpolation(i))
        Next
    End Sub
    Private Function GetInterpolation(i As UInteger) As Bullet
        Dim e As Bullet
        If BulletType = 0 Then
            If Sniper Then
                e = New Bullet.Laser(BulletColor,
                                         X + (X_End - X) / Count * (i - 1),
                                         Y + (Y_End - Y) / Count * (i - 1),
                                         16,
                                         Speed,
                                         Speed_End) With {
                                         .Tag = Tag,
                                         .Breakable = Breakable,
                                         .Direction = .Direction + (Direction_End - Direction) / Count * (i - 1)}
            Else
                e = New Bullet.Laser(BulletColor,
                                         X + (X_End - X) / Count * (i - 1),
                                         Y + (Y_End - Y) / Count * (i - 1),
                                         Direction + +(Direction_End - Direction) / Count * (i - 1),
                                         16,
                                         Speed,
                                         Speed_End) With {
                                         .Tag = Tag,
                                         .Breakable = Breakable}
            End If
        Else
            If Sniper Then
                e = New Bullet(BulletType,
                               BulletColor,
                               X + (X_End - X) / Count * (i - 1),
                               Y + (Y_End - Y) / Count * (i - 1),
                               Speed + (Speed_End - Speed) / Count * (i - 1),
                               Direction + (Direction_End - Direction) / Count * (i - 1)) With {
                               .Tag = Tag
                    }

            Else
                e = New Bullet(BulletType,
                               BulletColor,
                               X + (X_End - X) / Count * (i - 1),
                               Y + (Y_End - Y) / Count * (i - 1),
                               Speed + (Speed_End - Speed) / Count * (i - 1),
                               Direction + (Direction_End - Direction) / Count * (i - 1),
                               0) With {
                               .Tag = Tag
                    }
            End If
        End If

        Return e
    End Function
    Public Overrides Sub UpdateIcon()
        LB_Main.Foreground = Brushes.Cyan
        LB_Main.Content = CStr(Count)
    End Sub
    Public Overrides Sub ShowProperty()
        SW_Property.Load(Me)
    End Sub
End Class
Public Class BulletClearEvent
    Inherits EventPoint
    Public Property UseTag As Boolean = False
    Public Property Force As Boolean = False
    Public Sub New()
        Type = EventType.ClearBullet
    End Sub
    Public Overrides Sub Run()
        If UseTag Then
            For Each e In STG.SearchBullet(Tag)
                e.Break(True, Force)
            Next
        Else
            For Each e In STG.SearchBullet()
                e.Break(True, Force)
            Next
        End If
    End Sub

    Public Overrides Sub UpdateIcon()
        LB_Main.Foreground = Brushes.Red
        LB_Main.Content = "⬛"
    End Sub
    Public Overrides Sub ShowProperty()
        SW_Property.Load(Me)
    End Sub
End Class