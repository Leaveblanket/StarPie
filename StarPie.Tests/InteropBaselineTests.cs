using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace StarPie.Tests;

/// <summary>
/// Win32 互操作基线的机械断言（ADR-0051）：声明统一走 CsWin32 源生成，
/// 源码树不得出现手写 P/Invoke 声明；白名单按"文件 → 允许的声明数"精确登记，
/// 因此在该文件里新增一条声明也会被拦下。COM（<c>[ComImport]</c>）不属本基线。
/// </summary>
/// <remarks>
/// 为什么机械断言：手写声明能编译、能运行，评审容易漏看，而它绕过的正是
/// <c>NativeMethods.txt</c> 这个唯一声明清单的审计面——只有全树扫描才看得见。
/// 白名单每条都在代码处写明了原因（架构特定 PInvoke005 / 准安全路径）。
/// </remarks>
public sealed class InteropBaselineTests
{
    private static string RepoRoot
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "StarPie.slnx")))
                    return dir.FullName;
                dir = dir.Parent;
            }
            return Directory.GetCurrentDirectory();
        }
    }

    /// <summary>CsWin32 之外的手写互操作声明标记（两种源生成/运行时封送形态都不放行）。</summary>
    private static readonly string[] ForbiddenDeclarationMarkers =
    {
        "[DllImport(",
        "[LibraryImport(",
    };

    /// <summary>白名单：相对仓库根的路径 → 该文件允许出现的手写声明数。</summary>
    private static readonly Dictionary<string, int> PinvokeWhitelist = new(StringComparer.OrdinalIgnoreCase)
    {
        // Shell_NotifyIcon 在 win32metadata 中标记为架构特定（PInvoke005），AnyCPU 下 CsWin32 无法生成；
        // 如日后收敛到 PlatformTarget=x64 可连同 NOTIFYICONDATA 一起回收。
        [Path.Combine("StarPie.Ui", "Services", "Shell", "TrayIconManager.cs")] = 1,
        // SHGetFileInfo 同属架构特定（PInvoke005），与 SHFILEINFO 一起保留。
        [Path.Combine("StarPie.Ui", "Services", "Icons", "IconAssetService.cs")] = 1,
        // WinVerifyTrust 与 WinTrust* 结构族：准安全路径 + 手工封送 + 既有异常面（零行为变化优先）。
        [Path.Combine("StarPie.Host", "PluginRuntime", "Admission", "WinTrustSignatureVerifier.cs")] = 1,
    };

    /// <summary>声明清单是唯一的审计面：两个消费集都必须随包提供非空清单。</summary>
    private static readonly string[] NativeMethodsManifests =
    {
        Path.Combine("StarPie.Ui", "NativeMethods.txt"),
        Path.Combine("StarPie.Host", "NativeMethods.txt"),
    };

    private static readonly string[] ExcludedDirectories =
    {
        ".git", "bin", "obj", ".codegraph", ".venv", "artifacts", ".scratch", "node_modules",
    };

    [Fact]
    public void Source_tree_has_no_hand_written_pinvoke_outside_whitelist()
    {
        var offenders = new List<string>();

        foreach (string file in EnumerateSourceFiles())
        {
            string content = File.ReadAllText(file);
            int found = ForbiddenDeclarationMarkers.Sum(marker => CountOccurrences(content, marker));
            if (found == 0)
            {
                continue;
            }

            string relative = Path.GetRelativePath(RepoRoot, file);
            int allowed = PinvokeWhitelist.TryGetValue(relative, out int count) ? count : 0;
            if (found != allowed)
            {
                offenders.Add(allowed == 0
                    ? $"{relative}: {found} 处手写声明未登记白名单（新增 API 面请改 NativeMethods.txt）"
                    : $"{relative}: {found} 处手写声明，超出白名单的 {allowed} 处");
            }
        }

        Assert.True(offenders.Count == 0, $"手写 P/Invoke 回流（ADR-0051）:\n{string.Join('\n', offenders)}");
    }

    [Fact]
    public void Whitelisted_files_still_exist()
    {
        var missing = PinvokeWhitelist.Keys
            .Where(relative => !File.Exists(Path.Combine(RepoRoot, relative)))
            .ToList();

        Assert.True(missing.Count == 0, $"白名单指向的文件已不存在（回收其条目）:\n{string.Join('\n', missing)}");
    }

    [Fact]
    public void Interop_consuming_projects_ship_their_declaration_manifest()
    {
        foreach (string relative in NativeMethodsManifests)
        {
            string path = Path.Combine(RepoRoot, relative);
            Assert.True(File.Exists(path), $"声明清单缺失: {relative}");
            Assert.True(new FileInfo(path).Length > 0, $"声明清单为空: {relative}");
        }
    }

    private static int CountOccurrences(string content, string marker)
    {
        int count = 0;
        int index = content.IndexOf(marker, StringComparison.Ordinal);
        while (index >= 0)
        {
            count++;
            index = content.IndexOf(marker, index + marker.Length, StringComparison.Ordinal);
        }
        return count;
    }

    private static IEnumerable<string> EnumerateSourceFiles()
        => Directory.EnumerateFiles(RepoRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path =>
            {
                var segments = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                return !segments.Any(segment => ExcludedDirectories.Contains(segment, StringComparer.OrdinalIgnoreCase));
            })
            // 本测试的禁词清单即源码字面量，排除自身避免自匹配
            .Where(path => !path.EndsWith("InteropBaselineTests.cs", StringComparison.OrdinalIgnoreCase));
}
