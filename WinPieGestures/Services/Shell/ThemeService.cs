using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace WinPieGestures.Services.Shell
{
    /// <summary>
    /// App theme application (T09/ADR-0012): owns the current effective theme and the
    /// Win32 title-bar surface of the former static AppThemeManager, behind the
    /// IThemeService seam. Theme palettes live as XAML under Views/Styles/Themes and
    /// are applied to the Application resource tree by the host (AppHost) — this
    /// service only resolves, records state and drives the DWM title bar, so it never
    /// depends on Views. The Windows dark-mode probe is injectable so "follow system"
    /// resolution is unit-testable; production reads the personalize registry key live.
    /// </summary>
    public sealed class ThemeService : IThemeService
    {
        public string CurrentEffectiveTheme { get; private set; } = "Light";

        private readonly Func<bool> _windowsInDarkModeProbe;
        private Action<string>? _paletteApplier;

        public ThemeService() : this(null)
        {
        }

        public ThemeService(Func<bool>? windowsInDarkModeProbe)
        {
            _windowsInDarkModeProbe = windowsInDarkModeProbe ?? ProbeWindowsDarkMode;
        }

        /// <summary>绑定宿主层调色板应用回调（ADR-0012）：AppHost 构造后调用；主题切换时由
        /// ApplyTheme 触发，宿主把 Views/Styles/Themes 调色板写入 Application 资源。</summary>
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

        /// <summary>应用主题：解析有效主题名 → 记录状态 → 触发宿主调色板应用（App 级资源，
        /// ADR-0012）→ 设置窗口标题栏深色模式。rootElement 仅用于定位标题栏窗口。</summary>
        public void ApplyTheme(FrameworkElement? rootElement, string themeName)
        {
            if (rootElement == null) return;

            string effectiveTheme = ResolveEffectiveTheme(themeName);

            CurrentEffectiveTheme = effectiveTheme;

            _paletteApplier?.Invoke(effectiveTheme);

            bool isDark = !string.Equals(effectiveTheme, "Light", StringComparison.OrdinalIgnoreCase);
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
