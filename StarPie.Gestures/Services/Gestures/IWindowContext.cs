namespace StarPie.Services.Gestures
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
    /// 前台窗口上下文接缝：把活动窗口与全屏探测收在一个可注入表面后，
    /// 手势引擎做隔离与方案决策时不直接触碰 Win32/WPF。
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
