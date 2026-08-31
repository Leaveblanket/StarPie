using System.Windows;

namespace WinPieGestures.Services
{
    /// <summary>
    /// Theme seam (ADR-0002): owns the app's current effective theme, applies it to a
    /// window's resource tree and title bar, and resolves "follow system". Injected
    /// into the settings window, the tray, the wheel factory and the dialogs; the
    /// static AppThemeManager it replaces was deleted in T09.
    /// </summary>
    public interface IThemeService
    {
        /// <summary>Theme applied by the last successful ApplyTheme call ("Light", "Dark",
        /// "MidnightNavy", "RoyalViolet", "TitaniumGray"); "Light" until the first apply.</summary>
        string CurrentEffectiveTheme { get; }

        /// <summary>Applies the named app theme to the element's resource tree and its
        /// owner window's title bar; "System" resolves through the live Windows setting.</summary>
        void ApplyTheme(FrameworkElement? rootElement, string themeName);

        /// <summary>"System"/empty resolves to "Dark"/"Light" via the Windows setting;
        /// any other name passes through unchanged.</summary>
        string ResolveEffectiveTheme(string themeName);

        /// <summary>True when Windows itself is in dark mode (live registry read).</summary>
        bool IsWindowsInDarkTheme();
    }
}
