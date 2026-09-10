using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;

namespace P0.Host;

internal static class Program
{
    private const string PluginFileName = "P0.Plugin.dll";
    private const string TargetFramework = "net10.0-windows";

    /// <summary>探针目录：8 项白名单条目 + BAML 两态 + 3 项负对照。</summary>
    private static readonly (string Name, string EntryType, bool Ctx)[] Catalog =
    {
        ("baml-noctx", "P0.Plugin.Probes.BamlProbe", false),
        ("baml-ctx", "P0.Plugin.Probes.BamlProbe", true),
        ("view", "P0.Plugin.Probes.ViewProbe", true),
        ("window", "P0.Plugin.Probes.WindowProbe", true),
        ("resdict", "P0.Plugin.Probes.ResourceDictionaryProbe", true),
        ("template", "P0.Plugin.Probes.DataTemplateProbe", true),
        ("timer", "P0.Plugin.Probes.TimerProbe", true),
        ("animation", "P0.Plugin.Probes.AnimationProbe", true),
        ("subscription", "P0.Plugin.Probes.SubscriptionProbe", true),
        ("binding", "P0.Plugin.Probes.BindingProbe", true),
        ("neg-static-cache", "P0.Plugin.Probes.NegStaticCacheProbe", true),
        ("neg-app-resources", "P0.Plugin.Probes.NegAppResourcesProbe", true),
        ("neg-dependency-property", "P0.Plugin.Probes.NegDependencyPropertyProbe", true)
    };

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            Console.OutputEncoding = Encoding.UTF8;
        }
        catch
        {
            // 某些宿主不支持改编码，忽略。
        }

        var pluginDirectory = ResolvePluginDirectory(args);
        var pluginPath = Path.Combine(pluginDirectory, PluginFileName);
        if (!File.Exists(pluginPath))
        {
            Console.WriteLine($"找不到插件程序集：{pluginPath}");
            return 2;
        }

        var contractCopies = Directory.GetFiles(pluginDirectory, "P0.Contracts.dll", SearchOption.AllDirectories).Length;
        var packageFiles = Directory.GetFiles(pluginDirectory).Select(Path.GetFileName).OrderBy(n => n).ToArray();

        Console.WriteLine($"插件包目录：{pluginDirectory}");
        Console.WriteLine($"包内文件：{string.Join(", ", packageFiles)}");
        Console.WriteLine($"包内契约副本数（硬约束 3，期望 0）：{contractCopies}");
        Console.WriteLine($"GC 配置：IsServerGC={System.Runtime.GCSettings.IsServerGC} LatencyMode={System.Runtime.GCSettings.LatencyMode}");
        Console.WriteLine();

        var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        Harness.Init();

        // ALC 生命周期归因：装载 / resolver / Load 覆写 / 插件类型 / BAML / 全局缓存 逐变量隔离。
        var diagnostics = AlcDiagnostics.Run(pluginPath);

        var results = new List<ProbeResult>();
        foreach (var probe in Catalog)
        {
            results.Add(ProbeRunner.Run(probe.Name, probe.EntryType, probe.Ctx, pluginPath));
        }

        Harness.Teardown();
        application.Shutdown();

        var reportPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "p0-report.md"));
        ReportWriter.Write(reportPath, results, pluginDirectory, packageFiles, contractCopies, diagnostics);

        Console.WriteLine();
        Console.WriteLine($"报告：{reportPath}");
        var leaked = results.Count(r => !r.UnloadedCleanly);
        Console.WriteLine($"判定汇总：{results.Count - leaked}/{results.Count} 可卸载，{leaked} 未回收");
        return 0;
    }

    private static string ResolvePluginDirectory(string[] args)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "--plugin")
            {
                return Path.GetFullPath(args[i + 1]);
            }
        }

        var srcRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var pluginBin = Path.Combine(srcRoot, "P0.Plugin", "bin");
        if (Directory.Exists(pluginBin))
        {
            var candidates = Directory.GetDirectories(pluginBin, TargetFramework, SearchOption.AllDirectories);
            var release = candidates.FirstOrDefault(c => c.Contains($"{Path.DirectorySeparatorChar}Release{Path.DirectorySeparatorChar}"));
            var chosen = release ?? candidates.FirstOrDefault();
            if (chosen is not null)
            {
                return chosen;
            }
        }

        throw new DirectoryNotFoundException("未找到 P0.Plugin 输出目录，请用 --plugin <dir> 指定。");
    }
}
