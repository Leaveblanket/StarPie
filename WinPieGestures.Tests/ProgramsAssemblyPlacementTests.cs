using System.Linq;
using System.Threading.Tasks;

namespace WinPieGestures.Tests;

/// <summary>
/// B4/#77（模块化：M3 Programs 首个独立模块程序集）跨集归属与依赖收口：
/// M3 三件（扫描/目录/快捷方式解析）与 <see cref="ProgramEntry"/> 迁入
/// <c>StarPie.Programs</c>；程序集零共享内核(Core)/Host 依赖（图标补全改经组合根注入的
/// S1 委托）；Host 对话框链与测试工程显式引用 M3 出口。命名空间维持 WinPieGestures.*
/// （B10 才统一，ADR-0016 决策 12）。
/// </summary>
public sealed class ProgramsAssemblyPlacementTests
{
    [Fact]
    public void M3出口_归属独立模块程序集_且命名空间维持WinPieGestures()
    {
        Assert.Equal("StarPie.Programs", typeof(ProgramScanner).Assembly.GetName().Name);
        Assert.Equal("StarPie.Programs", typeof(ProgramCatalog).Assembly.GetName().Name);
        Assert.Equal("StarPie.Programs", typeof(ProgramEntry).Assembly.GetName().Name);
        Assert.Equal("StarPie.Programs", typeof(ShortcutResolver).Assembly.GetName().Name);

        Assert.Equal("WinPieGestures.Services.Programs", typeof(ProgramEntry).Namespace);
    }

    [Fact]
    public void M3程序集_不引用共享内核Core与Host()
    {
        string?[] referenced = typeof(ProgramCatalog).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name)
            .ToArray();

        Assert.DoesNotContain("StarPie.Core", referenced);
        Assert.DoesNotContain("StarPie", referenced);
    }

    [Fact]
    public async Task Host程序选择器链_跨集消费M3出口()
    {
        // ProgramPickerViewModel 的扫描委托/展示元素类型即 M3 ProgramEntry——
        // 编译期已证明 Host→Programs 显式引用；运行期再收口元素真实程序集归属。
        var vm = new ProgramPickerViewModel(
            () => new[] { new ProgramEntry("记事本", @"C:\Windows\notepad.exe", @"C:\Windows\notepad.exe", null) },
            new TestDialogService(),
            new LocalizationService());

        await vm.LoadAsync();

        var entry = Assert.Single(vm.DisplayedPrograms);
        Assert.Equal("StarPie.Programs", entry.GetType().Assembly.GetName().Name);
        Assert.Equal("StarPie.Programs", typeof(ProgramEntry).Assembly.GetName().Name);
    }
}
