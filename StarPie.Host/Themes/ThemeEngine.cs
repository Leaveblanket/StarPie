using System;

namespace StarPie.Themes
{
    /// <summary>
    /// 界面主题引擎：持有当前请求/有效主题，提供有效主题解析与状态切换。
    /// </summary>
    /// <remarks>
    /// <see cref="SetTheme"/> 是唯一状态入口——解析、记录 <see cref="CurrentEffectiveTheme"/>、
    /// 并触发调色板整项替换（经附加的 <see cref="IThemeApplier"/> 回抛宿主）。
    /// Windows 深浅色探测可注入，生产默认值实时读 Personalize 注册表键，使「跟随系统」的
    /// 解析可 headless 单测；系统深浅色变化的监听与窗口效果属宿主层，变化时由调用方驱动
    /// <see cref="RefreshSystemTheme"/>。本类零 WPF。
    /// </remarks>
    public sealed class ThemeEngine
    {
        /// <summary>最近一次应用的有效主题；首次应用前为 "Light"。</summary>
        public string CurrentEffectiveTheme { get; private set; } = "Light";

        /// <summary>当前请求的主题名（"System"/空 = 跟随系统；固定名 = 不跟随）。</summary>
        public string RequestedTheme { get; private set; } = "System";

        private readonly Func<bool> _windowsInDarkModeProbe;
        private IThemeApplier? _applier;
        private bool _hasApplied;

        public ThemeEngine() : this(null)
        {
        }

        public ThemeEngine(Func<bool>? windowsInDarkModeProbe)
        {
            _windowsInDarkModeProbe = windowsInDarkModeProbe ?? ProbeWindowsDarkMode;
        }

        /// <summary>绑定调色板应用端口（宿主装配面）：<see cref="SetTheme"/> 时经该端口整项替换活动主题槽。</summary>
        public void AttachApplier(IThemeApplier applier) => _applier = applier;

        /// <summary>Windows 自身处于深色模式时为 true（实时注册表读取，非 Windows 平台恒为 false）。</summary>
        public bool IsWindowsInDarkTheme() => _windowsInDarkModeProbe();

        /// <summary>"System"/空值按实时 Windows 设置解析为 "Dark"/"Light"；其余名称原样通过。</summary>
        public string ResolveEffectiveTheme(string themeName)
        {
            if (string.Equals(themeName, "System", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(themeName))
            {
                return IsWindowsInDarkTheme() ? "Dark" : "Light";
            }

            return themeName;
        }

        /// <summary>设置请求的主题：解析有效主题后应用——记录 <see cref="CurrentEffectiveTheme"/>
        /// → 触发调色板整项替换。同一有效主题重复设置是 no-op；首次应用恒执行
        /// （保证 App.xaml 静态 Light 首帧后调色板也入活动主题槽）。</summary>
        public void SetTheme(string themeName)
        {
            RequestedTheme = string.IsNullOrEmpty(themeName) ? "System" : themeName;
            string effectiveTheme = ResolveEffectiveTheme(themeName);
            if (_hasApplied && string.Equals(CurrentEffectiveTheme, effectiveTheme, StringComparison.Ordinal)) return;

            CurrentEffectiveTheme = effectiveTheme;
            _applier?.ApplyTheme(effectiveTheme);
            _hasApplied = true;
        }

        /// <summary>若当前跟随系统，则按最新系统状态重新 <see cref="SetTheme"/>（有效主题未变时
        /// no-op）；固定主题下为 no-op。公开以便系统深浅色回调与注入探针单测共用同一路径。</summary>
        public void RefreshSystemTheme()
        {
            if (string.Equals(RequestedTheme, "System", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(RequestedTheme))
            {
                SetTheme("System");
            }
        }

        private static bool ProbeWindowsDarkMode()
        {
            if (!OperatingSystem.IsWindows()) return false;

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
