using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace StarPie.Tests;

/// <summary>
/// 主题一致性测试：<c>StarPie.Ui/Themes</c> 五套 XAML 必须持有同一 key 集——
/// 缺键即失败，防止换入后 DynamicResource 悬空；四语言 resx 键集必须一致，
/// 并与主题令牌键零交集；界面主题与轮盘配色两份解析实现必须对同一输入给出同一结论；
/// App.xaml 的主题槽必须仍是合并字典第 0 项。纯文件级断言，不经容器。
/// </summary>
public sealed class ThemePaletteConsistencyTests
{
    private static readonly string[] ThemeNames = { "Light", "Dark", "MidnightNavy", "RoyalViolet", "TitaniumGray" };

    /// <summary>仓库根：测试程序集位于 <c>StarPie.Tests/bin/&lt;配置&gt;/&lt;TFM&gt;</c>，回退四级。</summary>
    private static string RepoRoot
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            for (int i = 0; i < 4; i++) dir = dir.Parent!;
            return dir.FullName;
        }
    }

    // 主题字典位于 StarPie.Ui/Themes。
    private static string ThemesDirectory => Path.Combine(RepoRoot, "StarPie.Ui", "Themes");

    // 四语言 resx 位于宿主内核（StarPie.Host/Kernel/Localization）。
    private static string LanguageResourcesFile
        => Path.Combine(RepoRoot, "StarPie.Host", "Kernel", "Localization", "Strings.resx");

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

    private static SortedSet<string> ReadLanguageKeys(string path)
    {
        Assert.True(File.Exists(path), $"language resource missing: {path}");
        string resx = File.ReadAllText(path);
        var keys = new SortedSet<string>(StringComparer.Ordinal);
        foreach (Match m in Regex.Matches(resx, "<data name=\"([^\"]+)\""))
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

    [Fact]
    public void LanguageKeys_DoNotOverlapThemeTokenKeys()
    {
        // 语言键与主题令牌键共享 Application 资源命名空间，零交集由测试保护。
        var themeKeys = ReadKeys("Light");
        var languageKeys = ReadLanguageKeys(LanguageResourcesFile);
        Assert.True(languageKeys.Count >= 200, $"expected full language table, got {languageKeys.Count}");

        var overlap = languageKeys.Intersect(themeKeys).ToList();
        Assert.True(overlap.Count == 0, $"language/theme key overlap: {string.Join(", ", overlap)}");
    }

    [Fact]
    public void AllLanguageResxFiles_ExposeTheSameKeySet()
    {
        // zh-CN 中性与 zh-TW/en/ja 卫星必须持有同一 key 集，缺/多键即失败，
        // 防止某语言漏配键值而回退到键名。
        var baseline = ReadLanguageKeys(LanguageResourcesFile);
        Assert.True(baseline.Count >= 200, $"expected full language table, got {baseline.Count}");

        string dir = Path.GetDirectoryName(LanguageResourcesFile)!;
        foreach (string file in new[] { "Strings.zh-TW.resx", "Strings.en.resx", "Strings.ja.resx" })
        {
            var keys = ReadLanguageKeys(Path.Combine(dir, file));
            var missing = baseline.Except(keys).ToList();
            var extra = keys.Except(baseline).ToList();
            Assert.True(missing.Count == 0, $"{file} missing keys: {string.Join(", ", missing)}");
            Assert.True(extra.Count == 0, $"{file} has extra keys: {string.Join(", ", extra)}");
        }
    }

    /// <summary>两份解析实现的覆盖输入（含哨兵族与固定名）与共同结论：探针两种取值都要走到。</summary>
    public static TheoryData<string?, bool, string> ResolveInputs => new()
    {
        // 哨兵族：System/空值按探针取值解析（大小写同义）
        { "System", true, "Dark" }, { "System", false, "Light" }, { "system", true, "Dark" },
        { "SYSTEM", false, "Light" }, { "", true, "Dark" }, { null, false, "Light" },
        // 固定名与未知名：原样透传
        { "Light", true, "Light" }, { "Dark", false, "Dark" },
        { "MidnightNavy", true, "MidnightNavy" }, { "遗留别名", false, "遗留别名" },
    };

    [Theory]
    [MemberData(nameof(ResolveInputs))]
    public void AppThemeAndWheelPalette_ResolveTheSameEffectiveName(string? name, bool windowsInDarkMode, string expected)
    {
        // 界面主题解析（M4，ThemeEngine）与轮盘配色解析（M2，WheelPaletteParser）刻意分列、不抽
        // 共用函数：共用签名要把哨兵值与两个返回值全部参数化，会比被抽象的 4 行更长，还会把 M2 与
        // M4 绑在一起。两处「必须一致」由本测试守（与键集/文案一致性测试同一惯例）。
        string appTheme = new ThemeEngine(() => windowsInDarkMode).ResolveEffectiveTheme(name!);
        string wheelPalette = WheelPaletteParser.ResolveEffectivePalette(name!, windowsInDarkMode);

        Assert.Equal(expected, appTheme);
        Assert.Equal(appTheme, wheelPalette);
    }

    [Fact]
    public void AppXaml_ThemeSlotIsFirstMergedDictionary()
    {
        // AppThemePaletteManager.FindThemeSlot 把「顶层恰有一项 Source 含 /Themes/、且它就是
        // App.xaml 静态 Light 那一项」当不变式：按 Source 字符串定位、原位替换。不改成按引用跟踪
        // 是因为 WPF 合并字典后项覆盖前项，原位替换是承重的。该不变式今天成立却无断言守着。
        string path = Path.Combine(RepoRoot, "StarPie.Ui", "App.xaml");
        Assert.True(File.Exists(path), $"App.xaml missing: {path}");

        XNamespace ns = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var merged = XDocument.Load(path)
            .Descendants(ns + "ResourceDictionary.MergedDictionaries")
            .Single()
            .Elements(ns + "ResourceDictionary")
            .Select((dictionary, index) => (index, source: (string?)dictionary.Attribute("Source") ?? ""))
            .ToList();

        var slot = Assert.Single(merged, d => d.source.Contains("/Themes/", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(0, slot.index);
        Assert.EndsWith("Themes/Light.xaml", slot.source, StringComparison.Ordinal);
    }
}
