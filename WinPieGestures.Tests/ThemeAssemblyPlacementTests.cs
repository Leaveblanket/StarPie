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
/// （<see cref="IThemeService"/>/<see cref="ThemeService"/>）、五套主题字典
/// （Views/Styles/Themes/*.xaml）、主题设置子 VM（<see cref="InterfaceThemeSettingsViewModel"/>/
/// <see cref="AppThemeOptionItem"/>）与调色板换入 <see cref="ThemePaletteManager"/> 位于
/// <c>StarPie.Theme</c>；模块注册器 <see cref="ThemeModuleRegistrar"/> 下放服务与主题 VM 的
/// DI 注册；ThemePaletteManager 与 ThemeService.AttachPaletteApplier 为 public（供宿主
/// AppHost 装配面跨程序集编排）；主题应用消息 AppThemeChangedMessage 位于共享内核消息 Hub。
/// Theme → Core 单向，不引用宿主/其它业务模块。
/// </summary>
public sealed class ThemeAssemblyPlacementTests
{
    [Fact]
    public void M4出口_归属独立模块程序集_且命名空间统一为StarPie()
    {
        Assert.Equal("StarPie.Theme", typeof(IThemeService).Assembly.GetName().Name);
        Assert.Equal("StarPie.Theme", typeof(ThemeService).Assembly.GetName().Name);
        Assert.Equal("StarPie.Theme", typeof(ThemePaletteManager).Assembly.GetName().Name);
        Assert.Equal("StarPie.Theme", typeof(InterfaceThemeSettingsViewModel).Assembly.GetName().Name);
        Assert.Equal("StarPie.Theme", typeof(AppThemeOptionItem).Assembly.GetName().Name);
        Assert.Equal("StarPie.Theme", typeof(ThemeModuleRegistrar).Assembly.GetName().Name);

        Assert.Equal("StarPie.Services.Shell", typeof(ThemeService).Namespace);
        Assert.Equal("StarPie.Services.Shell", typeof(IThemeService).Namespace);
        Assert.Equal("StarPie", typeof(ThemePaletteManager).Namespace);
        Assert.Equal("StarPie.ViewModels.Pages", typeof(InterfaceThemeSettingsViewModel).Namespace);
        Assert.Equal("StarPie.Modules", typeof(ThemeModuleRegistrar).Namespace);
    }

    [Fact]
    public void M4程序集_单向依赖共享内核Core_不引用Host与其他业务模块()
    {
        string?[] referenced = typeof(ThemeService).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name)
            .ToArray();

        Assert.Contains("StarPie.Core", referenced);
        Assert.DoesNotContain("StarPie", referenced);
        Assert.DoesNotContain("StarPie.Programs", referenced);
        Assert.DoesNotContain("StarPie.Shell", referenced);
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
        // （避免消息载体重复），不随模块迁移。
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
