using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace StarPie.Tests;

/// <summary>
/// 内存分层常驻的机械断言：GC 堆硬顶声明与工作集裁剪能力删除后的零残留。
/// 纯文件级断言，不经容器；运行时生效值口径由启动编排末尾的 GC.GetConfigurationVariables
/// 记载（Debug 构建可见；Release 构建核对产物 runtimeconfig.json）与编译产物 runtimeconfig.json
/// 共同承担，本类锁声明面与残留面。
/// </summary>
/// <remarks>
/// 为什么其他层看不见：GC 硬顶是运行期配置，模板写错或产物没并入都不让构建失败，启动也照常成功，
/// 内存上限只是静默失效；工作集裁剪的符号残留不参与编译与运行，只有全树扫描才看得见。
/// </remarks>
public sealed class MemoryResidencyTests
{
    private static string RepoRoot => FourSetBoundaryProbe.RepoRoot;

    private static string RuntimeconfigTemplatePath
        => Path.Combine(RepoRoot, "StarPie.Ui", "runtimeconfig.template.json");

    /// <summary>GC 堆硬顶预算：256 MiB。</summary>
    private const long GcHeapHardLimitBytes = 268435456;

    /// <summary>工作集裁剪已整体删除：源码树不再出现旧符号名（P/Invoke 与旧方法名）。</summary>
    private static readonly string[] ForbiddenSymbols = { "TrimMemory", "EmptyWorkingSet" };

    private static readonly string[] ScannedExtensions =
    {
        ".cs", ".xaml", ".resx", ".json", ".csproj", ".props", ".targets",
    };

    private static readonly string[] ExcludedDirectories =
    {
        ".git", "bin", "obj", ".codegraph", ".venv", "artifacts", ".scratch", "node_modules",
    };

    [Fact]
    public void Runtimeconfig_template_declares_gc_heap_hard_limit()
    {
        Assert.True(File.Exists(RuntimeconfigTemplatePath), $"runtimeconfig template missing: {RuntimeconfigTemplatePath}");

        using var doc = JsonDocument.Parse(File.ReadAllText(RuntimeconfigTemplatePath));
        long declared = doc.RootElement
            .GetProperty("configProperties")
            .GetProperty("System.GC.HeapHardLimit")
            .GetInt64();

        Assert.Equal(GcHeapHardLimitBytes, declared);
    }

    [Fact]
    public void Built_runtimeconfig_carries_heap_hard_limit_when_present()
    {
        string uiBin = Path.Combine(RepoRoot, "StarPie.Ui", "bin");
        if (!Directory.Exists(uiBin))
        {
            // 未构建时不强求产物存在；模板声明面已由上方测试锁定
            return;
        }

        string[] builtConfigs = Directory.GetFiles(uiBin, "StarPie.runtimeconfig.json", SearchOption.AllDirectories);
        Assert.NotEmpty(builtConfigs);

        // 产物按构建配置分目录,陈旧配置目录里可能留有模板生效前的旧产物;
        // 断言最近一次构建(任一产物)已并入硬顶即可
        bool carried = builtConfigs.Select(config =>
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(config));
            return doc.RootElement
                .GetProperty("runtimeOptions")
                .GetProperty("configProperties")
                .TryGetProperty("System.GC.HeapHardLimit", out JsonElement value)
                && value.GetInt64() == GcHeapHardLimitBytes;
        }).Any(carried => carried);

        Assert.True(carried, $"编译产物未并入 System.GC.HeapHardLimit(检查 {string.Join(", ", builtConfigs)})");
    }

    [Fact]
    public void Source_tree_has_no_trim_or_working_set_residue()
    {
        var offenders = new List<string>();

        foreach (string file in EnumerateSourceFiles())
        {
            string content = File.ReadAllText(file);
            foreach (string symbol in ForbiddenSymbols)
            {
                if (content.Contains(symbol, StringComparison.Ordinal))
                {
                    offenders.Add($"{file}: {symbol}");
                }
            }
        }

        Assert.True(offenders.Count == 0, $"工作集裁剪残留（需要时从 git 历史恢复）:\n{string.Join('\n', offenders)}");
    }

    private static IEnumerable<string> EnumerateSourceFiles()
        => Directory.EnumerateFiles(RepoRoot, "*.*", SearchOption.AllDirectories)
            .Where(path =>
            {
                string extension = Path.GetExtension(path);
                if (!ScannedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase)) return false;
                var segments = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                return !segments.Any(segment => ExcludedDirectories.Contains(segment, StringComparer.OrdinalIgnoreCase));
            })
            // 本测试的禁词清单即源码字面量,排除自身避免自匹配
            .Where(path => !path.EndsWith("MemoryResidencyTests.cs", StringComparison.OrdinalIgnoreCase));
}
