using StarPie.Sdk.Models;
using StarPie.Sdk.Services.Wheel;

namespace StarPie.Sdk.ViewModels.Wheel
{
    /// <summary>
    /// 轮盘外观只读状态接口：外观页实时预览渲染器 WheelPreviewRenderer 的唯一输入契约——主题风格/
    /// 配色方案选中与窄配色输入、几何与排版参数、核图标相关、预览所用 Profile 上下文。
    /// 不暴露整个运行态配置对象：渲染侧取配色数据只经 <see cref="PaletteInput"/>。
    /// </summary>
    /// <remarks>
    /// 实现方为轮盘模块外观设置子 VM（WheelAppearanceSettingsViewModel），经外观聚合 VM 的
    /// WheelAppearance 暴露给页面。接口只读，不暴露写入口/事件/命令，使渲染器与页面预览
    /// code-behind 不以具体聚合 VM/子 VM 类型为参数。
    /// </remarks>
    public interface IWheelAppearanceState
    {
        // ---- 主题风格与配色方案选中 -------------------------------------------------

        /// <summary>轮盘主题风格（ClassicRing / CleanSectors / Glassmorphism / CatPaw）。</summary>
        string WheelStyle { get; }

        /// <summary>轮盘配色方案（System/Dark/Light/固定方案或 CustomPreset_{id}）。</summary>
        string SelectedPalette { get; }

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

        // ---- 配色解析输入与预览所用 Profile 上下文 --------------------------------

        /// <summary>样式渲染器的窄配色输入：只含配色解析与光晕所需字段，
        /// 与运行态轮盘同类型、同组装入口。</summary>
        WheelPaletteInput PaletteInput { get; }

        /// <summary>预览渲染所用 Profile（优先选中方案，无选中时回落列表首项；空列表由渲染器兜底）。</summary>
        WheelProfile? PreviewProfile { get; }
    }
}
