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
using StarPie.Views.Controls;
using StarPie.Views.Pages;

namespace StarPie.Tests;

/// <summary>
/// 手势模块（Gestures）跨程序集归属、依赖与注册收口：手势管线
/// （<see cref="MouseHook"/>/<see cref="GestureController"/>/<see cref="GestureEngine"/>/
/// <see cref="IWindowContext"/>/<see cref="WindowContext"/>）、动作执行
/// （<see cref="IActionExecutorService"/>/<see cref="ActionExecutorService"/>/<see cref="ActionRouting"/>）、
/// 触发+手势设置页（VM <see cref="BehaviorSettingsViewModel"/>/<see cref="ProfileListViewModel"/>/
/// <see cref="SlotViewModel"/> + View <see cref="TriggerSettingsPage"/>/<see cref="GesturesSettingsPage"/>）
/// 与随共享 UI 基建去共享化下沉的热键录制控件 <see cref="HotkeyRecorderBox"/>（ADR-0022/#94，
/// 样式字典 Views/Styles/HotkeyRecorderBox.xaml 同驻模块）位于 <c>StarPie.Gestures</c>；
/// 模块注册器 <see cref="GesturesModuleRegistrar"/>
/// （RegisterNavigation + RegisterServices）驻本程序集，页面 VM 的 DI 注册与
/// <see cref="IProfilePreviewSource"/> 别名（实现方 ProfileListViewModel）下放本程序集；
/// 出口契约 <see cref="IProfilePreviewSource"/> 随实现方下沉
/// <c>StarPie.Gestures.Contracts</c>（ADR-0023/#97，自 Core 迁出，命名空间不变）；
/// 命名空间统一为 StarPie.*（跨程序集共享命名空间树）。
/// Gestures → Core 单向 + Gestures.Contracts（自身契约）+ Wheel.Contracts 契约边
/// （M1→M2 runtime 允许边清零，IWheelFactory/IWheelViewModel）+ Icons.Contracts 契约边
/// （ADR-0023/#95，不引用 Icons runtime）+ Dialogs.Contracts 契约边（ADR-0023/#96），
/// 不引用宿主/其它业务模块 runtime；MouseHook dev 分支经 Core AppDataPaths.IsDevInstance 回填缝。
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
        Assert.Equal("StarPie.Gestures", typeof(HotkeyRecorderBox).Assembly.GetName().Name);
        Assert.Equal("StarPie.Gestures", typeof(GesturesModuleRegistrar).Assembly.GetName().Name);

        Assert.Equal("StarPie.Services.Gestures", typeof(GestureEngine).Namespace);
        Assert.Equal("StarPie.Services.Actions", typeof(ActionExecutorService).Namespace);
        Assert.Equal("StarPie.ViewModels.Pages", typeof(ProfileListViewModel).Namespace);
        Assert.Equal("StarPie.ViewModels.Gestures", typeof(SlotViewModel).Namespace);
        Assert.Equal("StarPie.Views.Pages", typeof(TriggerSettingsPage).Namespace);
        Assert.Equal("StarPie.Views.Controls", typeof(HotkeyRecorderBox).Namespace);
        Assert.Equal("StarPie.Modules", typeof(GesturesModuleRegistrar).Namespace);
    }

    [Fact]
    public void M1程序集_单向依赖Contracts与Core_不引用Host与其他业务模块runtime()
    {
        string?[] referenced = typeof(GestureEngine).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name)
            .ToArray();

        Assert.Contains("StarPie.Core", referenced);
        // ADR-0023/#97：M1 runtime 实现自有契约（ProfileListViewModel 实现
        // IProfilePreviewSource，注册器注册别名）。
        Assert.Contains("StarPie.Gestures.Contracts", referenced);
        // M1 → M2 runtime 允许边清零：GestureEngine 经 Wheel.Contracts 的
        // IWheelFactory/IWheelViewModel 消费瞬态轮盘。
        Assert.Contains("StarPie.Wheel.Contracts", referenced);
        Assert.DoesNotContain("StarPie.Wheel", referenced);
        // ADR-0023/#95：手势链经 S1 契约程序集消费图标能力，不引用 Icons runtime。
        Assert.Contains("StarPie.Icons.Contracts", referenced);
        // ADR-0023/#96：手势链经 S6 契约程序集消费对话框能力。
        Assert.Contains("StarPie.Dialogs.Contracts", referenced);
        Assert.DoesNotContain("StarPie.Icons", referenced);
        Assert.DoesNotContain("StarPie", referenced);
        Assert.DoesNotContain("StarPie.Programs", referenced);
        Assert.DoesNotContain("StarPie.Shell", referenced);
        Assert.DoesNotContain("StarPie.Theme", referenced);
        Assert.DoesNotContain("StarPie.Dialogs", referenced);
    }

    [Fact]
    public void M1出口契约_随实现方下沉GesturesContracts_命名空间不变()
    {
        // ADR-0023/#97 Q4：IProfilePreviewSource 自 Core 迁出随实现方 M1 下沉
        // Gestures.Contracts，实现（ProfileListViewModel）与注册器驻 runtime。
        Assert.Equal("StarPie.Gestures.Contracts", typeof(IProfilePreviewSource).Assembly.GetName().Name);
        Assert.Equal("StarPie.ViewModels.Pages", typeof(IProfilePreviewSource).Namespace);

        Assert.Equal("StarPie.Gestures", typeof(ProfileListViewModel).Assembly.GetName().Name);
        Assert.True(typeof(IProfilePreviewSource).IsAssignableFrom(typeof(ProfileListViewModel)));
    }

    [Fact]
    public void MouseHook_dev触发键_经Core回填缝_不反向引用Host()
    {
        // MouseHook dev 分支读 Core AppDataPaths.IsDevInstance 回填缝
        // （组合根装配前以 DevInstance.IsActive 回填）——构造时据此选中间键/右键触发
        // （WM_MBUTTONDOWN=0x207 / WM_RBUTTONDOWN=0x204）。
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
        // ADR-0019/#87：M1 页面 VM（ProfileListViewModel/SlotViewModel）消费共享图标资产
        // 实例服务（S1），微型容器需注册替身（镜像组合根：IIconAssetService 单例）。
        services.AddSingleton<IIconAssetService>(new TestIconAssetService());

        // 镜像组合根装配顺序：主题（IThemeService）→ 轮盘（IWheelFactory，手势→轮盘允许边）
        // → 手势（手势管线/页面 VM）。
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
        // 别名即手势实现方单例（镜像组合根装配：契约随实现方下沉 Gestures.Contracts、
        // 别名在模块注册器注册）。
        Assert.IsType<ProfileListViewModel>(source);
        Assert.Same(profiles, source);
        Assert.Same(behavior, provider.GetRequiredService<BehaviorSettingsViewModel>());
    }
}
