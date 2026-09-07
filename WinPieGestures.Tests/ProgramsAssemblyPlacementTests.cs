using System.Linq;
using System.Threading.Tasks;

namespace StarPie.Tests;

/// <summary>
/// 程序模块（Programs）跨程序集归属与依赖收口：程序扫描/目录/快捷方式解析与
/// <see cref="ProgramEntry"/> 位于 <c>StarPie.Programs</c>；程序集零共享内核与宿主依赖
/// （图标补全经组合根注入的委托）；宿主对话框链与测试工程显式引用本模块出口。
/// </summary>
public sealed class ProgramsAssemblyPlacementTests
{
    [Fact]
    public void M3出口_归属独立模块程序集_且命名空间统一为StarPie()
    {
        Assert.Equal("StarPie.Programs", typeof(ProgramScanner).Assembly.GetName().Name);
        Assert.Equal("StarPie.Programs", typeof(ProgramCatalog).Assembly.GetName().Name);
        Assert.Equal("StarPie.Programs", typeof(ProgramEntry).Assembly.GetName().Name);
        Assert.Equal("StarPie.Programs", typeof(ShortcutResolver).Assembly.GetName().Name);

        Assert.Equal("StarPie.Services.Programs", typeof(ProgramEntry).Namespace);
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
        // ProgramPickerViewModel 的扫描委托/展示元素类型即 Programs 的 ProgramEntry——
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
