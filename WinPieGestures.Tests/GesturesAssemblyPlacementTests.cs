using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Resources;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using StarPie.Modules;
using StarPie.Services;
using StarPie.Services.Actions;
using StarPie.Services.Configuration;
using StarPie.Services.Dialogs;
using StarPie.Services.Gestures;
using StarPie.Services.Localization;
using StarPie.Services.Navigation;
using StarPie.ViewModels.Gestures;
using StarPie.ViewModels.Pages;
using StarPie.Views.Pages;

namespace StarPie.Tests;

/// <summary>
/// B9/#82（模块化：M1 Gestures 抽取·收口）跨集归属、依赖与注册收口：
/// 手势管线（<see cref="MouseHook"/>/<see cref="GestureController"/>/<see cref="GestureEngine"/>/
/// <see cref="IWindowContext"/>/<see cref="WindowContext"/>）、动作执行
/// （<see cref="IActionExecutorService"/>/<see cref="ActionExecutorService"/>/<see cref="ActionRouting"/>）、
/// 触发+手势设置页（VM <see cref="BehaviorSettingsViewModel"/>/<see cref="ProfileListViewModel"/>/
/// <see cref="SlotViewModel"/> + View <see cref="TriggerSettingsPage"/>/<see cref="GesturesSettingsPage"/>）
/// 迁入 <c>StarPie.Gestures</c>；模块注册器 <see cref="GesturesModuleRegistrar"/>
/// （RegisterNavigation + RegisterServices）随模块迁出 exe（原 M1ModuleRegistrar/M1PageTemplates.xaml
/// 替换为 GesturesModuleRegistrar/GesturesPageTemplates.xaml），页面 VM 的 DI 注册与
/// <see cref="IProfilePreviewSource"/> 别名（实现方 ProfileListViewModel）下放本程序集。
/// B10/#83：命名空间统一为 StarPie.*（全仓前缀替换，保持跨程序集共享命名空间树，
/// ADR-0016 决策 12）。
/// Gestures → Core 单向 + Gestures → Wheel 允许边（M1→M2，IWheelFactory/IWheelViewModel），
/// 不引用 Host/其它业务模块；MouseHook dev 分支经 Core AppDataPaths.IsDevInstance 回填缝。
/// </summary>
public sealed class GesturesAssemblyPlacementTests
{
    [Fact]
    public void M1出口_归属独立模块程序集_且命名空间统一为StarPie()
    {
        Assert.Equal("StarPie.Gestures", typeof(MouseHook).Assembly.GetName().Name);
        Assert.Equal("StarPie.Gestures", typeof(GestureController).Assembly.GetName().Name);
        Assert.Equal("StarPie.Gestures", typeof(GestureEngine).Assembly.GetName().Name);
        Assert.Equal("StarPie.Gestures", typeof(IWindowContext).Assembly.GetName().Name);
        Assert.Equal("StarPie.Gestures", typeof(WindowContext).Assembly.GetName().Name);
        Assert.Equal("StarPie.Gestures", typeof(IActionExecutorService).Assembly.GetName().Name);
        Assert.Equal("StarPie.Gestures", typeof(ActionExecutorService).Assembly.GetName().Name);
        Assert.Equal("StarPie.Gestures", typeof(ActionRouting).Assembly.GetName().Name);
        Assert.Equal("StarPie.Gestures", typeof(BehaviorSettingsViewModel).Assembly.GetName().Name);
        Assert.Equal("StarPie.Gestures", typeof(ProfileListViewModel).Assembly.GetName().Name);
        Assert.Equal("StarPie.Gestures", typeof(SlotViewModel).Assembly.GetName().Name);
        Assert.Equal("StarPie.Gestures", typeof(TriggerSettingsPage).Assembly.GetName().Name);
        Assert.Equal("StarPie.Gestures", typeof(GesturesSettingsPage).Assembly.GetName().Name);
        Assert.Equal("StarPie.Gestures", typeof(GesturesModuleRegistrar).Assembly.GetName().Name);

        Assert.Equal("StarPie.Services.Gestures", typeof(GestureEngine).Namespace);
        Assert.Equal("StarPie.Services.Actions", typeof(ActionExecutorService).Namespace);
        Assert.Equal("StarPie.ViewModels.Pages", typeof(ProfileListViewModel).Namespace);
        Assert.Equal("StarPie.ViewModels.Gestures", typeof(SlotViewModel).Namespace);
        Assert.Equal("StarPie.Views.Pages", typeof(TriggerSettingsPage).Namespace);
        Assert.Equal("StarPie.Modules", typeof(GesturesModuleRegistrar).Namespace);
    }

    [Fact]
    public void M1程序集_单向依赖共享内核Core并允许M2边_不引用Host与其他业务模块()
    {
        string?[] referenced = typeof(GestureEngine).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name)
            .ToArray();

        Assert.Contains("StarPie.Core", referenced);
        // M1→M2 允许边（assemblies.md §3）：GestureEngine/GestureController 经 IWheelFactory/
        // IWheelViewModel 消费瞬态轮盘（D5，ADR-0016 决策 11）。
        Assert.Contains("StarPie.Wheel", referenced);
        Assert.DoesNotContain("StarPie", referenced);
        Assert.DoesNotContain("StarPie.Programs", referenced);
        Assert.DoesNotContain("StarPie.Shell", referenced);
        Assert.DoesNotContain("StarPie.Theme", referenced);
    }

    [Fact]
    public void MouseHook_dev触发键_经Core回填缝_不反向引用Host()
    {
        // B9/#82：MouseHook 随 M1 迁出 exe 后 dev 分支读 Core AppDataPaths.IsDevInstance 回填缝
        // （组合根装配前以 DevInstance.IsActive 回填）——构造时据此选中间键/右键触发，
        // 行为与迁移前一致（WM_MBUTTONDOWN=0x207 / WM_RBUTTONDOWN=0x204）。
        const int wmMButtonDown = 0x0207;
        const int wmRButtonDown = 0x0204;
        const string downField = "_triggerDownMessage";
        const string upField = "_triggerUpMessage";

        bool original = AppDataPaths.IsDevInstance;
        try
        {
            AppDataPaths.IsDevInstance = true;
            var devHook = new MouseHook();
            Assert.Equal(wmMButtonDown, ReadField(devHook, downField));
            Assert.Equal(wmMButtonDown + 1, ReadField(devHook, upField)); // WM_MBUTTONUP=0x208

            AppDataPaths.IsDevInstance = false;
            var releaseHook = new MouseHook();
            Assert.Equal(wmRButtonDown, ReadField(releaseHook, downField));
            Assert.Equal(wmRButtonDown + 1, ReadField(releaseHook, upField)); // WM_RBUTTONUP=0x205
        }
        finally
        {
            AppDataPaths.IsDevInstance = original;
        }

        static int ReadField(MouseHook hook, string name)
            => (int)(hook.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(hook) ?? -1);
    }

    [Fact]
    public void M1注册器_跨程序集自报导航项_槽位与AutomationId不变()
    {
        var catalog = new NavigationCatalog();
        GesturesModuleRegistrar.RegisterNavigation(catalog);

        Assert.Equal(2, catalog.Entries.Count);
        Assert.Equal(new[] { NavigationSlot.Trigger, NavigationSlot.Gestures }, catalog.Entries.Select(e => e.Slot));
        Assert.Equal(new[] { "NavTab0", "NavTab2" }, catalog.Entries.Select(e => e.AutomationId));
        Assert.Equal(new[] { "TabTrigger", "TabGestures" }, catalog.Entries.Select(e => e.TitleKey));
        Assert.All(catalog.Entries, e => Assert.Equal("StarPie.Gestures", e.ViewModelType.Assembly.GetName().Name));
    }

    [Fact]
    public void GesturesPageTemplates模板字典_BAML已编入模块程序集()
    {
        var assembly = typeof(GesturesModuleRegistrar).Assembly;
        string resourcesName = assembly.GetManifestResourceNames().Single(n => n.EndsWith(".g.resources", StringComparison.OrdinalIgnoreCase));
        using var stream = assembly.GetManifestResourceStream(resourcesName);
        using var reader = new ResourceReader(stream!);
        var entries = reader.Cast<DictionaryEntry>().Select(e => (string)e.Key).ToArray();
        Assert.Contains(entries, name => name.Contains("gesturespagetemplates", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void M1注册器_RegisterServices_可经微型容器解析手势管线与两页面VM()
    {
        var services = new ServiceCollection();
        var config = new TestConfigService
        {
            Current = new AppConfig
            {
                Profiles = new List<WheelProfile> { new WheelProfile { ProcessName = "Global", SectorCount = 8 } }
            }
        };
        var localization = new LocalizationService();
        services.AddSingleton<IConfigService>(config);
        services.AddSingleton<ILocalizationService>(localization);
        services.AddSingleton<IMessenger>(TestHub.NewMessenger());
        services.AddSingleton<IDialogService>(new TestDialogService());

        // 镜像 Composition 装配顺序：M4（IThemeService）→ M2（IWheelFactory，M1→M2 允许边）
        // → M1（手势管线/页面 VM）。
        ThemeModuleRegistrar.RegisterServices(services);
        WheelModuleRegistrar.RegisterServices(services);
        GesturesModuleRegistrar.RegisterServices(services);
        using var provider = services.BuildServiceProvider();

        var hook = provider.GetRequiredService<MouseHook>();
        var executor = provider.GetRequiredService<IActionExecutorService>();
        var engine = provider.GetRequiredService<GestureEngine>();
        var controller = provider.GetRequiredService<GestureController>();
        var behavior = provider.GetRequiredService<BehaviorSettingsViewModel>();
        var profiles = provider.GetRequiredService<ProfileListViewModel>();
        var source = provider.GetRequiredService<IProfilePreviewSource>();

        Assert.Equal("StarPie.Gestures", hook.GetType().Assembly.GetName().Name);
        Assert.Equal("StarPie.Gestures", engine.GetType().Assembly.GetName().Name);
        Assert.Equal("StarPie.Gestures", controller.GetType().Assembly.GetName().Name);
        Assert.Equal("StarPie.Gestures", executor.GetType().Assembly.GetName().Name);
        Assert.Equal("StarPie.Gestures", behavior.GetType().Assembly.GetName().Name);
        Assert.Equal("StarPie.Gestures", profiles.GetType().Assembly.GetName().Name);
        // 别名即 M1 实现方单例（镜像 Composition 装配：#69/B8 契约在 Core、B9 别名下放模块）。
        Assert.IsType<ProfileListViewModel>(source);
        Assert.Same(profiles, source);
        Assert.Same(behavior, provider.GetRequiredService<BehaviorSettingsViewModel>());
    }
}
