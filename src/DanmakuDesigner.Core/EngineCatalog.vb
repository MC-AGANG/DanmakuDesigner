Option Strict On
Option Infer On

Namespace Global.DanmakuDesigner.Core

    ''' <summary>引擎资源的只读目录。名称与 AgSTG 中的枚举成员完全一致，用于生成 VB 代码。</summary>
    Public NotInheritable Class EnemyTypeInfo
        Public ReadOnly Property Id As Integer
        Public ReadOnly Property Name As String
        Public ReadOnly Property Display As String
        Public ReadOnly Property Width As Double
        Public ReadOnly Property Height As Double
        Public ReadOnly Property Hitbox As Double
        Public ReadOnly Property ColorCount As Integer
        Public Sub New(id As Integer, name As String, display As String, width As Double, height As Double, hitbox As Double, colorCount As Integer)
            Me.Id = id
            Me.Name = name
            Me.Display = display
            Me.Width = width
            Me.Height = height
            Me.Hitbox = hitbox
            Me.ColorCount = colorCount
        End Sub
    End Class

    Public NotInheritable Class BulletTypeInfo
        Public ReadOnly Property Id As Integer
        Public ReadOnly Property Name As String
        Public ReadOnly Property Display As String
        Public ReadOnly Property Width As Double
        Public ReadOnly Property Height As Double
        Public ReadOnly Property Hitbox As Double
        Public ReadOnly Property Spin As Boolean
        Public Sub New(id As Integer, name As String, display As String, width As Double, height As Double, hitbox As Double, Optional spin As Boolean = False)
            Me.Id = id
            Me.Name = name
            Me.Display = display
            Me.Width = width
            Me.Height = height
            Me.Hitbox = hitbox
            Me.Spin = spin
        End Sub
    End Class

    Public NotInheritable Class SizeInfo
        Public ReadOnly Property Width As Double
        Public ReadOnly Property Height As Double
        Public ReadOnly Property Hitbox As Double
        Public Sub New(w As Double, h As Double, hit As Double)
            Width = w
            Height = h
            Hitbox = hit
        End Sub
    End Class

    Public NotInheritable Class ChoiceInfo
        Public ReadOnly Property Value As String
        Public ReadOnly Property Display As String
        Public Sub New(value As String, display As String)
            Me.Value = value
            Me.Display = display
        End Sub
        Public Overrides Function ToString() As String
            Return Display
        End Function
    End Class

    Public Module EngineCatalog

        Public ReadOnly EnemyTypes As IReadOnlyList(Of EnemyTypeInfo) = New List(Of EnemyTypeInfo) From {
            New EnemyTypeInfo(0, "阴阳玉", "阴阳玉", 32, 32, 12, 4),
            New EnemyTypeInfo(1, "小妖精", "小妖精", 32, 32, 12, 8),
            New EnemyTypeInfo(2, "小蝴蝶", "小蝴蝶", 48, 32, 16, 2),
            New EnemyTypeInfo(3, "中蝴蝶", "中蝴蝶", 48, 48, 20, 2),
            New EnemyTypeInfo(4, "大蝴蝶", "大蝴蝶", 64, 64, 24, 2),
            New EnemyTypeInfo(5, "幽灵", "幽灵", 32, 32, 12, 4),
            New EnemyTypeInfo(6, "乌鸦", "乌鸦", 32, 32, 12, 1)
        }

        ''' <summary>可发射的普通子弹种类（0 号激光由激光积木单独处理）。</summary>
        Public ReadOnly BulletTypes As IReadOnlyList(Of BulletTypeInfo) = New List(Of BulletTypeInfo) From {
            New BulletTypeInfo(1, "鳞弹", "鳞弹", 16, 16, 6),
            New BulletTypeInfo(2, "环弹", "环弹", 16, 16, 6),
            New BulletTypeInfo(3, "小玉", "小玉", 16, 16, 6),
            New BulletTypeInfo(4, "米弹", "米弹", 16, 16, 3),
            New BulletTypeInfo(5, "苦无", "苦无", 16, 16, 3),
            New BulletTypeInfo(6, "棱弹", "棱弹", 16, 16, 3),
            New BulletTypeInfo(7, "札弹", "札弹", 16, 16, 6),
            New BulletTypeInfo(8, "座药", "座药", 16, 16, 3),
            New BulletTypeInfo(9, "黑心弹", "黑心弹", 16, 16, 3),
            New BulletTypeInfo(10, "小星弹", "小星弹", 16, 16, 3, True),
            New BulletTypeInfo(11, "星弹", "星弹", 32, 32, 6, True),
            New BulletTypeInfo(12, "中玉", "中玉", 32, 32, 8),
            New BulletTypeInfo(13, "蝶弹", "蝶弹", 32, 32, 6),
            New BulletTypeInfo(14, "刀弹", "刀弹", 32, 32, 3),
            New BulletTypeInfo(15, "椭弹", "椭弹", 32, 32, 6),
            New BulletTypeInfo(16, "心弹", "心弹", 32, 32, 8),
            New BulletTypeInfo(17, "箭头", "箭头", 32, 32, 3),
            New BulletTypeInfo(18, "小光玉", "小光玉", 32, 32, 6),
            New BulletTypeInfo(19, "点弹", "点弹", 8, 8, 3),
            New BulletTypeInfo(20, "大玉", "大玉", 64, 64, 20, True)
        }

        Public ReadOnly BulletColors As IReadOnlyList(Of String) = New List(Of String) From {
            "灰", "暗红", "红", "紫", "品红", "深蓝", "蓝", "青", "天蓝", "深绿", "绿", "黄绿", "土黄", "黄", "橙", "白"
        }

        ''' <summary>11~18 号子弹只提供 8 个纹理颜色变体。</summary>
        Public ReadOnly RestrictedBulletColorNames As IReadOnlyList(Of String) = New List(Of String) From {
            "灰", "红", "紫", "蓝", "青", "绿", "黄", "白"
        }

        ''' <summary>20 号大玉只提供 4 个纹理颜色变体。</summary>
        Public ReadOnly BigBulletColorNames As IReadOnlyList(Of String) = New List(Of String) From {
            "红", "蓝", "绿", "黄"
        }

        ''' <summary>每种敌机的纹理颜色变体；顺序与引擎 ResourcePack 的 eXXYY 资源索引一致。</summary>
        Public ReadOnly EnemyColorSets As IReadOnlyList(Of IReadOnlyList(Of String)) = New List(Of IReadOnlyList(Of String)) From {
            New List(Of String) From {"红", "绿", "蓝", "黄"},
            New List(Of String) From {"红", "绿", "蓝", "黄", "红（反色）", "绿（反色）", "蓝（反色）", "黄（反色）"},
            New List(Of String) From {"蓝", "红"},
            New List(Of String) From {"黄", "青"},
            New List(Of String) From {"正常", "反色"},
            New List(Of String) From {"红", "绿", "蓝", "黄"},
            New List(Of String) From {"黑色"}
        }

        ''' <summary>常用音效（与 ResourcePack.Sounds 的公共字段对应）。</summary>
        Public ReadOnly Sounds As IReadOnlyList(Of ChoiceInfo) = New List(Of ChoiceInfo) From {
            New ChoiceInfo("tan00", "tan00 · 发射"),
            New ChoiceInfo("tan01", "tan01 · 发射 2"),
            New ChoiceInfo("tan02", "tan02 · 发射 3"),
            New ChoiceInfo("lazer00", "lazer00 · 激光"),
            New ChoiceInfo("lazer01", "lazer01 · 激光 2"),
            New ChoiceInfo("enep00", "enep00 · 敌机击破"),
            New ChoiceInfo("enep01", "enep01 · 首领击破"),
            New ChoiceInfo("enep02", "enep02 · 敌机击破 2"),
            New ChoiceInfo("graze", "graze · 擦弹"),
            New ChoiceInfo("ch02", "ch02 · 符卡宣言"),
            New ChoiceInfo("etbreak", "etbreak · 符卡击破"),
            New ChoiceInfo("damage00", "damage00 · 受伤"),
            New ChoiceInfo("pause", "pause · 暂停"),
            New ChoiceInfo("gun00", "gun00 · 自机射击"),
            New ChoiceInfo("extend", "extend · 奖励"),
            New ChoiceInfo("timeout", "timeout · 时间告警"),
            New ChoiceInfo("kira00", "kira00 · 星光"),
            New ChoiceInfo("kira01", "kira01 · 星光 2")
        }

        Public ReadOnly Difficulties As IReadOnlyList(Of ChoiceInfo) = New List(Of ChoiceInfo) From {
            New ChoiceInfo("Easy", "Easy · 简单"),
            New ChoiceInfo("Normal", "Normal · 普通"),
            New ChoiceInfo("Hard", "Hard · 困难"),
            New ChoiceInfo("Lunatic", "Lunatic · 疯狂"),
            New ChoiceInfo("Extra", "Extra · 额外")
        }

        Public ReadOnly Formations As IReadOnlyList(Of ChoiceInfo) = New List(Of ChoiceInfo) From {
            New ChoiceInfo("SinglePoint", "单点"),
            New ChoiceInfo("LineHorizontal", "横向排开"),
            New ChoiceInfo("LineVertical", "纵向排开"),
            New ChoiceInfo("Ring", "环形阵"),
            New ChoiceInfo("Arc", "弧形阵"),
            New ChoiceInfo("RandomBox", "随机散布")
        }

        Public ReadOnly CueTypes As IReadOnlyList(Of ChoiceInfo) = New List(Of ChoiceInfo) From {
            New ChoiceInfo("Music", "音乐 · 显示曲名"),
            New ChoiceInfo("ClearBullets", "清除子弹"),
            New ChoiceInfo("Shake", "屏幕震动"),
            New ChoiceInfo("EndStage", "结束关卡")
        }

        Public Function EnemyType(id As Integer) As EnemyTypeInfo
            For Each item In EnemyTypes
                If item.Id = id Then Return item
            Next
            Return EnemyTypes(1)
        End Function

        ''' <summary>TH07 各 Boss 贴图帧数（Boss 6 没有可用贴图）。</summary>
        Public Function BossFrameCount(bossId As Integer) As Integer
            Select Case bossId
                Case 0, 8 : Return 16
                Case 1 : Return 7
                Case 2 : Return 2
                Case 3 : Return 1
                Case 4 : Return 9
                Case 5 : Return 4
                Case 6 : Return 0
                Case 7 : Return 9
                Case Else : Return 0
            End Select
        End Function

        Public Function BulletType(id As Integer) As BulletTypeInfo
            For Each item In BulletTypes
                If item.Id = id Then Return item
            Next
            Return BulletTypes(2)
        End Function

        Public Function BulletColorName(id As Integer) As String
            If id < 0 OrElse id >= BulletColors.Count Then Return BulletColors(15)
            Return BulletColors(id)
        End Function

        Public Function EnemyColors(enemyType As Integer) As IReadOnlyList(Of String)
            If enemyType < 0 OrElse enemyType >= EnemyColorSets.Count Then Return EnemyColorSets(1)
            Return EnemyColorSets(enemyType)
        End Function

        Public Function NormalizeEnemyColor(enemyType As Integer, color As Integer) As Integer
            Dim colors = EnemyColors(enemyType)
            If color < 0 OrElse color >= colors.Count Then Return 0
            Return color
        End Function

        Public Function BulletColorLabels(bulletType As Integer) As IReadOnlyList(Of String)
            Select Case bulletType
                Case 11 To 18
                    Return RestrictedBulletColorNames
                Case 20
                    Return BigBulletColorNames
                Case Else
                    Return BulletColors
            End Select
        End Function

        Public Function NormalizeBulletColor(bulletType As Integer, color As Integer) As Integer
            Dim colors = BulletColorLabels(bulletType)
            If color < 0 OrElse color >= colors.Count Then Return 0
            Return color
        End Function

        Public Function EnemySpriteSize(id As Integer) As SizeInfo
            Dim info = EnemyType(id)
            Return New SizeInfo(info.Width, info.Height, info.Hitbox)
        End Function

        Public Function BulletSpriteSize(id As Integer) As SizeInfo
            Select Case id
                Case 0
                    Return New SizeInfo(32, 32, 3)
                Case 1, 2, 3, 7
                    Return New SizeInfo(16, 16, 6)
                Case 4, 5, 6, 8, 9, 10
                    Return New SizeInfo(16, 16, 3)
                Case 11, 13, 15, 18
                    Return New SizeInfo(32, 32, 6)
                Case 12, 16
                    Return New SizeInfo(32, 32, 8)
                Case 14, 17
                    Return New SizeInfo(32, 32, 3)
                Case 19
                    Return New SizeInfo(8, 8, 3)
                Case 20
                    Return New SizeInfo(64, 64, 20)
                Case Else
                    Return New SizeInfo(16, 16, 6)
            End Select
        End Function

    End Module

End Namespace
