using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Windows.UI.ViewManagement;

namespace StarPie.Services.Shell
{
    /// <summary>
    /// 界面主题服务（<see cref="IThemeService"/> 实现）：持有当前有效主题，并提供
    /// IThemeService 接缝背后的 Win32 标题栏深浅色表面。
    /// </summary>
    /// <remarks>
    /// <see cref="SetTheme"/> 是唯一状态入口——解析、记录 <see cref="CurrentEffectiveTheme"/>、
    /// 并触发调色板整项替换（经附加的 applier）。主题调色板以
    /// XAML 存放于 Views/Styles/Themes，由同模块的 ThemePaletteManager 整项换入，
    /// 本服务不依赖 Views。Windows 深色探测可注入，使“跟随系统”解析可单测；
    /// 生产实现实时读 Personalize 注册表键。
    /// </remarks>
    public sealed class ThemeService : IThemeService
    {
        public string CurrentEffectiveTheme { get; private set; } = "Light";

        /// <summary>当前请求的主题名（"System"/空 = 跟随系统；固定名 = 不跟随）。</summary>
        public string RequestedTheme { get; private set; } = "System";

        private readonly Func<bool> _windowsInDarkModeProbe;
        private Action<string>? _paletteApplier;
        private bool _hasApplied;
        private UISettings? _systemThemeWatcher;

        public ThemeService() : this(null)
        {
        }

        public ThemeService(Func<bool>? windowsInDarkModeProbe)
        {
            _windowsInDarkModeProbe = windowsInDarkModeProbe ?? ProbeWindowsDarkMode;
        }

        /// <summary>绑定调色板应用回调：宿主 AppHost 构造后调用；SetTheme 时经该回调
        /// 整项替换 MergedDictionaries 的活动主题槽。</summary>
        public void AttachPaletteApplier(Action<string> paletteApplier)
        {
            _paletteApplier = paletteApplier;
        }

        public bool IsWindowsInDarkTheme() => _windowsInDarkModeProbe();

        /// <summary>"System"/空值按实时 Windows 设置解析为 "Dark"/"Light"；其余名称原样通过。</summary>
        public string ResolveEffectiveTheme(string themeName)
        {
            if (string.Equals(themeName, "System", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(themeName))
            {
                return IsWindowsInDarkTheme() ? "Dark" : "Light";
            }

            return themeName;
        }

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        /// <summary>设置请求的主题：解析有效主题后应用——记录 <see cref="CurrentEffectiveTheme"/>
        /// → 触发调色板整项替换。同一有效主题重复设置是
        /// no-op；首次应用恒执行（保证 App.xaml 静态 Light 首帧后调色板也入活动主题槽）。</summary>
        public void SetTheme(string themeName)
        {
            RequestedTheme = string.IsNullOrEmpty(themeName) ? "System" : themeName;
            string effectiveTheme = ResolveEffectiveTheme(themeName);
            if (_hasApplied && string.Equals(CurrentEffectiveTheme, effectiveTheme, StringComparison.Ordinal)) return;

            CurrentEffectiveTheme = effectiveTheme;
            _paletteApplier?.Invoke(effectiveTheme);
            _hasApplied = true;
        }

        /// <summary>开始监听 Windows 深浅色变化（宿主 AppHost.Run 在初始主题应用后调用；
        /// UISettings 实例保活至进程结束）。回调在后台线程触发，经 UI Dispatcher 封送后
        /// 仅当请求主题为 System/空时重新解析并换肤。</summary>
        public void EnableSystemThemeTracking()
        {
            if (_systemThemeWatcher != null) return;
            var watcher = new UISettings();
            watcher.ColorValuesChanged += (_, _) =>
            {
                Dispatcher? dispatcher = Application.Current?.Dispatcher;
                if (dispatcher != null && !dispatcher.CheckAccess())
                {
                    dispatcher.InvokeAsync(RefreshSystemTheme);
                }
                else
                {
                    RefreshSystemTheme();
                }
            };
            _systemThemeWatcher = watcher;
        }

        /// <summary>若当前跟随系统，则按最新系统状态重新 SetTheme("System")（有效主题未变时 no-op）；
        /// 固定主题下为 no-op。公开以便系统回调与注入探针单测共用同一路径。</summary>
        public void RefreshSystemTheme()
        {
            if (string.Equals(RequestedTheme, "System", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(RequestedTheme))
            {
                SetTheme("System");
            }
        }

        /// <summary>把当前有效主题应用到窗口 DWM 标题栏（资源已是 App 级，无需重复换入）。
        /// null root 安全且不改状态；SourceInitialized 前调用经事件兜底重试。</summary>
        public void ApplyWindowTheme(FrameworkElement? rootElement)
        {
            if (rootElement == null) return;

            bool isDark = !string.Equals(CurrentEffectiveTheme, "Light", StringComparison.OrdinalIgnoreCase);
            var window = rootElement as Window ?? Window.GetWindow(rootElement);
            if (window != null)
            {
                SetWindowDarkMode(window, isDark);
            }
        }

        private void SetWindowDarkMode(Window window, bool isDark)
        {
            if (window == null) return;
            try
            {
                var helper = new WindowInteropHelper(window);
                IntPtr hwnd = helper.Handle;
                if (hwnd == IntPtr.Zero)
                {
                    window.SourceInitialized += (s, e) => SetWindowDarkMode(window, isDark);
                    return;
                }

                int useDark = isDark ? 1 : 0;
                // DWMWA_USE_IMMERSIVE_DARK_MODE：20（Win10 18985+ / Win11），19（旧版 Win10）
                DwmSetWindowAttribute(hwnd, 20, ref useDark, sizeof(int));
                DwmSetWindowAttribute(hwnd, 19, ref useDark, sizeof(int));
            }
            catch { }
        }

        private static bool ProbeWindowsDarkMode()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                if (key != null)
                {
                    object? val = key.GetValue("AppsUseLightTheme");
                    if (val is int intVal)
                    {
                        return intVal == 0;
                    }
                }
            }
            catch { }
            return false;
        }
    }
}
