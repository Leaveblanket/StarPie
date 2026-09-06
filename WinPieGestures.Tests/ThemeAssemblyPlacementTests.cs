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
/// B7/#80（模块化：M4 Theme 抽取）跨集归属、依赖与可见性收口：
/// 主题服务（<see cref="IThemeService"/>/<see cref="ThemeService"/>）、五套主题字典
/// （Views/Styles/Themes/*.xaml）、主题设置子 VM（<see cref="InterfaceThemeSettingsViewModel"/>/
/// <see cref="AppThemeOptionItem"/>）与调色板换入 <see cref="ThemePaletteManager"/> 迁入
/// <c>StarPie.Theme</c>；模块注册器 <see cref="ThemeModuleRegistrar"/> 下放服务与主题 VM 的
/// DI 注册；ThemePaletteManager/ThemeService.AttachPaletteApplier 裁决 public（Host AppHost
/// 装配面，B6/#79 TrayIconManager 先例）；主题应用消息 AppThemeChangedMessage 仍归 Core S4 hub
/// （放行共享面）。B10/#83 命名空间统一为 StarPie.*（全仓前缀替换，ADR-0016 决策 12）。
/// Theme → Core 单向，不引用 Host/其它业务模块；M2 轮盘件（RadialWindow 等）B8 前仍驻 Host。
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
        // B7/#80 可见性裁决：ThemePaletteManager（原 Host internal）与
        // ThemeService.AttachPaletteApplier 公开——Host AppHost 装配面
        // （new ThemePaletteManager + AttachPaletteApplier + Apply），
        // 与 B6/#79 TrayIconManager 公开先例一致；不引入 InternalsVisibleTo。
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
        // AppThemeChangedMessage 语义归 M4，但类型定义集中于 S4 hub（messages.md 放行共享面），
        // B7 不随模块迁出——避免 M4/Core 消息 hub 重复载体。
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
