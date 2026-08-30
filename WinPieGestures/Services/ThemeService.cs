using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace WinPieGestures
{
    using Application = System.Windows.Application;
    using Color = System.Windows.Media.Color;
    using ColorConverter = System.Windows.Media.ColorConverter;

    /// <summary>
    /// App theme application (T09): the runtime state and Win32 surface of the former
    /// static AppThemeManager, behind the IThemeService seam. The Windows dark-mode
    /// probe is injectable so "follow system" resolution is unit-testable; production
    /// reads the personalize registry key live, preserving per-decision behavior.
    /// </summary>
    public sealed class ThemeService : IThemeService
    {
        public string CurrentEffectiveTheme { get; private set; } = "Light";

        private readonly Func<bool> _windowsInDarkModeProbe;

        public ThemeService() : this(null)
        {
        }

        public ThemeService(Func<bool>? windowsInDarkModeProbe)
        {
            _windowsInDarkModeProbe = windowsInDarkModeProbe ?? ProbeWindowsDarkMode;
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

        public void ApplyTheme(FrameworkElement? rootElement, string themeName)
        {
            if (rootElement == null) return;

            string effectiveTheme = ResolveEffectiveTheme(themeName);

            CurrentEffectiveTheme = effectiveTheme;

            bool isDark = !string.Equals(effectiveTheme, "Light", StringComparison.OrdinalIgnoreCase);

            switch (effectiveTheme.ToLowerInvariant())
            {
                case "dark":
                case "obsidiandark":
                    SetThemeBrushes(rootElement,
                        windowBg: "#090D16",
                        sidebarBg: "#0F172A",
                        cardBg: "#131C2E",
                        cardBorder: "#1E293B",
                        textPrimary: "#F8FAFC",
                        textSecondary: "#94A3B8",
                        textMuted: "#64748B",
                        inputBg: "#0B1120",
                        inputBorder: "#27354E",
                        itemHover: "#1E293B",
                        subtleCard: "#0B1120",
                        navTabDefaultBg: "#00000000",
                        navTabDefaultFg: "#94A3B8",
                        navTabHoverBg: "#1E293B",
                        navTabHoverFg: "#F8FAFC",
                        navTabActiveBg: "#1E293B",
                        navTabActiveFg: "#60A5FA",
                        buttonDefaultBg: "#1E293B",
                        buttonDefaultFg: "#F8FAFC",
                        buttonDefaultBorder: "#334155",
                        buttonHoverBg: "#334155",
                        accentPrimary: "#3B82F6",
                        accentHover: "#60A5FA",
                        accentText: "#FFFFFF",
                        previewCanvasBg: "#0B1120",
                        previewCanvasBorder: "#1E293B",
                        previewGridLine: "#1E293B");
                    break;

                case "midnightnavy":
                    SetThemeBrushes(rootElement,
                        windowBg: "#0A0F1D",
                        sidebarBg: "#0F172A",
                        cardBg: "#141E33",
                        cardBorder: "#1F2E4D",
                        textPrimary: "#F0F6FC",
                        textSecondary: "#A5B4FC",
                        textMuted: "#6366F1",
                        inputBg: "#0B1224",
                        inputBorder: "#24365C",
                        itemHover: "#1E2C4F",
                        subtleCard: "#0B1224",
                        navTabDefaultBg: "#00000000",
                        navTabDefaultFg: "#A5B4FC",
                        navTabHoverBg: "#1E2C4F",
                        navTabHoverFg: "#F0F6FC",
                        navTabActiveBg: "#1E2C4F",
                        navTabActiveFg: "#38BDF8",
                        buttonDefaultBg: "#18243E",
                        buttonDefaultFg: "#F0F6FC",
                        buttonDefaultBorder: "#2B3E68",
                        buttonHoverBg: "#2B3E68",
                        accentPrimary: "#0EA5E9",
                        accentHover: "#38BDF8",
                        accentText: "#FFFFFF",
                        previewCanvasBg: "#0B1224",
                        previewCanvasBorder: "#1F2E4D",
                        previewGridLine: "#1F2E4D");
                    break;

                case "royalviolet":
                    SetThemeBrushes(rootElement,
                        windowBg: "#0F0A1A",
                        sidebarBg: "#170F28",
                        cardBg: "#1E1435",
                        cardBorder: "#322156",
                        textPrimary: "#FAF5FF",
                        textSecondary: "#D8B4FE",
                        textMuted: "#A855F7",
                        inputBg: "#130B22",
                        inputBorder: "#3D2766",
                        itemHover: "#2A1B4A",
                        subtleCard: "#130B22",
                        navTabDefaultBg: "#00000000",
                        navTabDefaultFg: "#D8B4FE",
                        navTabHoverBg: "#2A1B4A",
                        navTabHoverFg: "#FAF5FF",
                        navTabActiveBg: "#2A1B4A",
                        navTabActiveFg: "#E9D5FF",
                        buttonDefaultBg: "#251842",
                        buttonDefaultFg: "#FAF5FF",
                        buttonDefaultBorder: "#412970",
                        buttonHoverBg: "#3A2664",
                        accentPrimary: "#A855F7",
                        accentHover: "#C084FC",
                        accentText: "#FFFFFF",
                        previewCanvasBg: "#130B22",
                        previewCanvasBorder: "#322156",
                        previewGridLine: "#322156");
                    break;

                case "titaniumgray":
                    SetThemeBrushes(rootElement,
                        windowBg: "#121214",
                        sidebarBg: "#18181B",
                        cardBg: "#202024",
                        cardBorder: "#2E2E33",
                        textPrimary: "#F4F4F5",
                        textSecondary: "#A1A1AA",
                        textMuted: "#71717A",
                        inputBg: "#141416",
                        inputBorder: "#333338",
                        itemHover: "#27272A",
                        subtleCard: "#141416",
                        navTabDefaultBg: "#00000000",
                        navTabDefaultFg: "#A1A1AA",
                        navTabHoverBg: "#27272A",
                        navTabHoverFg: "#F4F4F5",
                        navTabActiveBg: "#27272A",
                        navTabActiveFg: "#E4E4E7",
                        buttonDefaultBg: "#27272A",
                        buttonDefaultFg: "#F4F4F5",
                        buttonDefaultBorder: "#3F3F46",
                        buttonHoverBg: "#3F3F46",
                        accentPrimary: "#3B82F6",
                        accentHover: "#60A5FA",
                        accentText: "#FFFFFF",
                        previewCanvasBg: "#141416",
                        previewCanvasBorder: "#2E2E33",
                        previewGridLine: "#2E2E33");
                    break;

                case "light":
                default:
                    SetThemeBrushes(rootElement,
                        windowBg: "#F8FAFC",
                        sidebarBg: "#FFFFFF",
                        cardBg: "#FFFFFF",
                        cardBorder: "#E2E8F0",
                        textPrimary: "#0F172A",
                        textSecondary: "#475569",
                        textMuted: "#64748B",
                        inputBg: "#FFFFFF",
                        inputBorder: "#CBD5E1",
                        itemHover: "#F1F5F9",
                        subtleCard: "#F8FAFC",
                        navTabDefaultBg: "#00000000",
                        navTabDefaultFg: "#475569",
                        navTabHoverBg: "#F1F5F9",
                        navTabHoverFg: "#0F172A",
                        navTabActiveBg: "#EFF6FF",
                        navTabActiveFg: "#2563EB",
                        buttonDefaultBg: "#FFFFFF",
                        buttonDefaultFg: "#334155",
                        buttonDefaultBorder: "#CBD5E1",
                        buttonHoverBg: "#F1F5F9",
                        accentPrimary: "#2563EB",
                        accentHover: "#1D4ED8",
                        accentText: "#FFFFFF",
                        previewCanvasBg: "#F1F5F9",
                        previewCanvasBorder: "#CBD5E1",
                        previewGridLine: "#E2E8F0");
                    break;
            }

            // Apply Immersive Dark Mode to Window titlebar
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

        private static void SetThemeBrushes(FrameworkElement root,
            string windowBg, string sidebarBg, string cardBg, string cardBorder,
            string textPrimary, string textSecondary, string textMuted,
            string inputBg, string inputBorder, string itemHover, string subtleCard,
            string navTabDefaultBg, string navTabDefaultFg, string navTabHoverBg, string navTabHoverFg, string navTabActiveBg, string navTabActiveFg,
            string buttonDefaultBg, string buttonDefaultFg, string buttonDefaultBorder, string buttonHoverBg,
            string accentPrimary, string accentHover, string accentText,
            string previewCanvasBg, string previewCanvasBorder, string previewGridLine)
        {
            SetBrush(root, "WindowBackgroundBrush", windowBg);
            SetBrush(root, "SidebarBackgroundBrush", sidebarBg);
            SetBrush(root, "CardBackgroundBrush", cardBg);
            SetBrush(root, "CardBorderBrush", cardBorder);
            SetBrush(root, "TextPrimaryBrush", textPrimary);
            SetBrush(root, "TextSecondaryBrush", textSecondary);
            SetBrush(root, "TextMutedBrush", textMuted);
            SetBrush(root, "InputBackgroundBrush", inputBg);
            SetBrush(root, "InputBorderBrush", inputBorder);
            SetBrush(root, "ItemHoverBrush", itemHover);
            SetBrush(root, "SubtleCardBrush", subtleCard);

            SetBrush(root, "NavTabDefaultBgBrush", navTabDefaultBg);
            SetBrush(root, "NavTabDefaultFgBrush", navTabDefaultFg);
            SetBrush(root, "NavTabHoverBgBrush", navTabHoverBg);
            SetBrush(root, "NavTabHoverFgBrush", navTabHoverFg);
            SetBrush(root, "NavTabActiveBgBrush", navTabActiveBg);
            SetBrush(root, "NavTabActiveFgBrush", navTabActiveFg);

            SetBrush(root, "ButtonDefaultBgBrush", buttonDefaultBg);
            SetBrush(root, "ButtonDefaultFgBrush", buttonDefaultFg);
            SetBrush(root, "ButtonDefaultBorderBrush", buttonDefaultBorder);
            SetBrush(root, "ButtonHoverBgBrush", buttonHoverBg);

            SetBrush(root, "AccentPrimaryBrush", accentPrimary);
            SetBrush(root, "AccentHoverBrush", accentHover);
            SetBrush(root, "AccentTextBrush", accentText);

            SetBrush(root, "PreviewCanvasBackgroundBrush", previewCanvasBg);
            SetBrush(root, "PreviewCanvasBorderBrush", previewCanvasBorder);
            SetBrush(root, "PreviewGridLineBrush", previewGridLine);
        }

        private static void SetBrush(FrameworkElement root, string key, string hex)
        {
            var brush = CreateSolidBrush(hex);
            root.Resources[key] = brush;
            if (Application.Current != null)
            {
                Application.Current.Resources[key] = brush;
            }
        }

        private static SolidColorBrush CreateSolidBrush(string hex)
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
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
