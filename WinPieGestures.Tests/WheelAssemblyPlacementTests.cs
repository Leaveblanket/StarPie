using System.Linq;
using System.Collections;
using System.Resources;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using WinPieGestures.Modules;
using WinPieGestures.Services;
using WinPieGestures.Services.Configuration;
using WinPieGestures.Services.Dialogs;
using WinPieGestures.Services.Gestures;
using WinPieGestures.Services.Localization;
using WinPieGestures.Services.Shell;
using WinPieGestures.Services.Wheel;
using WinPieGestures.ViewModels.Pages;
using WinPieGestures.ViewModels.Wheel;
using WinPieGestures.Views.Converters;
using WinPieGestures.Views.Renderers;
using WinPieGestures.Views.Wheel;

namespace WinPieGestures.Tests;

/// <summary>
/// B8/#81（模块化：M2 Wheel 抽取，含 D5 解结）跨集归属、依赖与可见性收口：
/// 轮盘 VM（<see cref="WheelViewModel"/>/<see cref="IWheelViewModel"/>）、窗口
/// （<see cref="RadialWindow"/>）、样式渲染器（<see cref="IRadialStyleRenderer"/> 系 +
/// <see cref="WheelPreviewRenderer"/>）、轮盘配色（<see cref="WheelPalette"/>/
/// <see cref="WheelPaletteCatalog"/>/<see cref="WheelPaletteParser"/>，物理收编本集 Models；
/// <see cref="CustomColorPreset"/> 仍居 Core——AppConfig 配置 POCO 依赖）、轮盘视觉几何
/// （<see cref="WheelGeometry"/>）、轮盘工厂（<see cref="WheelFactory"/>/<see cref="IWheelFactory"/>，
/// D5）与核图标预览转换器（<see cref="CoreIconGeometryConverter"/>/<see cref="CoreIconNameConverter"/>，
/// B5/#78 暂留 Host 的归属裁决：随 M2）迁入 <c>StarPie.Wheel</c>；模块注册器
/// <see cref="WheelModuleRegistrar"/> 下放轮盘工厂与外观设置子 VM 的 DI 注册。
/// D5 解结（ADR-0016 决策 11）：轮盘工厂实现随 M2、<see cref="IWheelFactory"/> 留 M2 侧接口，
/// M1 手势侧（Host <see cref="GestureEngine"/>）只经接口消费；预览 Profile 只读契约
/// <see cref="IProfilePreviewSource"/> 上提共享内核 Core（实现方 M1 ProfileListViewModel、
/// 消费方 M2 WheelAppearanceSettingsViewModel 均只依赖 Core）。
/// 命名空间维持 WinPieGestures.*（B10 才统一，ADR-0016 决策 12）。
/// Wheel → Core 单向 + Wheel → Theme 允许边（IThemeService），不引用 Host/其它业务模块。
/// </summary>
public sealed class WheelAssemblyPlacementTests
{
    [Fact]
    public void M2出口_归属独立模块程序集_且命名空间维持WinPieGestures()
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

        Assert.Equal("WinPieGestures.ViewModels.Wheel", typeof(WheelViewModel).Namespace);
        Assert.Equal("WinPieGestures.ViewModels.Pages", typeof(WheelAppearanceSettingsViewModel).Namespace);
        Assert.Equal("WinPieGestures.Views.Wheel", typeof(RadialWindow).Namespace);
        Assert.Equal("WinPieGestures.Views.Renderers", typeof(WheelPreviewRenderer).Namespace);
        Assert.Equal("WinPieGestures.Models", typeof(WheelPalette).Namespace);
        Assert.Equal("WinPieGestures.Services.Wheel", typeof(WheelGeometry).Namespace);
        Assert.Equal("WinPieGestures.Services.Wheel", typeof(WheelFactory).Namespace);
        Assert.Equal("WinPieGestures.Services.Wheel", typeof(IWheelFactory).Namespace);
        Assert.Equal("WinPieGestures.Views.Converters", typeof(CoreIconGeometryConverter).Namespace);
        Assert.Equal("WinPieGestures.Modules", typeof(WheelModuleRegistrar).Namespace);
    }

    [Fact]
    public void M2程序集_单向依赖共享内核Core并允许M4边_不引用Host与其他业务模块()
    {
        string?[] referenced = typeof(WheelViewModel).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name)
            .ToArray();

        Assert.Contains("StarPie.Core", referenced);
        // M2→M4 允许边（assemblies.md §3）：RadialWindow/WheelFactory 消费 IThemeService。
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
        // IWheelFactory 与实现同在 StarPie.Wheel（D5/ADR-0016 决策 11）。
        Assert.Equal("StarPie.Wheel", typeof(IWheelFactory).Assembly.GetName().Name);
        Assert.True(typeof(IWheelFactory).IsAssignableFrom(typeof(WheelFactory)));

        // M1 手势侧（GestureEngine 仍驻 Host，B9 收编 M1 前）构造注入的是 M2 接口——
        // Host → StarPie.Wheel 仅接口面，不反向组装 M2 瞬态轮盘。
        Assert.Equal("StarPie", typeof(GestureEngine).Assembly.GetName().Name);
        var wheelFactoryParam = typeof(GestureEngine)
            .GetConstructors()
            .Single()
            .GetParameters()
            .Single(p => p.Name == "wheelFactory");
        Assert.Equal(typeof(IWheelFactory), wheelFactoryParam.ParameterType);
        Assert.Equal("StarPie.Wheel", wheelFactoryParam.ParameterType.Assembly.GetName().Name);
    }

    [Fact]
    public void D5解结_预览Profile只读契约_上提共享内核Core()
    {
        Assert.Equal("StarPie.Core", typeof(IProfilePreviewSource).Assembly.GetName().Name);
        Assert.Equal("WinPieGestures.ViewModels.Pages", typeof(IProfilePreviewSource).Namespace);
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
        // M1 实现方（ProfileListViewModel）仍驻 Host：以只读契约别名注册替身，镜像 Composition 装配。
        services.AddSingleton<IProfilePreviewSource>(new FakeProfilePreviewSource());
        // IThemeService 由 M4 注册器提供（Wheel 消费允许边）。
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
