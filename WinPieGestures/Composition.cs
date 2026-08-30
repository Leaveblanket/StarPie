using System;
using System.Collections.Generic;
using System.Windows;

namespace WinPieGestures
{
    /// <summary>
    /// Manual composition root (ADR-0002): assembles the runtime services — mouse hook,
    /// gesture controller, tray icon and the settings window — in one place, replacing
    /// the implicit StartupUri instantiation. Also owns the app-level exit coordination
    /// (ADR-0003): tray exit disposes the tray and shuts the application down, while
    /// closing the settings window only hides it to the tray.
    /// </summary>
    internal sealed class Composition : IDisposable
    {
        private readonly MouseHook _mouseHook;
        // Kept alive for its hook-event subscriptions; the hook roots the controller.
        private readonly GestureController? _gestureController;
        private TrayIconManager? _trayIcon;
        private SettingsWindow? _settingsWindow;

        /// <summary>True while an app-level exit is in flight; the settings window
        /// consults it to close for real instead of hiding to the tray.</summary>
        public static bool IsExiting { get; private set; }

        public Composition()
        {
            _mouseHook = new MouseHook();

            _gestureController = new GestureController(_mouseHook);
        }

        /// <summary>Starts the mouse hook, creates the tray and the initial settings
        /// window and shows the latter — what StartupUri used to do implicitly, now
        /// with the assembly order under explicit control.</summary>
        public void Run()
        {
            _mouseHook.Start();

            _settingsWindow = new SettingsWindow(
                exitApplication: ExitApplication,
                showTrayBalloonTip: (title, text) => _trayIcon?.ShowBalloonTip(title, text));

            _trayIcon = new TrayIconManager(
                onDoubleClick: () => _settingsWindow.ShowSettings(0),
                menuProvider: BuildTrayMenuEntries);
            _trayIcon.SetTooltip(DefaultTooltip);

            _settingsWindow.Show();
        }

        public void Dispose()
        {
            _trayIcon?.Dispose();
            _trayIcon = null;
            _mouseHook.Stop();
        }

        private List<TrayMenuEntry> BuildTrayMenuEntries()
        {
            var entries = new List<TrayMenuEntry>
            {
                TrayMenuEntry.Header("StarPie v1.4.1" + DevInstance.Suffix),
                TrayMenuEntry.Separator()
            };

            string pauseText = _mouseHook.IsPaused ? I18n.T("TrayResume") : I18n.T("TrayPause");
            entries.Add(TrayMenuEntry.Item(pauseText, TogglePauseGestures));
            entries.Add(TrayMenuEntry.Item(I18n.T("TrayPreferences"), () => _settingsWindow?.ShowSettings(0)));
            entries.Add(TrayMenuEntry.Item(I18n.T("TrayAppearance"), () => _settingsWindow?.ShowSettings(1)));
            entries.Add(TrayMenuEntry.Item(I18n.T("TrayGestures"), () => _settingsWindow?.ShowSettings(2)));
            entries.Add(TrayMenuEntry.Item(I18n.T("TrayAbout"), () => _settingsWindow?.ShowSettings(4)));
            entries.Add(TrayMenuEntry.Item(I18n.T("TrayElevate"), () => _settingsWindow?.ElevateAndRestart()));
            entries.Add(TrayMenuEntry.Separator());
            entries.Add(TrayMenuEntry.Item(I18n.T("TrayExit"), ExitApplication));

            return entries;
        }

        private void TogglePauseGestures()
        {
            _mouseHook.IsPaused = !_mouseHook.IsPaused;
            _trayIcon?.SetTooltip(_mouseHook.IsPaused
                ? $"StarPie ({I18n.T("TrayPause")})"
                : DefaultTooltip);
        }

        private static string DefaultTooltip => I18n.T("TrayTooltip") + DevInstance.Suffix;

        private void ExitApplication()
        {
            try
            {
                _settingsWindow?.SavePendingChanges();
            }
            catch { }

            if (_trayIcon != null)
            {
                _trayIcon.Dispose();
                _trayIcon = null;
            }

            IsExiting = true;
            Application.Current.Shutdown();
        }
    }
}
