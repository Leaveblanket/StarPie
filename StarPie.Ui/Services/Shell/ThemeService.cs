using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Windows.UI.ViewManagement;

namespace StarPie.Services.Shell
{
    /// <summary>
    /// 界面主题服务（<see cref="IThemeService"/> 实现）：把内核主题引擎的有效主题应用到
    /// 窗口，并在"跟随系统"下监听 Windows 深浅色变化。
    /// </summary>
    /// <remarks>
    /// 主题状态与解析在 <see cref="ThemeEngine"/>（宿主内核，零 WPF）；本服务只承担
    /// WPF/WinRT 侧效果——DWM 标题栏深浅色应用与 <c>UISettings</c> 变化经 UI Dispatcher
    /// 封送后驱动引擎重解析。调色板整项替换经引擎的 <see cref="IThemeApplier"/> 端口由宿主
    /// 装配（Ui 侧调色板适配器），本服务不触碰视图资源。
    /// </remarks>
    public sealed class ThemeService : IThemeService
    {
        private readonly ThemeEngine _engine;
        private UISettings? _systemThemeWatcher;

        public ThemeService() : this(null)
        {
        }

        public ThemeService(Func<bool>? windowsInDarkModeProbe)
        {
            _engine = new ThemeEngine(windowsInDarkModeProbe);
        }

        /// <summary>当前请求的主题名（"System"/空 = 跟随系统；固定名 = 不跟随）。</summary>
        public string RequestedTheme => _engine.RequestedTheme;

        public string CurrentEffectiveTheme => _engine.CurrentEffectiveTheme;

        /// <summary>绑定调色板应用端口（宿主装配面）：<see cref="SetTheme"/> 时经该端口整项替换活动主题槽。</summary>
        public void AttachApplier(IThemeApplier applier) => _engine.AttachApplier(applier);

        public bool IsWindowsInDarkTheme() => _engine.IsWindowsInDarkTheme();

        /// <summary>"System"/空值按实时 Windows 设置解析为 "Dark"/"Light"；其余名称原样通过。</summary>
        public string ResolveEffectiveTheme(string themeName) => _engine.ResolveEffectiveTheme(themeName);

        /// <summary>设置请求的主题：解析、记录有效主题并触发调色板整项替换；
        /// 同一有效主题重复设置是 no-op，首次应用恒执行。</summary>
        public void SetTheme(string themeName) => _engine.SetTheme(themeName);

        /// <summary>若当前跟随系统，则按最新系统状态重新解析并换肤（固定主题下为 no-op）。</summary>
        public void RefreshSystemTheme() => _engine.RefreshSystemTheme();

        /// <summary>开始监听 Windows 深浅色变化（宿主在初始主题应用后调用；
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

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
    }
}
