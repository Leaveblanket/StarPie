using System.IO;
using System.Text.RegularExpressions;
using StarPie.Modules;
using StarPie.PluginRuntime.Admission;
using StarPie.PluginRuntime.Diagnostics;
using StarPie.Services;
using StarPie.Services.Navigation;
using StarPie.ViewModels.Pages;

namespace StarPie.Tests;

/// <summary>
/// 本地化文案键的引用侧存在性：<c>GetString</c> 传裸键是明文约定，缺键时服务静默回退成键名
/// （<see cref="LocalizationServiceTests"/> 有一条用例确认该语义），因此引用侧必须由测试兜住。
/// </summary>
/// <remarks>
/// 覆盖面按三类引用分开断言，逐类对应一种漏键方式：
/// <list type="bullet">
/// <item>源码字面量调用——拼错或删键；</item>
/// <item>插件状态/准入枚举到键的映射——新增枚举值落到 <c>_ =&gt;</c> 兜底臂（两个枚举的兜底臂
/// 都返回一条既有键，所以「键存在」拦不住它，必须断言映射是单射）；</item>
/// <item>导航目录标题键——目录注册项的键随页面增删漂移。</item>
/// </list>
/// 插件自身提供的文案键（<c>Descriptor.TitleKey</c> 走宿主 resx、插件无 resx 机制）不在此列。
/// </remarks>
public sealed class LocalizationKeyCoverageTests
{
    /// <summary><c>GetString("Key")</c> 的字面量调用。</summary>
    private static readonly Regex GetStringLiteralCall =
        new(@"GetString\(\s*""([^""]+)""", RegexOptions.Compiled);

    /// <summary>resx 键定义。</summary>
    private static readonly Regex ResxKeyDefinition =
        new(@"<data name=""([^""]+)""", RegexOptions.Compiled);

    [Fact]
    public void 源码GetString的字面量键_全部存在于resx()
    {
        HashSet<string> keys = ResxKeys();
        var unresolved = new List<string>();
        int callCount = 0;

        foreach (string file in SourceCodeFiles())
        {
            string[] lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                foreach (Match match in GetStringLiteralCall.Matches(lines[i]))
                {
                    callCount++;
                    string key = match.Groups[1].Value;
                    if (!keys.Contains(key))
                    {
                        unresolved.Add($"{Path.GetRelativePath(FourSetBoundaryProbe.RepoRoot, file)}:{i + 1} → {key}");
                    }
                }
            }
        }

        Assert.True(callCount >= 60, $"expected a broad literal call set, got {callCount}");
        Assert.True(unresolved.Count == 0, "GetString keys missing from resx: " + string.Join("; ", unresolved));
    }

    [Fact]
    public void 插件状态与准入枚举_单射映射到存在的文案键()
    {
        HashSet<string> keys = ResxKeys();

        PluginRuntimeStatus[] statuses = Enum.GetValues<PluginRuntimeStatus>();
        List<string> statusKeys = statuses.Select(PluginManagerItemViewModel.ToStatusKey).ToList();
        Assert.All(statusKeys, key => Assert.Contains(key, keys));
        Assert.Equal(statuses.Length, statusKeys.Distinct(StringComparer.Ordinal).Count());

        PluginAdmission[] admissions = Enum.GetValues<PluginAdmission>();
        List<string> admissionKeys = admissions.Select(PluginManagerItemViewModel.ToAdmissionKey).ToList();
        Assert.All(admissionKeys, key => Assert.Contains(key, keys));
        Assert.Equal(admissions.Length, admissionKeys.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void 导航目录注册的标题键_全部存在于resx()
    {
        HashSet<string> keys = ResxKeys();
        var catalog = new NavigationCatalog();
        foreach (ICompositionContributor contributor in BuiltInContributors.CreateAll(new AppHostDelegates()))
        {
            contributor.RegisterNavigation(catalog);
        }

        IReadOnlyList<NavigationPageRegistration> entries = catalog.Entries;
        Assert.True(entries.Count >= 5, $"expected the five fixed pages, got {entries.Count}");

        var unresolved = entries
            .Where(entry => !keys.Contains(entry.TitleKey))
            .Select(entry => $"{entry.AutomationId} → {entry.TitleKey}")
            .ToList();
        Assert.True(unresolved.Count == 0, "navigation TitleKey missing from resx: " + string.Join("; ", unresolved));
    }

    private static HashSet<string> ResxKeys()
    {
        string path = Path.Combine(
            FourSetBoundaryProbe.RepoRoot, "StarPie.Host", "Localization", "Strings.resx");
        Assert.True(File.Exists(path), $"language resource missing: {path}");

        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in ResxKeyDefinition.Matches(File.ReadAllText(path)))
        {
            keys.Add(match.Groups[1].Value);
        }

        Assert.True(keys.Count >= 200, $"expected the full language table, got {keys.Count}");
        return keys;
    }

    /// <summary>四集的源码文件（排除构建产物）；插件工程不在本次覆盖面内。</summary>
    private static IEnumerable<string> SourceCodeFiles()
    {
        string[] projects = { "StarPie.Ui", "StarPie.Host", "StarPie.Sdk", "StarPie.Sdk.Wpf" };
        foreach (string project in projects)
        {
            IEnumerable<string> files = Directory.EnumerateFiles(
                Path.Combine(FourSetBoundaryProbe.RepoRoot, project), "*.cs", SearchOption.AllDirectories);
            foreach (string file in files)
            {
                string[] segments = file.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (!segments.Contains("obj") && !segments.Contains("bin"))
                {
                    yield return file;
                }
            }
        }
    }
}
