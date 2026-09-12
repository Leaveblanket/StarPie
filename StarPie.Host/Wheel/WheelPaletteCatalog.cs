using System;

namespace StarPie.Wheel
{
    /// <summary>
    /// 轮盘配色静态色值目录：系统预设与各风格默认深浅观感、中性/紧急回落的唯一 hex 来源。
    /// </summary>
    /// <remarks>
    /// 方案名换算与自定义预设匹配在 <see cref="WheelPaletteParser"/>，渲染层不内联这些值。
    /// </remarks>
    public static class WheelPaletteCatalog
    {
        // ---- 系统预设（解析器按方案名整组替换，各风格一致） ----

        public static WheelPalette StandardLight { get; } = Create(
            sectorBgHex: "#F0F8FAFC",
            sectorBorderHex: "#3064748B",
            highlightBgHex: "#FF2563EB",
            highlightBorderHex: "#FF60A5FA",
            textHex: "#FF0F172A");

        public static WheelPalette MatchaForest { get; } = Create(
            sectorBgHex: "#E6142E1F",
            sectorBorderHex: "#4034D399",
            highlightBgHex: "#FF10B981",
            highlightBorderHex: "#FF6EE7B7",
            textHex: "#FFF0FDF4");

        public static WheelPalette GlacialIce { get; } = Create(
            sectorBgHex: "#E0E0F2FE",
            sectorBorderHex: "#6038BDF8",
            highlightBgHex: "#FF0284C7",
            highlightBorderHex: "#FFBAE6FD",
            textHex: "#FF0C4A6E");

        public static WheelPalette MorandiMuted { get; } = Create(
            sectorBgHex: "#E62C302E",
            sectorBorderHex: "#409CA3AF",
            highlightBgHex: "#FF78716C",
            highlightBorderHex: "#FFD6D3D1",
            textHex: "#FFF5F5F4");

        // ---- 中性/紧急回落 ----

        /// <summary>渲染器中性深色默认；CatPaw 的 Custom 分支与未知风格均回落此值。</summary>
        public static WheelPalette NeutralDark { get; } = Create(
            sectorBgHex: "#EB18181B",
            sectorBorderHex: "#30FFFFFF",
            highlightBgHex: "#FF2563EB",
            highlightBorderHex: "#FF60A5FA",
            textHex: "#FFF8FAFC");

        /// <summary>坏值/空值全局回落：任一解析失败即整组替换（核色与扇区色不同源）。</summary>
        public static WheelPalette Emergency { get; } = new WheelPalette(
            sectorBg: Parse("#E618181B"),
            sectorBorder: Parse("#35FFFFFF"),
            highlightBg: Parse("#FF3B82F6"),
            highlightBorder: Parse("#A0FFFFFF"),
            textColor: Parse("#F8FAFC"),
            coreBg: Parse("#F018181B"),
            coreBorder: Parse("#30FFFFFF"));

        // ---- 风格默认深浅观感（各风格 Light/Dark/Custom 的固有观感，随风格切换不变） ----

        private static readonly WheelPalette ClassicRingLight = Create(
            sectorBgHex: "#F5F8FAFC",
            sectorBorderHex: "#3564748B",
            highlightBgHex: "#FF2563EB",
            highlightBorderHex: "#FF93C5FD",
            textHex: "#FF0F172A");

        private static readonly WheelPalette ClassicRingDark = Create(
            sectorBgHex: "#F018181B",
            sectorBorderHex: "#40FFFFFF",
            highlightBgHex: "#FF2563EB",
            highlightBorderHex: "#FF93C5FD",
            textHex: "#FFF8FAFC");

        private static readonly WheelPalette CleanSectorsLight = Create(
            sectorBgHex: "#F8FFFFFF",
            sectorBorderHex: "#35CBD5E1",
            highlightBgHex: "#FF059669",
            highlightBorderHex: "#FF10B981",
            textHex: "#FF0F172A");

        private static readonly WheelPalette CleanSectorsDark = Create(
            sectorBgHex: "#F20F172A",
            sectorBorderHex: "#35334155",
            highlightBgHex: "#FF10B981",
            highlightBorderHex: "#FF6EE7B7",
            textHex: "#FFF8FAFC");

        private static readonly WheelPalette GlassmorphismLight = Create(
            sectorBgHex: "#45FFFFFF",
            sectorBorderHex: "#85FFFFFF",
            highlightBgHex: "#D86366F1",
            highlightBorderHex: "#FFFFFFFF",
            textHex: "#FF0F172A");

        private static readonly WheelPalette GlassmorphismDark = Create(
            sectorBgHex: "#40181E32",
            sectorBorderHex: "#50E2E8F0",
            highlightBgHex: "#D07C3AED",
            highlightBorderHex: "#FFF5F3FF",
            textHex: "#FFF8FAFC");

        private static readonly WheelPalette CatPawPastel = Create(
            sectorBgHex: "#FFF7F9",
            sectorBorderHex: "#F472B6",
            highlightBgHex: "#FB7185",
            highlightBorderHex: "#FFE4E6",
            textHex: "#881337");

        /// <summary>
        /// 按风格取默认观感：非 Light 一律走深色变体；CatPaw 除 Custom 外恒为粉彩观感；
        /// 未知风格与 CatPaw Custom 回落中性深色。
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
            => WheelPalette.Create(
                sectorBg: Parse(sectorBgHex),
                sectorBorder: Parse(sectorBorderHex),
                highlightBg: Parse(highlightBgHex),
                highlightBorder: Parse(highlightBorderHex),
                textColor: Parse(textHex));

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
