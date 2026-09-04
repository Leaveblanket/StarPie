using System;
using System.Windows;

namespace WinPieGestures.Services.Shell
{
    /// <summary>
    /// Theme seam (ADR-0002/0013/#47): owns the app's current effective theme, applies
    /// theme changes through the single <see cref="SetTheme"/> entry point and drives the
    /// DWM title bar via <see cref="ApplyWindowTheme"/>. Palette swapping lives behind
    /// the host-level ThemePaletteManager callback; this service never touches Views.
    /// Injected into the settings window, the dialogs, the wheel factory and the tray;
    /// pages never hold IThemeService (ADR-0009 whitelist).
    /// </summary>
    public interface IThemeService
    {
        /// <summary>Theme applied by the last successful SetTheme call ("Light", "Dark",
        /// "MidnightNavy", "RoyalViolet", "TitaniumGray"); "Light" until the first apply.</summary>
        string CurrentEffectiveTheme { get; }

        /// <summary>Raised after the effective theme actually changes (single entry point
        /// contract: subscribers observe SetTheme only).</summary>
        event Action? ThemeChanged;

        /// <summary>Single state + resource entry point: resolves "System"/empty through
        /// the live Windows probe, records <see cref="CurrentEffectiveTheme"/>, triggers the
        /// host palette replacement and raises <see cref="ThemeChanged"/>. Re-applying the
        /// same effective theme is a no-op.</summary>
        void SetTheme(string themeName);

        /// <summary>Applies the current effective theme to a window's DWM title bar only
        /// (resources are already app-wide). Null root is safe and keeps state unchanged.</summary>
        void ApplyWindowTheme(FrameworkElement? rootElement);

        /// <summary>"System"/empty resolves to "Dark"/"Light" via the Windows setting;
        /// any other name passes through unchanged.</summary>
        string ResolveEffectiveTheme(string themeName);

        /// <summary>True when Windows itself is in dark mode (live registry read).</summary>
        bool IsWindowsInDarkTheme();
    }
}
