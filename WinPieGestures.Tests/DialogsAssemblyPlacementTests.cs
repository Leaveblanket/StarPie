using System.Collections;
using System.Linq;
using System.Resources;
using Microsoft.Extensions.DependencyInjection;
using StarPie.Modules;
using StarPie.Services.Configuration;
using StarPie.Services.Dialogs;
using StarPie.Services.Icons;
using StarPie.Services.Localization;
using StarPie.Services.Programs;
using StarPie.Services.Shell;
using StarPie.ViewModels.Dialogs;
using StarPie.Views.Controls;
using StarPie.Views.Dialogs;

namespace StarPie.Tests;

/// <summary>
/// 对话框模块（Dialogs，ADR-0020/#88）跨程序集归属、依赖与可见性收口：S6 实现
/// （<see cref="DialogService"/> + 五对对话框 VM/Window + 取色行为
/// <see cref="SpectrumCanvasBehavior"/>）位于 <c>StarPie.Dialogs</c>；契约
/// <see cref="IDialogService"/> 与结果 record 留共享内核 Core；模块注册器
/// <see cref="DialogsModuleRegistrar"/> 下放 DI 注册。依赖方向：Dialogs → Core 单向 +
/// Dialogs → Theme 允许边（窗口主题应用消费 <see cref="IThemeService"/>）+ Dialogs →
/// Icons.Contracts 契约边（ADR-0023/#95：S1 契约独立成集，不再经 Core 消费 S1 类型），
/// 不引用 Host/Programs/其它业务模块——程序扫描经 Core 契约 <see cref="IProgramScanner"/>
/// 注入；DialogService 裁决 public（宿主 SetOwner 装配面）。
/// </summary>
public sealed class DialogsAssemblyPlacementTests
{
    [Fact]
    public void S6出口_归属独立模块程序集_且命名空间统一为StarPie()
    {
        Assert.Equal("StarPie.Dialogs", typeof(DialogService).Assembly.GetName().Name);
        Assert.Equal("StarPie.Dialogs", typeof(ProgramPickerViewModel).Assembly.GetName().Name);
        Assert.Equal("StarPie.Dialogs", typeof(IconPickerViewModel).Assembly.GetName().Name);
        Assert.Equal("StarPie.Dialogs", typeof(ColorPickerViewModel).Assembly.GetName().Name);
        Assert.Equal("StarPie.Dialogs", typeof(InputViewModel).Assembly.GetName().Name);
        Assert.Equal("StarPie.Dialogs", typeof(ScreenEyedropperViewModel).Assembly.GetName().Name);
        Assert.Equal("StarPie.Dialogs", typeof(ProgramPickerWindow).Assembly.GetName().Name);
        Assert.Equal("StarPie.Dialogs", typeof(IconPickerWindow).Assembly.GetName().Name);
        Assert.Equal("StarPie.Dialogs", typeof(ColorPickerWindow).Assembly.GetName().Name);
        Assert.Equal("StarPie.Dialogs", typeof(InputDialog).Assembly.GetName().Name);
        Assert.Equal("StarPie.Dialogs", typeof(ScreenEyedropperWindow).Assembly.GetName().Name);
        Assert.Equal("StarPie.Dialogs", typeof(SpectrumCanvasBehavior).Assembly.GetName().Name);
        Assert.Equal("StarPie.Dialogs", typeof(DialogsModuleRegistrar).Assembly.GetName().Name);

        Assert.Equal("StarPie.Services.Dialogs", typeof(DialogService).Namespace);
        Assert.Equal("StarPie.ViewModels.Dialogs", typeof(ProgramPickerViewModel).Namespace);
        Assert.Equal("StarPie.Views.Dialogs", typeof(ProgramPickerWindow).Namespace);
        Assert.Equal("StarPie.Views.Controls", typeof(SpectrumCanvasBehavior).Namespace);
        Assert.Equal("StarPie.Modules", typeof(DialogsModuleRegistrar).Namespace);
    }

    [Fact]
    public void S6程序集_单向依赖共享内核Core并允许M4边_不引用Host与其他业务模块()
    {
        string?[] referenced = typeof(DialogService).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name)
            .ToArray();

        Assert.Contains("StarPie.Core", referenced);
        // 对话框窗口主题应用消费 M4 IThemeService（允许边，同 M2→M4 先例）。
        Assert.Contains("StarPie.Theme", referenced);
        // ADR-0023/#95：对话框链经 S1 契约程序集消费图标能力，不引用 Icons runtime。
        Assert.Contains("StarPie.Icons.Contracts", referenced);
        Assert.DoesNotContain("StarPie.Icons", referenced);
        Assert.DoesNotContain("StarPie", referenced);
        Assert.DoesNotContain("StarPie.Programs", referenced);
        Assert.DoesNotContain("StarPie.Shell", referenced);
        Assert.DoesNotContain("StarPie.Wheel", referenced);
        Assert.DoesNotContain("StarPie.Gestures", referenced);
    }

    [Fact]
    public void S6契约_驻共享内核_实现与窗口在独立模块程序集()
    {
        Assert.Equal("StarPie.Core", typeof(IDialogService).Assembly.GetName().Name);
        Assert.Equal("StarPie.Services.Dialogs", typeof(IDialogService).Namespace);

        Assert.Equal("StarPie.Dialogs", typeof(DialogService).Assembly.GetName().Name);
        Assert.True(typeof(IDialogService).IsAssignableFrom(typeof(DialogService)));
    }

    [Fact]
    public void Dialog窗口_BAML已编入模块程序集()
    {
        var assembly = typeof(DialogService).Assembly;
        string resourcesName = assembly.GetManifestResourceNames().Single(n => n.EndsWith(".g.resources", System.StringComparison.OrdinalIgnoreCase));
        using var stream = assembly.GetManifestResourceStream(resourcesName);
        using var reader = new ResourceReader(stream!);
        var entries = reader.Cast<DictionaryEntry>().Select(e => (string)e.Key).ToArray();

        Assert.Contains(entries, name => name.Contains("views/dialogs/programpickerwindow.baml", System.StringComparison.OrdinalIgnoreCase));
        Assert.Contains(entries, name => name.Contains("views/dialogs/iconpickerwindow.baml", System.StringComparison.OrdinalIgnoreCase));
        Assert.Contains(entries, name => name.Contains("views/dialogs/colorpickerwindow.baml", System.StringComparison.OrdinalIgnoreCase));
        Assert.Contains(entries, name => name.Contains("views/dialogs/inputdialog.baml", System.StringComparison.OrdinalIgnoreCase));
        Assert.Contains(entries, name => name.Contains("views/dialogs/screeneyedropperwindow.baml", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DialogService_依赖经共享契约注入_不直连业务模块具体类型()
    {
        // 扫描/图标/.lnk/主题能力全部经契约注入（IProgramScanner 驻 Core、
        // IIconAssetService 驻 Icons.Contracts（ADR-0023/#95）、IShortcutTargetResolver
        // #95 中间态暂驻 Core、IThemeService 驻 M4）——构造签名不含 Programs/Host
        // 具体类型，编译期证明 S6 不反向引用业务模块。
        var ctor = typeof(DialogService).GetConstructors().Single();
        var paramTypes = ctor.GetParameters().Select(p => p.ParameterType).ToArray();

        Assert.Contains(paramTypes, t => t == typeof(IProgramScanner));
        Assert.Contains(paramTypes, t => t == typeof(IIconAssetService));
        Assert.Contains(paramTypes, t => t == typeof(IShortcutTargetResolver));
        Assert.Contains(paramTypes, t => t == typeof(IThemeService));
        Assert.All(paramTypes, t =>
            Assert.NotEqual("StarPie", t.Assembly.GetName().Name));
    }

    [Fact]
    public void S6注册器_RegisterServices_可经微型容器解析对话框服务()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILocalizationService>(new LocalizationService());
        services.AddSingleton<IIconAssetService>(new TestIconAssetService());
        services.AddSingleton<IShortcutTargetResolver>(new FakeShortcutResolver());
        services.AddSingleton<IProgramScanner>(new FakeProgramScanner());
        // IThemeService 由主题模块注册器提供（对话框消费允许边）。
        ThemeModuleRegistrar.RegisterServices(services);

        DialogsModuleRegistrar.RegisterServices(services);
        using var provider = services.BuildServiceProvider();

        var dialogService = provider.GetRequiredService<IDialogService>();

        Assert.IsType<DialogService>(dialogService);
        Assert.Equal("StarPie.Dialogs", dialogService.GetType().Assembly.GetName().Name);
        Assert.Same(dialogService, provider.GetRequiredService<DialogService>());
    }

    /// <summary>.lnk 解析契约替身（不触 COM/Win32）。</summary>
    private sealed class FakeShortcutResolver : IShortcutTargetResolver
    {
        public bool ResolveShortcutTarget(string lnkPath, out string targetPath, out string iconPath, out int iconIndex)
        {
            targetPath = "";
            iconPath = "";
            iconIndex = 0;
            return false;
        }
    }

    /// <summary>扫描契约替身：返回空候选（不触真实注册表/文件 IO）。</summary>
    private sealed class FakeProgramScanner : IProgramScanner
    {
        public IReadOnlyList<ProgramEntry> ScanInstalledPrograms() => Array.Empty<ProgramEntry>();
    }
}
