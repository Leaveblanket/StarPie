using System;
using System.IO;
using System.Text.RegularExpressions;

namespace WinPieGestures.Tests;

/// <summary>
/// 主题令牌键集一致性测试（ADR-0012/ADR-0013，#46）：Views/Styles/Themes 五套 XAML 必须
/// 持有同一 key 集——缺键即失败，防止换入后 DynamicResource 悬空。纯文件级断言，不经容器。
/// </summary>
public sealed class ThemePaletteConsistencyTests
{
    private static readonly string[] ThemeNames = { "Light", "Dark", "MidnightNavy", "RoyalViolet", "TitaniumGray" };

    private static string ThemesDirectory
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            for (int i = 0; i < 4; i++) dir = dir.Parent!;
            return Path.Combine(dir.FullName, "WinPieGestures", "Views", "Styles", "Themes");
        }
    }

    private static SortedSet<string> ReadKeys(string theme)
    {
        string path = Path.Combine(ThemesDirectory, theme + ".xaml");
        Assert.True(File.Exists(path), $"theme file missing: {path}");
        string xaml = File.ReadAllText(path);
        var keys = new SortedSet<string>(StringComparer.Ordinal);
        foreach (Match m in Regex.Matches(xaml, "x:Key=\"([^\"]+)\""))
        {
            keys.Add(m.Groups[1].Value);
        }
        return keys;
    }

    [Fact]
    public void AllThemePalettes_ExposeTheSameKeySet()
    {
        var baseline = ReadKeys("Light");
        Assert.True(baseline.Count >= 20, $"expected at least 20 token keys, got {baseline.Count}");

        foreach (string theme in ThemeNames)
        {
            var keys = ReadKeys(theme);
            var missing = baseline.Except(keys).ToList();
            var extra = keys.Except(baseline).ToList();
            Assert.True(missing.Count == 0, $"{theme}.xaml missing keys: {string.Join(", ", missing)}");
            Assert.True(extra.Count == 0, $"{theme}.xaml has extra keys: {string.Join(", ", extra)}");
        }
    }
}
