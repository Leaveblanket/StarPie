using System;

namespace WinPieGestures.Models
{
    /// <summary>
    /// 轮盘配色静态色值目录（ADR-0014 决策 3/10）：系统预设与各风格默认深浅观感、
    /// 中性/紧急回落的唯一 hex 来源。方案名换算与自定义预设匹配在
    /// <see cref="WheelPaletteParser"/>，渲染层不内联这些值。
    /// </summary>
    public static class WheelPaletteCatalog
    {
        // ---- 系统预设（解析器按方案名整组替换，各风格一致） ----

        public static WheelPalette StandardLight { get; } = Create(
            "#F0F8FAFC", "#3064748B", "#FF2563EB", "#FF60A5FA", "#FF0F172A");

        public static WheelPalette MatchaForest { get; } = Create(
            "#E6142E1F", "#4034D399", "#FF10B981", "#FF6EE7B7", "#FFF0FDF4");

        public static WheelPalette GlacialIce { get; } = Create(
            "#E0E0F2FE", "#6038BDF8", "#FF0284C7", "#FFBAE6FD", "#FF0C4A6E");

        public static WheelPalette MorandiMuted { get; } = Create(
            "#E62C302E", "#409CA3AF", "#FF78716C", "#FFD6D3D1", "#FFF5F5F4");

        // ---- 中性/紧急回落 ----

        /// <summary>渲染器中性深色默认（BaseStyleRenderer 原 GetDefaultColors；CatPaw Custom 分支沿用）。</summary>
        public static WheelPalette NeutralDark { get; } = Create(
            "#EB18181B", "#30FFFFFF", "#FF2563EB", "#FF60A5FA", "#FFF8FAFC");

        /// <summary>坏值/空值全局回落：任一解析失败即整组替换（核色与扇区色不同源，沿用原 catch 表）。</summary>
        public static WheelPalette Emergency { get; } = new WheelPalette(
            Parse("#E618181B"), Parse("#35FFFFFF"), Parse("#FF3B82F6"), Parse("#A0FFFFFF"), Parse("#F8FAFC"),
            Parse("#F018181B"), Parse("#30FFFFFF"));

        // ---- 风格默认深浅观感（各风格 Light/Dark/Custom 的固有观感，随风格切换不变） ----

        private static readonly WheelPalette ClassicRingLight = Create(
            "#F5F8FAFC", "#3564748B", "#FF2563EB", "#FF93C5FD", "#FF0F172A");

        private static readonly WheelPalette ClassicRingDark = Create(
            "#F018181B", "#40FFFFFF", "#FF2563EB", "#FF93C5FD", "#FFF8FAFC");

        private static readonly WheelPalette CleanSectorsLight = Create(
            "#F8FFFFFF", "#35CBD5E1", "#FF059669", "#FF10B981", "#FF0F172A");

        private static readonly WheelPalette CleanSectorsDark = Create(
            "#F20F172A", "#35334155", "#FF10B981", "#FF6EE7B7", "#FFF8FAFC");

        private static readonly WheelPalette GlassmorphismLight = Create(
            "#45FFFFFF", "#85FFFFFF", "#D86366F1", "#FFFFFFFF", "#FF0F172A");

        private static readonly WheelPalette GlassmorphismDark = Create(
            "#40181E32", "#50E2E8F0", "#D07C3AED", "#FFF5F3FF", "#FFF8FAFC");

        private static readonly WheelPalette CatPawPastel = Create(
            "#FFF7F9", "#F472B6", "#FB7185", "#FFE4E6", "#881337");

        /// <summary>
        /// 按风格取默认观感（沿用各渲染器原 GetDefaultColors 语义）：非 Light 一律走深色变体；
        /// CatPaw 除 Custom 外恒为粉彩观感，Custom 回落中性深色。未知风格回落中性深色。
        /// </summary>
        public static WheelPalette GetStyleDefault(string style, string effectiveTheme)
        {
            switch (style)
            {
                case "ClassicRing":
                    return effectiveTheme == "Light" ? ClassicRingLight : ClassicRingDark;
                case "CleanSectors":
                    return effectiveTheme == "Light" ? CleanSectorsLight : CleanSectorsDark;
                case "Glassmorphism":
                    return effectiveTheme == "Light" ? GlassmorphismLight : GlassmorphismDark;
                case "CatPaw":
                    return effectiveTheme == "Custom" ? NeutralDark : CatPawPastel;
                default:
                    return NeutralDark;
            }
        }

        /// <summary>主题名恰为 "Light" 时是否套用标准浅色表（CatPaw 为保持粉彩观感不套用）。</summary>
        public static bool UsesStandardLightFallback(string style)
            => !string.Equals(style, "CatPaw", StringComparison.Ordinal);

        private static WheelPalette Create(string sectorBgHex, string sectorBorderHex, string highlightBgHex, string highlightBorderHex, string textHex)
            => WheelPalette.Create(Parse(sectorBgHex), Parse(sectorBorderHex), Parse(highlightBgHex), Parse(highlightBorderHex), Parse(textHex));

        private static RgbColor Parse(string hex)
        {
            if (!RgbColor.TryParseHex(hex, out var color))
            {
                throw new InvalidOperationException($"轮盘配色目录含非法 hex: {hex}");
            }
            return color;
        }
    }
}
