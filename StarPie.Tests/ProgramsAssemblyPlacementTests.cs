using System.Linq;
using System.Threading.Tasks;
using StarPie.Modules;
using StarPie.Services.Icons;

namespace StarPie.Tests;

/// <summary>
/// 程序模块（Programs）跨程序集归属与依赖收口：程序扫描/目录/快捷方式解析位于
/// <c>StarPie.Programs</c>；ADR-0023/#96 起 M3 出口契约（<see cref="IProgramScanner"/>/
/// <see cref="ProgramEntry"/>/<see cref="ProgramCatalog"/> + SPI
/// <see cref="IShortcutTargetResolver"/>）随实现方下沉 <c>StarPie.Programs.Contracts</c>
/// （自 Core 迁出，命名空间不变）；ProgramScanner 另经 StarPie.Icons.Contracts 的
/// <see cref="IIconAssetService"/> 契约补图标（ADR-0023/#95）；本 runtime 只引用自身契约与
/// Icons.Contracts，不再引用共享内核 Core/其它业务模块；宿主对话框链与测试工程显式引用契约出口。
/// </summary>
public sealed class ProgramsAssemblyPlacementTests
{
    [Fact]
    public void M3出口_归属独立模块程序集_且命名空间统一为StarPie()
    {
        Assert.Equal("StarPie.Programs", typeof(ProgramScanner).Assembly.GetName().Name);
        Assert.Equal("StarPie.Programs", typeof(ShortcutResolver).Assembly.GetName().Name);

        Assert.Equal("StarPie.Services.Programs", typeof(ProgramEntry).Namespace);
    }

    [Fact]
    public void M3出口契约_随实现方下沉ProgramsContracts_命名空间不变()
    {
        Assert.Equal("StarPie.Programs.Contracts", typeof(ProgramEntry).Assembly.GetName().Name);
        Assert.Equal("StarPie.Programs.Contracts", typeof(IProgramScanner).Assembly.GetName().Name);
        Assert.Equal("StarPie.Programs.Contracts", typeof(ProgramCatalog).Assembly.GetName().Name);
        Assert.Equal("StarPie.Services.Programs", typeof(ProgramEntry).Namespace);
        Assert.Equal("StarPie.Services.Programs", typeof(IProgramScanner).Namespace);
        Assert.Equal("StarPie.Services.Programs", typeof(ProgramCatalog).Namespace);

        Assert.Equal("StarPie.Programs", typeof(ProgramScanner).Assembly.GetName().Name);
        Assert.True(typeof(IProgramScanner).IsAssignableFrom(typeof(ProgramScanner)));
    }

    [Fact]
    public void M3程序集_单向依赖自身契约与IconsContracts_不引用CoreHost与其他业务模块()
    {
        string?[] referenced = typeof(ProgramScanner).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name)
            .ToArray();

        // ADR-0023/#96：M3 契约随实现方下沉 Programs.Contracts，runtime 不再引用共享内核。
        Assert.Contains("StarPie.Programs.Contracts", referenced);
        Assert.DoesNotContain("StarPie.Core", referenced);
        // ADR-0023/#95：ProgramScanner 经 S1 契约程序集消费 IIconAssetService，
        // 不引用 Icons runtime。
        Assert.Contains("StarPie.Icons.Contracts", referenced);
        Assert.DoesNotContain("StarPie.Icons", referenced);
        Assert.DoesNotContain("StarPie", referenced);
        Assert.DoesNotContain("StarPie.Gestures", referenced);
        Assert.DoesNotContain("StarPie.Shell", referenced);
        Assert.DoesNotContain("StarPie.Theme", referenced);
        Assert.DoesNotContain("StarPie.Wheel", referenced);
    }

    [Fact]
    public void SPI契约_随M3下沉ProgramsContracts_并由ShortcutResolver在模块内实现()
    {
        // ADR-0023/#96：SPI 随实现方 M3 下沉 Programs.Contracts（自 Core 迁出）。
        Assert.Equal("StarPie.Programs.Contracts", typeof(IShortcutTargetResolver).Assembly.GetName().Name);
        Assert.Equal("StarPie.Services.Icons", typeof(IShortcutTargetResolver).Namespace);

        Assert.Equal("StarPie.Programs", typeof(ShortcutResolver).Assembly.GetName().Name);
        Assert.True(typeof(IShortcutTargetResolver).IsAssignableFrom(typeof(ShortcutResolver)));
    }

    [Fact]
    public void Core不含模块出口契约()
    {
        // ADR-0023/#96/#97：扫描/SPI/对话框/主题/轮盘/预览源契约随实现方下沉各
        // *.Contracts，Core 不再承载任何模块出口契约（类型级 + 依赖级双层收口）。
        Assert.NotEqual("StarPie.Core", typeof(IProgramScanner).Assembly.GetName().Name);
        Assert.NotEqual("StarPie.Core", typeof(ProgramEntry).Assembly.GetName().Name);
        Assert.NotEqual("StarPie.Core", typeof(ProgramCatalog).Assembly.GetName().Name);
        Assert.NotEqual("StarPie.Core", typeof(IShortcutTargetResolver).Assembly.GetName().Name);
        Assert.NotEqual("StarPie.Core", typeof(IDialogService).Assembly.GetName().Name);
        Assert.NotEqual("StarPie.Core", typeof(IThemeService).Assembly.GetName().Name);
        Assert.NotEqual("StarPie.Core", typeof(IWheelFactory).Assembly.GetName().Name);
        Assert.NotEqual("StarPie.Core", typeof(IWheelViewModel).Assembly.GetName().Name);
        Assert.NotEqual("StarPie.Core", typeof(IWheelAppearanceState).Assembly.GetName().Name);
        Assert.NotEqual("StarPie.Core", typeof(IProfilePreviewSource).Assembly.GetName().Name);

        string?[] coreReferences = typeof(StarPie.Services.Localization.ILocalizationService).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name)
            .ToArray();
        Assert.DoesNotContain("StarPie.Programs.Contracts", coreReferences);
        Assert.DoesNotContain("StarPie.Dialogs.Contracts", coreReferences);
        Assert.DoesNotContain("StarPie.Icons.Contracts", coreReferences);
        Assert.DoesNotContain("StarPie.Theme.Contracts", coreReferences);
        Assert.DoesNotContain("StarPie.Wheel.Contracts", coreReferences);
        Assert.DoesNotContain("StarPie.Gestures.Contracts", coreReferences);
    }

    [Fact]
    public void M3注册器_归属独立模块程序集()
    {
        Assert.Equal("StarPie.Programs", typeof(ProgramsModuleRegistrar).Assembly.GetName().Name);
    }

    [Fact]
    public async Task Host程序选择器链_跨集消费共享出口()
    {
        // ProgramPickerViewModel 的扫描契约/展示元素类型即 Programs.Contracts 的
        // IProgramScanner/ProgramEntry——编译期已证明消费方显式引用契约程序集；运行期再收口
        // 元素真实程序集归属。
        var scanner = new FakeScanner();
        var vm = new ProgramPickerViewModel(
            scanner,
            new TestDialogService(),
            new LocalizationService(),
            new ShortcutResolver());

        await vm.LoadAsync();

        var entry = Assert.Single(vm.DisplayedPrograms);
        Assert.Equal("StarPie.Programs.Contracts", entry.GetType().Assembly.GetName().Name);
        Assert.Equal("StarPie.Programs.Contracts", typeof(ProgramEntry).Assembly.GetName().Name);
    }

    /// <summary>扫描契约测试替身：返回单条候选（集成性质不触真实 IO）。</summary>
    private sealed class FakeScanner : IProgramScanner
    {
        public IReadOnlyList<ProgramEntry> ScanInstalledPrograms()
            => new[] { new ProgramEntry("记事本", @"C:\Windows\notepad.exe", @"C:\Windows\notepad.exe", null) };
    }
}
