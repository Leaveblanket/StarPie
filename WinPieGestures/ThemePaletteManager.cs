using System;
using System.Collections;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace WinPieGestures
{
    /// <summary>
    /// 主题调色板管理器（ADR-0013/#46）：自包含“加载 Views/Styles/Themes/*.xaml →
    /// 缓存/冻结 → 整项替换 Application MergedDictionaries 活动主题槽”，取代 AppHost 的
    /// 直接键覆盖（ADR-0012 决策 2 被取代）。App.xaml 静态合并 Light 仅作设计时/首帧；
    /// 本管理器把目标主题字典放入合并字典的主题槽（含 /Themes/ 的第一项），切 Light 即
    /// 替换回 Light 字典，直接键零残留。宿主只编排调用，不实现资源细节。
    /// </summary>
    internal sealed class ThemePaletteManager
    {
        // 配置名/遗留别名 → 主题文件规范名（T09 语义：ObsidianDark 等价 Dark）。
        private static readonly Dictionary<string, string> ThemeFileNames = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Light"] = "Light",
            ["Dark"] = "Dark",
            ["ObsidianDark"] = "Dark",
            ["MidnightNavy"] = "MidnightNavy",
            ["RoyalViolet"] = "RoyalViolet",
            ["TitaniumGray"] = "TitaniumGray"
        };

        private readonly Dictionary<string, ResourceDictionary> _palettes = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>把 effectiveTheme 调色板整项替换进 Application 合并字典的主题槽；未知主题名回落 Light。</summary>
        public void Apply(string effectiveTheme, Application app)
        {
            if (app == null) return;

            ResourceDictionary palette = LoadPalette(effectiveTheme);
            var merged = app.Resources.MergedDictionaries;
            int slot = FindThemeSlot(merged);
            if (slot < 0)
            {
                merged.Insert(0, palette);
            }
            else if (!ReferenceEquals(merged[slot], palette))
            {
                merged[slot] = palette;
            }
        }

        /// <summary>主题槽 = MergedDictionaries 中 Source 含 /Themes/ 的第一项（App.xaml 静态 Light 位）。</summary>
        private static int FindThemeSlot(IList<ResourceDictionary> merged)
        {
            for (int i = 0; i < merged.Count; i++)
            {
                if (merged[i].Source?.OriginalString.Contains("/Themes/", StringComparison.OrdinalIgnoreCase) == true)
                {
                    return i;
                }
            }

            return -1;
        }

        private ResourceDictionary LoadPalette(string theme)
        {
            string file = ThemeFileNames.TryGetValue(theme, out string? name) ? name : "Light";
            if (_palettes.TryGetValue(file, out ResourceDictionary? cached)) return cached;

            var source = new Uri($"pack://application:,,,/StarPie;component/Views/Styles/Themes/{file}.xaml", UriKind.Absolute);
            var palette = new ResourceDictionary { Source = source };
            foreach (DictionaryEntry entry in palette)
            {
                if (entry.Key is string && entry.Value is SolidColorBrush brush && brush.CanFreeze)
                {
                    brush.Freeze();
                }
            }

            _palettes[file] = palette;
            return palette;
        }
    }
}
