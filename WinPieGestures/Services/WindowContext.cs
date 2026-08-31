using System;
using System.Runtime.InteropServices;

namespace WinPieGestures.Services
{
    /// <summary>
    /// Win32 implementation of <see cref="IWindowContext"/>; merges the former
    /// ActiveWindowHelper and FullScreenHelper statics plus live modifier-key state.
    /// </summary>
    public sealed class WindowContext : IWindowContext
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern IntPtr GetShellWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetDesktopWindow();

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        private const uint MONITOR_DEFAULTTONEAREST = 2;
        private const int VK_CONTROL = 0x11;
        private const int VK_SHIFT = 0x10;
        private const int VK_MENU = 0x12;

        public string GetForegroundProcessName()
        {
            try
            {
                IntPtr hWnd = GetForegroundWindow();
                if (hWnd == IntPtr.Zero)
                    return "unknown.exe";

                GetWindowThreadProcessId(hWnd, out uint processId);
                if (processId == 0)
                    return "unknown.exe";

                using (System.Diagnostics.Process proc = System.Diagnostics.Process.GetProcessById((int)processId))
                {
                    string processName = proc.ProcessName;
                    if (string.IsNullOrEmpty(processName))
                        return "unknown.exe";

                    return processName.ToLower() + ".exe";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to get active window process: {ex.Message}");
                return "unknown.exe";
            }
        }

        public bool IsForegroundFullScreen()
        {
            IntPtr hWnd = GetForegroundWindow();
            if (hWnd == IntPtr.Zero) return false;

            // Exclude desktop background and shell manager
            if (hWnd == GetShellWindow() || hWnd == GetDesktopWindow()) return false;

            if (!GetWindowRect(hWnd, out RECT windowRect)) return false;

            IntPtr hMonitor = MonitorFromWindow(hWnd, MONITOR_DEFAULTTONEAREST);
            if (hMonitor == IntPtr.Zero) return false;

            MONITORINFO monitorInfo = new MONITORINFO();
            monitorInfo.cbSize = Marshal.SizeOf(monitorInfo);

            if (!GetMonitorInfo(hMonitor, ref monitorInfo)) return false;

            // Full-screen when the window rect covers the entire monitor rect
            return windowRect.Left <= monitorInfo.rcMonitor.Left &&
                   windowRect.Top <= monitorInfo.rcMonitor.Top &&
                   windowRect.Right >= monitorInfo.rcMonitor.Right &&
                   windowRect.Bottom >= monitorInfo.rcMonitor.Bottom;
        }

        public GestureModifierKeys GetActiveModifierKeys()
        {
            GestureModifierKeys keys = GestureModifierKeys.None;
            if ((GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0) keys |= GestureModifierKeys.Control;
            if ((GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0) keys |= GestureModifierKeys.Shift;
            if ((GetAsyncKeyState(VK_MENU) & 0x8000) != 0) keys |= GestureModifierKeys.Alt;
            return keys;
        }
    }
}
