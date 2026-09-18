using System;
using System.Collections;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using StarPie.Sdk.Services.Themes;

namespace StarPie.Ui.Adapters
{
    /// <summary>
    /// 主题调色板适配器（实现内核主题应用端口）：自包含"加载 Themes/*.xaml → 缓存/冻结 →
    /// 整项替换 Application MergedDictionaries 活动主题槽"。
    /// </summary>
    /// <remarks>
    /// App.xaml 静态合并 Light 仅作设计时/首帧；本适配器把目标主题字典放入合并字典的主题槽
    /// （含 /Themes/ 的第一项），切 Light 即替换回 Light 字典，直接键零残留。
    /// 内核主题引擎零 WPF，主题呈现只能在此适配；Application 缺席（如无窗口环境）时安全返回。
    /// </remarks>
    internal sealed class AppThemePaletteManager : IThemeApplier
    {
        private readonly Dictionary<string, ResourceDictionary> _palettes = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>把 effectiveTheme 调色板整项替换进 Application 合并字典的主题槽；未知主题名回落 Light。</summary>
        public void ApplyTheme(string effectiveTheme)
        {
            if (Application.Current is not { } app) return;

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
            // 规范名即文件名：五套具体主题与 Themes/*.xaml 逐一同名；
            // System（无字典）与未知名/遗留值回落 Light。
            string file = AppThemeNames.CanonicalOrNull(theme) ?? AppThemeNames.Light;
            if (_palettes.TryGetValue(file, out ResourceDictionary? cached)) return cached;

            var source = new Uri($"pack://application:,,,/Themes/{file}.xaml", UriKind.Absolute);
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
