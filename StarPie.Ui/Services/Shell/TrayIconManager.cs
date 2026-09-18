using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using Windows.Win32;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.HiDpi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace StarPie.Ui.Services.Shell
{
    /// <summary>
    /// 托盘上下文菜单的一行。条目在每次菜单打开时由属主重新提供，因此标签
    /// （语言、暂停状态等）总是最新，无需刷新调用。
    /// </summary>
    public sealed class TrayMenuEntry
    {
        public string? Label;
        public bool IsHeader;
        public Action? Callback;

        /// <summary>菜单行的稳定发布标识（无障碍客户端与 e2e 据此定位条目）；分隔线/无标识条目为 null。</summary>
        public string? AutomationId;

        /// <summary>可点性。为假时条目灰显且不响应点击——用于"此刻不可用，且标签已写明原因"
        /// 的条目（不可用要看得见，不能给一个点了没反应的按钮）。</summary>
        public bool IsEnabled = true;

        public static TrayMenuEntry Header(string label) => new() { Label = label, IsHeader = true };
        public static TrayMenuEntry Separator() => new();
        public static TrayMenuEntry Item(string label, Action callback) => new() { Label = label, Callback = callback };
        public static TrayMenuEntry Item(string label, Action callback, bool enabled)
            => new() { Label = label, Callback = callback, IsEnabled = enabled };

        /// <summary>带稳定标识的可点条目（<see cref="AutomationId"/> 同时用于定位与点击目标）。</summary>
        public static TrayMenuEntry Item(string label, Action callback, string automationId)
            => new() { Label = label, Callback = callback, AutomationId = automationId };

        /// <summary>带稳定标识的可点条目；<paramref name="enabled"/> 为假时灰显不可点。</summary>
        public static TrayMenuEntry Item(string label, Action callback, bool enabled, string automationId)
            => new() { Label = label, Callback = callback, IsEnabled = enabled, AutomationId = automationId };
    }

    /// <summary>
    /// 纯 WPF 实现的系统托盘集成（WPF 无内置托盘支持）：经 Shell_NotifyIcon P/Invoke 与
    /// 隐藏消息窗口接收回调，用带主题的无边框 WPF 窗口作为上下文菜单。
    /// </summary>
    /// <remarks>
    /// 本类与 <see cref="TrayMenuEntry"/> 是模块公开面，由宿主 AppHost 装配
    /// （new + 菜单 provider），不反向引用宿主或其它模块内部。托盘菜单深色配色不直读
    /// 主题服务，改经组合根注入的 <c>Func&lt;bool&gt;</c> 深色探针（模块间同模式）。
    /// </remarks>
    public sealed class TrayIconManager : IDisposable
    {
        private const uint IconId = 1;
        private const int CallbackMessage = 0x8001; // WM_APP + 1
        private const int NIM_ADD = 0, NIM_MODIFY = 1, NIM_DELETE = 2;
        private const uint NIF_MESSAGE = 0x01, NIF_ICON = 0x02, NIF_TIP = 0x04, NIF_INFO = 0x10;
        private const int WM_LBUTTONDBLCLK = 0x0203;
        private const int WM_RBUTTONUP = 0x0205;
        private const int WM_CONTEXTMENU = 0x007B;
        private const uint NIIF_INFO = 0x01;

        /// <remarks>
        /// Shell_NotifyIcon 与 NOTIFYICONDATA 保留手写：元数据将其标记为架构特定（PInvoke005），
        /// AnyCPU 下 CsWin32 无法生成（白名单，ADR-0051）；如日后设为 PlatformTarget=x64 可回收。
        /// 其余声明来自 CsWin32 源生成（ADR-0051）。
        /// </remarks>
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

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool Shell_NotifyIcon(int dwMessage, ref NOTIFYICONDATA lpData);

        private readonly Func<bool> _windowsInDarkModeProbe;
        private readonly Action _onDoubleClick;
        private readonly Func<IReadOnlyList<TrayMenuEntry>> _menuProvider;
        private readonly HwndSource _source;
        private readonly int _taskbarCreatedMessage;
        private IntPtr _hIcon;
        private string _currentTip = string.Empty;
        private Window? _menuWindow;

        /// <summary>
        /// 托盘消息窗口的常驻窗口名（对外唯一定位面：第二实例按窗口名查找本进程的常驻 HWND）。
        /// <see cref="HwndSourceParameters"/> 的首参是窗口标题，窗口类名由 WPF 生成为
        /// <c>HwndWrapper[...]</c>——外部只能按标题定位。
        /// </summary>
        public const string WindowName = "StarPieTrayWindow";

        /// <summary>
        /// 托盘上下文菜单窗口的标题（无边框、不入任务栏，标题仅作发布标识/定位面：
        /// 菜单每次打开新建窗口，UIA 与 e2e 据此按窗口标题定位）。
        /// </summary>
        public const string MenuWindowName = "StarPieTrayMenu";

        public TrayIconManager(
            Func<bool> windowsInDarkModeProbe,
            Action onDoubleClick,
            Func<IReadOnlyList<TrayMenuEntry>> menuProvider)
        {
            _windowsInDarkModeProbe = windowsInDarkModeProbe ?? throw new ArgumentNullException(nameof(windowsInDarkModeProbe));
            _onDoubleClick = onDoubleClick;
            _menuProvider = menuProvider;

            // Explorer 崩溃/重启后重新注册图标
            _taskbarCreatedMessage = (int)PInvoke.RegisterWindowMessage("TaskbarCreated");

            // 隐藏弹出窗口，用于接收托盘图标回调
            var parameters = new HwndSourceParameters(WindowName, 0, 0)
            {
                WindowStyle = unchecked((int)0x80000000),      // WS_POPUP
                ExtendedWindowStyle = 0x00000080               // WS_EX_TOOLWINDOW: never in Alt+Tab
            };
            _source = new HwndSource(parameters);
            _source.AddHook(WndProc);

            _hIcon = LoadTrayIcon();
            AddIcon();
        }

        /// <summary>
        /// 托盘消息窗口句柄（常驻 HWND，进程存活期间不变）：常驻职责的窗口消息接收端挂在这里，
        /// 使这些消息不依赖设置台窗口是否存在。
        /// </summary>
        public IntPtr Handle => _source.Handle;

        /// <summary>挂窗口消息钩子（常驻职责的消息接收）；返回的委托可交给 <see cref="RemoveHook"/> 成对摘除。</summary>
        public void AddHook(HwndSourceHook hook) => _source.AddHook(hook);

        /// <summary>摘除 <see cref="AddHook"/> 挂上的窗口消息钩子。</summary>
        public void RemoveHook(HwndSourceHook hook) => _source.RemoveHook(hook);

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
                _ = PInvoke.DestroyIcon(new HICON(_hIcon));
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
            int size = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXSMICON);
            if (size <= 0) size = 16;
            try
            {
                string? exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                {
                    Span<HICON> icons = stackalloc HICON[1];
                    uint extracted = PInvoke.PrivateExtractIcons(exePath, 0, size, size, icons, out _, 0);
                    if (extracted > 0 && !icons[0].IsNull) return icons[0];
                }
            }
            catch { }
            return PInvoke.LoadIcon(default, PInvoke.IDI_APPLICATION);
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
            bool dark = _windowsInDarkModeProbe();

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
                Title = MenuWindowName,
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
            bool enabled = entry.IsEnabled;
            var text = new TextBlock
            {
                Text = entry.Label,
                FontSize = 12.5,
                Foreground = enabled
                    ? (dark
                        ? new SolidColorBrush(Color.FromArgb(0xF2, 0xFF, 0xFF, 0xFF))
                        : new SolidColorBrush(Color.FromArgb(0xF0, 0x1A, 0x1A, 0x1A)))
                    : (dark
                        ? new SolidColorBrush(Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF))
                        : new SolidColorBrush(Color.FromArgb(0x70, 0x1A, 0x1A, 0x1A))),
                Margin = new Thickness(14, 7, 18, 7)
            };
            // 条目标识挂在文本上：菜单行是 Border+TextBlock 组合（非 Control，无 Invoke 模式），
            // 文本是 UIA 树里的可见落点，点击经冒泡命中行回调。
            if (!string.IsNullOrEmpty(entry.AutomationId))
            {
                AutomationProperties.SetAutomationId(text, entry.AutomationId);
            }

            var row = new Border
            {
                Child = text,
                Background = Brushes.Transparent,
                Cursor = enabled ? Cursors.Hand : Cursors.Arrow,
                CornerRadius = new CornerRadius(5),
                Margin = new Thickness(6, 0, 6, 0)
            };
            if (!enabled)
            {
                // 不可用条目：无悬停反馈、不挂点击——原因写在标签里，不靠"点了没反应"传达。
                return row;
            }

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
            PInvoke.GetCursorPos(out System.Drawing.Point cursor);
            double scale = GetCursorMonitorScale(cursor) / 96.0;
            if (scale <= 0) scale = 1.0;

            double x = cursor.X / scale + 2;
            double y = cursor.Y / scale - height + 2; // 向上弹出（托盘通常在底部）

            var area = SystemParameters.WorkArea;
            if (x + width > area.Right + 4) x = area.Right - width + 2;
            if (x < area.Left) x = area.Left + 2;
            if (y < area.Top) y = area.Top + 2;                    // 任务栏在顶部时向下弹出
            if (y + height > area.Bottom + 4) y = area.Bottom - height + 2;

            window.Left = x;
            window.Top = y;
        }

        private static uint GetCursorMonitorScale(System.Drawing.Point cursor)
        {
            try
            {
                HMONITOR monitor = PInvoke.MonitorFromPoint(cursor, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
                if (!monitor.IsNull && PInvoke.GetDpiForMonitor(monitor, MONITOR_DPI_TYPE.MDT_EFFECTIVE_DPI, out uint dpiX, out _).Value == 0)
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
