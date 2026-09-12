using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using StarPie.Compatibility;
using StarPie.Events;
using StarPie.Manifest;
using StarPie.PluginHosting;
using StarPie.PluginHosting.Verification;
using StarPie.PluginRuntime.Admission;
using StarPie.PluginRuntime.Diagnostics;
using StarPie.PluginRuntime.Loading;
using StarPie.PluginRuntime.Manifest;
using StarPie.PluginRuntime.Registry;
using StarPie.PluginRuntime.Unloading;

namespace StarPie.Tests;

/// <summary>
/// UI 插件 STA 卸载矩阵（plugins.md §5.2/§8 的测试矩阵）：真实 UI 示例插件包在 collectible
/// ALC + STA harness 里装载 → 逐特性驱动 → 安全点卸载 → 逐项断言「探针对象回收 + 资产登记表
/// 清零 + 全局根扫描无残留」；ALC 与程序集存活只记诊断，不作为 UI 插件的失败判据。
/// </summary>
/// <remarks>
/// 走的是生产链路：清单解析 → 装载管线（UI 线程 RegisterUi）→ 导航/命令/开窗的真实宿主入口 →
/// 安全点卸载管线（ReleasingUi 资产清理 + 泄漏验证）。插件侧探针经插件 ALC 内的
/// <c>SampleUiProbes</c> 弱引用表读取；宿主侧探针（视图/窗口/字典/模板/绑定）由测试在
/// 独立帧里持弱引用——跨卸载持有插件对象强引用会污染回收判定，捕获一律用 NoInlining 帧。
/// </remarks>
public sealed class PluginUiUnloadMatrixTests
{
    private const string PluginId = "starpie.builtin.sample-ui";
    private const string PluginAssemblyName = "StarPie.Plugin.SampleUi";
    private const string PluginProbesTypeName = PluginAssemblyName + ".SampleUiProbes";
    private const string PluginWindowTypeName = PluginAssemblyName + ".SampleUiWindow";
    private const string GreetCommandId = "sample-ui.greet";

    private static readonly string PackageDirectory =
        Path.Combine(AppContext.BaseDirectory, "plugins", PluginId);

    private static readonly string PluginPageIdentifier = $"NavPlugin_{PluginId}";

    [Fact]
    public async Task 装载_四类扩展点与四类中介资产注册就位()
    {
        await StaTestHarness.RunAsync(async () =>
        {
            using LoadFixture fixture = await LoadSampleUiAsync();

            // 资产登记表：资源根 + 资源字典 + 页 + 设置区 + 窗口描述符 + 命令 + 菜单项 + 定时器 + 订阅。
            Assert.Equal(9, fixture.Coordinator.Assets.CountFor(PluginId));

            // 导航页进目录且挂在插件名下；设置区与托盘菜单出现在宿主汇总面。
            Assert.Contains(
                fixture.Catalog.Entries,
                entry => entry.AutomationId == PluginPageIdentifier && entry.PluginId == PluginId);
            Assert.Contains(
                fixture.Coordinator.SettingsSections,
                section => section.PluginId == PluginId);
            Assert.Contains(
                fixture.Coordinator.MenuItems,
                item => item.PluginId == PluginId);

            // 设置区块工厂走宿主渲染路径取一次 VM（插件自绘区块的视图经插件字典的模板映射）。
            object section = fixture.Coordinator.SettingsSections
                .Single(section => section.PluginId == PluginId)
                .Descriptor.ViewModelFactory();
            Assert.Equal("SampleUiSettingsViewModel", section.GetType().Name);

            // 只注册了窗口工厂：宿主未创建窗口实例。
            Assert.Empty(PluginWindows(PluginWindowTypeName));
            Assert.True(fixture.LoadResult.LoadContext!.IsCollectible);
        });
    }

    [Fact]
    public async Task 视图_停驻插件页时卸载_回落固定页且视图探针回收()
    {
        await StaTestHarness.RunAsync(async () =>
        {
            using LoadFixture fixture = await LoadSampleUiAsync();

            fixture.Executor.Navigate(PluginPageIdentifier);
            Assert.IsNotType<FixedPageViewModel>(fixture.Store.CurrentViewModel);

            // 在独立帧里渲染当前页并捕获视图弱引用（不跨卸载持有视图强引用）。
            (Window HostWindow, WeakReference ViewProbe) rendered = RenderCurrentPage(fixture.Navigation);
            try
            {
                Assert.True(rendered.ViewProbe.IsAlive, "导航到插件页后视图必须已实例化");

                PluginUnloadResult unload = await fixture.UnloadAsync();

                fixture.AssertUnloaded(unload);
                Assert.True(unload.Reclaimed, string.Join("；", unload.Diagnostics));
                AssertCommonClean(fixture);

                // 停驻页随插件卸载回落固定首页：导航状态不得滞留已出账页面的 VM。
                Assert.IsType<FixedPageViewModel>(fixture.Store.CurrentViewModel);

                // 旧页视觉树要等一个布局趟 + 调度器 idle 泵才彻底摘净（WPF 把视觉树与呈现的
                // 收尾排在延迟队列里）：生产时间线上这些帧自然发生，测试在此显式走完。
                rendered.HostWindow.UpdateLayout();
                await StaTestHarness.Dispatcher.InvokeAsync(
                    () => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);
                AssertCommonProbesDead(fixture, "page-vm", "page-open-window-command");

                CollectGarbage();
                Assert.False(rendered.ViewProbe.IsAlive, "卸载后插件视图必须可回收");
            }
            finally
            {
                rendered.HostWindow.Close();
            }
        });
    }

    [Fact]
    public async Task 绑定_卸载后断开_绑定表达式与源目标回收()
    {
        await StaTestHarness.RunAsync(async () =>
        {
            using LoadFixture fixture = await LoadSampleUiAsync();

            fixture.Executor.Navigate(PluginPageIdentifier);
            (Window HostWindow, WeakReference ViewProbe) rendered = RenderCurrentPage(fixture.Navigation);
            try
            {
                Assert.True(rendered.ViewProbe.IsAlive, "导航到插件页后视图必须已实例化");
                WeakReference bindingProbe = CaptureBindingExpression();

                PluginUnloadResult unload = await fixture.UnloadAsync();

                fixture.AssertUnloaded(unload);
                AssertCommonClean(fixture);

                rendered.HostWindow.UpdateLayout();
                await StaTestHarness.Dispatcher.InvokeAsync(
                    () => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);
                CollectGarbage();
                Assert.False(bindingProbe.IsAlive, "卸载后绑定表达式必须随视图回收");
                Assert.False(rendered.ViewProbe.IsAlive);
                AssertCommonProbesDead(fixture, "page-vm");
            }
            finally
            {
                rendered.HostWindow.Close();
            }

            // 独立帧：找到插件视图里的编辑框并捕获其绑定表达式的弱引用。
            [MethodImpl(MethodImplOptions.NoInlining)]
            WeakReference CaptureBindingExpression()
            {
                FrameworkElement view = (FrameworkElement)rendered.ViewProbe.Target!;
                var input = (TextBox)view.FindName("MessageInput")!;
                BindingExpression binding = input.GetBindingExpression(TextBox.TextProperty)!;
                Assert.NotNull(binding);
                return new WeakReference(binding);
            }
        });
    }

    [Fact]
    public async Task 窗口_经命令开窗卸载后关闭出账_实例回收()
    {
        await StaTestHarness.RunAsync(async () =>
        {
            using LoadFixture fixture = await LoadSampleUiAsync();

            // 生产开窗路径：托盘菜单点击 → 宿主路由插件命令 → 命令体经契约开窗。
            fixture.Coordinator.ExecuteCommand(GreetCommandId);
            Assert.Single(PluginWindows(PluginWindowTypeName));
            WeakReference windowProbe = CaptureWindowProbe();

            PluginUnloadResult unload = await fixture.UnloadAsync();

            Assert.Equal(PluginUnloadStatus.Unloaded, unload.Status);
            Assert.True(unload.Reclaimed, string.Join("；", unload.Diagnostics));
            AssertCommonClean(fixture);
            Assert.Empty(PluginWindows(PluginWindowTypeName));

            // 关窗的销毁收尾排在调度器队列上：idle 泵走完再判回收。
            await StaTestHarness.Dispatcher.InvokeAsync(
                () => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);
            CollectGarbage();
            Assert.False(windowProbe.IsAlive, "卸载后插件窗口实例必须可回收");

            // 独立帧：取窗口实例的弱引用（不跨卸载持有窗口强引用）。
            [MethodImpl(MethodImplOptions.NoInlining)]
            WeakReference CaptureWindowProbe() => new(PluginWindows(PluginWindowTypeName).Single());
        });
    }

    [Fact]
    public async Task 动画_随窗口摘除_Storyboard与附着目标回收()
    {
        await StaTestHarness.RunAsync(async () =>
        {
            using LoadFixture fixture = await LoadSampleUiAsync();

            fixture.Coordinator.ExecuteCommand(GreetCommandId);
            Assert.Single(PluginWindows(PluginWindowTypeName));
            WeakReference windowProbe = CaptureWindowProbe();

            // 动画项的额外断言：只 Stop 不清零——摘除方式是宿主在句柄出账时 Storyboard.Remove，
            // 测试先显式 Stop 一次，登记表必须原样保留动画资产。
            StopStoryboard();
            Assert.Equal(11, fixture.Coordinator.Assets.CountFor(PluginId));

            PluginUnloadResult unload = await fixture.UnloadAsync();

            Assert.Equal(PluginUnloadStatus.Unloaded, unload.Status);
            AssertCommonClean(fixture);
            // 关窗与动画摘除的收尾排在调度器队列上：idle 泵走完再判回收。
            await StaTestHarness.Dispatcher.InvokeAsync(
                () => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);
            AssertCommonProbesDead(fixture, "storyboard", "animation-target", "window");

            // 动画时钟挂在窗口元素上：窗口回收后附着目标随之消失。
            Assert.False(windowProbe.IsAlive);

            [MethodImpl(MethodImplOptions.NoInlining)]
            WeakReference CaptureWindowProbe() => new(PluginWindows(PluginWindowTypeName).Single());

            [MethodImpl(MethodImplOptions.NoInlining)]
            void StopStoryboard()
            {
                IReadOnlyDictionary<string, WeakReference> probes = fixture.PluginProbes();
                var storyboard = (System.Windows.Media.Animation.Storyboard)probes["storyboard"].Target!;
                var target = (FrameworkElement)probes["animation-target"].Target!;
                storyboard.Stop(target);
            }
        });
    }

    [Fact]
    public async Task 资源字典_卸载后整根摘除_字典回收()
    {
        await StaTestHarness.RunAsync(async () =>
        {
            using LoadFixture fixture = await LoadSampleUiAsync();
            var host = fixture.Coordinator.GetOrCreateHost(PluginId);

            // 插件资源根已并入宿主资源，插件字典并入根容器。
            Assert.Contains(host.ResourceRoot.Dictionary, StaTestHarness.Application.Resources.MergedDictionaries);
            WeakReference dictionaryProbe = CaptureMergedDictionaryProbe(host);

            PluginUnloadResult unload = await fixture.UnloadAsync();

            Assert.Equal(PluginUnloadStatus.Unloaded, unload.Status);
            AssertCommonClean(fixture);
            Assert.DoesNotContain(
                host.ResourceRoot.Dictionary,
                StaTestHarness.Application.Resources.MergedDictionaries);
            Assert.Empty(host.ResourceRoot.Dictionary.MergedDictionaries);

            CollectGarbage();
            Assert.False(dictionaryProbe.IsAlive, "卸载后插件资源字典必须可回收");

            // 独立帧：取并入根容器的插件字典弱引用（不跨卸载持有字典强引用）。
            [MethodImpl(MethodImplOptions.NoInlining)]
            WeakReference CaptureMergedDictionaryProbe(PluginUiHost inner)
                => new(inner.ResourceRoot.Dictionary.MergedDictionaries.Single());
        });
    }

    [Fact]
    public async Task DataTemplate_卸载后随根摘除_模板实例回收()
    {
        await StaTestHarness.RunAsync(async () =>
        {
            using LoadFixture fixture = await LoadSampleUiAsync();

            fixture.Executor.Navigate(PluginPageIdentifier);
            (Window HostWindow, WeakReference ViewProbe) rendered = RenderCurrentPage(fixture.Navigation);
            try
            {
                WeakReference templateProbe = CaptureTemplateProbe(fixture);

                PluginUnloadResult unload = await fixture.UnloadAsync();

                fixture.AssertUnloaded(unload);
                AssertCommonClean(fixture);

                rendered.HostWindow.UpdateLayout();
                await StaTestHarness.Dispatcher.InvokeAsync(
                    () => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);
                CollectGarbage();
                Assert.False(templateProbe.IsAlive, "卸载后插件 DataTemplate 必须随资源根回收");
            }
            finally
            {
                rendered.HostWindow.Close();
            }

            // 独立帧：按页面 VM 类型（插件 ALC 副本）从插件字典取模板的弱引用。
            [MethodImpl(MethodImplOptions.NoInlining)]
            WeakReference CaptureTemplateProbe(LoadFixture inner)
            {
                var host = inner.Coordinator.GetOrCreateHost(PluginId);
                ResourceDictionary merged = host.ResourceRoot.Dictionary.MergedDictionaries.Single();
                Type pageVmType = inner.Catalog.Entries
                    .Single(entry => entry.AutomationId == PluginPageIdentifier)
                    .ViewModelType;
                return new WeakReference(merged[pageVmType]!);
            }
        });
    }

    [Fact]
    public async Task 定时器_先跑后卸_停止且回调与回调目标回收()
    {
        await StaTestHarness.RunAsync(async () =>
        {
            using LoadFixture fixture = await LoadSampleUiAsync();

            // 等定时器至少触发一次：证明"跑过的定时器"确实被卸载停止，而不是从未运行。
            await AwaitHeartbeatTickAsync(fixture.PluginProbes());

            PluginUnloadResult unload = await fixture.UnloadAsync();

            Assert.Equal(PluginUnloadStatus.Unloaded, unload.Status);
            AssertCommonClean(fixture);
            AssertCommonProbesDead(fixture, "heartbeat", "timer-tick");
        });
    }

    [Fact]
    public async Task 事件订阅_卸载后断开_处理委托回收()
    {
        await StaTestHarness.RunAsync(async () =>
        {
            using LoadFixture fixture = await LoadSampleUiAsync();

            fixture.Events.Publish(MinimizedToTrayMessage.Instance);
            Assert.Equal(1, fixture.Events.Received);

            PluginUnloadResult unload = await fixture.UnloadAsync();

            Assert.Equal(PluginUnloadStatus.Unloaded, unload.Status);
            AssertCommonClean(fixture);
            Assert.Equal(1, fixture.Events.Disposed);

            // 卸载后订阅已断：再发布不再投递到插件处理委托。
            fixture.Events.Publish(MinimizedToTrayMessage.Instance);
            Assert.Equal(1, fixture.Events.Received);
            AssertCommonProbesDead(fixture, "event-handler");
        });
    }

    [Fact]
    public async Task ALC存活_降级判定只记诊断不上判()
    {
        await StaTestHarness.RunAsync(async () =>
        {
            using LoadFixture fixture = await LoadSampleUiAsync();

            PluginUnloadResult unload = await fixture.UnloadAsync();

            // WPF 宿主的降级回收判定：入口实例必须回收，否则状态不是 Unloaded；
            // ALC 与程序集即使存活也只是 Residuals 里的诊断行，不改变 Unloaded 与 Reclaimed。
            Assert.Equal(PluginUnloadStatus.Unloaded, unload.Status);
            Assert.True(unload.Reclaimed);
            Assert.Null(unload.FailureReason);
            Assert.Contains("UI 资产：已清零（登记表清零且全局根无残留）", unload.Diagnostics);

            // 回收判定现场三行诊断：插件对象 / ALC / 程序集。
            Assert.Contains(unload.Diagnostics, line => line.StartsWith("插件对象：", StringComparison.Ordinal));
            Assert.Contains(unload.Diagnostics, line => line.StartsWith("ALC：", StringComparison.Ordinal));
            Assert.Contains(unload.Diagnostics, line => line.StartsWith("程序集：", StringComparison.Ordinal));
            Assert.DoesNotContain(
                unload.Residuals,
                residual => residual.Kind is PluginResidualKind.PluginObject
                    or PluginResidualKind.ScopeHandle
                    or PluginResidualKind.InFlightCall);
        });
    }

    [Fact]
    public async Task 负对照_绕过契约直并全局资源与自建窗口_隔离并按id摘除()
    {
        await StaTestHarness.RunAsync(async () =>
        {
            string pluginsRoot = Directory.CreateTempSubdirectory("starpie-bypass-ui-matrix").FullName;
            try
            {
                PluginLoadRequest request = PluginTestPackage.CreateLoadRequest(
                    pluginsRoot,
                    "com.example.bypass-ui",
                    typeof(BypassUiTestPlugin),
                    uiSection: $$"""
                        "sdk": "{{UiSdkAbi.Version}}", "entryType": "{{typeof(BypassUiTestModule).FullName}}"
                        """);
                var coordinator = new PluginUiCoordinator(
                    StaTestHarness.Application, StaTestHarness.Dispatcher, new RecordingPluginEvents());
                var pipeline = new PluginLoadPipeline(new CapabilityRegistry(), uiCoordinator: coordinator);

                // 越权注册"成功"：绕过契约即不进契约层，装载照常进活动态——拦截责任在泄漏验证。
                PluginLoadResult result = await pipeline.LoadAsync(request, CancellationToken.None);
                Assert.Equal(PluginLoadStatus.Active, result.Status);
                Assert.Single(PluginWindows(typeof(BypassUiTestWindow).FullName!));

                var unloadPipeline = new PluginUnloadPipeline(
                    () => { },
                    reclaimPolicy: PluginReclaimPolicy.Diagnostic,
                    uiCoordinator: coordinator);
                PluginUnloadResult unload = await unloadPipeline.UnloadAsync(
                    PluginUnloadRequest.FromLoaded(result), CancellationToken.None);

                // 按隔离流程处理：泄漏验证命中两类越权资产，卸载收口成隔离，残留逐条可定位。
                Assert.Equal(PluginUnloadStatus.Quarantined, unload.Status);
                Assert.Contains("UI 资产未清零", unload.FailureReason);
                Assert.Contains(
                    unload.Residuals,
                    residual => residual.Kind == PluginResidualKind.UiAsset
                        && residual.Detail.Contains("未登记的插件窗口"));
                Assert.Contains(
                    unload.Residuals,
                    residual => residual.Kind == PluginResidualKind.UiAsset
                        && residual.Detail.Contains("宿主资源残留插件资源字典"));

                // 兜底摘除：越权并入的字典已被验证器按 plugin id 从全局资源摘掉。
                Assert.DoesNotContain(
                    StaTestHarness.Application.Resources.MergedDictionaries,
                    dictionary => dictionary.Contains(BypassUiTestModule.LeakedDictionaryKey));

                // 越权自建窗口已被验证器按 plugin id 兜底关闭：共享 Application 现场恢复干净。
                Assert.Empty(PluginWindows(typeof(BypassUiTestWindow).FullName!));
            }
            finally
            {
                try { Directory.Delete(pluginsRoot, recursive: true); } catch { }
            }
        });
    }

    // 装载与导航夹具 ----------------------------------------------------------------------

    /// <summary>装载真实 UI 示例插件包：生产清单 → 装载管线（UI 线程注册）→ 活动态。</summary>
    private static async Task<LoadFixture> LoadSampleUiAsync()
    {
        PluginManifest manifest = PluginManifestParser
            .Parse(File.ReadAllText(Path.Combine(PackageDirectory, "plugin.json")))
            .Manifest!;
        var request = new PluginLoadRequest(
            manifest,
            PackageDirectory,
            new PluginAdmissionDecision(PluginAdmission.BuiltIn, "内置：命中随包第一方插件清单"));

        NavigationFixture navigation = NavigationFixture.Create();
        var events = new RecordingPluginEvents();
        var coordinator = new PluginUiCoordinator(
            StaTestHarness.Application, StaTestHarness.Dispatcher, events, navigation.Catalog);
        var pipeline = new PluginLoadPipeline(new CapabilityRegistry(), uiCoordinator: coordinator);

        PluginLoadResult result = await pipeline.LoadAsync(request, CancellationToken.None);
        Assert.Equal(PluginLoadStatus.Active, result.Status);
        Assert.NotNull(result.LoadContext);
        return new LoadFixture(result, coordinator, events, navigation);
    }

    /// <summary>在宿主渲染窗口里实例化当前页视图，返回窗口与视图弱引用（独立帧，不留强引用）。</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (Window HostWindow, WeakReference ViewProbe) RenderCurrentPage(NavigationFixture navigation)
    {
        Window hostWindow = navigation.RenderCurrentPage();
        FrameworkElement? view = FindPluginVisual(hostWindow, PluginId);
        Assert.NotNull(view);
        return (hostWindow, new WeakReference(view));
    }

    /// <summary>等宿主签发定时器至少触发一次（反射读插件心跳计数，不持有强引用）。</summary>
    private static async Task AwaitHeartbeatTickAsync(IReadOnlyDictionary<string, WeakReference> probes)
    {
        object? heartbeat = probes["heartbeat"].Target
            ?? throw new InvalidOperationException("心跳探针已被回收");
        PropertyInfo ticks = heartbeat.GetType().GetProperty("Ticks")!;

        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(3);
        while ((int)ticks.GetValue(heartbeat)! == 0)
        {
            if (DateTimeOffset.UtcNow > deadline)
            {
                throw new TimeoutException("宿主签发定时器 3 秒内未触发");
            }

            await Task.Delay(50);
        }
    }

    /// <summary>当前 Application 里指定类型全名的窗口（宿主侧窗口探针的入口）。</summary>
    private static IReadOnlyList<Window> PluginWindows(string typeFullName)
        => StaTestHarness.Application.Windows
            .OfType<Window>()
            .Where(window => window.GetType().FullName == typeFullName)
            .ToList();

    /// <summary>在视觉树里找插件 ALC 来源的元素。</summary>
    private static FrameworkElement? FindPluginVisual(DependencyObject root, string pluginId)
    {
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int index = 0; index < count; index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, index);
            if (child is FrameworkElement element
                && string.Equals(
                    AssemblyLoadContext.GetLoadContext(element.GetType().Assembly)?.Name,
                    PluginLoadContext.ContextNameFor(pluginId),
                    StringComparison.Ordinal))
            {
                return element;
            }

            FrameworkElement? nested = FindPluginVisual(child, pluginId);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }

    private static void CollectGarbage()
    {
        for (int round = 0; round < 3; round++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }

    /// <summary>断言登记表清零 + 全局根扫描无残留（plugins.md §5.2 判据）。</summary>
    private static void AssertCommonClean(LoadFixture fixture)
    {
        Assert.Equal(0, fixture.Coordinator.Assets.CountFor(PluginId));
        Assert.Empty(fixture.Coordinator.Assets.SnapshotAll());
        var verifier = new PluginUiLeakVerifier(StaTestHarness.Application);
        Assert.Empty(verifier.Verify(PluginId, fixture.Coordinator.Assets));
    }

    /// <summary>断言插件侧探针全部死亡（对照卸载前缓存的快照；先 GC 再判，对象可回收≠已回收）。</summary>
    private static void AssertCommonProbesDead(LoadFixture fixture, params string[] names)
    {
        CollectGarbage();
        foreach (string name in names)
        {
            Assert.True(
                fixture.ProbeSnapshot.TryGetValue(name, out WeakReference? probe),
                $"插件探针缺失：{name}");
            Assert.False(probe.IsAlive, $"卸载后探针仍存活：{name}");
        }
    }

    /// <summary>装载现场：探针在卸载交接前快照一次，卸载请求经 FromLoaded 交接。</summary>
    private sealed class LoadFixture : IDisposable
    {
        public LoadFixture(
            PluginLoadResult loadResult,
            PluginUiCoordinator coordinator,
            RecordingPluginEvents events,
            NavigationFixture navigation)
        {
            LoadResult = loadResult;
            Coordinator = coordinator;
            Events = events;
            Navigation = navigation;
        }

        public PluginLoadResult LoadResult { get; }

        public PluginUiCoordinator Coordinator { get; }

        public RecordingPluginEvents Events { get; }

        public NavigationFixture Navigation { get; }

        public NavigationCatalog Catalog => Navigation.Catalog;

        public NavigationStore Store => Navigation.Store;

        public NavigationExecutor Executor => Navigation.Executor;

        /// <summary>卸载前的插件探针快照（WeakReference 不构成强根，缓存安全）。</summary>
        public IReadOnlyDictionary<string, WeakReference> ProbeSnapshot { get; private set; }
            = new Dictionary<string, WeakReference>();

        private bool _unloaded;

        /// <summary>读插件侧探针表：只在交接前经装载上下文读取。</summary>
        public IReadOnlyDictionary<string, WeakReference> PluginProbes()
        {
            Assembly entry = LoadResult.LoadContext!.Assemblies
                .Single(assembly => assembly.GetName().Name == PluginAssemblyName);
            Type probesType = entry.GetType(PluginProbesTypeName, throwOnError: true)!;
            return (IReadOnlyDictionary<string, WeakReference>)probesType
                .GetMethod("Snapshot", Type.EmptyTypes)!
                .Invoke(null, null)!;
        }

        /// <summary>断言卸载收口为 Unloaded（未达时把失败原因与逐步诊断带出来）。</summary>
        public void AssertUnloaded(PluginUnloadResult unload)
        {
            Assert.True(
                unload.Status == PluginUnloadStatus.Unloaded,
                $"卸载未收口为 Unloaded：{unload.FailureReason}｜残留：{string.Join("；", unload.Residuals.Select(residual => residual.Detail))}｜诊断：{string.Join("；", unload.Diagnostics)}");
        }

        /// <summary>安全点卸载：先快照探针，再经 FromLoaded 交接走完整管线（WPF 宿主降级档）。</summary>
        public async Task<PluginUnloadResult> UnloadAsync()
        {
            _unloaded = true;
            ProbeSnapshot = PluginProbes();
            var pipeline = new PluginUnloadPipeline(
                () => { },
                reclaimPolicy: PluginReclaimPolicy.Diagnostic,
                uiCoordinator: Coordinator);
            return await pipeline.UnloadAsync(
                PluginUnloadRequest.FromLoaded(LoadResult), CancellationToken.None);
        }

        public void Dispose()
        {
            // 未走卸载管线的用例（断言中止或纯装载观察）：兜底回收 UI 资产，
            // 共享的 STA harness Application 不得把本用例的现场带进下一个用例。
            if (!_unloaded)
            {
                try
                {
                    Coordinator.ReleaseAsync(PluginId, CancellationToken.None)
                        .GetAwaiter().GetResult();
                }
                catch
                {
                    // 兜底尽力而为：真实结论仍由用例内的卸载管线断言负责。
                }
            }

            Navigation.Dispose();
        }
    }

    /// <summary>
    /// 导航现场（与生产同构）：目录 + 执行缝 + 主框架 VM + 宿主渲染窗口；固定首页解析为
    /// 测试替身 VM。目录实例交给插件 UI 门面，插件页随装载注册进同一目录。
    /// </summary>
    private sealed class NavigationFixture : IDisposable
    {
        private NavigationFixture(
            NavigationCatalog catalog,
            NavigationStore store,
            NavigationExecutor executor,
            MainViewModel main,
            Window hostWindow,
            ContentControl content)
        {
            Catalog = catalog;
            Store = store;
            Executor = executor;
            Main = main;
            HostWindow = hostWindow;
            Content = content;
        }

        public NavigationCatalog Catalog { get; }

        public NavigationStore Store { get; }

        public NavigationExecutor Executor { get; }

        public MainViewModel Main { get; }

        public Window HostWindow { get; }

        public ContentControl Content { get; }

        /// <summary>建目录（固定首页用替身 VM）+ 执行缝 + 主框架 VM + 宿主渲染窗口。</summary>
        public static NavigationFixture Create()
        {
            var services = new ServiceCollection();
            services.AddTransient<FixedPageViewModel>();
            IServiceProvider provider = services.BuildServiceProvider();

            var catalog = new NavigationCatalog();
            catalog.RegisterPage<FixedPageViewModel>(
                NavigationSlot.Trigger,
                NavigationSlots.GetAutomationId(NavigationSlot.Trigger),
                "PageTrigger",
                string.Empty);

            var store = new NavigationStore();
            var executor = new NavigationExecutor(store, catalog, provider);
            var main = new MainViewModel(store, catalog, executor, new LocalizationService());

            var content = new ContentControl();
            content.SetBinding(
                ContentControl.ContentProperty,
                new Binding(nameof(NavigationStore.CurrentViewModel)) { Source = store });
            var hostWindow = new Window
            {
                Content = content,
                Width = 400,
                Height = 300,
                ShowActivated = false,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
            };
            hostWindow.Show();
            return new NavigationFixture(catalog, store, executor, main, hostWindow, content);
        }

        /// <summary>把当前页渲染出来：强制模板应用与布局，插件视图由隐式 DataTemplate 实例化。</summary>
        public Window RenderCurrentPage()
        {
            Content.ApplyTemplate();
            Content.UpdateLayout();
            return HostWindow;
        }

        public void Dispose()
        {
            Content.Content = null;
            HostWindow.Close();
            Main.Dispose();
        }
    }

    /// <summary>固定首页替身 VM（只满足导航执行缝的 INPC 契约面）。</summary>
    public sealed class FixedPageViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        /// <inheritdoc/>
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged
        {
            add { }
            remove { }
        }
    }
}

/// <summary>事件中介替身：记录订阅断开与投递计数，供矩阵驱动宿主事件。</summary>
public sealed class RecordingPluginEvents : IPluginEvents
{
    private readonly List<Delegate> _handlers = new();

    /// <summary>处理委托被调用的总次数。</summary>
    public int Received { get; private set; }

    /// <summary>已断开的订阅数。</summary>
    public int Disposed { get; private set; }

    /// <inheritdoc/>
    public IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : class
    {
        ArgumentNullException.ThrowIfNull(handler);
        _handlers.Add(handler);
        return new Handle(this, handler);
    }

    /// <summary>发布一条宿主事件（测试驱动用）。</summary>
    public void Publish<TEvent>(TEvent payload) where TEvent : class
    {
        foreach (Action<TEvent> handler in _handlers.OfType<Action<TEvent>>())
        {
            handler(payload);
            Received++;
        }
    }

    private sealed class Handle : IDisposable
    {
        private readonly RecordingPluginEvents _owner;
        private readonly Delegate _handler;
        private bool _disposed;

        public Handle(RecordingPluginEvents owner, Delegate handler)
        {
            _owner = owner;
            _handler = handler;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _owner.Disposed++;
            _owner._handlers.Remove(_handler);
        }
    }
}
