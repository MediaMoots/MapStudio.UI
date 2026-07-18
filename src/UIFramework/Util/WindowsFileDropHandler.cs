using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace UIFramework
{
    /// <summary>
    /// Owns Windows shell file-drop registration for the native OpenTK window.
    /// </summary>
    internal sealed class WindowsFileDropHandler : IDisposable
    {
        private const uint WM_DROPFILES = 0x0233;
        private const uint WM_COPYGLOBALDATA = 0x0049;
        private const uint MSGFLT_ALLOW = 1;
        private const int GWL_WNDPROC = -4;

        private readonly IntPtr windowHandle;
        private readonly Action<string> onFileDrop;
        private readonly WindowProc windowProc;
        private readonly IntPtr originalWindowProc;
        private bool disposed;

        public WindowsFileDropHandler(Action<string> onFileDrop)
        {
            windowHandle = GetActiveWindow();
            this.windowHandle = windowHandle;
            this.onFileDrop = onFileDrop;
            windowProc = WindowProcedure;
            originalWindowProc = SetWindowLongPtr(windowHandle, GWL_WNDPROC,
                Marshal.GetFunctionPointerForDelegate(windowProc));
            if (originalWindowProc == IntPtr.Zero)
            {
                int error = Marshal.GetLastWin32Error();
                throw new Win32Exception(error,
                    $"Failed to register the window file-drop handler (Win32 error {error}).");
            }

            // Explorer may run at a lower integrity level than an editor launched from an IDE.
            // Permit only the two shell messages that carry a legacy file drop.
            ChangeWindowMessageFilterEx(windowHandle, WM_DROPFILES, MSGFLT_ALLOW, IntPtr.Zero);
            ChangeWindowMessageFilterEx(windowHandle, WM_COPYGLOBALDATA, MSGFLT_ALLOW, IntPtr.Zero);
            DragAcceptFiles(windowHandle, true);
        }

        private IntPtr WindowProcedure(IntPtr handle, uint message, IntPtr wParam, IntPtr lParam)
        {
            if (message != WM_DROPFILES)
                return CallWindowProc(originalWindowProc, handle, message, wParam, lParam);

            try
            {
                uint fileCount = DragQueryFile(wParam, uint.MaxValue, null, 0);
                for (uint i = 0; i < fileCount; i++)
                {
                    uint pathLength = DragQueryFile(wParam, i, null, 0);
                    var path = new StringBuilder(checked((int)pathLength + 1));
                    DragQueryFile(wParam, i, path, (uint)path.Capacity);
                    onFileDrop(path.ToString());
                }
            }
            finally
            {
                DragFinish(wParam);
            }

            // We own WM_DROPFILES so OpenTK's legacy handler must not process it again.
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            if (disposed)
                return;

            DragAcceptFiles(windowHandle, false);
            SetWindowLongPtr(windowHandle, GWL_WNDPROC, originalWindowProc);
            disposed = true;
        }

        private delegate IntPtr WindowProc(IntPtr handle, uint message, IntPtr wParam, IntPtr lParam);

        private static IntPtr SetWindowLongPtr(IntPtr handle, int index, IntPtr value)
        {
            return IntPtr.Size == 8
                ? SetWindowLongPtr64(handle, index, value)
                : new IntPtr(SetWindowLong32(handle, index, value.ToInt32()));
        }

        [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
        private static extern int SetWindowLong32(IntPtr handle, int index, int value);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr64(IntPtr handle, int index, IntPtr value);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr CallWindowProc(IntPtr previousWindowProc, IntPtr handle,
            uint message, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr GetActiveWindow();

        [DllImport("shell32.dll")]
        private static extern void DragAcceptFiles(IntPtr handle, [MarshalAs(UnmanagedType.Bool)] bool accept);

        [DllImport("shell32.dll", EntryPoint = "DragQueryFileW", CharSet = CharSet.Unicode)]
        private static extern uint DragQueryFile(IntPtr dropHandle, uint fileIndex,
            StringBuilder path, uint pathLength);

        [DllImport("shell32.dll")]
        private static extern void DragFinish(IntPtr dropHandle);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ChangeWindowMessageFilterEx(IntPtr handle, uint message,
            uint action, IntPtr filterChangeStatus);
    }
}
