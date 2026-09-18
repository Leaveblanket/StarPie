using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace StarPie.Host.Gestures
{
    /// <summary>
    /// Win32 implementation of <see cref="IWindowContext"/>; merges the former
    /// ActiveWindowHelper and FullScreenHelper statics plus live modifier-key state.
    /// Win32 声明来自 CsWin32 源生成（清单为项目根 NativeMethods.txt，ADR-0051）。
    /// </summary>
    /// <remarks>
    /// 平台注解带版本号：生成 API 声明为 windows5.0，无版本号的 "windows" 会被分析器
    /// 视为低于该要求而触发 CA1416；本集为 net10.0 跨平台 TFM，注解即"这段需要 Windows"的事实声明。
    /// </remarks>
    [SupportedOSPlatform("windows10.0.19041.0")]
    public sealed class WindowContext : IWindowContext
    {
        public string GetForegroundProcessName()
        {
            try
            {
                HWND hWnd = PInvoke.GetForegroundWindow();
                if (hWnd.IsNull)
                    return "unknown.exe";

                _ = PInvoke.GetWindowThreadProcessId(hWnd, out uint processId);
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
            HWND hWnd = PInvoke.GetForegroundWindow();
            if (hWnd.IsNull) return false;

            // Exclude desktop background and shell manager
            if (hWnd == PInvoke.GetShellWindow() || hWnd == PInvoke.GetDesktopWindow()) return false;

            if (!PInvoke.GetWindowRect(hWnd, out RECT windowRect)) return false;

            HMONITOR hMonitor = PInvoke.MonitorFromWindow(hWnd, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
            if (hMonitor.IsNull) return false;

            MONITORINFO monitorInfo = default;
            monitorInfo.cbSize = (uint)Marshal.SizeOf<MONITORINFO>();

            if (!PInvoke.GetMonitorInfo(hMonitor, ref monitorInfo)) return false;

            // Full-screen when the window rect covers the entire monitor rect
            return windowRect.left <= monitorInfo.rcMonitor.left &&
                   windowRect.top <= monitorInfo.rcMonitor.top &&
                   windowRect.right >= monitorInfo.rcMonitor.right &&
                   windowRect.bottom >= monitorInfo.rcMonitor.bottom;
        }

        public GestureModifierKeys GetActiveModifierKeys()
        {
            GestureModifierKeys keys = GestureModifierKeys.None;
            if ((PInvoke.GetAsyncKeyState((int)VIRTUAL_KEY.VK_CONTROL) & 0x8000) != 0) keys |= GestureModifierKeys.Control;
            if ((PInvoke.GetAsyncKeyState((int)VIRTUAL_KEY.VK_SHIFT) & 0x8000) != 0) keys |= GestureModifierKeys.Shift;
            if ((PInvoke.GetAsyncKeyState((int)VIRTUAL_KEY.VK_MENU) & 0x8000) != 0) keys |= GestureModifierKeys.Alt;
            return keys;
        }
    }
}
