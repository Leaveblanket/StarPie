using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace StarPie.Tests;

/// <summary>
/// 架构文档体系的不变量：把 <c>docs/architecture.md</c> §6 的维护义务变成机械断言。
/// 覆盖五条——路由表与磁盘叶子一一对应、ADR 头部状态合法、全仓 ADR 引用无死链（含源码注释）、
/// 叶子不残留完成态流水与日期快照、源码注释不含变更史编号（见 comments.md 禁止清单）。
/// </summary>
/// <remarks>
/// 断言失败时先修文档，不要放宽断言：这四条正是过去靠人记而反复失守的部分
/// （13 篇 ADR 瘦身后留下 12 处指向不存在决策号的引用、入口附录被删后 <c>agents/domain.md</c> 长期指向不存在的章节）。
/// </remarks>
public sealed class DocInvariantTests
{
    private static string RepoRoot => FourSetBoundaryProbe.RepoRoot;
    private static string DocsRoot => Path.Combine(RepoRoot, "docs");
    private static string EntryFile => Path.Combine(DocsRoot, "architecture.md");
    private static string LeafDir => Path.Combine(DocsRoot, "architecture");
    private static string AdrDir => Path.Combine(DocsRoot, "adr");

    /// <summary>源码树扫描时跳过的目录（构建产物、隔离环境与工作区）。</summary>
    private static readonly string[] ExcludedDirs =
    {
        ".git", "bin", "obj", ".venv", ".scratch", ".codegraph", "artifacts", "node_modules", "TestResults",
    };

    /// <summary>叶子不得出现的完成态流水词（§6.7：完成即删，历史归 git 与 issue）。</summary>
    private static readonly string[] CompletedStateWords =
    {
        "已删除", "已移除", "已撤销", "已清零", "已退役", "已落地",
    };

    private static readonly Regex AdrMarkdownReference =
        new(@"(\d{4})-[a-z0-9][a-z0-9-]*\.md", RegexOptions.Compiled);

    private static readonly Regex AdrBareReference =
        new(@"ADR-(\d{4})", RegexOptions.Compiled);

    private static readonly Regex DateSnapshot =
        new(@"\b20\d{2}-\d{2}-\d{2}\b", RegexOptions.Compiled);

    /// <summary>源码注释里的变更史编号：issue 号与批次/阶段编号。ADR 决策引用与模块代号
    /// （`M1`–`M5`/`S1`–`S6`/`H1`）按 comments.md 不属变更史，不在匹配内。</summary>
    private static readonly Regex ChangeHistoryNumber =
        new(@"(?<![0-9A-Fa-fx#])#\d{1,4}(?![0-9A-Fa-f])|\b[TPB]\d+(?:\.\d+)?\b", RegexOptions.Compiled);

    [Fact]
    public void 源码注释不含变更史编号()
    {
        var offenders = new List<string>();
        foreach (string file in EnumerateFiles(RepoRoot, "*.cs").Concat(EnumerateFiles(RepoRoot, "*.xaml")))
        {
            int lineNumber = 0;
            foreach (string line in File.ReadLines(file))
            {
                lineNumber++;
                string trimmed = line.TrimStart();
                bool isComment = trimmed.StartsWith("///", StringComparison.Ordinal)
                    || trimmed.StartsWith("//", StringComparison.Ordinal)
                    || trimmed.StartsWith("<!--", StringComparison.Ordinal);
                if (!isComment || line.Contains("TODO(#", StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (Match match in ChangeHistoryNumber.Matches(line))
                {
                    offenders.Add($"{Path.GetRelativePath(RepoRoot, file)}:{lineNumber} 「{match.Value}」");
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "源码注释不承载变更史：issue 号与批次/阶段编号一律不写（ADR 决策引用与模块代号可以写）；"
                + "指向未来动作的短待办用 TODO 形式。" + Environment.NewLine
                + string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void 路由表与磁盘叶子一一对应_且每个叶子恰好登记一次()
    {
        string[] routingLeaves = ExtractRoutingTableLeaves();
        string[] diskLeaves = Directory.EnumerateFiles(LeafDir, "*.md")
            .Select(path => Path.GetFileName(path))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        foreach (string leaf in routingLeaves)
        {
            Assert.True(
                File.Exists(Path.Combine(LeafDir, leaf)),
                $"路由表指向不存在的叶子：docs/architecture/{leaf}");
        }

        string[] missingFromTable = diskLeaves.Except(routingLeaves).ToArray();
        Assert.True(
            missingFromTable.Length == 0,
            $"叶子未在入口路由表登记（architecture.md §2）：{string.Join(", ", missingFromTable)}");

        string[] duplicated = routingLeaves
            .GroupBy(leaf => leaf, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        Assert.True(
            duplicated.Length == 0,
            $"路由表对同一叶子登记了多行：{string.Join(", ", duplicated)}");
    }

    [Fact]
    public void 每篇ADR头部状态合法()
    {
        var offenders = new List<string>();
        foreach (string adr in Directory.EnumerateFiles(AdrDir, "*.md"))
        {
            string statusLine = File.ReadLines(adr).FirstOrDefault(line => line.StartsWith("> Status:", StringComparison.Ordinal)) ?? string.Empty;
            string value = statusLine["> Status:".Length..].Trim();
            bool legal = value.StartsWith("Active", StringComparison.Ordinal)
                || Regex.IsMatch(value, @"^Superseded by \d{4}\b");
            if (!legal)
            {
                offenders.Add($"{Path.GetFileName(adr)}：{(statusLine.Length == 0 ? "缺少 > Status: 行" : value)}");
            }
        }

        Assert.True(
            offenders.Count == 0,
            "ADR 头部状态须为 Active / Superseded by NNN / Active（部分被 NNN 修订）：" + Environment.NewLine
                + string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void ADR引用无死链_含源码注释()
    {
        var offenders = new List<string>();
        foreach ((string file, int lineNumber, string line) in EnumerateScannedLines())
        {
            foreach (Match match in AdrMarkdownReference.Matches(line))
            {
                AssertResolvable(match.Groups[1].Value, file, lineNumber, line, offenders);
            }

            foreach (Match match in AdrBareReference.Matches(line))
            {
                AssertResolvable(match.Groups[1].Value, file, lineNumber, line, offenders);
            }
        }

        Assert.True(
            offenders.Count == 0,
            "以下位置引用了不存在的 ADR（若确为有意提及已删除的编号，同句须写明「已删除」）：" + Environment.NewLine
                + string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void 叶子不残留完成态流水与日期快照()
    {
        var offenders = new List<string>();
        foreach (string leaf in Directory.EnumerateFiles(LeafDir, "*.md"))
        {
            int lineNumber = 0;
            foreach (string line in File.ReadLines(leaf))
            {
                lineNumber++;
                foreach (string word in CompletedStateWords.Where(word => line.Contains(word, StringComparison.Ordinal)))
                {
                    offenders.Add($"{Path.GetFileName(leaf)}:{lineNumber} 完成态词「{word}」");
                }

                if (DateSnapshot.IsMatch(line))
                {
                    offenders.Add($"{Path.GetFileName(leaf)}:{lineNumber} 日期快照");
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "叶子不记录已落地/已清零/批次流水与日期快照（§6.7）：" + Environment.NewLine
                + string.Join(Environment.NewLine, offenders));
    }

    /// <summary>现有 ADR 的编号集合（文件名形态为 <c>{编号}-{slug}.md</c>）。</summary>
    private static readonly Lazy<HashSet<string>> ExistingAdrNumbers = new(() =>
        Directory.EnumerateFiles(AdrDir, "*.md")
            .Select(path => Path.GetFileName(path))
            .Where(name => name.Length > 4 && name[4] == '-')
            .Select(name => name[..4])
            .ToHashSet(StringComparer.Ordinal));

    private static void AssertResolvable(
        string number, string file, int lineNumber, string line, List<string> offenders)
    {
        if (ExistingAdrNumbers.Value.Contains(number))
        {
            return;
        }

        // 有意提及已删除编号的句子（如「历史 ADR-0002 已删除」）是自知的，放行。
        if (line.Contains("已删除", StringComparison.Ordinal))
        {
            return;
        }

        offenders.Add($"{Path.GetRelativePath(RepoRoot, file)}:{lineNumber} → ADR-{number} 不存在");
    }

    /// <summary>读入口 §2 路由表中指向 <c>architecture/*.md</c> 的叶子名（每行一个文件）。</summary>
    private static string[] ExtractRoutingTableLeaves()
    {
        var leaves = new List<string>();
        bool inRoutingSection = false;
        foreach (string line in File.ReadLines(EntryFile))
        {
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                inRoutingSection = line.StartsWith("## 2.", StringComparison.Ordinal);
                continue;
            }

            if (!inRoutingSection)
            {
                continue;
            }

            foreach (Match match in Regex.Matches(line, @"\]\(architecture/([^)#]+\.md)\)"))
            {
                leaves.Add(match.Groups[1].Value);
            }
        }

        Assert.True(leaves.Count > 0, "未能从 docs/architecture.md §2 解析出任何叶子链接");
        return leaves.ToArray();
    }

    /// <summary>枚举扫描面（docs/ 全部 Markdown + 全仓 C#/XAML 源码），跳过围栏代码块与排除目录。</summary>
    private static IEnumerable<(string File, int LineNumber, string Line)> EnumerateScannedLines()
    {
        foreach (string file in EnumerateFiles(DocsRoot, "*.md").Concat(
                     EnumerateFiles(RepoRoot, "*.cs")).Concat(
                     EnumerateFiles(RepoRoot, "*.xaml")))
        {
            bool insideFence = false;
            int lineNumber = 0;
            foreach (string line in File.ReadLines(file))
            {
                lineNumber++;
                if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
                {
                    insideFence = !insideFence;
                    continue;
                }

                if (!insideFence)
                {
                    yield return (file, lineNumber, line);
                }
            }
        }
    }

    private static IEnumerable<string> EnumerateFiles(string root, string pattern)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            string current = pending.Pop();
            foreach (string directory in Directory.EnumerateDirectories(current))
            {
                string name = Path.GetFileName(directory);
                if (!ExcludedDirs.Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    pending.Push(directory);
                }
            }

            foreach (string file in Directory.EnumerateFiles(current, pattern))
            {
                yield return file;
            }
        }
    }
}
