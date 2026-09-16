using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace StarPie.Tests;

/// <summary>
/// 架构文档体系的不变量：把《docs/agents/docs-conventions.md》的口径与入口 §6 的维护义务变成机械断言。
/// 覆盖十一条——路由表与磁盘叶子一一对应、ADR 头部状态合法、全仓 ADR 引用无死链（含源码注释）、
/// 叶子不残留完成态流水/等号标记与日期快照（模式读文约）、ADR 不残留日期快照与等号标记（模式读文约）、
/// 叶子的 `规划：` 标记落在「目标态与差距」节内（口径读文约）、
/// layout 树路径在磁盘存在、源码根的一级目录反向登记在 layout 树（四集 + <c>plugins/</c>）、
/// 文档反引号里的类型名在源码命中（豁免与占位口径读文约）、文档声称的常量归属与源码一致、
/// 源码注释不含变更史编号（见 comments.md 禁止清单）。
/// </summary>
/// <remarks>
/// 断言失败时先修文档，不要放宽断言：这些是文档体系里唯一会被静默忽略的失真面——没有别的层拦得住。
/// 其他层也不会有落点：<c>docs/</c> 不参与编译与运行——叶子消失、ADR 编号写错或目录未登记，
/// 都不会让任一编译单元或运行路径失败。
/// </remarks>
public sealed class DocInvariantTests
{
    // --- 文约小节名与扫描面 ---------------------------------------------------------
    private static string RepoRoot => FourSetBoundaryProbe.RepoRoot;
    private static string DocsRoot => Path.Combine(RepoRoot, "docs");
    private static string EntryFile => Path.Combine(DocsRoot, "architecture.md");
    private static string LeafDir => Path.Combine(DocsRoot, "architecture");
    private static string AdrDir => Path.Combine(DocsRoot, "adr");

    /// <summary>源码树扫描时跳过的目录（构建产物、IDE 工作区与隔离环境）。</summary>
    private static readonly string[] ExcludedDirs =
    {
        ".git", ".vs", ".vscode", ".codebuddy", "bin", "obj", ".venv", ".scratch",
        ".codegraph", "artifacts", "node_modules", "TestResults", ".pytest_cache",
    };

    /// <summary>源码根（四集）——反向登记锁的扫描面：其一级子目录必须在 layout.md §1 树出现。</summary>
    private static readonly string[] SourceRoots =
    {
        "StarPie.Ui", "StarPie.Sdk", "StarPie.Sdk.Wpf", "StarPie.Host",
    };

    /// <summary>文约文件：机检口径与豁免清单的正典（入口 §6 指向它；词表增删只改它，不改本文件）。</summary>
    private static string ConventionsFile => Path.Combine(DocsRoot, "agents", "docs-conventions.md");

    /// <summary>文约中承载机检 ERE 模式的小节标题（叶子：完成态流水与日期快照）。</summary>
    private const string PatternsSection = "## 机检口径";

    /// <summary>文约中承载 ADR 机检模式的小节标题（只收在任何体裁下都失真的形态）。</summary>
    private const string AdrPatternsSection = "## ADR 机检口径";

    /// <summary>文约中承载「类型名机检口径」的小节标题（跳过行标记与占位片段）。</summary>
    private const string TypeNameSection = "## 类型名机检口径";

    /// <summary>文约中承载「目标态结构口径」的小节标题。</summary>
    private const string StructureSection = "## 目标态结构口径";

    /// <summary>文约中承载「豁免清单」的小节标题。</summary>
    private const string ExemptionSection = "## 豁免清单";

    /// <summary>文约中承载「常量归属断言」的小节标题。</summary>
    private const string OwnershipSection = "## 归属断言";

    /// <summary>内联代码段（单个反引号，不跨行）。</summary>
    private static readonly Regex InlineCodeSpan = new(@"`([^`\n]+)`", RegexOptions.Compiled);

    /// <summary>整段是 PascalCase 标识符（至少含一个小写字母——排除 `UI`/`WPF` 这类缩写与 `M1`/`S1` 模块代号）。</summary>
    private static readonly Regex PascalCaseIdentifier = new(@"^[A-Z][A-Za-z0-9]*[a-z][A-Za-z0-9]*$", RegexOptions.Compiled);

    private static readonly Regex AdrMarkdownReference =
        new(@"(\d{4})-[a-z0-9][a-z0-9-]*\.md", RegexOptions.Compiled);

    private static readonly Regex AdrBareReference =
        new(@"ADR-(\d{4})", RegexOptions.Compiled);

    private static readonly Regex DateSnapshot =
        new(@"\b20\d{2}-\d{2}-\d{2}\b", RegexOptions.Compiled);

    /// <summary>全仓源码里出现过的标识符集合（<c>*.cs</c>/<c>*.xaml</c>/<c>*.csproj</c>/<c>*.json</c>）——
    /// 文档类型名机检的命中面。</summary>
    private static readonly Lazy<HashSet<string>> SourceIdentifiers = new(() =>
        Regex.Matches(
                string.Join(
                    "\n",
                    EnumerateFiles(RepoRoot, "*.cs")
                        .Concat(EnumerateFiles(RepoRoot, "*.xaml"))
                        .Concat(EnumerateFiles(RepoRoot, "*.csproj"))
                        .Concat(EnumerateFiles(RepoRoot, "*.json"))
                        .Select(File.ReadAllText)),
                @"[A-Za-z_][A-Za-z0-9_]*")
            .Select(match => match.Value)
            .ToHashSet(StringComparer.Ordinal));

    /// <summary>源码注释里的变更史编号：issue 号与批次/阶段编号。ADR 决策引用与模块代号
    /// （`M1`–`M5`/`S1`–`S6`/`H1`）按 comments.md 不属变更史，不在匹配内。</summary>
    private static readonly Regex ChangeHistoryNumber =
        new(@"(?<![0-9A-Fa-fx#])#\d{1,4}(?![0-9A-Fa-f])|\b[TPB]\d+(?:\.\d+)?\b", RegexOptions.Compiled);

    // --- 源码注释不含变更史编号 -----------------------------------------------------
    [Fact]
    public void 源码注释不含变更史编号()
    {
        var offenders = new List<string>();
        foreach (string pattern in CommentScanPatterns)
        {
            foreach (string file in EnumerateFiles(RepoRoot, pattern))
            {
                foreach ((int lineNumber, string line) in CommentLines(file))
                {
                    if (line.Contains("TODO(#", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    foreach (Match match in ChangeHistoryNumber.Matches(line))
                    {
                        offenders.Add($"{Path.GetRelativePath(RepoRoot, file)}:{lineNumber} 「{match.Value}」");
                    }
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "源码注释不承载变更史：issue 号与批次/阶段编号一律不写（ADR 决策引用与模块代号可以写）；"
                + "指向未来动作的短待办用 TODO 形式。" + Environment.NewLine
                + string.Join(Environment.NewLine, offenders));
    }

    // --- 基建：编号禁令的扫描面与注释行判定 -----------------------------------------
    /// <summary>注释编号禁令的扫描面：C# / XAML 源码与工程、构建属性、脚本。</summary>
    private static readonly string[] CommentScanPatterns =
    {
        "*.cs", "*.xaml", "*.csproj", "*.props", "*.targets", "*.ps1",
    };

    /// <summary>
    /// 按后缀枚举注释行：C# 的 <c>//</c>、PowerShell 的 <c>#</c>、XML 的跨行 <c>&lt;!-- --&gt;</c>。
    /// 三种形态的行首标记不同，只按 <c>//</c> 判定会漏掉脚本与工程文件。
    /// </summary>
    private static IEnumerable<(int LineNumber, string Line)> CommentLines(string file)
    {
        string suffix = Path.GetExtension(file).ToLowerInvariant();
        bool inXmlComment = false;
        int lineNumber = 0;
        foreach (string line in File.ReadLines(file))
        {
            lineNumber++;
            string trimmed = line.TrimStart();
            switch (suffix)
            {
                case ".cs":
                    if (trimmed.StartsWith("//", StringComparison.Ordinal))
                    {
                        yield return (lineNumber, line);
                    }

                    break;
                case ".ps1":
                    if (trimmed.StartsWith('#'))
                    {
                        yield return (lineNumber, line);
                    }

                    break;
                default:
                    bool opened = line.Contains("<!--", StringComparison.Ordinal);
                    bool closed = line.Contains("-->", StringComparison.Ordinal);
                    if (inXmlComment || opened)
                    {
                        yield return (lineNumber, line);
                    }

                    if (opened && !closed)
                    {
                        inXmlComment = true;
                    }
                    else if (inXmlComment && closed)
                    {
                        inXmlComment = false;
                    }

                    break;
            }
        }
    }

    // --- 入口路由表与磁盘叶子一一对应 -----------------------------------------------
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

    // --- ADR 头部状态合法 -----------------------------------------------------------
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

    // --- ADR 引用无死链 -------------------------------------------------------------
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

    // --- 叶子不残留完成态流水与日期快照 ---------------------------------------------
    [Fact]
    public void 叶子不残留完成态流水与日期快照()
    {
        List<string> offenders = ScanForbiddenPatterns(
            Directory.EnumerateFiles(LeafDir, "*.md"), ReadConventionsPatterns());

        Assert.True(
            offenders.Count == 0,
            "叶子不记录完成态流水、等号式状态标记与日期快照（文约 §2）：" + Environment.NewLine
                + string.Join(Environment.NewLine, offenders));
    }

    // --- layout 树：路径存在与目录登记 ----------------------------------------------
    [Fact]
    public void layout树中的路径在磁盘存在()
    {
        string[] treePaths = EnumerateTreePaths().ToArray();
        Assert.True(treePaths.Length > 0, "未能从 layout.md §1 解析出任何路径——树结构或小节标题变了？");
        string[] offenders = treePaths
            .Where(path => !File.Exists(Path.Combine(RepoRoot, path)) && !Directory.Exists(Path.Combine(RepoRoot, path)))
            .ToArray();
        Assert.True(
            offenders.Length == 0,
            "§1 树是物理路径正典：树中路径必须在磁盘存在（文约 §1：失真优先于重复）：" + Environment.NewLine
                + string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void 四集与plugins的一级目录都在layout树登记()
    {
        HashSet<string> treePaths = EnumerateTreePaths().ToHashSet(StringComparer.Ordinal);
        var offenders = new List<string>();
        foreach (string root in SourceRoots)
        {
            string rootPath = Path.Combine(RepoRoot, root);
            Assert.True(Directory.Exists(rootPath), $"源码根在磁盘不存在：{root}");
            offenders.AddRange(Directory.EnumerateDirectories(rootPath)
                .Select(directory => Path.GetFileName(directory))
                .Where(name => !IsArtifactDirectory(name) && !treePaths.Contains($"{root}/{name}"))
                .Select(name => $"{root}/{name}"));
        }

        if (!treePaths.Contains("plugins"))
        {
            offenders.Add("plugins");
        }

        Assert.True(
            offenders.Count == 0,
            "layout.md §1 树是物理路径正典：源码根一级子目录必须登记（新增目录 → 树里补一行，见文约「维护义务」3）："
                + Environment.NewLine
                + string.Join(Environment.NewLine, offenders));
    }

    // --- 文档反引号类型名在源码命中 -------------------------------------------------
    [Fact]
    public void 文档反引号类型名在源码命中()
    {
        string skipMarker = ReadRule(TypeNameSection, "跳过行标记");
        string placeholder = ReadRule(TypeNameSection, "占位片段");
        HashSet<string> exempt = ReadConventionsTokens(ExemptionSection);
        HashSet<string> sourceIdentifiers = SourceIdentifiers.Value;
        var offenders = new List<string>();
        int checkedCount = 0;

        foreach (string file in EnumerateFiles(DocsRoot, "*.md"))
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

                if (insideFence || line.Contains(skipMarker, StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (Match span in InlineCodeSpan.Matches(line))
                {
                    string token = span.Groups[1].Value.Trim();
                    if (!PascalCaseIdentifier.IsMatch(token) || token.Contains(placeholder, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    checkedCount++;
                    if (!exempt.Contains(token) && !sourceIdentifiers.Contains(token))
                    {
                        offenders.Add($"{Path.GetRelativePath(RepoRoot, file)}:{lineNumber} 「{token}」");
                    }
                }
            }
        }

        Assert.True(checkedCount > 100, "文档里解析出的候选类型名过少——扫描面或提取口径坏了？");
        Assert.True(
            offenders.Count == 0,
            "文档把不存在的名字当类型名用：改为源码里的真名，或用 `Xxx` 占位写法，或把外部框架名／ADR 历史名加进文约「豁免清单」："
                + Environment.NewLine
                + string.Join(Environment.NewLine, offenders));
    }

    // --- ADR 不残留日期快照与等号式状态标记 -----------------------------------------
    [Fact]
    public void ADR不残留日期快照与等号式状态标记()
    {
        string[] patterns = ReadConventionsBlock(AdrPatternsSection);
        string[] notSubset = patterns.Except(ReadConventionsPatterns()).ToArray();
        Assert.True(
            notSubset.Length == 0,
            "ADR 词表须是「机检口径」词表的子集（见文约「ADR 机检口径」）：" + string.Join(", ", notSubset));

        List<string> offenders = ScanForbiddenPatterns(Directory.EnumerateFiles(AdrDir, "*.md"), patterns);
        Assert.True(
            offenders.Count == 0,
            "ADR 不承载时间维度与目标态快照（文约 §6/§7；词表见文约「ADR 机检口径」）：" + Environment.NewLine
                + string.Join(Environment.NewLine, offenders));
    }

    // --- 基建：禁止模式扫描 ---------------------------------------------------------
    /// <summary>扫描指定 Markdown 文件的禁止模式与日期快照，返回「文件名:行 说明」清单。</summary>
    private static List<string> ScanForbiddenPatterns(IEnumerable<string> files, string[] patterns)
    {
        var offenders = new List<string>();
        foreach (string file in files)
        {
            int lineNumber = 0;
            foreach (string line in File.ReadLines(file))
            {
                lineNumber++;
                foreach (string pattern in patterns.Where(pattern => Regex.IsMatch(line, pattern)))
                {
                    offenders.Add($"{Path.GetFileName(file)}:{lineNumber} 禁止模式「{pattern}」");
                }

                if (DateSnapshot.IsMatch(line))
                {
                    offenders.Add($"{Path.GetFileName(file)}:{lineNumber} 日期快照");
                }
            }
        }

        return offenders;
    }

    // --- 叶子的规划标记落在目标态节内 -----------------------------------------------
    [Fact]
    public void 叶子的规划标记落在目标态与差距节内()
    {
        string sectionKeyword = ReadRule(StructureSection, "目标态节标题");
        string marker = ReadRule(StructureSection, "目标态行标记");
        var offenders = new List<string>();
        foreach (string leaf in Directory.EnumerateFiles(LeafDir, "*.md"))
        {
            int lineNumber = 0;
            bool inTargetSection = false;
            bool insideFence = false;
            foreach (string line in File.ReadLines(leaf))
            {
                lineNumber++;
                if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
                {
                    insideFence = !insideFence;
                    continue;
                }

                if (insideFence)
                {
                    continue;
                }

                int headingLevel = HeadingLevel(line);
                if (headingLevel > 0)
                {
                    if (line.Contains(sectionKeyword, StringComparison.Ordinal))
                    {
                        inTargetSection = true;
                    }
                    else if (headingLevel <= 2)
                    {
                        inTargetSection = false;
                    }
                }

                if (!inTargetSection && line.Contains(marker, StringComparison.Ordinal))
                {
                    offenders.Add($"{Path.GetFileName(leaf)}:{lineNumber} 「{marker}」不在含「{sectionKeyword}」的小节内");
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "叶子只许在「目标态与差距」节承载目标态（文约 §6；口径见文约「目标态结构口径」）：" + Environment.NewLine
                + string.Join(Environment.NewLine, offenders));
    }

    // --- 基建：标题层级与 layout 树解析 ---------------------------------------------
    /// <summary>Markdown 标题层级（`# ` 为 1、`## ` 为 2…）；非标题行返回 0。</summary>
    private static int HeadingLevel(string line)
    {
        int level = 0;
        while (level < line.Length && line[level] == '#')
        {
            level++;
        }

        return level > 0 && level < line.Length && line[level] == ' ' ? level : 0;
    }

    /// <summary>layout.md §1 树里登记的物理路径（相对仓库根，如 <c>StarPie.Ui/Adapters</c>）。</summary>
    private static IEnumerable<string> EnumerateTreePaths()
    {
        string treeFile = Path.Combine(LeafDir, "layout.md");
        var segments = new List<string>();
        bool inTreeSection = false;
        bool insideFence = false;

        foreach (string line in File.ReadLines(treeFile))
        {
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                inTreeSection = line.StartsWith("## 1.", StringComparison.Ordinal);
                insideFence = false;
                continue;
            }

            if (!inTreeSection)
            {
                continue;
            }

            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                insideFence = !insideFence;
                continue;
            }

            if (!insideFence)
            {
                continue;
            }

            Match match = Regex.Match(line, @"^((?:[│ ]   )*)(?:├── |└── )?(.*)$");
            int depth = match.Groups[1].Value.Length / 4;
            string namePart = match.Groups[2].Value.Split('#')[0].Trim().TrimEnd('/');
            if (namePart.Length == 0)
            {
                continue;
            }

            segments = segments.Take(depth).ToList();
            string parent = string.Join("/", segments);
            foreach (string rawName in namePart.Split(" / ", StringSplitOptions.RemoveEmptyEntries))
            {
                string name = rawName.Trim();
                if (name == "StarPie")
                {
                    continue;
                }

                yield return parent.Length == 0 ? name : $"{parent}/{name}";
                segments.Add(name);
            }
        }
    }

    // --- 基建：文约围栏块与规则读取 -------------------------------------------------
    /// <summary>读文约「机检口径」小节围栏块里的 ERE 模式（<c>#</c> 行为注释）。</summary>
    private static string[] ReadConventionsPatterns() => ReadConventionsBlock(PatternsSection);

    /// <summary>读文约指定小节的围栏块（跳过空行与 <c>#</c> 注释行）。</summary>
    private static string[] ReadConventionsBlock(string section)
    {
        string[] lines = File.ReadAllLines(ConventionsFile);
        int index = Array.FindIndex(lines, line => line.StartsWith(section, StringComparison.Ordinal));
        Assert.True(index >= 0, $"文约缺少「{section}」小节：{Path.GetRelativePath(RepoRoot, ConventionsFile)}");

        while (index < lines.Length && !lines[index].TrimStart().StartsWith("```", StringComparison.Ordinal))
        {
            index++;
        }

        Assert.True(index < lines.Length, $"文约「{section}」小节缺少围栏块");
        index++;
        var entries = new List<string>();
        for (; index < lines.Length && !lines[index].TrimStart().StartsWith("```", StringComparison.Ordinal); index++)
        {
            string line = lines[index].Trim();
            if (line.Length > 0 && !line.StartsWith('#'))
            {
                entries.Add(line);
            }
        }

        Assert.True(entries.Count > 0, $"文约「{section}」围栏块为空");
        return entries.ToArray();
    }

    /// <summary>读文约指定小节里的 `<规则名> @ <值>` 规则值（恰好一条，否则断言失败）。</summary>
    private static string ReadRule(string section, string name)
    {
        string[] values = ReadConventionsBlock(section)
            .Where(entry => entry.Contains(" @ ", StringComparison.Ordinal))
            .Where(entry => entry[..entry.IndexOf(" @ ", StringComparison.Ordinal)].Trim() == name)
            .Select(entry => entry[(entry.IndexOf(" @ ", StringComparison.Ordinal) + 3)..].Trim())
            .ToArray();
        Assert.True(values.Length == 1, $"文约「{section}」须恰好一条规则「{name}」（格式：`<规则名> @ <值>`）");
        return values[0];
    }

    /// <summary>读文约指定小节的 token 清单（`<token>  # <理由>`，取 `#` 之前的部分）。</summary>
    private static HashSet<string> ReadConventionsTokens(string section) =>
        ReadConventionsBlock(section)
            .Select(entry => entry.Split('#')[0].Trim())
            .Where(token => token.Length > 0)
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>构建产物与 IDE 工作区目录：不参与「源码根一级子目录」判定。</summary>
    private static bool IsArtifactDirectory(string name) =>
        name.StartsWith(".", StringComparison.Ordinal)
        || ExcludedDirs.Contains(name, StringComparer.OrdinalIgnoreCase);

    // --- 文档声称的常量归属与源码一致 -----------------------------------------------
    [Fact]
    public void 文档声称的常量归属与源码一致()
    {
        string[] claims = ReadConventionsBlock(OwnershipSection);
        var offenders = new List<string>();
        foreach (string claim in claims)
        {
            int separator = claim.IndexOf(" @ ", StringComparison.Ordinal);
            if (separator <= 0)
            {
                offenders.Add($"格式应为 `<名字> @ <源码相对路径>`：{claim}");
                continue;
            }

            string name = claim[..separator].Trim();
            string relativePath = claim[(separator + 3)..].Trim();
            string fullPath = Path.Combine(RepoRoot, relativePath);
            if (!File.Exists(fullPath))
            {
                offenders.Add($"归属断言指向不存在的文件：{relativePath}");
                continue;
            }

            if (!File.ReadAllText(fullPath).Contains(name, StringComparison.Ordinal))
            {
                offenders.Add($"{name} 已不在 {relativePath}——文档的归属声称需同步");
            }
        }

        Assert.True(
            offenders.Count == 0,
            "文约「归属断言」与源码不符（文约 §1：失真优先于重复）：" + Environment.NewLine
                + string.Join(Environment.NewLine, offenders));
    }

    // --- 基建：ADR 编号集、引用判定与扫描面枚举 -------------------------------------
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
