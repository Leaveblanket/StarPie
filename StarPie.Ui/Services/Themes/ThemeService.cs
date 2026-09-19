using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using StarPie.Sdk.Services.Themes;
using Windows.UI.ViewManagement;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using StarPie.Sdk.Wpf.Services.Themes;

namespace StarPie.Ui.Services.Themes
{
    /// <summary>
    /// 界面主题服务（<see cref="IThemeService"/> 实现）：把内核主题引擎的有效主题应用到
    /// 窗口，并在"跟随系统"下监听 Windows 深浅色变化。
    /// </summary>
    /// <remarks>
    /// 主题状态与解析在 <see cref="ThemeEngine"/>（宿主内核，零 WPF）；本服务只承担
    /// WPF/WinRT 侧效果——DWM 标题栏深浅色应用与 <c>UISettings</c> 变化经 UI Dispatcher
    /// 封送后驱动引擎重解析。调色板整项替换经引擎的 <see cref="IThemeApplier"/> 端口由宿主
    /// 装配（Ui 侧调色板适配器），本服务不触碰视图资源。契约承诺的「首次应用前为
    /// <c>Light</c>」在本边界把引擎未应用态的 null 投影出来。
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

        public string CurrentEffectiveTheme => _engine.CurrentEffectiveTheme ?? AppThemeNames.Light;

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
        /// 深浅判定必须与调色板侧的回落同源：查主题目录的深色集合，未知/遗留主题名与 Light
        /// 一律浅色——按「非 Light 即暗」判定会配出浅色画刷 + 暗色标题栏。
        /// null root 安全且不改状态；SourceInitialized 前调用经事件兜底重试。</summary>
        public void ApplyWindowTheme(FrameworkElement? rootElement)
        {
            if (rootElement == null) return;

            bool isDark = AppThemeNames.DarkThemes.Contains(CurrentEffectiveTheme);
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
                ReadOnlySpan<byte> attribute = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref useDark, 1));
                // DWMWA_USE_IMMERSIVE_DARK_MODE：20（Win10 18985+ / Win11）；19 是 20H1 之前的旧取值，
                // SDK 枚举未收录，按原行为保留第二次调用（旧系统上生效）。
                _ = PInvoke.DwmSetWindowAttribute(new HWND(hwnd), DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE, attribute);
                _ = PInvoke.DwmSetWindowAttribute(new HWND(hwnd), (DWMWINDOWATTRIBUTE)19, attribute);
            }
            catch { }
        }
    }
}
