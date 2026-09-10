using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;
using P0.Contracts;

namespace P0.Host;

/// <summary>单次探针运行的状态。除 <c>AlcStrong</c>（Unload 前必须持有）外只放弱引用与字符串。</summary>
internal sealed class ProbeRun
{
    public ProbeRun(string name, bool useContextualReflection)
    {
        Name = name;
        UseContextualReflection = useContextualReflection;
    }

    public string Name { get; }

    public bool UseContextualReflection { get; }

    public AssetRegistry Registry { get; } = new();

    // ---- 强引用：全部在判定前显式丢弃 ----
    public AssemblyLoadContext? AlcStrong { get; set; }

    public Assembly? AssemblyStrong { get; set; }

    public IProbeEntry? EntryStrong { get; set; }

    public ProbeContext? ContextStrong { get; set; }

    // ---- 弱引用：判定的唯一依据 ----
    public WeakReference? AlcRef { get; set; }

    public WeakReference? AssemblyRef { get; set; }

    public WeakReference? EntryRef { get; set; }

    /// <summary>插件对象与插件委托的弱引用集合（视图、窗口、字典、模板、定时器回调、事件处理器、Binding…）。</summary>
    public List<WeakReference> ObjectRefs { get; } = new();

    public List<string> Notes { get; } = new();

    public List<string> Observations { get; } = new();

    public List<string> RootsBeforeCleanup { get; } = new();

    public List<string> RootsAfterCleanup { get; } = new();

    public List<string> RootsAfterSweep { get; } = new();

    public bool Configured { get; set; }

    public bool Materialized { get; set; }

    public bool Stopped { get; set; }

    public bool Cleaned { get; set; }

    public bool Swept { get; set; }

    public bool Unloaded { get; set; }

    public string Error { get; set; } = string.Empty;

    public void Note(string message) => Notes.Add(message);
}

internal sealed record Snapshot(int AliveObjects, bool AlcAlive, bool AssemblyAlive, bool EntryAlive);

internal sealed record ProbeResult(ProbeRun Run, Snapshot AfterCleanup, Snapshot AfterUnload, bool UnloadedCleanly);

/// <summary>探针执行器：装载 → 登记 → 宿主创建 → 运行 → 清理 → 全局根扫描 → GC → WeakReference 判定 → Unload → 再判定。</summary>
internal static class ProbeRunner
{
    public static ProbeResult Run(string probeName, string entryTypeName, bool useContextualReflection, string pluginAssemblyPath)
    {
        Console.WriteLine($"--- probe {probeName} (ctx={useContextualReflection}) ---");
        var run = new ProbeRun(probeName, useContextualReflection);

        ExecuteProbe(run, entryTypeName, pluginAssemblyPath);
        DropStrongRefs(run);

        Collect();
        var afterCleanup = Take(run);

        Unload(run);
        Collect();
        var afterUnload = Take(run);

        var clean = run.Unloaded
                    && !afterUnload.AlcAlive
                    && !afterUnload.AssemblyAlive
                    && afterUnload.AliveObjects == 0;

        var result = new ProbeResult(run, afterCleanup, afterUnload, clean);
        Console.WriteLine($"    verdict: {(clean ? "UNLOADED" : "LEAKED")} | 组件存活={afterUnload.AliveObjects}/{run.ObjectRefs.Count} alcAlive={afterUnload.AlcAlive} asmAlive={afterUnload.AssemblyAlive}");
        foreach (var note in run.Notes)
        {
            Console.WriteLine($"    note: {note}");
        }

        foreach (var finding in run.RootsAfterCleanup.Concat(run.RootsAfterSweep))
        {
            Console.WriteLine($"    root: {finding}");
        }

        return result;
    }

    /// <summary>装载、登记、创建、运行、清理、扫描。所有插件类型局部变量都死在这一帧内。</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ExecuteProbe(ProbeRun run, string entryTypeName, string pluginAssemblyPath)
    {
        var alc = new PluginLoadContext(pluginAssemblyPath);
        run.AlcStrong = alc;
        run.AlcRef = new WeakReference(alc);

        IDisposable? scope = null;
        try
        {
            var assembly = alc.LoadFromAssemblyPath(pluginAssemblyPath);
            run.AssemblyStrong = assembly;
            run.AssemblyRef = new WeakReference(assembly);

            if (run.UseContextualReflection)
            {
                // 宿主包装方式（P0 问题 1）：进入插件 ALC 的上下文反射域后，
                // WPF/框架内部触发的 Assembly.Load 才会落到本 ALC。
                scope = alc.EnterContextualReflection();
                run.Note("已进入 EnterContextualReflection 作用域");
            }
            else
            {
                run.Note("未使用 EnterContextualReflection（对照）");
            }

            var type = assembly.GetType(entryTypeName, throwOnError: true)!;
            var entry = (IProbeEntry)Activator.CreateInstance(type)!;
            run.EntryStrong = entry;
            run.EntryRef = new WeakReference(entry);
            run.ObjectRefs.Add(new WeakReference(entry));

            var context = new ProbeContext(run);
            run.ContextStrong = context;
            entry.Configure(context);
            run.Configured = true;

            Materialize(run);
            Exercise(run);

            run.RootsBeforeCleanup.AddRange(GlobalRootScan.Scan(assembly, "cleanup 前"));

            entry.Stop();
            run.Stopped = true;
            run.EntryStrong = null;
            run.ContextStrong = null;

            Cleanup(run);
            run.Cleaned = true;
            run.Note($"宿主事件订阅数（cleanup 后）={HostEventHub.SubscriberCount}");
            run.RootsAfterCleanup.AddRange(GlobalRootScan.Scan(assembly, "cleanup 后"));

            if (run.RootsAfterCleanup.Count > 0)
            {
                // 泄漏隔离：按扫描结果摘除插件拥有的全局根，验证「扫描能定位 → 摘除能回收」。
                SweepQuarantine(run, assembly);
                run.Swept = true;
                run.RootsAfterSweep.AddRange(GlobalRootScan.Scan(assembly, "隔离摘除后"));
            }
        }
        catch (Exception ex)
        {
            run.Error = Describe(ex);
            run.Note($"探针异常: {ex}");
            TryEmergencyUnload(run);
        }
        finally
        {
            scope?.Dispose();
        }
    }

    /// <summary>宿主在 UI 线程调用工厂创建资产，产物先入登记表再进视觉树（§5.1 约束 6）。</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Materialize(ProbeRun run)
    {
        var registry = run.Registry;
        foreach (var asset in registry.Pending.ToArray())
        {
            object? created;
            try
            {
                created = asset.Factory();
            }
            catch (Exception ex)
            {
                run.Note($"创建失败 {asset.Kind}/{asset.Name}: {ex.GetType().Name}: {ex.Message}");
                run.Observations.Add($"{asset.Kind}/{asset.Name}: 创建失败");
                continue;
            }

            if (created is null)
            {
                run.Note($"{asset.Kind}/{asset.Name}: 工厂返回 null");
                run.Observations.Add($"{asset.Kind}/{asset.Name}: null");
                continue;
            }

            registry.Live.Add(created);
            run.ObjectRefs.Add(new WeakReference(created));

            switch (asset.Kind)
            {
                case ProbeAssetKind.View:
                    var view = (Control)created;
                    Harness.ContentHost.Content = view;
                    run.Observations.Add($"view/{asset.Name}: {created.GetType().Name} 已进宿主容器");
                    break;

                case ProbeAssetKind.Window:
                    var window = (Window)created;
                    registry.PluginWindows.Add(window);
                    window.Show();
                    Harness.DoEvents(80);
                    run.Observations.Add($"window/{asset.Name}: shown={window.IsVisible} loaded={window.IsLoaded}");
                    break;

                case ProbeAssetKind.ResourceDictionary:
                    if (registry.MergedViaUri)
                    {
                        run.Note("资源字典已走 pack URI 路径，跳过工厂回退");
                    }
                    else
                    {
                        run.ContextStrong!.MergeIntoPluginRoot((ResourceDictionary)created);
                        run.Observations.Add($"resdict/{asset.Name}: 走工厂回退并入");
                    }

                    break;

                case ProbeAssetKind.DataTemplate:
                    var template = (DataTemplate)created;
                    Harness.ContentHost.ContentTemplate = template;
                    var dataType = template.DataType as Type;
                    if (dataType is not null)
                    {
                        var content = Activator.CreateInstance(dataType);
                        registry.Live.Add(content!);
                        run.ObjectRefs.Add(new WeakReference(content!));
                        Harness.ContentHost.Content = content;
                        run.Observations.Add($"template/{asset.Name}: DataType={dataType.Name} 内容已套用");
                    }
                    else
                    {
                        run.Observations.Add($"template/{asset.Name}: DataType 非 Type（{template.DataType?.GetType().Name ?? "null"}）");
                    }

                    break;

                case ProbeAssetKind.Timer:
                    run.Observations.Add($"timer/{asset.Name}: 已由宿主签发并启动");
                    break;

                case ProbeAssetKind.Animation:
                    var storyboard = (Storyboard)created;
                    storyboard.Begin(Harness.AnimationTarget, isControllable: true);
                    run.Observations.Add($"animation/{asset.Name}: 已在宿主元素上 Begin");
                    break;

                case ProbeAssetKind.Subscription:
                    run.Observations.Add($"subscription/{asset.Name}: 句柄已登记");
                    break;

                case ProbeAssetKind.Binding:
                    var binding = (Binding)created;
                    BindingOperations.SetBinding(Harness.BindingTarget, TextBlock.TextProperty, binding);
                    run.Observations.Add($"binding/{asset.Name}: 已挂到宿主 TextBlock");
                    break;
            }
        }

        run.Materialized = true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Exercise(ProbeRun run)
    {
        Harness.DoEvents(260);
        HostEventHub.Publish("ping");
        Harness.DoEvents(80);

        var registry = run.Registry;
        run.Observations.Add($"登记表: 登记={registry.Pending.Count} 已创建={registry.Live.Count} 句柄={registry.Handles.Count} 插件窗口={registry.PluginWindows.Count}");
        foreach (var (key, value) in registry.Reports)
        {
            run.Observations.Add($"插件回执 {key}={value}");
        }

        run.Observations.Add($"宿主事件订阅数（运行中）={HostEventHub.SubscriberCount}");
        run.Observations.Add($"ContentHost 视觉子元素={VisualTreeHelper.GetChildrenCount(Harness.ContentHost)}");
        run.Observations.Add($"BindingTarget.Text='{Harness.BindingTarget.Text}' AnimationTarget.Opacity={Harness.AnimationTarget.Opacity:0.00}");
        run.Observations.Add($"Application.Windows={Application.Current.Windows.Count}");
        run.Observations.Add($"ALC 私有程序集={string.Join(", ", (run.AlcStrong as PluginLoadContext)?.LoadedPrivateAssemblies ?? Array.Empty<string>())}");
    }

    /// <summary>卸载管线 §8 步骤 4：UI 线程上的有序清理。</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Cleanup(ProbeRun run)
    {
        var registry = run.Registry;

        // a. 视图/模板清出视觉树，断 DataContext / Binding；摘除宿主元素上的动画与绑定
        Harness.ContentHost.Content = null;
        Harness.ContentHost.ContentTemplate = null;
        Harness.ContentHost.DataContext = null;
        BindingOperations.ClearBinding(Harness.BindingTarget, TextBlock.TextProperty);
        Harness.BindingTarget.DataContext = null;

        // 宿主签发的动画：把 Storyboard 从宿主元素上摘除（Remove 才会释放时钟树）
        foreach (var storyboard in registry.Live.OfType<Storyboard>())
        {
            storyboard.Remove(Harness.AnimationTarget);
            storyboard.Stop(Harness.AnimationTarget);
        }

        Harness.AnimationTarget.BeginAnimation(UIElement.OpacityProperty, null);
        Harness.AnimationTarget.Opacity = 1.0;

        // b. 关闭插件窗口并等待 Closed
        foreach (var window in registry.PluginWindows)
        {
            if (window.IsVisible)
            {
                window.Close();
            }
        }

        var closed = Harness.WaitUntil(
            () => !Application.Current.Windows.Cast<Window>().Any(registry.PluginWindows.Contains));
        if (!closed)
        {
            run.Note("插件窗口未在超时内关闭");
        }

        // c. 从 MergedDictionaries 摘除插件资源根（一次摘净）
        if (registry.PluginResourceRoot is { } root)
        {
            Application.Current.Resources.MergedDictionaries.Remove(root);
            root.MergedDictionaries.Clear();
        }

        // e/f. 停止宿主签发定时器、断开宿主中介订阅（按句柄整体释放，不依赖插件自觉 Dispose）
        foreach (var handle in registry.Handles)
        {
            handle.Dispose();
        }

        // g. 断言资产登记表清零
        registry.ClearAll();
        Harness.DoEvents(60);
    }

    /// <summary>泄漏隔离：按全局根扫描结果摘除插件拥有的全局根（模拟"隔离 + 手工摘除"）。</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void SweepQuarantine(ProbeRun run, Assembly pluginAssembly)
    {
        var merged = Application.Current.Resources.MergedDictionaries;
        var removed = 0;
        for (var i = merged.Count - 1; i >= 0; i--)
        {
            if (merged[i].GetType().Assembly == pluginAssembly)
            {
                merged.RemoveAt(i);
                removed++;
            }
        }

        run.Note($"隔离摘除：Application.Current.Resources.MergedDictionaries 移除插件字典 {removed} 个");
        Harness.DoEvents(40);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void TryEmergencyUnload(ProbeRun run)
    {
        try
        {
            Cleanup(run);
            run.Cleaned = true;
        }
        catch (Exception ex)
        {
            run.Note($"兜底清理失败: {ex.GetType().Name}: {ex.Message}");
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void DropStrongRefs(ProbeRun run)
    {
        run.EntryStrong = null;
        run.ContextStrong = null;
        run.AssemblyStrong = null;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Unload(ProbeRun run)
    {
        var alc = run.AlcStrong;
        if (alc is null)
        {
            return;
        }

        alc.Unload();
        run.AlcStrong = null;
        run.Unloaded = true;

        for (var i = 0; i < 12 && (run.AlcRef?.IsAlive ?? false); i++)
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);
            GC.WaitForPendingFinalizers();
        }
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

    private static Snapshot Take(ProbeRun run) => new(
        run.ObjectRefs.Count(reference => reference.IsAlive),
        run.AlcRef?.IsAlive ?? false,
        run.AssemblyRef?.IsAlive ?? false,
        run.EntryRef?.IsAlive ?? false);

    /// <summary>把异常链摊平成一行。</summary>
    private static string Describe(Exception exception)
    {
        var text = $"{exception.GetType().Name}: {exception.Message}";
        var inner = exception.InnerException;
        var depth = 0;
        while (inner is not null && depth++ < 4)
        {
            text += $" <- {inner.GetType().Name}: {inner.Message}";
            inner = inner.InnerException;
        }

        return text;
    }
}
