using System.Linq;
using System.Collections;
using System.Resources;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using StarPie.Modules;
using StarPie.Services;
using StarPie.Services.Configuration;
using StarPie.Services.Dialogs;
using StarPie.Services.Gestures;
using StarPie.Services.Localization;
using StarPie.Services.Shell;
using StarPie.Services.Wheel;
using StarPie.ViewModels.Pages;
using StarPie.ViewModels.Wheel;
using StarPie.Views.Converters;
using StarPie.Views.Renderers;
using StarPie.Views.Wheel;

namespace StarPie.Tests;

/// <summary>
/// 轮盘模块（Wheel）跨程序集归属、依赖与可见性收口：轮盘 VM 实现
/// （<see cref="WheelViewModel"/>）、窗口（<see cref="RadialWindow"/>）、样式渲染器
/// （<see cref="IRadialStyleRenderer"/> 系 + <see cref="WheelPreviewRenderer"/>）、轮盘配色
/// （<see cref="WheelPalette"/>/<see cref="WheelPaletteCatalog"/>/
/// <see cref="WheelPaletteParser"/>，位于本集 Models；<see cref="CustomColorPreset"/> 仍居
/// 共享内核——AppConfig 配置 POCO 依赖）、轮盘视觉几何（<see cref="WheelGeometry"/>）、
/// 轮盘工厂实现（<see cref="WheelFactory"/>）与核图标预览转换器
/// （<see cref="CoreIconGeometryConverter"/>/<see cref="CoreIconNameConverter"/>）位于
/// <c>StarPie.Wheel</c>；出口契约（<see cref="IWheelViewModel"/>/
/// <see cref="IWheelFactory"/> + 签名暴露件 <see cref="IWheelAppearanceState"/>）随实现方
/// 下沉 <c>StarPie.Wheel.Contracts</c>（ADR-0023/#97，自 StarPie.Wheel 迁出）；模块注册器
/// <see cref="WheelModuleRegistrar"/> 下放轮盘工厂与外观设置子 VM 的 DI 注册。
/// 手势侧（<see cref="GestureEngine"/>，位于 StarPie.Gestures）只经 Wheel.Contracts 接口
/// 消费（M1→M2 runtime 允许边清零）；预览 Profile 只读契约 <see cref="IProfilePreviewSource"/>
/// 随实现方 M1 下沉 <c>StarPie.Gestures.Contracts</c>（消费方 WheelAppearanceSettingsViewModel
/// 经契约边消费）；IThemeService 消费改经 <c>StarPie.Theme.Contracts</c> 契约边
/// （M2→M4 runtime 允许边清零）。Wheel → Core + Wheel.Contracts + Theme.Contracts +
/// Gestures.Contracts + Dialogs.Contracts + Icons.Contracts，不引用宿主/其它业务模块 runtime。
/// </summary>
public sealed class WheelAssemblyPlacementTests
{
    [Fact]
    public void M2出口_归属独立模块程序集_且命名空间统一为StarPie()
    {
        Assert.Equal("StarPie.Wheel", typeof(WheelViewModel).Assembly.GetName().Name);
        Assert.Equal("StarPie.Wheel", typeof(WheelAppearanceSettingsViewModel).Assembly.GetName().Name);
        Assert.Equal("StarPie.Wheel", typeof(RadialWindow).Assembly.GetName().Name);
        Assert.Equal("StarPie.Wheel", typeof(IRadialStyleRenderer).Assembly.GetName().Name);
        Assert.Equal("StarPie.Wheel", typeof(WheelPreviewRenderer).Assembly.GetName().Name);
        Assert.Equal("StarPie.Wheel", typeof(WheelPalette).Assembly.GetName().Name);
        Assert.Equal("StarPie.Wheel", typeof(WheelPaletteCatalog).Assembly.GetName().Name);
        Assert.Equal("StarPie.Wheel", typeof(WheelPaletteParser).Assembly.GetName().Name);
        Assert.Equal("StarPie.Wheel", typeof(WheelGeometry).Assembly.GetName().Name);
        Assert.Equal("StarPie.Wheel", typeof(WheelFactory).Assembly.GetName().Name);
        Assert.Equal("StarPie.Wheel", typeof(CoreIconGeometryConverter).Assembly.GetName().Name);
        Assert.Equal("StarPie.Wheel", typeof(CoreIconNameConverter).Assembly.GetName().Name);
        Assert.Equal("StarPie.Wheel", typeof(WheelModuleRegistrar).Assembly.GetName().Name);

        Assert.Equal("StarPie.ViewModels.Wheel", typeof(WheelViewModel).Namespace);
        Assert.Equal("StarPie.ViewModels.Pages", typeof(WheelAppearanceSettingsViewModel).Namespace);
        Assert.Equal("StarPie.Views.Wheel", typeof(RadialWindow).Namespace);
        Assert.Equal("StarPie.Views.Renderers", typeof(WheelPreviewRenderer).Namespace);
        Assert.Equal("StarPie.Models", typeof(WheelPalette).Namespace);
        Assert.Equal("StarPie.Services.Wheel", typeof(WheelGeometry).Namespace);
        Assert.Equal("StarPie.Services.Wheel", typeof(WheelFactory).Namespace);
        Assert.Equal("StarPie.Views.Converters", typeof(CoreIconGeometryConverter).Namespace);
        Assert.Equal("StarPie.Modules", typeof(WheelModuleRegistrar).Namespace);
    }

    [Fact]
    public void M2契约_随实现方下沉WheelContracts_命名空间不变()
    {
        // ADR-0023/#97：IWheelFactory/IWheelViewModel/IWheelAppearanceState 自 StarPie.Wheel
        // 迁出，实现（WheelFactory/WheelViewModel/WheelAppearanceSettingsViewModel）驻 runtime。
        Assert.Equal("StarPie.Wheel.Contracts", typeof(IWheelFactory).Assembly.GetName().Name);
        Assert.Equal("StarPie.Wheel.Contracts", typeof(IWheelViewModel).Assembly.GetName().Name);
        Assert.Equal("StarPie.Wheel.Contracts", typeof(IWheelAppearanceState).Assembly.GetName().Name);

        Assert.Equal("StarPie.Services.Wheel", typeof(IWheelFactory).Namespace);
        Assert.Equal("StarPie.ViewModels.Wheel", typeof(IWheelViewModel).Namespace);
        Assert.Equal("StarPie.ViewModels.Wheel", typeof(IWheelAppearanceState).Namespace);

        Assert.Equal("StarPie.Wheel", typeof(WheelFactory).Assembly.GetName().Name);
        Assert.Equal("StarPie.Wheel", typeof(WheelViewModel).Assembly.GetName().Name);
        Assert.True(typeof(IWheelFactory).IsAssignableFrom(typeof(WheelFactory)));
        Assert.True(typeof(IWheelViewModel).IsAssignableFrom(typeof(WheelViewModel)));
        Assert.True(typeof(IWheelAppearanceState).IsAssignableFrom(typeof(WheelAppearanceSettingsViewModel)));
    }

    [Fact]
    public void M2程序集_单向依赖Contracts与Core_不引用Host与其他业务模块runtime()
    {
        string?[] referenced = typeof(WheelViewModel).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name)
            .ToArray();

        Assert.Contains("StarPie.Core", referenced);
        // ADR-0023/#97：M2 runtime 实现自有契约。
        Assert.Contains("StarPie.Wheel.Contracts", referenced);
        // M2 → M4 runtime 允许边清零：RadialWindow/WheelFactory 消费 IThemeService 改经契约边。
        Assert.Contains("StarPie.Theme.Contracts", referenced);
        Assert.DoesNotContain("StarPie.Theme", referenced);
        // 预览 Profile 契约边（IProfilePreviewSource 随实现方 M1 下沉 Gestures.Contracts）。
        Assert.Contains("StarPie.Gestures.Contracts", referenced);
        Assert.DoesNotContain("StarPie.Gestures", referenced);
        // ADR-0023/#95：轮盘链经 S1 契约程序集消费图标能力，不引用 Icons runtime。
        Assert.Contains("StarPie.Icons.Contracts", referenced);
        // ADR-0023/#96：轮盘链经 S6 契约程序集消费对话框能力。
        Assert.Contains("StarPie.Dialogs.Contracts", referenced);
        Assert.DoesNotContain("StarPie.Icons", referenced);
        Assert.DoesNotContain("StarPie", referenced);
        Assert.DoesNotContain("StarPie.Programs", referenced);
        Assert.DoesNotContain("StarPie.Shell", referenced);
        Assert.DoesNotContain("StarPie.Dialogs", referenced);
    }

    [Fact]
    public void RadialWindow窗口_BAML已编入模块程序集()
    {
        var assembly = typeof(RadialWindow).Assembly;
        string resourcesName = assembly.GetManifestResourceNames().Single(n => n.EndsWith(".g.resources", System.StringComparison.OrdinalIgnoreCase));
        using var stream = assembly.GetManifestResourceStream(resourcesName);
        using var reader = new ResourceReader(stream!);
        var entries = reader.Cast<DictionaryEntry>().Select(e => (string)e.Key).ToArray();
        Assert.Contains(entries, name => name.Contains("views/wheel/radialwindow.baml", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void D5解结_工厂契约随M2下沉WheelContracts_M1手势侧只经契约接口引用()
    {
        // IWheelFactory 契约随实现方下沉 Wheel.Contracts（ADR-0023/#97），实现仍在 Wheel runtime。
        Assert.Equal("StarPie.Wheel.Contracts", typeof(IWheelFactory).Assembly.GetName().Name);
        Assert.Equal("StarPie.Wheel", typeof(WheelFactory).Assembly.GetName().Name);
        Assert.True(typeof(IWheelFactory).IsAssignableFrom(typeof(WheelFactory)));

        // 手势侧（GestureEngine，位于 StarPie.Gestures）构造注入的是轮盘契约接口——
        // StarPie.Gestures → StarPie.Wheel.Contracts 仅契约面，不反向组装瞬态轮盘。
        Assert.Equal("StarPie.Gestures", typeof(GestureEngine).Assembly.GetName().Name);
        var wheelFactoryParam = typeof(GestureEngine)
            .GetConstructors()
            .Single()
            .GetParameters()
            .Single(p => p.Name == "wheelFactory");
        Assert.Equal(typeof(IWheelFactory), wheelFactoryParam.ParameterType);
        Assert.Equal("StarPie.Wheel.Contracts", wheelFactoryParam.ParameterType.Assembly.GetName().Name);

        // 手势 → 轮盘单向成立：GestureEngine 所在程序集引用 Wheel.Contracts 而非 Wheel runtime，
        // 亦不反向引用宿主（其余跨模块契约经各自 Contracts）。
        string?[] m1References = typeof(GestureEngine).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToArray();
        Assert.Contains("StarPie.Wheel.Contracts", m1References);
        Assert.DoesNotContain("StarPie.Wheel", m1References);
        Assert.DoesNotContain("StarPie", m1References);
    }

    [Fact]
    public void D5解结_预览Profile只读契约_随实现方M1下沉GesturesContracts()
    {
        // ADR-0023/#97 Q4：预览源语义属 M1 配置方案数据，随实现方下沉 Gestures.Contracts
        //（自 Core 迁出，命名空间不变），消费方 Wheel runtime 经契约边引用。
        Assert.Equal("StarPie.Gestures.Contracts", typeof(IProfilePreviewSource).Assembly.GetName().Name);
        Assert.Equal("StarPie.ViewModels.Pages", typeof(IProfilePreviewSource).Namespace);
    }

    [Fact]
    public void M2注册器_RegisterServices_可经微型容器解析轮盘工厂与外观设置子VM()
    {
        var services = new ServiceCollection();
        var config = new TestConfigService { Current = new AppConfig() };
        var localization = new LocalizationService();
        services.AddSingleton<IConfigService>(config);
        services.AddSingleton<ILocalizationService>(localization);
        services.AddSingleton<IMessenger>(TestHub.NewMessenger());
        services.AddSingleton<IDialogService>(new TestDialogService());
        // ADR-0019/#87：WheelFactory 消费共享图标资产实例服务（S1），微型容器需注册替身。
        services.AddSingleton<IIconAssetService>(new TestIconAssetService());
        // 实现方（ProfileListViewModel，位于 StarPie.Gestures）：本用例只验证轮盘注册器
        // 对只读契约（现驻 Gestures.Contracts）的消费，以替身注册别名即可
        //（真实别名装配由 GesturesModuleRegistrar 覆盖）。
        services.AddSingleton<IProfilePreviewSource>(new FakeProfilePreviewSource());
        // IThemeService 由主题模块注册器提供（轮盘消费允许边）。
        ThemeModuleRegistrar.RegisterServices(services);

        WheelModuleRegistrar.RegisterServices(services);
        using var provider = services.BuildServiceProvider();

        var factory = provider.GetRequiredService<IWheelFactory>();
        var appearance = provider.GetRequiredService<WheelAppearanceSettingsViewModel>();

        Assert.IsType<WheelFactory>(factory);
        Assert.Equal("StarPie.Wheel", factory.GetType().Assembly.GetName().Name);
        Assert.Equal("StarPie.Wheel", appearance.GetType().Assembly.GetName().Name);
        Assert.Same(appearance, provider.GetRequiredService<WheelAppearanceSettingsViewModel>());
        Assert.Same(config.Current, appearance.CurrentConfig);
    }

    private sealed class FakeProfilePreviewSource : IProfilePreviewSource
    {
        public WheelProfile? PreviewProfile { get; set; }
    }
}
