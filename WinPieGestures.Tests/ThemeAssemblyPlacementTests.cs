using System.Collections;
using System.IO;
using System.Linq;
using System.Resources;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using StarPie.Modules;
using StarPie.Services;
using StarPie.Services.Configuration;
using StarPie.Services.Localization;
using StarPie.Services.Messages;
using StarPie.Services.Shell;
using StarPie.ViewModels.Pages;

namespace StarPie.Tests;

/// <summary>
/// 主题模块（Theme）跨程序集归属、依赖与可见性收口：主题服务
/// （<see cref="ThemeService"/>）、五套主题字典（Views/Styles/Themes/*.xaml）、主题设置子 VM
/// （<see cref="InterfaceThemeSettingsViewModel"/>/<see cref="AppThemeOptionItem"/>）与调色板
/// 换入 <see cref="ThemePaletteManager"/> 位于 <c>StarPie.Theme</c>；出口契约
/// <see cref="IThemeService"/> 随实现方下沉 <c>StarPie.Theme.Contracts</c>（ADR-0023/#97，
/// 自 StarPie.Theme 迁出，命名空间不变）；模块注册器 <see cref="ThemeModuleRegistrar"/> 下放
/// 服务与主题 VM 的 DI 注册；ThemePaletteManager 与 ThemeService.AttachPaletteApplier 为
/// public（供宿主 AppHost 装配面跨程序集编排）；主题应用消息 AppThemeChangedMessage 位于
/// 共享内核消息 Hub。Theme → Core + Theme.Contracts 单向，不引用宿主/其它业务模块 runtime。
/// </summary>
public sealed class ThemeAssemblyPlacementTests
{
    [Fact]
    public void M4出口_归属独立模块程序集_且命名空间统一为StarPie()
    {
        Assert.Equal("StarPie.Theme", typeof(ThemeService).Assembly.GetName().Name);
        Assert.Equal("StarPie.Theme", typeof(ThemePaletteManager).Assembly.GetName().Name);
        Assert.Equal("StarPie.Theme", typeof(InterfaceThemeSettingsViewModel).Assembly.GetName().Name);
        Assert.Equal("StarPie.Theme", typeof(AppThemeOptionItem).Assembly.GetName().Name);
        Assert.Equal("StarPie.Theme", typeof(ThemeModuleRegistrar).Assembly.GetName().Name);

        Assert.Equal("StarPie.Services.Shell", typeof(ThemeService).Namespace);
        Assert.Equal("StarPie", typeof(ThemePaletteManager).Namespace);
        Assert.Equal("StarPie.ViewModels.Pages", typeof(InterfaceThemeSettingsViewModel).Namespace);
        Assert.Equal("StarPie.Modules", typeof(ThemeModuleRegistrar).Namespace);
    }

    [Fact]
    public void M4契约_随实现方下沉ThemeContracts_命名空间不变()
    {
        // ADR-0023/#97：IThemeService 自 StarPie.Theme 迁出，实现（ThemeService）与
        // 注册器仍驻 runtime。
        Assert.Equal("StarPie.Theme.Contracts", typeof(IThemeService).Assembly.GetName().Name);
        Assert.Equal("StarPie.Services.Shell", typeof(IThemeService).Namespace);

        Assert.Equal("StarPie.Theme", typeof(ThemeService).Assembly.GetName().Name);
        Assert.True(typeof(IThemeService).IsAssignableFrom(typeof(ThemeService)));
    }

    [Fact]
    public void M4程序集_单向依赖Core与自身契约_不引用Host与其他业务模块runtime()
    {
        string?[] referenced = typeof(ThemeService).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name)
            .ToArray();

        Assert.Contains("StarPie.Core", referenced);
        // ADR-0023/#97：M4 runtime 实现自有契约（ThemeService 实现 IThemeService）。
        Assert.Contains("StarPie.Theme.Contracts", referenced);
        Assert.DoesNotContain("StarPie", referenced);
        Assert.DoesNotContain("StarPie.Programs", referenced);
        Assert.DoesNotContain("StarPie.Shell", referenced);
        Assert.DoesNotContain("StarPie.Dialogs", referenced);
        Assert.DoesNotContain("StarPie.Wheel", referenced);
        Assert.DoesNotContain("StarPie.Gestures", referenced);
        Assert.DoesNotContain("StarPie.Icons", referenced);
    }

    [Fact]
    public void 主题换入装配面_裁决public_供HostAppHost跨程序集编排()
    {
        // ThemePaletteManager 与 ThemeService.AttachPaletteApplier 为 public：
        // 宿主 AppHost 装配面（new ThemePaletteManager + AttachPaletteApplier + Apply）
        // 跨程序集编排；不引入 InternalsVisibleTo。
        Assert.True(typeof(ThemePaletteManager).IsPublic);
        Assert.True(typeof(ThemeService).IsPublic);
        Assert.True(typeof(IThemeService).IsPublic);
        Assert.True(typeof(ThemeModuleRegistrar).IsPublic);
        Assert.True(typeof(ThemeService).GetMethod(nameof(ThemeService.AttachPaletteApplier))?.IsPublic == true);
    }

    [Fact]
    public void 五套主题字典_BAML已编入模块程序集()
    {
        var assembly = typeof(ThemePaletteManager).Assembly;
        string resourcesName = assembly.GetManifestResourceNames().Single(n => n.EndsWith(".g.resources", System.StringComparison.OrdinalIgnoreCase));
        using var stream = assembly.GetManifestResourceStream(resourcesName);
        using var reader = new ResourceReader(stream!);
        var entries = reader.Cast<DictionaryEntry>().Select(e => (string)e.Key).ToArray();
        foreach (string theme in new[] { "light", "dark", "midnightnavy", "royalviolet", "titaniumgray" })
        {
            Assert.Contains(entries, name => name.Contains($"themes/{theme}.baml", System.StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void 主题应用消息_仍归共享内核Core消息Hub()
    {
        // AppThemeChangedMessage 语义属主题模块，但类型定义集中在共享内核消息 Hub
        // （避免消息载体重复）。
        Assert.Equal("StarPie.Core", typeof(AppThemeChangedMessage).Assembly.GetName().Name);
        Assert.Equal("StarPie.Services.Messages", typeof(AppThemeChangedMessage).Namespace);
    }

    [Fact]
    public void M4注册器_RegisterServices_可经微型容器解析服务与主题设置子VM()
    {
        var services = new ServiceCollection();
        var config = new TestConfigService { Current = new AppConfig() };
        var localization = new LocalizationService();
        services.AddSingleton<IConfigService>(config);
        services.AddSingleton<ILocalizationService>(localization);
        services.AddSingleton<IMessenger>(TestHub.NewMessenger());

        ThemeModuleRegistrar.RegisterServices(services);
        using var provider = services.BuildServiceProvider();

        var themeService = provider.GetRequiredService<ThemeService>();
        var vm = provider.GetRequiredService<InterfaceThemeSettingsViewModel>();

        Assert.Same(themeService, provider.GetRequiredService<IThemeService>());
        Assert.Equal("StarPie.Theme", themeService.GetType().Assembly.GetName().Name);
        Assert.Equal("StarPie.Theme", vm.GetType().Assembly.GetName().Name);
        Assert.Equal("System", vm.AppTheme);
    }
}
