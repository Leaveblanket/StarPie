using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Windows;
using P0.Contracts;

namespace P0.Host;

/// <summary>诊断用 ALC：可分别关掉 AssemblyDependencyResolver 与 Load 覆写。</summary>
internal sealed class DiagLoadContext : AssemblyLoadContext
{
    private static readonly HashSet<string> SharedAssemblies = new(StringComparer.OrdinalIgnoreCase) { "P0.Contracts" };

    private readonly AssemblyDependencyResolver? _resolver;
    private readonly bool _overrideLoad;

    public DiagLoadContext(string pluginPath, bool useResolver, bool overrideLoad)
        : base($"P0.Diag:{Guid.NewGuid():N}", isCollectible: true)
    {
        _resolver = useResolver ? new AssemblyDependencyResolver(pluginPath) : null;
        _overrideLoad = overrideLoad;
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (!_overrideLoad)
        {
            return null;
        }

        var name = assemblyName.Name ?? string.Empty;
        if (SharedAssemblies.Contains(name)
            || name.StartsWith("System.", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var path = _resolver?.ResolveAssemblyToPath(assemblyName);
        return path is null ? null : LoadFromAssemblyPath(path);
    }
}

/// <summary>
/// 把「collectible ALC 装完插件后回收不了」归因到具体机制：
/// 装载本身 / AssemblyDependencyResolver / Load 覆写 / BAML / 插件 UI 类型 / WPF 全局缓存。
/// </summary>
internal static class AlcDiagnostics
{
    private const string PluginAssemblyName = "P0.Plugin";

    private sealed record Case(
        string Label,
        string EntryType,
        bool UseResolver,
        bool OverrideLoad,
        bool RunPluginCode,
        bool Unload,
        bool UseContentHost = true,
        bool PurgeCaches = false);

    private sealed class DiagState
    {
        public AssemblyLoadContext? Alc { get; set; }

        public WeakReference? AlcRef { get; set; }

        public WeakReference? AssemblyRef { get; set; }

        public WeakReference? EntryRef { get; set; }

        public List<WeakReference> AssetRefs { get; } = new();

        public string Detail { get; set; } = string.Empty;
    }

    private static readonly Case[] Cases =
    {
        new("A 仅装载（resolver + Load覆写）", "P0.Plugin.Probes.ViewProbe", true, true, false, true),
        new("B 仅装载（无 resolver）", "P0.Plugin.Probes.ViewProbe", false, true, false, true),
        new("C 仅装载（纯 ALC，无覆写）", "P0.Plugin.Probes.ViewProbe", false, false, false, true),
        new("D 插件 POCO（不进宿主容器）", "P0.Plugin.Probes.DiagPocoProbe", true, true, true, true, false),
        new("D2 插件 POCO（进宿主容器）", "P0.Plugin.Probes.DiagPocoProbe", true, true, true, true),
        new("E BAML 字典（只含框架类型）", "P0.Plugin.Probes.DiagFrameworkDictionaryProbe", true, true, true, true),
        new("F BAML 字典（含插件类型）", "P0.Plugin.Probes.DiagBamlDictionaryProbe", true, true, true, true),
        new("G BAML 视图（UserControl）", "P0.Plugin.Probes.ViewProbe", true, true, true, true),
        new("H BAML 窗口（Window）", "P0.Plugin.Probes.WindowProbe", true, true, true, true),
        new("I 视图 + 不 Unload（对照）", "P0.Plugin.Probes.ViewProbe", true, true, true, false),
        new("J 视图 + 清 WPF 全局缓存", "P0.Plugin.Probes.ViewProbe", true, true, true, true, true, true),
        new("K BAML 字典 + 清 WPF 全局缓存", "P0.Plugin.Probes.DiagFrameworkDictionaryProbe", true, true, true, true, true, true),
        new("L 视图（无 resolver，有覆写）", "P0.Plugin.Probes.ViewProbe", false, true, true, true),
        new("M 视图（无 resolver，无覆写）", "P0.Plugin.Probes.ViewProbe", false, false, true, true),
        new("N 视图 + 清缓存 + 无 resolver", "P0.Plugin.Probes.ViewProbe", false, true, true, true, true, true)
    };

    public static List<string> Run(string pluginPath)
    {
        var log = new List<string>();
        Emit(log, "=== ALC 生命周期诊断（逐变量隔离） ===");
        foreach (var item in Cases)
        {
            Measure(pluginPath, item, log);
        }

        Emit(log, string.Empty);
        return log;
    }

    /// <summary>只跑一个有代表性的用例：跑完保持进程存活，供外部（dotnet-dump/SOS）抓取托管堆定位根。</summary>
    public static void RunOne(string pluginPath, string labelPrefix, List<string> log)
    {
        var item = Cases.FirstOrDefault(c => c.Label.StartsWith(labelPrefix, StringComparison.Ordinal));
        if (item is null)
        {
            Emit(log, $"未找到诊断用例：{labelPrefix}");
            return;
        }

        Emit(log, "=== ALC 单例诊断（dump 取证模式） ===");
        Measure(pluginPath, item, log);
    }

    private static void Measure(string pluginPath, Case item, List<string> log)
    {
        var state = Stage(pluginPath, item);
        if (item.PurgeCaches)
        {
            foreach (var line in RootFinder.Purge(PluginAssemblyName))
            {
                Emit(log, $"      purge: {line}");
            }

            foreach (var line in RootFinder.PurgeBamlTypeTable(PluginAssemblyName))
            {
                Emit(log, $"      purge: {line}");
            }

            foreach (var line in RootFinder.PurgeReachable(PluginAssemblyName, maxDepth: 4, budget: 100000))
            {
                Emit(log, $"      purge: {line}");
            }
        }

        if (item.Unload)
        {
            Unstage(state);
        }

        Collect();
        var alive = state.AlcRef!.IsAlive;
        Emit(
            log,
            $"{item.Label,-34} alc={(alive ? "存活" : "已回收")} " +
            $"asm={(state.AssemblyRef!.IsAlive ? "存活" : "已回收")} " +
            $"entry={(state.EntryRef is null ? "-" : state.EntryRef.IsAlive ? "存活" : "已回收")} " +
            $"assets={state.AssetRefs.Count(r => r.IsAlive)}/{state.AssetRefs.Count} {state.Detail}");

        if (alive)
        {
            foreach (var hit in RootFinder.Find(PluginAssemblyName))
            {
                Emit(log, $"      root: {hit}");
            }
        }
    }

    /// <summary>装载 + 跑插件注册 + 宿主创建 + 清理；所有插件类型局部变量死在这一帧。</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static DiagState Stage(string pluginPath, Case item)
    {
        var state = new DiagState();
        var alc = new DiagLoadContext(pluginPath, item.UseResolver, item.OverrideLoad);
        state.Alc = alc;
        state.AlcRef = new WeakReference(alc);

        try
        {
            var assembly = alc.LoadFromAssemblyPath(pluginPath);
            state.AssemblyRef = new WeakReference(assembly);

            if (item.RunPluginCode)
            {
                var type = assembly.GetType(item.EntryType, throwOnError: true)!;
                var entry = (IProbeEntry)Activator.CreateInstance(type)!;
                state.EntryRef = new WeakReference(entry);

                var run = new ProbeRun("diag", true);
                var context = new ProbeContext(run);
                entry.Configure(context);

                var labels = new List<string>();
                foreach (var asset in run.Registry.Pending.ToArray())
                {
                    var created = asset.Factory();
                    if (created is null)
                    {
                        continue;
                    }

                    run.Registry.Live.Add(created);
                    state.AssetRefs.Add(new WeakReference(created));
                    labels.Add(asset.Kind.ToString());
                    if (item.UseContentHost && asset.Kind == ProbeAssetKind.View)
                    {
                        Harness.ContentHost.Content = created;
                    }
                }

                Harness.DoEvents(150);
                Harness.ContentHost.Content = null;
                Harness.ContentHost.DataContext = null;
                foreach (var window in run.Registry.PluginWindows)
                {
                    if (window.IsVisible)
                    {
                        window.Close();
                    }
                }

                Harness.DoEvents(60);
                if (run.Registry.PluginResourceRoot is { } root)
                {
                    Application.Current.Resources.MergedDictionaries.Remove(root);
                    root.MergedDictionaries.Clear();
                }

                run.Registry.ClearAll();
                entry.Stop();
                state.Detail = labels.Count == 0 ? "（无资产）" : $"assets={string.Join("+", labels)}";
            }
        }
        catch (Exception ex)
        {
            state.Detail = $"异常 {ex.GetType().Name}: {ex.Message}";
        }

        return state;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Unstage(DiagState state)
    {
        state.Alc?.Unload();
        state.Alc = null;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Collect()
    {
        for (var i = 0; i < 4; i++)
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);
            GC.WaitForPendingFinalizers();
        }

        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);
    }

    private static void Emit(List<string> log, string line)
    {
        Console.WriteLine(line);
        log.Add(line);
    }
}
