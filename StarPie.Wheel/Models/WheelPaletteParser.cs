using System;

namespace WinPieGestures.Models
{
    /// <summary>
    /// 轮盘配色解析器（ADR-0014 决策 3/10）：输入配色方案名（System/Dark/Light/
    /// MatchaForest/GlacialIce/MorandiMuted/Custom/CustomPreset_*）与运行配置/OS 深浅色，
    /// 输出最终色值组。只做纯数据换算，不依赖 WPF；System↔OS、系统预设、自定义预设
    /// （id/name/CustomPreset_ 前缀）匹配、Custom 微调与坏值/空值回落集中于此。
    /// </summary>
    public static class WheelPaletteParser
    {
        /// <summary>System/空值按 OS 深浅色解析为 Dark/Light；命名方案原样透传（沿用渲染器原语义）。</summary>
        public static string ResolveEffectiveTheme(string theme, bool windowsInDarkMode)
        {
            if (string.IsNullOrEmpty(theme) || string.Equals(theme, "System", StringComparison.OrdinalIgnoreCase))
            {
                return windowsInDarkMode ? "Dark" : "Light";
            }
            return theme;
        }

        /// <summary>方案名 → 色值组：风格默认观感为基底，依次应用标准浅色/系统预设/
        /// 自定义预设匹配/Custom 微调；最终任一色值非法即整组回落紧急色。</summary>
        public static WheelPalette Resolve(string theme, AppConfig config, bool windowsInDarkMode, string style)
        {
            string effectiveTheme = ResolveEffectiveTheme(theme, windowsInDarkMode);
            theme ??= "";
            WheelPalette palette = WheelPaletteCatalog.GetStyleDefault(style, effectiveTheme);

            if (theme == "Light" && WheelPaletteCatalog.UsesStandardLightFallback(style))
            {
                return WheelPaletteCatalog.StandardLight;
            }
            if (theme == "MatchaForest")
            {
                return WheelPaletteCatalog.MatchaForest;
            }
            if (theme == "GlacialIce")
            {
                return WheelPaletteCatalog.GlacialIce;
            }
            if (theme == "MorandiMuted")
            {
                return WheelPaletteCatalog.MorandiMuted;
            }
            if (theme.StartsWith("CustomPreset_", StringComparison.Ordinal) || IsReferencedPreset(theme, config))
            {
                CustomColorPreset? preset = FindPreset(theme, config);
                if (preset != null)
                {
                    // 命中预设即整组采用其色值；任一字段 null/非法与现状一致整组回落紧急色。
                    if (TryParsePreset(preset, out var sectorBg, out var sectorBorder, out var highlightBg, out var highlightBorder, out var textColor))
                    {
                        return WheelPalette.Create(sectorBg, sectorBorder, highlightBg, highlightBorder, textColor);
                    }
                    return WheelPaletteCatalog.Emergency;
                }

                // 带前缀但预设已不存在：保持风格默认观感（现状 Find 未命中行为）。
                return palette;
            }
            if (theme == "Custom")
            {
                return ResolveCustom(config, palette);
            }

            return palette;
        }

        private static bool IsReferencedPreset(string theme, AppConfig config)
            => config.CustomColorPresets != null && config.CustomColorPresets.Exists(p => p.Id == theme || p.Name == theme);

        private static CustomColorPreset? FindPreset(string theme, AppConfig config)
            => config.CustomColorPresets?.Find(p => p.Id == theme || p.Name == theme || ("CustomPreset_" + p.Id) == theme);

        private static WheelPalette ResolveCustom(AppConfig config, WheelPalette styleDefault)
        {
            bool invalid = false;
            RgbColor sectorBg = styleDefault.SectorBg;
            if (config.CustomSectorBg != null && !RgbColor.TryParseHex(config.CustomSectorBg, out sectorBg)) invalid = true;

            RgbColor sectorBorder = styleDefault.SectorBorder;
            if (config.CustomSectorBorder != null && !RgbColor.TryParseHex(config.CustomSectorBorder, out sectorBorder)) invalid = true;

            RgbColor highlightBg = styleDefault.HighlightBg;
            if (config.CustomHighlightBg != null && !RgbColor.TryParseHex(config.CustomHighlightBg, out highlightBg)) invalid = true;

            RgbColor highlightBorder = styleDefault.HighlightBorder;
            if (config.CustomHighlightBorder != null && !RgbColor.TryParseHex(config.CustomHighlightBorder, out highlightBorder)) invalid = true;

            RgbColor textColor = styleDefault.TextColor;
            if (config.CustomText != null && !RgbColor.TryParseHex(config.CustomText, out textColor)) invalid = true;

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
