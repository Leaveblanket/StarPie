using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace WinPieGestures.Services.Shell
{
    /// <summary>
    /// App theme service (T09/ADR-0012/ADR-0013 #47): owns the current effective theme and
    /// the Win32 title-bar surface behind the IThemeService seam. <see cref="SetTheme"/> is
    /// the single state entry point — it resolves, records <see cref="CurrentEffectiveTheme"/>,
    /// raises <see cref="ThemeChanged"/> and triggers the host palette replacement via the
    /// attached applier. Theme palettes live as XAML under Views/Styles/Themes and are swapped
    /// wholesale by ThemePaletteManager (host layer), so this service never depends on Views.
    /// The Windows dark-mode probe is injectable so "follow system" resolution is
    /// unit-testable; production reads the personalize registry key live.
    /// </summary>
    public sealed class ThemeService : IThemeService
    {
        public string CurrentEffectiveTheme { get; private set; } = "Light";

        public event Action? ThemeChanged;

        private readonly Func<bool> _windowsInDarkModeProbe;
        private Action<string>? _paletteApplier;
        private bool _hasApplied;

        public ThemeService() : this(null)
        {
        }

        public ThemeService(Func<bool>? windowsInDarkModeProbe)
        {
            _windowsInDarkModeProbe = windowsInDarkModeProbe ?? ProbeWindowsDarkMode;
        }

        /// <summary>绑定宿主层调色板应用回调（ADR-0012/0013）：AppHost 构造后调用；SetTheme 时
        /// 宿主经 ThemePaletteManager 整项替换 MergedDictionaries 活动主题槽。</summary>
        internal void AttachPaletteApplier(Action<string> paletteApplier)
        {
            _paletteApplier = paletteApplier;
        }

        public bool IsWindowsInDarkTheme() => _windowsInDarkModeProbe();

        /// <summary>"System"/empty resolves to "Dark"/"Light" via the live Windows
        /// setting; any other name passes through unchanged.</summary>
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

        /// <summary>唯一状态/资源入口（ADR-0013）：解析 → 记录 <see cref="CurrentEffectiveTheme"/>
        /// → 触发宿主调色板整项替换 → 广播 <see cref="ThemeChanged"/>。同一有效主题重复设置是
        /// no-op；首次应用恒执行（保证 App.xaml 静态 Light 首帧后 manager 调色板也入槽）。</summary>
        public void SetTheme(string themeName)
        {
            string effectiveTheme = ResolveEffectiveTheme(themeName);
            if (_hasApplied && string.Equals(CurrentEffectiveTheme, effectiveTheme, StringComparison.Ordinal)) return;

            CurrentEffectiveTheme = effectiveTheme;
            _paletteApplier?.Invoke(effectiveTheme);
            _hasApplied = true;
            ThemeChanged?.Invoke();
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
                // DWMWA_USE_IMMERSIVE_DARK_MODE = 20 (Win10 18985+ / Win11), 19 (older Win10)
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
