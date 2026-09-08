using System.Linq;
using System.Threading.Tasks;
using StarPie.Modules;
using StarPie.Services.Icons;

namespace StarPie.Tests;

/// <summary>
/// 程序模块（Programs）跨程序集归属与依赖收口：程序扫描/目录/快捷方式解析位于
/// <c>StarPie.Programs</c>；ADR-0019/#87 起 M3 单向依赖 Core（契约
/// <see cref="IShortcutTargetResolver"/> 驻共享内核）；ADR-0020/#88 起纯数据
/// <see cref="ProgramEntry"/> 与扫描契约 <see cref="IProgramScanner"/> 上提 Core
/// （实现 <see cref="ProgramScanner"/> 在模块内、注册器下放注册），不引用 Host/其它业务模块；
/// 宿主对话框链与测试工程显式引用共享出口。
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
    public void M3出口_ProgramEntry与IProgramScanner及ProgramCatalog_上提共享内核Core()
    {
        Assert.Equal("StarPie.Core", typeof(ProgramEntry).Assembly.GetName().Name);
        Assert.Equal("StarPie.Core", typeof(IProgramScanner).Assembly.GetName().Name);
        Assert.Equal("StarPie.Core", typeof(ProgramCatalog).Assembly.GetName().Name);
        Assert.Equal("StarPie.Services.Programs", typeof(ProgramEntry).Namespace);
        Assert.Equal("StarPie.Services.Programs", typeof(IProgramScanner).Namespace);
        Assert.Equal("StarPie.Services.Programs", typeof(ProgramCatalog).Namespace);

        Assert.Equal("StarPie.Programs", typeof(ProgramScanner).Assembly.GetName().Name);
        Assert.True(typeof(IProgramScanner).IsAssignableFrom(typeof(ProgramScanner)));
    }

    [Fact]
    public void M3程序集_单向依赖共享内核Core_不引用Host与其他业务模块()
    {
        string?[] referenced = typeof(ProgramScanner).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name)
            .ToArray();

        Assert.Contains("StarPie.Core", referenced);
        Assert.DoesNotContain("StarPie", referenced);
        Assert.DoesNotContain("StarPie.Gestures", referenced);
        Assert.DoesNotContain("StarPie.Shell", referenced);
        Assert.DoesNotContain("StarPie.Theme", referenced);
        Assert.DoesNotContain("StarPie.Wheel", referenced);
    }

    [Fact]
    public void M3契约_驻共享内核_并由ShortcutResolver在模块内实现()
    {
        Assert.Equal("StarPie.Core", typeof(IShortcutTargetResolver).Assembly.GetName().Name);
        Assert.Equal("StarPie.Services.Icons", typeof(IShortcutTargetResolver).Namespace);

        Assert.Equal("StarPie.Programs", typeof(ShortcutResolver).Assembly.GetName().Name);
        Assert.True(typeof(IShortcutTargetResolver).IsAssignableFrom(typeof(ShortcutResolver)));
    }

    [Fact]
    public void M3注册器_归属独立模块程序集()
    {
        Assert.Equal("StarPie.Programs", typeof(ProgramsModuleRegistrar).Assembly.GetName().Name);
    }

    [Fact]
    public async Task Host程序选择器链_跨集消费共享出口()
    {
        // ProgramPickerViewModel 的扫描契约/展示元素类型即 Core 的 IProgramScanner/ProgramEntry——
        // 编译期已证明消费方显式引用共享内核；运行期再收口元素真实程序集归属。
        var scanner = new FakeScanner();
        var vm = new ProgramPickerViewModel(
            scanner,
            new TestDialogService(),
            new LocalizationService(),
            new ShortcutResolver());

        await vm.LoadAsync();

        var entry = Assert.Single(vm.DisplayedPrograms);
        Assert.Equal("StarPie.Core", entry.GetType().Assembly.GetName().Name);
        Assert.Equal("StarPie.Core", typeof(ProgramEntry).Assembly.GetName().Name);
    }

    /// <summary>扫描契约测试替身：返回单条候选（集成性质不触真实 IO）。</summary>
    private sealed class FakeScanner : IProgramScanner
    {
        public IReadOnlyList<ProgramEntry> ScanInstalledPrograms()
            => new[] { new ProgramEntry("记事本", @"C:\Windows\notepad.exe", @"C:\Windows\notepad.exe", null) };
    }
}
