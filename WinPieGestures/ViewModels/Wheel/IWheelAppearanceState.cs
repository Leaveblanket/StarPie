namespace WinPieGestures.ViewModels.Wheel
{
    /// <summary>
    /// 轮盘外观只读状态接口（ADR-0014 决策 8，#55）：外观页实时预览渲染器
    /// <c>WheelPreviewRenderer</c> 的唯一输入契约——皮肤/配色方案选中、几何与排版参数、
    /// 核图标相关、当前运行配置与预览所用 Profile 上下文。
    /// 实现方当前为外观聚合页 VM（<c>AppearanceSettingsViewModel</c>，临时承接口径）；
    /// #56 抽取轮盘外观设置子 VM 后改由其实现。接口只读，不暴露写入口/事件/命令，
    /// 以便渲染器与页面预览 code-behind 不以具体聚合 VM 类型为参数。
    /// </summary>
    public interface IWheelAppearanceState
    {
        // ---- 皮肤与配色方案选中 -------------------------------------------------

        /// <summary>轮盘皮肤（ClassicRing / CleanSectors / Glassmorphism / CatPaw）。</summary>
        string UiStyle { get; }

        /// <summary>轮盘配色方案（System/Dark/Light/固定方案或 CustomPreset_{id}）。</summary>
        string SelectedTheme { get; }

        // ---- 几何与排版参数 -----------------------------------------------------

        /// <summary>扇区切削形态（Original / Circle / RoundedCapsule / HexagonHive）。</summary>
        string Shape { get; }

        double WheelRadius { get; }

        double InnerRadius { get; }

        double CoreRadius { get; }

        double SectorGap { get; }

        double SectorCornerRadius { get; }

        /// <summary>排版模式（IconAndText / IconOnly / TextOnly）。</summary>
        string IconLayoutMode { get; }

        bool ShowText { get; }

        double SectorIconSize { get; }

        double SectorFontSize { get; }

        // ---- 核图标相关 ---------------------------------------------------------

        bool ShowCoreIcon { get; }

        string CoreIconType { get; }

        string CoreCustomIconKey { get; }

        string CoreCustomIconSvg { get; }

        string CoreCustomImagePath { get; }

        // ---- 当前运行配置与预览所用 Profile 上下文 --------------------------------

        /// <summary>运行态配置（样式渲染器 Initialize 消费：主题/配色解析与光晕等）。</summary>
        AppConfig CurrentConfig { get; }

        /// <summary>预览渲染所用 Profile（优先选中方案，无选中时回落列表首项；空列表由渲染器兜底）。</summary>
        WheelProfile? PreviewProfile { get; }
    }
}
