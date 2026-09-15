using System.Collections.Generic;
using StarPie.Models;

namespace StarPie.Services.Wheel
{
    /// <summary>
    /// 配色解析与样式渲染的窄输入：只含解析方案名与自定义预调味所需的配置字段，
    /// 不携带整个 <see cref="AppConfig"/>（ADR-0044 决策 4）。
    /// </summary>
    /// <remarks>
    /// 运行态与预览态共用同一个类型与同一个组装入口 <see cref="FromConfig"/>——不给预览侧自建
    /// 「设置页对应物」，否则同一 <c>Initialize</c> 会被两个来源不同的类型各喂一份。
    /// 组装即复制：持有的是快照，配置后续变更不会回流到已构造的实例（ADR-0044 决策 3）。
    /// 落地在 Sdk 而非宿主内核：样式渲染契约（<c>IRadialStyleRenderer</c>）与只读外观状态接口
    /// （<c>IWheelAppearanceState</c>）都要引用它，而 SDK 是最下层、不能被反向引用。
    /// </remarks>
    public sealed class WheelPaletteInput
    {
        /// <summary>空输入：无自定义预设、无微调值、光晕按各风格默认。</summary>
        public static readonly WheelPaletteInput Empty = new WheelPaletteInput(
            new List<CustomColorPreset>(),
            customSectorBg: null,
            customSectorBorder: null,
            customHighlightBg: null,
            customHighlightBorder: null,
            customText: null,
            highlightGlowColor: null,
            highlightGlowRadius: 0.0,
            highlightGlowOpacity: -1.0);

        /// <summary>用户保存的自定义配色预设快照（可能为空列表，不为 null）。</summary>
        public IReadOnlyList<CustomColorPreset> CustomPresets { get; }

        public string? CustomSectorBg { get; }

        public string? CustomSectorBorder { get; }

        public string? CustomHighlightBg { get; }

        public string? CustomHighlightBorder { get; }

        public string? CustomText { get; }

        public string? HighlightGlowColor { get; }

        public double HighlightGlowRadius { get; }

        public double HighlightGlowOpacity { get; }

        public WheelPaletteInput(
            IReadOnlyList<CustomColorPreset> customPresets,
            string? customSectorBg,
            string? customSectorBorder,
            string? customHighlightBg,
            string? customHighlightBorder,
            string? customText,
            string? highlightGlowColor,
            double highlightGlowRadius,
            double highlightGlowOpacity)
        {
            CustomPresets = customPresets ?? new List<CustomColorPreset>();
            CustomSectorBg = customSectorBg;
            CustomSectorBorder = customSectorBorder;
            CustomHighlightBg = customHighlightBg;
            CustomHighlightBorder = customHighlightBorder;
            CustomText = customText;
            HighlightGlowColor = highlightGlowColor;
            HighlightGlowRadius = highlightGlowRadius;
            HighlightGlowOpacity = highlightGlowOpacity;
        }

        /// <summary>从运行态配置取一份配色输入快照：预设列表整表复制，微调字段按值携带。</summary>
        public static WheelPaletteInput FromConfig(AppConfig? config)
        {
            if (config == null) return Empty;

            return new WheelPaletteInput(
                config.CustomColorPresets == null
                    ? new List<CustomColorPreset>()
                    : new List<CustomColorPreset>(config.CustomColorPresets),
                config.CustomSectorBg,
                config.CustomSectorBorder,
                config.CustomHighlightBg,
                config.CustomHighlightBorder,
                config.CustomText,
                config.HighlightGlowColor,
                config.HighlightGlowRadius,
                config.HighlightGlowOpacity);
        }
    }
}
