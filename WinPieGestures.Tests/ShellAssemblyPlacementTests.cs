using System.Collections;
using System.IO;
using System.Linq;
using System.Resources;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using StarPie.Modules;
using StarPie.Services;
using StarPie.Services.Navigation;
using StarPie.Services.Shell;
using StarPie.ViewModels.Pages;
using StarPie.Views.Pages;

namespace StarPie.Tests;

/// <summary>
/// 壳层模块（Shell）跨程序集归属与依赖收口：壳层服务（托盘/自启/内存）与高级/关于设置面
/// （VM+View）位于 <c>StarPie.Shell</c>；模块注册器（<see cref="ShellModuleRegistrar"/>：
/// RegisterNavigation + RegisterServices）随模块驻本程序集；宿主回调契约
/// <see cref="AppHostDelegates"/> 与共享页面基类 <see cref="SettingsPageBase"/> 位于
/// 共享内核 <c>StarPie.Core</c>（跨程序集页面共用）。
/// Shell → Core 单向，不引用宿主/其它业务模块。
/// </summary>
public sealed class ShellAssemblyPlacementTests
{
    [Fact]
    public void M5出口_归属独立模块程序集_且命名空间统一为StarPie()
    {
        Assert.Equal("StarPie.Shell", typeof(TrayIconManager).Assembly.GetName().Name);
        Assert.Equal("StarPie.Shell", typeof(MemoryOptimizer).Assembly.GetName().Name);
        Assert.Equal("StarPie.Shell", typeof(GeneralSettingsViewModel).Assembly.GetName().Name);
        Assert.Equal("StarPie.Shell", typeof(AboutViewModel).Assembly.GetName().Name);
        Assert.Equal("StarPie.Shell", typeof(AdvancedSettingsPage).Assembly.GetName().Name);
        Assert.Equal("StarPie.Shell", typeof(AboutSettingsPage).Assembly.GetName().Name);
        Assert.Equal("StarPie.Shell", typeof(ShellModuleRegistrar).Assembly.GetName().Name);

        Assert.Equal("StarPie.Services.Shell", typeof(TrayIconManager).Namespace);
        Assert.Equal("StarPie.Services.Shell", typeof(MemoryOptimizer).Namespace);
        Assert.Equal("StarPie.ViewModels.Pages", typeof(GeneralSettingsViewModel).Namespace);
        Assert.Equal("StarPie.ViewModels.Pages", typeof(AboutViewModel).Namespace);
        Assert.Equal("StarPie.Modules", typeof(ShellModuleRegistrar).Namespace);
    }

    [Fact]
    public void M5程序集_单向依赖共享内核Core_不引用Host与其他业务模块()
    {
        string?[] referenced = typeof(GeneralSettingsViewModel).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name)
            .ToArray();

        Assert.Contains("StarPie.Core", referenced);
        Assert.DoesNotContain("StarPie", referenced);
        Assert.DoesNotContain("StarPie.Programs", referenced);
    }

    [Fact]
    public void 宿主回调契约与页面基类_归属共享内核Core且公开()
    {
        Assert.Equal("StarPie.Core", typeof(AppHostDelegates).Assembly.GetName().Name);
        Assert.True(typeof(AppHostDelegates).IsPublic);
        Assert.Equal("StarPie.Core", typeof(SettingsPageBase).Assembly.GetName().Name);
        Assert.True(typeof(SettingsPageBase).IsPublic);
    }

    [Fact]
    public void M5注册器_跨程序集自报导航项_槽位与AutomationId不变()
    {
        var catalog = new NavigationCatalog();
        ShellModuleRegistrar.RegisterNavigation(catalog);

        Assert.Equal(2, catalog.Entries.Count);
        Assert.Equal(new[] { NavigationSlot.Advanced, NavigationSlot.About }, catalog.Entries.Select(e => e.Slot));
        Assert.Equal(new[] { "NavTab3", "NavTab4" }, catalog.Entries.Select(e => e.AutomationId));
        Assert.Equal(new[] { "TabAdvanced", "TabAbout" }, catalog.Entries.Select(e => e.TitleKey));
        Assert.All(catalog.Entries, e => Assert.Equal("StarPie.Shell", e.ViewModelType.Assembly.GetName().Name));
    }

    [Fact]
    public void ShellPageTemplates模板字典_BAML已编入模块程序集()
    {
        var assembly = typeof(ShellModuleRegistrar).Assembly;
        string resourcesName = assembly.GetManifestResourceNames().Single(n => n.EndsWith(".g.resources", System.StringComparison.OrdinalIgnoreCase));
        using var stream = assembly.GetManifestResourceStream(resourcesName);
        using var reader = new ResourceReader(stream!);
        var entries = reader.Cast<DictionaryEntry>().Select(e => (string)e.Key).ToArray();
        Assert.Contains(entries, name => name.Contains("shellpagetemplates", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void M5注册器_RegisterServices_可经微型容器解析两页面VM()
    {
        var services = new ServiceCollection();
        var config = new TestConfigService { Current = new AppConfig() };
        var localization = new LocalizationService();
        var messenger = TestHub.NewMessenger();
        services.AddSingleton<IConfigService>(config);
        services.AddSingleton<ILocalizationService>(localization);
        services.AddSingleton<IMessenger>(messenger);
        services.AddSingleton<IDialogService>(new TestDialogService());
        services.AddSingleton<ISaveDebouncer>(new TestSaveDebouncer());
        services.AddSingleton(new SettingsSaveOrchestrator(config, new TestSaveDebouncer(), messenger));
        services.AddSingleton(new JsonConfigService(
            Path.Combine(Path.GetTempPath(), $"StarPie.ShellTest-{System.Guid.NewGuid():N}.json"),
            localization));
        services.AddSingleton(new AppHostDelegates());

        ShellModuleRegistrar.RegisterServices(services);
        using var provider = services.BuildServiceProvider();

        var general = provider.GetRequiredService<GeneralSettingsViewModel>();
        var about = provider.GetRequiredService<AboutViewModel>();

        Assert.Equal("StarPie.Shell", general.GetType().Assembly.GetName().Name);
        Assert.Equal("StarPie.Shell", about.GetType().Assembly.GetName().Name);
        Assert.Same(general, provider.GetRequiredService<GeneralSettingsViewModel>());
    }
}
