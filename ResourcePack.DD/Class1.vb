Imports System.IO
Imports ResourcePack.DD.My.Resources

Public Class Textures
    Public Shared Logo_w As ImageBrush
    Public Shared BT_Close As ImageBrush
    Public Shared BT_Minimize As ImageBrush
    Public Shared BT_Maximize As ImageBrush
    Public Shared BT_Restore As ImageBrush
    Public Shared Sub Load()
        Logo_w = New ImageBrush(B2I(MyResource.logo_w))

        BT_Close = New ImageBrush(B2I(MyResource.close))
        BT_Minimize = New ImageBrush(B2I(MyResource.minimize))
        BT_Maximize = New ImageBrush(B2I(MyResource.maximize))
        BT_Restore = New ImageBrush(B2I(MyResource.restore))
    End Sub
    Public Shared Function B2I(byteArray As Byte()) As BitmapImage
        Using stream As Stream = New MemoryStream(byteArray)
            Dim image As New BitmapImage()
            stream.Position = 0
            image.BeginInit()
            image.CacheOption = BitmapCacheOption.OnLoad
            image.StreamSource = stream
            image.EndInit()
            image.Freeze()
            Return image
        End Using
    End Function
End Class
