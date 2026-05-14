Imports System.Runtime.InteropServices
Imports ResourcePack
Class Application
    Private LW As LoadWindow
    Private Async Sub Application_Startup(sender As Object, e As StartupEventArgs)
        LW = New LoadWindow
        LW.Show()
        Await LoadResource()
        Dim mw As New MainWindow
        FullScreenManager.RepairWpfWindowFullScreenBehavior(mw)
        mw.Show()
        LW.Close()
    End Sub

    ' Application-level events, such as Startup, Exit, and DispatcherUnhandledException
    ' can be handled in this file.
    Private Async Function LoadResource() As Task
        Textures.Load()
        Sounds.Load()
        DD.Textures.Load()
    End Function
End Class
Public NotInheritable Class FullScreenManager
    Private Sub New()
    End Sub
    Public Shared Sub RepairWpfWindowFullScreenBehavior(wpfWindow As Window)
        If wpfWindow Is Nothing Then
            Return
        End If
        If wpfWindow.WindowState = WindowState.Maximized Then
            wpfWindow.WindowState = WindowState.Normal
            AddHandler wpfWindow.Loaded, Sub() wpfWindow.WindowState = WindowState.Maximized
        End If
        AddHandler wpfWindow.SourceInitialized, Sub()
                                                    Dim handle As IntPtr = New Interop.WindowInteropHelper(wpfWindow).Handle
                                                    Dim source As Interop.HwndSource = Interop.HwndSource.FromHwnd(handle)
                                                    If source IsNot Nothing Then
                                                        source.AddHook(AddressOf WindowProc)
                                                    End If
                                                End Sub
    End Sub

    Private Shared Function WindowProc(hwnd As IntPtr, msg As Integer, wParam As IntPtr, lParam As IntPtr, ByRef handled As Boolean) As IntPtr
        Select Case msg
            Case &H24
                WmGetMinMaxInfo(hwnd, lParam)
                handled = True
                Exit Select
        End Select
        Return 0
    End Function

    Private Shared Sub WmGetMinMaxInfo(hwnd As IntPtr, lParam As IntPtr)
        Dim mmi = CType(Marshal.PtrToStructure(lParam, GetType(MINMAXINFO)), MINMAXINFO)
        Dim MONITOR_DEFAULTTONEAREST As Integer = &H2
        Dim monitor As IntPtr = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST)
        If monitor <> IntPtr.Zero Then
            Dim monitorInfo = New MONITORINFO()
            GetMonitorInfo(monitor, monitorInfo)
            Dim rcWorkArea As RECT = monitorInfo.rcWork
            Dim rcMonitorArea As RECT = monitorInfo.rcMonitor
            mmi.ptMaxPosition.x = Math.Abs(rcWorkArea.left - rcMonitorArea.left)
            mmi.ptMaxPosition.y = Math.Abs(rcWorkArea.top - rcMonitorArea.top)
            mmi.ptMaxSize.x = Math.Abs(rcWorkArea.right - rcWorkArea.left)
            mmi.ptMaxSize.y = Math.Abs(rcWorkArea.bottom - rcWorkArea.top)
        End If
        Marshal.StructureToPtr(mmi, lParam, True)
    End Sub

    <DllImport("user32")>
    Friend Shared Function GetMonitorInfo(hMonitor As IntPtr, lpmi As MONITORINFO) As Boolean
    End Function

    <DllImport("User32")>
    Friend Shared Function MonitorFromWindow(handle As IntPtr, flags As Integer) As IntPtr
    End Function

#Region "Nested type: MINMAXINFO"

    <StructLayout(LayoutKind.Sequential)>
    Friend Structure MINMAXINFO
        Public ptReserved As POINT
        Public ptMaxSize As POINT
        Public ptMaxPosition As POINT
        Public ptMinTrackSize As POINT
        Public ptMaxTrackSize As POINT
    End Structure

#End Region

#Region "Nested type: MONITORINFO"
    <StructLayout(LayoutKind.Sequential, CharSet:=CharSet.Auto)>
    Friend Class MONITORINFO
        Public cbSize As Integer = Marshal.SizeOf(GetType(MONITORINFO))
        Public rcMonitor As RECT
        Public rcWork As RECT
        Public dwFlags As Integer
    End Class

#End Region

#Region "Nested type: POINT"
    <StructLayout(LayoutKind.Sequential)>
    Friend Structure POINT

        Public x As Integer

        Public y As Integer
        Public Sub New(x As Integer, y As Integer)
            Me.x = x
            Me.y = y
        End Sub
    End Structure

#End Region

#Region "Nested type: RECT"
    <StructLayout(LayoutKind.Sequential, Pack:=0)>
    Friend Structure RECT
        Public left As Integer
        Public top As Integer
        Public right As Integer
        Public bottom As Integer
        Public Shared ReadOnly Empty As RECT

        Public ReadOnly Property Width() As Integer
            Get
                Return Math.Abs(right - left)
            End Get
        End Property

        Public ReadOnly Property Height() As Integer
            Get
                Return bottom - top
            End Get
        End Property

        Public Sub New(left As Integer, top As Integer, right As Integer, bottom As Integer)
            Me.left = left
            Me.top = top
            Me.right = right
            Me.bottom = bottom
        End Sub

        Public Sub New(rcSrc As RECT)
            left = rcSrc.left
            top = rcSrc.top
            right = rcSrc.right
            bottom = rcSrc.bottom
        End Sub

        Public ReadOnly Property IsEmpty() As Boolean
            Get
                Return left >= right OrElse top >= bottom
            End Get
        End Property

        Public Overrides Function ToString() As String
            If Me = Empty Then
                Return "RECT {Empty}"
            End If
            Return "RECT { left : " + left + " / top : " + top + " / right : " + right + " / bottom : " + bottom + " }"
        End Function

        Public Overrides Function Equals(obj As Object) As Boolean
            If Not (TypeOf obj Is RECT) Then
                Return False
            End If
            Return Me = CType(obj, RECT)
        End Function

        Public Overrides Function GetHashCode() As Integer
            Return left.GetHashCode() + top.GetHashCode() + right.GetHashCode() + bottom.GetHashCode()
        End Function

        Public Shared Operator =(rect1 As RECT, rect2 As RECT) As Boolean
            Return rect1.left = rect2.left AndAlso rect1.top = rect2.top AndAlso rect1.right = rect2.right AndAlso rect1.bottom = rect2.bottom
        End Operator

        Public Shared Operator <>(rect1 As RECT, rect2 As RECT) As Boolean
            Return Not rect1 = rect2
        End Operator
    End Structure

#End Region
End Class