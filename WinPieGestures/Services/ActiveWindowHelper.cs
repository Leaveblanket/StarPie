using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace WinPieGestures
{
    public static class ActiveWindowHelper
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        /// <summary>
        /// Gets the process name of the active foreground window.
        /// Returns lowercase name with ".exe" extension (e.g., "chrome.exe").
        /// Returns "unknown.exe" on failure.
        /// </summary>
        public static string GetActiveWindowProcessName()
        {
            try
            {
                IntPtr hWnd = GetForegroundWindow();
                if (hWnd == IntPtr.Zero)
                    return "unknown.exe";

                uint processId;
                GetWindowThreadProcessId(hWnd, out processId);

                if (processId == 0)
                    return "unknown.exe";

                using (Process proc = Process.GetProcessById((int)processId))
                {
                    string processName = proc.ProcessName;
                    if (string.IsNullOrEmpty(processName))
                        return "unknown.exe";

                    return processName.ToLower() + ".exe";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to get active window process: {ex.Message}");
                return "unknown.exe";
            }
        }
    }
}
