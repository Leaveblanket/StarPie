using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace WinPieGestures
{
    /// <summary>
    /// One row of the tray context menu. Entries are supplied by the owner on every
    /// menu open, so labels (language, pause state) are always fresh without refresh calls.
    /// </summary>
    internal sealed class TrayMenuEntry
    {
        public string? Label;
        public bool IsHeader;
        public Action? Callback;

        public static TrayMenuEntry Header(string label) => new() { Label = label, IsHeader = true };
        public static TrayMenuEntry Separator() => new();
        public static TrayMenuEntry Item(string label, Action callback) => new() { Label = label, Callback = callback };
    }

    /// <summary>
    /// Pure WPF replacement for the WinForms NotifyIcon tray integration (WPF has no
    /// built-in tray support): Shell_NotifyIcon interop with a hidden message window
    /// for callbacks, and a themed borderless WPF window as the context menu.
    /// </summary>
    internal sealed class TrayIconManager : IDisposable
    {
        private const uint IconId = 1;
        private const int CallbackMessage = 0x8001; // WM_APP + 1
        private const int NIM_ADD = 0, NIM_MODIFY = 1, NIM_DELETE = 2;
        private const uint NIF_MESSAGE = 0x01, NIF_ICON = 0x02, NIF_TIP = 0x04, NIF_INFO = 0x10;
        private const int WM_LBUTTONDBLCLK = 0x0203;
        private const int WM_RBUTTONUP = 0x0205;
        private const int WM_CONTEXTMENU = 0x007B;
        private const uint NIIF_INFO = 0x01;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NOTIFYICONDATA
        {
            public int cbSize;
            public IntPtr hWnd;
            public uint uID;
            public uint uFlags;
            public uint uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
            public uint dwState;
            public uint dwStateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
            public uint uTimeout;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
            public uint dwInfoFlags;
            public Guid guidItem;
            public IntPtr hBalloonIcon;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool Shell_NotifyIcon(int dwMessage, ref NOTIFYICONDATA lpData);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint PrivateExtractIcons(string lpszFile, int nIconIndex, int cxIcon, int cyIcon, [Out] IntPtr[] phicon, [Out] uint[] piconid, uint nIcons, uint nFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int RegisterWindowMessage(string lpString);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(POINT pt, uint uFlags);

        [DllImport("shcore.dll")]
        private static extern int GetDpiForMonitor(IntPtr hMonitor, int dpiType, out uint dpiX, out uint dpiY);

        private const int SM_CXSMICON = 49;
        private const int IDI_APPLICATION = 32512;

        private readonly Action _onDoubleClick;
        private readonly Func<IReadOnlyList<TrayMenuEntry>> _menuProvider;
        private readonly HwndSource _source;
        private readonly int _taskbarCreatedMessage;
        private IntPtr _hIcon;
        private string _currentTip = string.Empty;
        private Window? _menuWindow;

        public TrayIconManager(Action onDoubleClick, Func<IReadOnlyList<TrayMenuEntry>> menuProvider)
        {
            _onDoubleClick = onDoubleClick;
            _menuProvider = menuProvider;

            // Re-register after an Explorer crash/restart
            _taskbarCreatedMessage = RegisterWindowMessage("TaskbarCreated");

            // Hidden popup window that receives the tray icon callbacks
            var parameters = new HwndSourceParameters("StarPieTrayWindow", 0, 0)
            {
                WindowStyle = unchecked((int)0x80000000),      // WS_POPUP
                ExtendedWindowStyle = 0x00000080               // WS_EX_TOOLWINDOW: never in Alt+Tab
            };
            _source = new HwndSource(parameters);
            _source.AddHook(WndProc);

            _hIcon = LoadTrayIcon();
            AddIcon();
        }

        public void SetTooltip(string tip)
        {
            _currentTip = Truncate(tip, 127);
            var data = BaseData();
            data.uFlags = NIF_TIP;
            data.szTip = _currentTip;
            Shell_NotifyIcon(NIM_MODIFY, ref data);
        }

        public void ShowBalloonTip(string title, string text)
        {
            var data = BaseData();
            data.uFlags = NIF_INFO;
            data.szInfoTitle = Truncate(title, 63);
            data.szInfo = Truncate(text, 255);
            data.dwInfoFlags = NIIF_INFO;
            Shell_NotifyIcon(NIM_MODIFY, ref data);
        }

        public void Dispose()
        {
            try { _menuWindow?.Close(); } catch { }
            try
            {
                var data = BaseData();
                Shell_NotifyIcon(NIM_DELETE, ref data);
            }
            catch { }
            if (_hIcon != IntPtr.Zero)
            {
                DestroyIcon(_hIcon);
                _hIcon = IntPtr.Zero;
            }
            _source.RemoveHook(WndProc);
            _source.Dispose();
        }

        private void AddIcon()
        {
            var data = BaseData();
            data.szTip = _currentTip;
            Shell_NotifyIcon(NIM_ADD, ref data);
        }

        private NOTIFYICONDATA BaseData()
        {
            return new NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = _source.Handle,
                uID = IconId,
                uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
                uCallbackMessage = (uint)CallbackMessage,
                hIcon = _hIcon,
                szTip = _currentTip,
                szInfo = string.Empty,
                szInfoTitle = string.Empty
            };
        }

        private static IntPtr LoadTrayIcon()
        {
            int size = GetSystemMetrics(SM_CXSMICON);
            if (size <= 0) size = 16;
            try
            {
                string? exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                {
                    var icons = new IntPtr[1];
                    uint extracted = PrivateExtractIcons(exePath, 0, size, size, icons, new uint[1], 1, 0);
                    if (extracted > 0 && icons[0] != IntPtr.Zero) return icons[0];
                }
            }
            catch { }
            return LoadIcon(IntPtr.Zero, (IntPtr)IDI_APPLICATION);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == _taskbarCreatedMessage)
            {
                AddIcon();
                return IntPtr.Zero;
            }

            if (msg == CallbackMessage)
            {
                switch (lParam.ToInt64())
                {
                    case WM_LBUTTONDBLCLK:
                        _onDoubleClick();
                        handled = true;
                        break;
                    case WM_RBUTTONUP:
                    case WM_CONTEXTMENU:
                        ShowMenu();
                        handled = true;
                        break;
                }
            }
            return IntPtr.Zero;
        }

        private void ShowMenu()
        {
            if (_menuWindow != null) { _menuWindow.Close(); _menuWindow = null; }

            var entries = _menuProvider();
            bool dark = AppThemeManager.IsWindowsInDarkTheme();

            var panel = new StackPanel { MinWidth = 214 };
            foreach (var entry in entries)
            {
                panel.Children.Add(
                    entry.IsHeader ? BuildHeader(entry, dark) :
                    entry.Callback == null ? BuildSeparator(dark) :
                    BuildItem(entry, dark));
            }

            var surface = new Border
            {
                Child = panel,
                Background = dark
                    ? new SolidColorBrush(Color.FromArgb(0xF5, 0x22, 0x22, 0x24))
                    : new SolidColorBrush(Color.FromArgb(0xF8, 0xFA, 0xFA, 0xFC)),
                BorderBrush = dark
                    ? new SolidColorBrush(Color.FromArgb(0x3C, 0xFF, 0xFF, 0xFF))
                    : new SolidColorBrush(Color.FromArgb(0x22, 0x00, 0x00, 0x00)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(0, 6, 0, 6),
                Effect = new DropShadowEffect { BlurRadius = 16, ShadowDepth = 2, Opacity = 0.35 }
            };

            var root = new Grid { Margin = new Thickness(10) };
            root.Children.Add(surface);
            root.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double width = Math.Max(root.DesiredSize.Width, 234);
            double height = root.DesiredSize.Height;

            var window = new Window
            {
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                ShowInTaskbar = false,
                Topmost = true,
                ResizeMode = ResizeMode.NoResize,
                ShowActivated = true,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Width = width,
                Height = height,
                Content = root
            };
            window.Deactivated += (s, e) => window.Close();
            window.PreviewKeyDown += (s, e) => { if (e.Key == Key.Escape) window.Close(); };
            window.Closed += (s, e) => { if (ReferenceEquals(_menuWindow, window)) _menuWindow = null; };

            PositionAtCursor(window, width, height);

            _menuWindow = window;
            window.Show();
            window.Activate();
        }

        private UIElement BuildHeader(TrayMenuEntry entry, bool dark)
        {
            return new TextBlock
            {
                Text = entry.Label,
                FontSize = 12.5,
                FontWeight = FontWeights.Bold,
                Foreground = dark
                    ? new SolidColorBrush(Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF))
                    : new SolidColorBrush(Color.FromArgb(0xAA, 0x00, 0x00, 0x00)),
                Margin = new Thickness(14, 5, 18, 7),
                IsHitTestVisible = false
            };
        }

        private static UIElement BuildSeparator(bool dark)
        {
            return new Border
            {
                Height = 1,
                Background = dark
                    ? new SolidColorBrush(Color.FromArgb(0x28, 0xFF, 0xFF, 0xFF))
                    : new SolidColorBrush(Color.FromArgb(0x1E, 0x00, 0x00, 0x00)),
                Margin = new Thickness(10, 4, 10, 4),
                IsHitTestVisible = false
            };
        }

        private UIElement BuildItem(TrayMenuEntry entry, bool dark)
        {
            var hoverBrush = dark
                ? new SolidColorBrush(Color.FromArgb(0x25, 0xFF, 0xFF, 0xFF))
                : new SolidColorBrush(Color.FromArgb(0x14, 0x00, 0x00, 0x00));
            var text = new TextBlock
            {
                Text = entry.Label,
                FontSize = 12.5,
                Foreground = dark
                    ? new SolidColorBrush(Color.FromArgb(0xF2, 0xFF, 0xFF, 0xFF))
                    : new SolidColorBrush(Color.FromArgb(0xF0, 0x1A, 0x1A, 0x1A)),
                Margin = new Thickness(14, 7, 18, 7)
            };
            var row = new Border
            {
                Child = text,
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                CornerRadius = new CornerRadius(5),
                Margin = new Thickness(6, 0, 6, 0)
            };
            row.MouseEnter += (s, e) => row.Background = hoverBrush;
            row.MouseLeave += (s, e) => row.Background = Brushes.Transparent;
            row.MouseLeftButtonUp += (s, e) =>
            {
                var callback = entry.Callback;
                _menuWindow?.Close();
                if (callback != null)
                {
                    _source.Dispatcher.BeginInvoke(callback, DispatcherPriority.Normal);
                }
            };
            return row;
        }

        private static void PositionAtCursor(Window window, double width, double height)
        {
            GetCursorPos(out POINT pt);
            double scale = GetCursorMonitorScale(pt) / 96.0;
            if (scale <= 0) scale = 1.0;

            double x = pt.x / scale + 2;
            double y = pt.y / scale - height + 2; // opens upward (tray usually at the bottom)

            var area = SystemParameters.WorkArea;
            if (x + width > area.Right + 4) x = area.Right - width + 2;
            if (x < area.Left) x = area.Left + 2;
            if (y < area.Top) y = area.Top + 2;                    // taskbar at top: open downward
            if (y + height > area.Bottom + 4) y = area.Bottom - height + 2;

            window.Left = x;
            window.Top = y;
        }

        private static uint GetCursorMonitorScale(POINT pt)
        {
            try
            {
                IntPtr monitor = MonitorFromPoint(pt, 2 /* MONITOR_DEFAULTTONEAREST */);
                if (monitor != IntPtr.Zero && GetDpiForMonitor(monitor, 0 /* MDT_EFFECTIVE_DPI */, out uint dpiX, out _) == 0)
                {
                    return dpiX;
                }
            }
            catch { }
            return 96;
        }

        private static string Truncate(string value, int maxChars)
        {
            return string.IsNullOrEmpty(value) || value.Length <= maxChars ? value : value.Substring(0, maxChars);
        }
    }
}
