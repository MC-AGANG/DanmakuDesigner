Imports System.IO
Imports ResourcePack.DD.My.Resources

Public Class Textures
    Public Shared Logo_w As ImageBrush
    Public Shared BT_Close As ImageBrush
    Public Shared BT_Minimize As ImageBrush
    Public Shared BT_Maximize As ImageBrush
    Public Shared BT_Restore As ImageBrush

    Public Shared BT_New As ImageBrush
    Public Shared BT_Open As ImageBrush
    Public Shared BT_Save As ImageBrush
    Public Shared BT_SaveAs As ImageBrush
    Public Shared BT_CloseFile As ImageBrush

    Public Shared BT_Undo As ImageBrush
    Public Shared BT_Redo As ImageBrush

    Public Shared BT_Run As ImageBrush
    Public Shared BT_Pause As ImageBrush
    Public Shared BT_Stop As ImageBrush
    Public Shared BT_Refresh As ImageBrush
    Public Shared Sub Load()
        Logo_w = New ImageBrush(B2I(MyResource.logo_w))

        BT_Close = New ImageBrush(B2I(MyResource.close))
        BT_Minimize = New ImageBrush(B2I(MyResource.minimize))
        BT_Maximize = New ImageBrush(B2I(MyResource.maximize))
        BT_Restore = New ImageBrush(B2I(MyResource.restore))

        BT_New = New ImageBrush(B2I(MyResource.newfile))
        BT_Open = New ImageBrush(B2I(MyResource.open))
        BT_Save = New ImageBrush(B2I(MyResource.save))
        BT_SaveAs = New ImageBrush(B2I(MyResource.saveas))
        BT_CloseFile = New ImageBrush(B2I(MyResource.closefile))

        BT_Undo = New ImageBrush(B2I(MyResource.undo))
        BT_Redo = New ImageBrush(B2I(MyResource.redo))

        BT_Run = New ImageBrush(B2I(MyResource.run))
        BT_Pause = New ImageBrush(B2I(MyResource.pause))
        BT_Stop = New ImageBrush(B2I(MyResource._stop))
        BT_Refresh = New ImageBrush(B2I(MyResource.refresh))
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
