using System.Windows;

namespace StarPie.Services.Shell
{
    /// <summary>
    /// 界面主题服务接缝：拥有当前有效主题；主题变更经单一入口 <see cref="SetTheme"/> 应用，
    /// 并负责窗口 DWM 标题栏的深浅色切换（<see cref="ApplyWindowTheme"/>）。
    /// </summary>
    /// <remarks>
    /// 调色板整项替换由模块级 ThemePaletteManager 回调执行，本服务不触碰 Views。
    /// 消费方为宿主的窗口/对话框工厂与轮盘侧；页面不持有本服务（壳层 View 效果白名单）。
    /// </remarks>
    public interface IThemeService
    {
        /// <summary>最近一次成功 SetTheme 应用的有效主题（"Light"/"Dark"/"MidnightNavy"/
        /// "RoyalViolet"/"TitaniumGray"）；首次应用前为 "Light"。</summary>
        string CurrentEffectiveTheme { get; }

        /// <summary>唯一的状态/资源入口：经实时 Windows 探测解析 "System"/空值，记录
        /// <see cref="CurrentEffectiveTheme"/>，触发宿主调色板整项替换。
        /// 重复应用同一有效主题为 no-op。</summary>
        void SetTheme(string themeName);

        /// <summary>仅把当前有效主题应用到窗口的 DWM 标题栏（资源已是 App 级）。
        /// null root 安全且不改状态。</summary>
        void ApplyWindowTheme(FrameworkElement? rootElement);

        /// <summary>"System"/空值按 Windows 设置解析为 "Dark"/"Light"；其余名称原样通过。</summary>
        string ResolveEffectiveTheme(string themeName);

        /// <summary>Windows 自身处于深色模式时为 true（实时注册表读取）。</summary>
        bool IsWindowsInDarkTheme();
    }
}
