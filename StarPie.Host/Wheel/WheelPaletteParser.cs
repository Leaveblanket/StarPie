using System;
using System.Linq;
using StarPie.Services.Wheel;

namespace StarPie.Wheel
{
    /// <summary>
    /// 轮盘配色解析器：输入配色方案名（System/Dark/Light/
    /// MatchaForest/GlacialIce/MorandiMuted/Custom/CustomPreset_*）与运行配置/OS 深浅色，
    /// 输出最终色值组。只做纯数据换算，不依赖 WPF；System↔OS、系统预设、自定义预设
    /// （id/name/CustomPreset_ 前缀）匹配、Custom 微调与坏值/空值回落集中于此。
    /// </summary>
    public static class WheelPaletteParser
    {
        /// <summary>System/空值按 OS 深浅色解析为 Dark/Light；命名方案原样透传（沿用渲染器原语义）。</summary>
        public static string ResolveEffectivePalette(string palette, bool windowsInDarkMode)
        {
            if (string.IsNullOrEmpty(palette) || string.Equals(palette, WheelPaletteNames.System, StringComparison.OrdinalIgnoreCase))
            {
                return windowsInDarkMode ? "Dark" : "Light";
            }
            return palette;
        }

        /// <summary>方案名 → 色值组：风格默认观感为基底，依次应用标准浅色/系统预设/
        /// 自定义预设匹配/Custom 微调；最终任一色值非法即整组回落紧急色。</summary>
        public static WheelPalette Resolve(string palette, WheelPaletteInput input, bool windowsInDarkMode, string style)
        {
            string effectivePalette = ResolveEffectivePalette(palette, windowsInDarkMode);
            palette ??= "";
            WheelPalette styleDefault = WheelPaletteCatalog.GetStyleDefault(style, effectivePalette);

            if (palette == WheelPaletteNames.Light && WheelPaletteCatalog.UsesStandardLightFallback(style))
            {
                return WheelPaletteCatalog.StandardLight;
            }
            if (palette == WheelPaletteNames.MatchaForest)
            {
                return WheelPaletteCatalog.MatchaForest;
            }
            if (palette == WheelPaletteNames.GlacialIce)
            {
                return WheelPaletteCatalog.GlacialIce;
            }
            if (palette == WheelPaletteNames.MorandiMuted)
            {
                return WheelPaletteCatalog.MorandiMuted;
            }
            if (palette.StartsWith(WheelPaletteNames.CustomPresetPrefix, StringComparison.Ordinal) || IsReferencedPreset(palette, input))
            {
                CustomColorPreset? preset = FindPreset(palette, input);
                if (preset != null)
                {
                    // 命中预设即整组采用其色值；任一字段 null/非法即整组回落紧急色。
                    if (TryParsePreset(preset, out var sectorBg, out var sectorBorder, out var highlightBg, out var highlightBorder, out var textColor))
                    {
                        return WheelPalette.Create(sectorBg, sectorBorder, highlightBg, highlightBorder, textColor);
                    }
                    return WheelPaletteCatalog.Emergency;
                }

                // 带前缀但预设已不存在：保持风格默认观感。
                return styleDefault;
            }
            if (palette == WheelPaletteNames.Custom)
            {
                return ResolveCustom(input, styleDefault);
            }

            return styleDefault;
        }

        private static bool IsReferencedPreset(string palette, WheelPaletteInput input)
            => input.CustomPresets.Any(p => p.Id == palette || p.Name == palette);

        private static CustomColorPreset? FindPreset(string palette, WheelPaletteInput input)
            => input.CustomPresets.FirstOrDefault(p => p.Id == palette || p.Name == palette || WheelPaletteNames.CustomPresetPrefix + p.Id == palette);

        private static WheelPalette ResolveCustom(WheelPaletteInput input, WheelPalette styleDefault)
        {
            bool invalid = false;
            RgbColor sectorBg = styleDefault.SectorBg;
            if (input.CustomSectorBg != null && !RgbColor.TryParseHex(input.CustomSectorBg, out sectorBg)) invalid = true;

            RgbColor sectorBorder = styleDefault.SectorBorder;
            if (input.CustomSectorBorder != null && !RgbColor.TryParseHex(input.CustomSectorBorder, out sectorBorder)) invalid = true;

            RgbColor highlightBg = styleDefault.HighlightBg;
            if (input.CustomHighlightBg != null && !RgbColor.TryParseHex(input.CustomHighlightBg, out highlightBg)) invalid = true;

            RgbColor highlightBorder = styleDefault.HighlightBorder;
            if (input.CustomHighlightBorder != null && !RgbColor.TryParseHex(input.CustomHighlightBorder, out highlightBorder)) invalid = true;

            RgbColor textColor = styleDefault.TextColor;
            if (input.CustomText != null && !RgbColor.TryParseHex(input.CustomText, out textColor)) invalid = true;

            // Custom 微调字段为 null 时逐字段保留风格默认观感；任一非 null 字段非法即整组回落紧急色。
            if (invalid)
            {
                return WheelPaletteCatalog.Emergency;
            }
            return WheelPalette.Create(sectorBg, sectorBorder, highlightBg, highlightBorder, textColor);
        }

        private static bool TryParsePreset(CustomColorPreset preset, out RgbColor sectorBg, out RgbColor sectorBorder, out RgbColor highlightBg, out RgbColor highlightBorder, out RgbColor textColor)
        {
            sectorBg = default;
            sectorBorder = default;
            highlightBg = default;
            highlightBorder = default;
            textColor = default;
            return RgbColor.TryParseHex(preset.SectorBg, out sectorBg)
                && RgbColor.TryParseHex(preset.SectorBorder, out sectorBorder)
                && RgbColor.TryParseHex(preset.HighlightBg, out highlightBg)
                && RgbColor.TryParseHex(preset.HighlightBorder, out highlightBorder)
                && RgbColor.TryParseHex(preset.TextColor, out textColor);
        }
    }
}
