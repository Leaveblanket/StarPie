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
/// 轮盘模块（Wheel）跨程序集归属、依赖与可见性收口：轮盘 VM
/// （<see cref="WheelViewModel"/>/<see cref="IWheelViewModel"/>）、窗口
/// （<see cref="RadialWindow"/>）、样式渲染器（<see cref="IRadialStyleRenderer"/> 系 +
/// <see cref="WheelPreviewRenderer"/>）、轮盘配色（<see cref="WheelPalette"/>/
/// <see cref="WheelPaletteCatalog"/>/<see cref="WheelPaletteParser"/>，位于本集 Models；
/// <see cref="CustomColorPreset"/> 仍居共享内核——AppConfig 配置 POCO 依赖）、轮盘视觉几何
/// （<see cref="WheelGeometry"/>）、轮盘工厂（<see cref="WheelFactory"/>/
/// <see cref="IWheelFactory"/>）与核图标预览转换器（<see cref="CoreIconGeometryConverter"/>/
/// <see cref="CoreIconNameConverter"/>）位于 <c>StarPie.Wheel</c>；模块注册器
/// <see cref="WheelModuleRegistrar"/> 下放轮盘工厂与外观设置子 VM 的 DI 注册。
/// 工厂接口与实现同驻本模块，手势侧（<see cref="GestureEngine"/>，位于 StarPie.Gestures）
/// 只经接口消费；预览 Profile 只读契约 <see cref="IProfilePreviewSource"/> 位于共享内核
/// Core（实现方 ProfileListViewModel、消费方 WheelAppearanceSettingsViewModel 均只依赖 Core）。
/// Wheel → Core 单向 + Wheel → Theme 允许边（IThemeService），不引用宿主/其它业务模块。
/// </summary>
public sealed class WheelAssemblyPlacementTests
{
    [Fact]
    public void M2出口_归属独立模块程序集_且命名空间统一为StarPie()
    {
        Assert.Equal("StarPie.Wheel", typeof(WheelViewModel).Assembly.GetName().Name);
        Assert.Equal("StarPie.Wheel", typeof(IWheelViewModel).Assembly.GetName().Name);
        Assert.Equal("StarPie.Wheel", typeof(IWheelAppearanceState).Assembly.GetName().Name);
        Assert.Equal("StarPie.Wheel", typeof(WheelAppearanceSettingsViewModel).Assembly.GetName().Name);
        Assert.Equal("StarPie.Wheel", typeof(RadialWindow).Assembly.GetName().Name);
        Assert.Equal("StarPie.Wheel", typeof(IRadialStyleRenderer).Assembly.GetName().Name);
        Assert.Equal("StarPie.Wheel", typeof(WheelPreviewRenderer).Assembly.GetName().Name);
        Assert.Equal("StarPie.Wheel", typeof(WheelPalette).Assembly.GetName().Name);
        Assert.Equal("StarPie.Wheel", typeof(WheelPaletteCatalog).Assembly.GetName().Name);
        Assert.Equal("StarPie.Wheel", typeof(WheelPaletteParser).Assembly.GetName().Name);
        Assert.Equal("StarPie.Wheel", typeof(WheelGeometry).Assembly.GetName().Name);
        Assert.Equal("StarPie.Wheel", typeof(WheelFactory).Assembly.GetName().Name);
        Assert.Equal("StarPie.Wheel", typeof(IWheelFactory).Assembly.GetName().Name);
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
        Assert.Equal("StarPie.Services.Wheel", typeof(IWheelFactory).Namespace);
        Assert.Equal("StarPie.Views.Converters", typeof(CoreIconGeometryConverter).Namespace);
        Assert.Equal("StarPie.Modules", typeof(WheelModuleRegistrar).Namespace);
    }

    [Fact]
    public void M2程序集_单向依赖共享内核Core并允许M4边_不引用Host与其他业务模块()
    {
        string?[] referenced = typeof(WheelViewModel).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name)
            .ToArray();

        Assert.Contains("StarPie.Core", referenced);
        // 轮盘 → 主题允许边：RadialWindow/WheelFactory 消费 IThemeService。
        Assert.Contains("StarPie.Theme", referenced);
        Assert.DoesNotContain("StarPie", referenced);
        Assert.DoesNotContain("StarPie.Programs", referenced);
        Assert.DoesNotContain("StarPie.Shell", referenced);
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
    public void D5解结_工厂随M2且接口留M2侧_M1手势侧只经接口引用()
    {
        // IWheelFactory 与实现同在 StarPie.Wheel。
        Assert.Equal("StarPie.Wheel", typeof(IWheelFactory).Assembly.GetName().Name);
        Assert.True(typeof(IWheelFactory).IsAssignableFrom(typeof(WheelFactory)));

        // 手势侧（GestureEngine，位于 StarPie.Gestures）构造注入的是轮盘接口——
        // StarPie.Gestures → StarPie.Wheel 仅接口面，不反向组装瞬态轮盘。
        Assert.Equal("StarPie.Gestures", typeof(GestureEngine).Assembly.GetName().Name);
        var wheelFactoryParam = typeof(GestureEngine)
            .GetConstructors()
            .Single()
            .GetParameters()
            .Single(p => p.Name == "wheelFactory");
        Assert.Equal(typeof(IWheelFactory), wheelFactoryParam.ParameterType);
        Assert.Equal("StarPie.Wheel", wheelFactoryParam.ParameterType.Assembly.GetName().Name);

        // 手势 → 轮盘单向成立：GestureEngine 所在程序集不反向引用宿主（其余跨模块契约经 Core）。
        string?[] m1References = typeof(GestureEngine).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToArray();
        Assert.DoesNotContain("StarPie", m1References);
    }

    [Fact]
    public void D5解结_预览Profile只读契约_上提共享内核Core()
    {
        Assert.Equal("StarPie.Core", typeof(IProfilePreviewSource).Assembly.GetName().Name);
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
        // 对只读契约的消费，以替身注册别名即可（真实别名装配由 GesturesModuleRegistrar 覆盖）。
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
