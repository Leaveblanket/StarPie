using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WinPieGestures.Services.Gestures
{
    /// <summary>
    /// Hook event argument: raw screen coordinates plus whether the event was
    /// consumed by gesture handling. Intentionally free of UI-framework types
    /// so the hook stays a pure adapter (ADR-0002).
    /// </summary>
    public class MouseHookEventArgs : EventArgs
    {
        public GesturePoint Position { get; }
        public bool Handled { get; set; }

        public MouseHookEventArgs(GesturePoint position)
        {
            Position = position;
            Handled = false;
        }
    }

    public class MouseHook
    {
        private const int WH_MOUSE_LL = 14;
        private const int WM_MOUSEMOVE = 0x0200;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_RBUTTONUP = 0x0205;
        private const int WM_MBUTTONDOWN = 0x0207;
        private const int WM_MBUTTONUP = 0x0208;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, uint dwExtraInfo);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT lpPoint);

        private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
        private const uint MOUSEEVENTF_MIDDLEUP = 0x0040;

        // Dev instances trigger on the middle button so they can coexist with the
        // installed release, which keeps the default right-button gesture.
        private readonly int _triggerDownMessage = DevInstance.IsActive ? WM_MBUTTONDOWN : WM_RBUTTONDOWN;
        private readonly int _triggerUpMessage = DevInstance.IsActive ? WM_MBUTTONUP : WM_RBUTTONUP;

        public bool IsPaused { get; set; } = false;

        public event EventHandler<MouseHookEventArgs> OnRightButtonDown;
        public event EventHandler<MouseHookEventArgs> OnRightButtonUp;
        public event EventHandler<MouseHookEventArgs> OnMouseMove;

        private LowLevelMouseProc _proc;
        private IntPtr _hookId = IntPtr.Zero;

        // Flags to prevent recursive hook interception when we replay right click events
        private bool _ignoreNextRButtonDown = false;
        private bool _ignoreNextRButtonUp = false;

        // Hook stability and health check variables
        private System.Threading.Timer _healthCheckTimer;
        private POINT _lastSystemCursorPos;
        private int _hookEventsCountSinceLastCheck = 0;

        public MouseHook()
        {
            _proc = HookCallback;
        }

        public void Start()
        {
            if (_hookId == IntPtr.Zero)
            {
                _hookId = SetHook(_proc);
                if (_hookId == IntPtr.Zero)
                {
                    throw new Exception("Failed to set low-level mouse hook.");
                }

                // Initialize health check
                _hookEventsCountSinceLastCheck = 0;
                GetCursorPos(out _lastSystemCursorPos);
                _healthCheckTimer = new System.Threading.Timer(CheckHookHealth, null, 3000, 3000);
            }
        }

        public void Stop()
        {
            if (_healthCheckTimer != null)
            {
                _healthCheckTimer.Dispose();
                _healthCheckTimer = null;
            }

            if (_hookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }
        }

        private void CheckHookHealth(object state)
        {
            if (_hookId == IntPtr.Zero) return;

            POINT currentPos;
            if (GetCursorPos(out currentPos))
            {
                bool mouseMoved = currentPos.x != _lastSystemCursorPos.x || currentPos.y != _lastSystemCursorPos.y;
                _lastSystemCursorPos = currentPos;

                if (mouseMoved)
                {
                    // If system mouse moved, but we received 0 hook events, hook is likely dead!
                    if (System.Threading.Interlocked.Exchange(ref _hookEventsCountSinceLastCheck, 0) == 0)
                    {
                        System.Windows.Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
                        {
                            Debug.WriteLine("Mouse hook health check failed. Re-registering hook...");
                            try
                            {
                                Stop();
                                Start();
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Failed to re-register hook: {ex.Message}");
                            }
                        }));
                    }
                }
                else
                {
                    // Reset count if mouse did not move to avoid false positive
                    System.Threading.Interlocked.Exchange(ref _hookEventsCountSinceLastCheck, 0);
                }
            }
        }

        private IntPtr SetHook(LowLevelMouseProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_MOUSE_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            System.Threading.Interlocked.Increment(ref _hookEventsCountSinceLastCheck);

            if (IsPaused)
            {
                return CallNextHookEx(_hookId, nCode, wParam, lParam);
            }

            if (nCode >= 0)
            {
                int message = (int)wParam;
                MSLLHOOKSTRUCT hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);

                if (message == _triggerDownMessage)
                {
                    if (_ignoreNextRButtonDown)
                    {
                        _ignoreNextRButtonDown = false;
                        return CallNextHookEx(_hookId, nCode, wParam, lParam);
                    }

                    var args = new MouseHookEventArgs(new GesturePoint(hookStruct.pt.x, hookStruct.pt.y));
                    OnRightButtonDown?.Invoke(this, args);
                    if (args.Handled)
                    {
                        return (IntPtr)1; // Block the event from propagating
                    }
                }
                else if (message == _triggerUpMessage)
                {
                    if (_ignoreNextRButtonUp)
                    {
                        _ignoreNextRButtonUp = false;
                        return CallNextHookEx(_hookId, nCode, wParam, lParam);
                    }

                    var args = new MouseHookEventArgs(new GesturePoint(hookStruct.pt.x, hookStruct.pt.y));
                    OnRightButtonUp?.Invoke(this, args);
                    if (args.Handled)
                    {
                        return (IntPtr)1; // Block the event from propagating
                    }
                }
                else if (message == WM_MOUSEMOVE)
                {
                    var args = new MouseHookEventArgs(new GesturePoint(hookStruct.pt.x, hookStruct.pt.y));
                    OnMouseMove?.Invoke(this, args);
                    if (args.Handled)
                    {
                        return (IntPtr)1; // Block the event from propagating
                    }
                }
            }

            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        /// <summary>
        /// Replays a right mouse click at the current position.
        /// Temporarily ignores our own hook to avoid infinite loop.
        /// </summary>
        public void ReplayRightClick()
        {
            _ignoreNextRButtonDown = true;
            _ignoreNextRButtonUp = true;
            if (DevInstance.IsActive)
            {
                mouse_event(MOUSEEVENTF_MIDDLEDOWN, 0, 0, 0, 0);
                mouse_event(MOUSEEVENTF_MIDDLEUP, 0, 0, 0, 0);
            }
            else
            {
                mouse_event(MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, 0);
                mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, 0);
            }
        }
    }
}
