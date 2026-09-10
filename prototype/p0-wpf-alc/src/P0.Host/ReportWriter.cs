using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace P0.Host;

internal static class ReportWriter
{
    private static readonly Dictionary<string, string> Dimensions = new(StringComparer.Ordinal)
    {
        ["view"] = "插件视图（XAML UserControl）",
        ["window"] = "插件窗口（XAML Window）",
        ["resdict"] = "插件资源字典",
        ["template"] = "插件 DataTemplate",
        ["timer"] = "宿主签发 DispatcherTimer",
        ["animation"] = "宿主中介动画（Storyboard）",
        ["subscription"] = "宿主中介事件订阅",
        ["binding"] = "插件 Binding",
        ["baml-ctx"] = "BAML / pack URI（EnterContextualReflection）",
        ["baml-noctx"] = "BAML / pack URI（对照：无上下文反射）",
        ["neg-static-cache"] = "负对照：插件静态缓存",
        ["neg-app-resources"] = "负对照：绕过契约直并全局资源",
        ["neg-dependency-property"] = "负对照：插件自建 DependencyProperty"
    };

    public static void Write(
        string path,
        IReadOnlyList<ProbeResult> results,
        string pluginDirectory,
        IReadOnlyList<string?> packageFiles,
        int contractCopies,
        IReadOnlyList<string> diagnostics)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# P0 打样报告：collectible ALC 中的 WPF 插件能否真正卸载");
        builder.AppendLine();
        builder.AppendLine("> 由 `prototype/p0-wpf-alc` 的宿主一键生成（`run.cmd` / `./run.sh`）。");
        builder.AppendLine("> 判定口径与 `docs/architecture/plugins.md` §8 一致：资产登记表清零 + 全局根扫描 + WeakReference + GC。");
        builder.AppendLine();

        builder.AppendLine("## 1. 环境与包约束");
        builder.AppendLine();
        builder.AppendLine($"- 插件包目录：`{pluginDirectory}`");
        builder.AppendLine($"- 包内文件：{string.Join("、", packageFiles.Select(f => $"`{f}`"))}");
        builder.AppendLine($"- 包内 `P0.Contracts.dll` 副本数（§5.1 约束 3，期望 0）：**{contractCopies}**");
        builder.AppendLine($"- ServerGC：{System.Runtime.GCSettings.IsServerGC}；GC LatencyMode：{System.Runtime.GCSettings.LatencyMode}");
        builder.AppendLine();

        builder.AppendLine("## 2. 判定汇总");
        builder.AppendLine();
        builder.AppendLine("| 探针 | 维度 | 上下文反射 | cleanup 后残留 | cleanup 后对象存活 | Unload 后 ALC | Unload 后程序集 | 判定 |");
        builder.AppendLine("|---|---|---|---|---|---|---|---|");
        foreach (var result in results)
        {
            var run = result.Run;
            var roots = run.RootsAfterCleanup.Count == 0 ? "0" : $"{run.RootsAfterCleanup.Count}（已隔离摘除）";
            var verdict = run.Error.Length > 0
                ? $"异常：{run.Error}"
                : result.UnloadedCleanly ? "可卸载" : "未回收";
            builder.AppendLine(
                $"| `{run.Name}` | {Dimensions.GetValueOrDefault(run.Name, run.Name)} | {(run.UseContextualReflection ? "是" : "否")} " +
                $"| {roots} | {result.AfterCleanup.AliveObjects}/{run.ObjectRefs.Count} " +
                $"| {(result.AfterUnload.AlcAlive ? "存活" : "已回收")} | {(result.AfterUnload.AssemblyAlive ? "存活" : "已回收")} | {verdict} |");
        }

        builder.AppendLine();
        builder.AppendLine("## 3. 逐探针明细");
        builder.AppendLine();
        foreach (var result in results)
        {
            var run = result.Run;
            builder.AppendLine($"### {run.Name} — {Dimensions.GetValueOrDefault(run.Name, run.Name)}");
            builder.AppendLine();
            builder.AppendLine($"- 流程：Configure={run.Configured} Materialize={run.Materialized} Stop={run.Stopped} Cleanup={run.Cleaned} 隔离摘除={run.Swept} Unload={run.Unloaded}");
            builder.AppendLine($"- 对象探针数：{run.ObjectRefs.Count}（entry + 资产 + 插件委托）");
            builder.AppendLine($"- cleanup 后：对象存活 {result.AfterCleanup.AliveObjects}，程序集存活 {result.AfterCleanup.AssemblyAlive}");
            builder.AppendLine($"- Unload 后：对象存活 {result.AfterUnload.AliveObjects}，程序集存活 {result.AfterUnload.AssemblyAlive}，ALC 存活 {result.AfterUnload.AlcAlive}，entry 存活 {result.AfterUnload.EntryAlive}");
            if (run.Error.Length > 0)
            {
                builder.AppendLine($"- **异常**：{run.Error}");
            }

            AppendList(builder, "运行观测", run.Observations);
            AppendList(builder, "宿主备注", run.Notes);
            AppendList(builder, "全局根残留（cleanup 前）", run.RootsBeforeCleanup);
            AppendList(builder, "全局根残留（cleanup 后）", run.RootsAfterCleanup);
            AppendList(builder, "全局根残留（隔离摘除后）", run.RootsAfterSweep);
            builder.AppendLine();
        }

        builder.AppendLine("## 4. ALC 生命周期归因（逐变量隔离）");
        builder.AppendLine();
        builder.AppendLine("```text");
        foreach (var line in diagnostics)
        {
            builder.AppendLine(line);
        }

        builder.AppendLine("```");
        builder.AppendLine();
        builder.AppendLine("## 5. 说明");
        builder.AppendLine();
        builder.AppendLine("- `cleanup 前` 的全局根命中共存是**预期**：资产此时仍在宿主容器与全局资源里，用来证明扫描器确实看得见插件对象。");
        builder.AppendLine("- 负对照的期望结果是**未回收**：它们证明判定器不是永远报成功。");
        builder.AppendLine("- 插件内部静态缓存这类根不属于 WPF 全局根，扫描器扫不到——这正是 §8 要求 WeakReference 判定兜底的原因。");
        builder.AppendLine();

        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(false));
    }

    private static void AppendList(StringBuilder builder, string title, IReadOnlyList<string> items)
    {
        if (items.Count == 0)
        {
            return;
        }

        builder.AppendLine($"- {title}：");
        foreach (var item in items)
        {
            builder.AppendLine($"  - {item}");
        }
    }
}
