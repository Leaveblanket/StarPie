using System.IO;
using System.Text.RegularExpressions;

namespace StarPie.Tests;

/// <summary>
/// XAML 引用侧键存在性：<c>StarPie.Ui</c> 全部 XAML 里的 <c>{DynamicResource X}</c> 必须能在 Ui 内
/// 解析到某个定义（resx 键、主题令牌，或任一字典的 <c>x:Key</c>）。
/// </summary>
/// <remarks>
/// 既有三条守护都在定义侧——五套主题彼此键集一致、语言键与主题令牌零交集、四语言 resx 键集一致。
/// 「五套一起缺同一个键」在那三条里天生看不见：定义侧全缺时，引用侧留下的
/// <c>{DynamicResource}</c> 解析静默为 null、不报错，本测试专守这一缺口。
/// 本测试只看「Ui 内是否存在该键」，不做合并链与作用域分析：页面局部字典里定义的同名键也算存在。
/// </remarks>
public sealed class XamlResourceKeyTests
{
    /// <summary><c>{DynamicResource Key}</c> 的引用形态（全仓无嵌套标记扩展写法）。</summary>
    private static readonly Regex DynamicResourceReference =
        new(@"\{DynamicResource\s+([A-Za-z0-9_.]+)\}", RegexOptions.Compiled);

    /// <summary>字典键定义。</summary>
    private static readonly Regex XamlKeyDefinition =
        new(@"x:Key=""([^""]+)""", RegexOptions.Compiled);

    /// <summary>resx 键定义。</summary>
    private static readonly Regex ResxKeyDefinition =
        new(@"<data name=""([^""]+)""", RegexOptions.Compiled);

    [Fact]
    public void UiXaml的DynamicResource引用_全部解析到已定义键()
    {
        List<string> uiXamlFiles = UiXamlFiles().ToList();

        var defined = new HashSet<string>(StringComparer.Ordinal);
        defined.UnionWith(ReadKeys(LanguageResourcesFile, ResxKeyDefinition));
        foreach (string file in uiXamlFiles)
        {
            defined.UnionWith(ReadKeys(file, XamlKeyDefinition));
        }

        var unresolved = new List<string>();
        int referenceCount = 0;
        foreach (string file in uiXamlFiles)
        {
            string[] lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                foreach (Match match in DynamicResourceReference.Matches(lines[i]))
                {
                    referenceCount++;
                    string key = match.Groups[1].Value;
                    if (!defined.Contains(key))
                    {
                        unresolved.Add($"{Relative(file)}:{i + 1} → {key}");
                    }
                }
            }
        }

        Assert.True(referenceCount >= 150, $"expected a full token/string reference set, got {referenceCount}");
        Assert.True(unresolved.Count == 0,
            "unresolved {DynamicResource} keys: " + string.Join("; ", unresolved));
    }

    [Fact]
    public void 主题令牌定义_不成对缺失于五套字典()
    {
        // 引用侧守护之外的反向兜底：令牌在五套之间必须一致（细则见 ThemePaletteConsistencyTests），
        // 本处只断言主题目录确实被扫描到，避免上面的引用扫描因路径漂移而变成空跑。
        List<string> themeFiles = Directory
            .EnumerateFiles(Path.Combine(FourSetBoundaryProbe.RepoRoot, "StarPie.Ui", "Themes"), "*.xaml")
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(5, themeFiles.Count);
        Assert.All(themeFiles, file => Assert.True(ReadKeys(file, XamlKeyDefinition).Count >= 20, file));
    }

    private static IEnumerable<string> UiXamlFiles()
        => Directory
            .EnumerateFiles(Path.Combine(FourSetBoundaryProbe.RepoRoot, "StarPie.Ui"), "*.xaml", SearchOption.AllDirectories)
            .Where(IsSourceFile)
            .OrderBy(path => path, StringComparer.Ordinal);

    private static string LanguageResourcesFile
        => Path.Combine(FourSetBoundaryProbe.RepoRoot, "StarPie.Host", "Kernel", "Localization", "Strings.resx");

    /// <summary>排除构建产物：<c>obj</c> 下的 <c>*.g.cs</c> 同名 XAML 会重复计数。</summary>
    private static bool IsSourceFile(string path)
    {
        string[] segments = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return !segments.Contains("obj") && !segments.Contains("bin");
    }

    private static SortedSet<string> ReadKeys(string path, Regex pattern)
    {
        Assert.True(File.Exists(path), $"resource missing: {path}");
        var keys = new SortedSet<string>(StringComparer.Ordinal);
        foreach (Match match in pattern.Matches(File.ReadAllText(path)))
        {
            keys.Add(match.Groups[1].Value);
        }

        return keys;
    }

    private static string Relative(string path)
        => Path.GetRelativePath(FourSetBoundaryProbe.RepoRoot, path);
}
