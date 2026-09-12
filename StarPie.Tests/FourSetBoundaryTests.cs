using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace StarPie.Tests;

/// <summary>
/// 四集边界基线·工程面（#111；ADR-0027 / plugins.md §2）：
/// StarPie.Sdk（net10.0，零 WPF 零第三方包）、StarPie.Sdk.Wpf（WPF 类型契约面）、
/// StarPie.Host（net10.0，零 WPF）、StarPie.Ui（WinExe，程序集名保持 StarPie，
/// 发布产物 StarPie.exe）。本文件与 <see cref="RuntimeNoCrossReferenceTests"/> 是
/// P1.3–P1.10 归并搬迁的机械化护栏：故意引入违规（Host 引 WPF / Sdk 引第三方包 /
/// 反向引用 / 发布路径漂移）会被直接测出。
/// </summary>
public sealed class FourSetBoundaryTests
{
    private const string UiProject = @"StarPie.Ui\StarPie.Ui.csproj";
    private const string SdkProject = @"StarPie.Sdk\StarPie.Sdk.csproj";
    private const string SdkWpfProject = @"StarPie.Sdk.Wpf\StarPie.Sdk.Wpf.csproj";
    private const string HostProject = @"StarPie.Host\StarPie.Host.csproj";
    private const string TestsProject = @"StarPie.Tests\StarPie.Tests.csproj";
    private const string RootTfm = "net10.0-windows10.0.19041.0";

    /// <summary>仓库全部工程（四集 + 测试工程）——解决方案登记与引用面断言的扫描基准。</summary>
    private static readonly string[] AllProjects =
    {
        UiProject, SdkProject, SdkWpfProject, HostProject, TestsProject,
    };

    [Fact]
    public void 解决方案_只登记四集与测试工程_旧工程路径已移除()
    {
        XDocument slnx = XDocument.Load(Path.Combine(FourSetBoundaryProbe.RepoRoot, "StarPie.slnx"));
        string[] paths = slnx.Descendants("Project")
            .Select(element => (string)element.Attribute("Path")!)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            AllProjects.OrderBy(path => path, StringComparer.Ordinal).ToArray(),
            paths);
        Assert.All(
            FourSetBoundaryProbe.LegacyProjectPaths,
            legacy => Assert.DoesNotContain(legacy, paths));
        Assert.DoesNotContain(@"StarPie\StarPie.csproj", paths);

        foreach (string project in AllProjects)
        {
            Assert.True(
                File.Exists(Path.Combine(FourSetBoundaryProbe.RepoRoot, project)),
                $"解决方案登记的工程文件不存在: {project}");
        }
    }

    [Fact]
    public void 根构建属性_四集共享面集中一处_工程级只留差异()
    {
        // 根 props 是四集共享面（TFM/可空性/隐式 using/分析器级别/根命名空间）的唯一来源；
        // 断言读“生效值”而非 csproj 字面量：属性留在根 props 或下移工程都不改变结论。
        XDocument rootProps = FourSetBoundaryProbe.LoadRootBuildProps();
        Assert.Equal(RootTfm, FourSetBoundaryProbe.GetProperty(rootProps, "TargetFramework"));
        Assert.Equal("enable", FourSetBoundaryProbe.GetProperty(rootProps, "Nullable"));
        Assert.Equal("enable", FourSetBoundaryProbe.GetProperty(rootProps, "ImplicitUsings"));
        Assert.Equal("latest", FourSetBoundaryProbe.GetProperty(rootProps, "AnalysisLevel"));
        Assert.Equal("StarPie", FourSetBoundaryProbe.GetProperty(rootProps, "RootNamespace"));

        // TFM：Ui / Sdk.Wpf / 测试工程继承根 props；Sdk / Host 显式收窄为 net10.0（零 WPF 面）。
        foreach (string project in new[] { UiProject, SdkWpfProject, TestsProject })
        {
            Assert.Null(FourSetBoundaryProbe.GetProperty(FourSetBoundaryProbe.LoadProject(project), "TargetFramework"));
            Assert.Equal(RootTfm, FourSetBoundaryProbe.GetEffectiveProperty(project, "TargetFramework"));
        }
        foreach (string project in new[] { SdkProject, HostProject })
        {
            Assert.Equal("net10.0", FourSetBoundaryProbe.GetEffectiveProperty(project, "TargetFramework"));
        }

        foreach (string project in AllProjects)
        {
            // 共享属性禁止在工程级重复声明（防漂移）；测试工程的 RootNamespace 是登记在案的差异。
            XDocument csproj = FourSetBoundaryProbe.LoadProject(project);
            foreach (string shared in new[] { "Nullable", "ImplicitUsings", "AnalysisLevel" })
            {
                Assert.Null(FourSetBoundaryProbe.GetProperty(csproj, shared));
            }
            if (project != TestsProject)
            {
                Assert.Null(FourSetBoundaryProbe.GetProperty(csproj, "RootNamespace"));
            }

            // 中央包管理：csproj 的 PackageReference 不写版本。
            Assert.All(
                csproj.Descendants("PackageReference"),
                reference => Assert.Null(reference.Attribute("Version")));
        }
    }

    [Fact]
    public void Ui集_保持WinExe与程序集名StarPie_发布产物仍为StarPie()
    {
        XDocument csproj = FourSetBoundaryProbe.LoadProject(UiProject);
        Assert.Equal("WinExe", FourSetBoundaryProbe.GetProperty(csproj, "OutputType"));
        Assert.Equal("StarPie", FourSetBoundaryProbe.GetProperty(csproj, "AssemblyName"));
        Assert.True(FourSetBoundaryProbe.GetBoolProperty(csproj, "UseWPF"));

        // 编译产物级证据：程序集名 StarPie 且有托管入口点（exe）。
        System.Reflection.Assembly ui = typeof(App).Assembly;
        Assert.Equal("StarPie", ui.GetName().Name);
        Assert.NotNull(ui.EntryPoint);
        Assert.Equal("StarPie", Path.GetFileNameWithoutExtension(ui.Location));
        Assert.True(
            File.Exists(Path.Combine(AppContext.BaseDirectory, "StarPie.exe")),
            "StarPie.Ui 应产出 apphost StarPie.exe（发布路径见 CI 断言）");

        // 其余三集是类库（唯一可执行体是 Ui）。
        foreach (string project in new[] { SdkProject, SdkWpfProject, HostProject })
        {
            string? outputType = FourSetBoundaryProbe.GetProperty(FourSetBoundaryProbe.LoadProject(project), "OutputType");
            Assert.True(outputType is null || outputType == "Library", $"{project} 不应声明 {outputType}（四集唯一入口是 Ui）");
        }
    }

    [Fact]
    public void Sdk_net10非WindowsTFM_零WPF零第三方包零工程引用()
    {
        XDocument csproj = FourSetBoundaryProbe.LoadProject(SdkProject);
        Assert.Equal("net10.0", FourSetBoundaryProbe.GetEffectiveProperty(SdkProject, "TargetFramework"));
        Assert.False(FourSetBoundaryProbe.GetEffectiveBoolProperty(SdkProject, "UseWPF"));

        // 零第三方包：csproj 不允许任何包/框架引用；零 WPF 与零依赖：非 windows TFM + 无工程引用。
        Assert.Empty(csproj.Descendants("PackageReference"));
        Assert.Empty(csproj.Descendants("FrameworkReference"));
        Assert.Empty(FourSetBoundaryProbe.ProjectReferences(SdkProject));
    }

    [Fact]
    public void SdkWpf_是WPF契约面_UseWPF与windowsTFM()
    {
        Assert.True(FourSetBoundaryProbe.GetEffectiveBoolProperty(SdkWpfProject, "UseWPF"));
        Assert.StartsWith("net10.0-windows", FourSetBoundaryProbe.GetEffectiveProperty(SdkWpfProject, "TargetFramework"));
    }

    [Fact]
    public void Host_net10非WindowsTFM_零WPF()
    {
        Assert.Equal("net10.0", FourSetBoundaryProbe.GetEffectiveProperty(HostProject, "TargetFramework"));
        Assert.False(FourSetBoundaryProbe.GetEffectiveBoolProperty(HostProject, "UseWPF"));
    }

    [Fact]
    public void 跨集依赖方向_单向且Ui是唯一组合根()
    {
        // Ui → Host → Sdk、Ui → Sdk.Wpf → Sdk：Ui 必须显式引用三集，且只许引用已知工程。
        string[] uiReferences = FourSetBoundaryProbe.ProjectReferences(UiProject);
        Assert.Contains("StarPie.Sdk", uiReferences);
        Assert.Contains("StarPie.Sdk.Wpf", uiReferences);
        Assert.Contains("StarPie.Host", uiReferences);
        Assert.DoesNotContain("StarPie.Ui", uiReferences);
        Assert.All(uiReferences, name => Assert.Contains(name, FourSetBoundaryProbe.KnownProjectNames));
        Assert.All(uiReferences, name => Assert.DoesNotContain(name, FourSetBoundaryProbe.LegacyAssemblyNames));

        // Sdk 零依赖；Sdk.Wpf/Host 只可引用 Sdk；三集都不得反向引用 Ui。
        Assert.Empty(FourSetBoundaryProbe.ProjectReferences(SdkProject));
        foreach (string project in new[] { SdkWpfProject, HostProject })
        {
            string[] references = FourSetBoundaryProbe.ProjectReferences(project);
            Assert.All(references, name => Assert.Equal("StarPie.Sdk", name));
            Assert.DoesNotContain("StarPie.Ui", references);
        }

        // 已撤销旧集不在任何工程的引用面（测试工程同样只引用四集）。
        foreach (string project in AllProjects)
        {
            Assert.All(
                FourSetBoundaryProbe.ProjectReferences(project),
                name => Assert.DoesNotContain(name, FourSetBoundaryProbe.LegacyAssemblyNames));
        }
    }

    [Fact]
    public void CI发布与e2e定位_同步到StarPieUi工程()
    {
        string workflow = File.ReadAllText(Path.Combine(FourSetBoundaryProbe.RepoRoot, ".github", "workflows", "build-and-test.yml"));
        Assert.Contains("dotnet publish StarPie.Ui/StarPie.Ui.csproj", workflow);
        Assert.DoesNotContain("StarPie/StarPie.csproj", workflow);

        string conftest = File.ReadAllText(Path.Combine(FourSetBoundaryProbe.RepoRoot, "tests", "conftest.py"));
        Assert.Contains("\"StarPie.Ui\", \"bin\"", conftest);
        Assert.DoesNotContain("\"StarPie\", \"bin\"", conftest);
    }
}
