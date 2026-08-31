namespace WinPieGestures.Services
{
    [Flags]
    public enum GestureModifierKeys
    {
        None = 0,
        Control = 1,
        Shift = 2,
        Alt = 4,
    }

    /// <summary>
    /// Foreground-window context seam (ADR-0002): merges ActiveWindowHelper and
    /// FullScreenHelper behind one injectable surface so the gesture engine can
    /// make its isolation and profile decisions without any Win32 or WPF calls.
    /// </summary>
    public interface IWindowContext
    {
        /// <summary>Process name of the foreground window, lowercase with ".exe"
        /// suffix (e.g. "chrome.exe"); "unknown.exe" when it cannot be determined.</summary>
        string GetForegroundProcessName();

        /// <summary>True when the foreground window covers its entire monitor.</summary>
        bool IsForegroundFullScreen();

        /// <summary>Modifier keys currently held down (queried live, per event).</summary>
        GestureModifierKeys GetActiveModifierKeys();
    }
}
