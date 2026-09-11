using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Xml.Linq;

namespace StarPie.Tests;

/// <summary>
/// 四集边界断言（#111）的共享探针：仓库根定位、csproj 读取与程序集元数据读取。
/// 只服务 <see cref="FourSetBoundaryTests"/> 与 <see cref="RuntimeNoCrossReferenceTests"/>；
/// 断言本体留在两个测试文件中。
/// </summary>
internal static class FourSetBoundaryProbe
{
    /// <summary>四集程序集名（Ui 集程序集名保持 StarPie，发布产物 StarPie.exe）。</summary>
    internal static readonly string[] FourSetAssemblyNames =
    {
        "StarPie", "StarPie.Sdk", "StarPie.Sdk.Wpf", "StarPie.Host",
    };

    /// <summary>
    /// 旧 15 集中的业务/契约 runtime 程序集名：P1 归并期（P1.3–P1.10）只许 Ui 组合根引用，
    /// Sdk/Sdk.Wpf/Host 三集不得引用（跨集只经 SDK）。
    /// </summary>
    internal static readonly string[] LegacyAssemblyNames =
    {
        "StarPie.Core",
        "StarPie.Dialogs", "StarPie.Dialogs.Contracts",
        "StarPie.Gestures", "StarPie.Gestures.Contracts",
        "StarPie.Icons", "StarPie.Icons.Contracts",
        "StarPie.Programs", "StarPie.Programs.Contracts",
        "StarPie.Shell",
        "StarPie.Theme", "StarPie.Theme.Contracts",
        "StarPie.Wheel", "StarPie.Wheel.Contracts",
    };

    /// <summary>WPF 桌面程序集名（含 System.Xaml 这类 System.* 命名但属 WPF 栈者）。</summary>
    internal static readonly string[] WpfAssemblyNames =
    {
        "PresentationFramework", "PresentationCore", "WindowsBase", "System.Xaml",
        "System.Windows.Forms", "System.Windows.Extensions", "System.Drawing.Common", "ReachFramework",
    };

    /// <summary>Ui 集允许直接引用的全部工程名 = 旧 14 集 + 四集自身。测试工程与旧集反向引用不在其列。</summary>
    internal static readonly string[] KnownProjectNames =
        LegacyAssemblyNames.Concat(new[] { "StarPie.Sdk", "StarPie.Sdk.Wpf", "StarPie.Host" }).ToArray();

    /// <summary>从测试输出目录向上定位含 StarPie.slnx 的仓库根（测试不依赖当前工作目录）。</summary>
    internal static string RepoRoot { get; } = FindRepoRoot();

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "StarPie.slnx")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        throw new InvalidOperationException("未能从测试输出目录向上定位仓库根（找不到 StarPie.slnx）。");
    }

    /// <summary>读取仓库内 csproj（相对仓库根，Windows 分隔符）。</summary>
    internal static XDocument LoadProject(string relativePath)
        => XDocument.Load(Path.Combine(RepoRoot, relativePath.Replace('\\', Path.DirectorySeparatorChar)));

    /// <summary>取 csproj 属性值（按 MSBuild 语义取最后一个同名属性；缺失返回 null）。</summary>
    internal static string? GetProperty(XDocument csproj, string name)
        => csproj.Descendants("PropertyGroup")
            .SelectMany(group => group.Elements())
            .Where(element => element.Name.LocalName == name)
            .Select(element => element.Value.Trim())
            .LastOrDefault();

    /// <summary>取 csproj 布尔属性（缺失/非 true 均视为 false）。</summary>
    internal static bool GetBoolProperty(XDocument csproj, string name)
        => string.Equals(GetProperty(csproj, name), "true", StringComparison.OrdinalIgnoreCase);

    /// <summary>csproj 直接声明的 ProjectReference 工程名（不含路径与扩展名）。</summary>
    internal static string[] ProjectReferences(string relativePath)
        => LoadProject(relativePath)
            .Descendants("ProjectReference")
            .Select(element => Path.GetFileNameWithoutExtension(((string)element.Attribute("Include")!).Replace('\\', '/')))
            .ToArray();

    /// <summary>按简单名加载四集程序集（测试工程已显式引用四集，产物随输出目录就位）。</summary>
    internal static Assembly LoadFourSetAssembly(string simpleName)
    {
        Assembly? loaded = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(assembly => assembly.GetName().Name == simpleName);
        if (loaded is not null)
        {
            return loaded;
        }
        try
        {
            return Assembly.Load(simpleName);
        }
        catch (FileNotFoundException)
        {
            return Assembly.LoadFrom(Path.Combine(AppContext.BaseDirectory, simpleName + ".dll"));
        }
    }

    /// <summary>程序集元数据里的直接引用程序集名。</summary>
    internal static string[] ReferencedNames(Assembly assembly)
        => assembly.GetReferencedAssemblies().Select(reference => reference.Name!).ToArray();

    /// <summary>平台程序集判定：.NET 基类库/运行时自带的系统程序集（第三方包一律不在列）。</summary>
    internal static bool IsPlatformAssemblyName(string name)
        => name.StartsWith("System", StringComparison.Ordinal)
            || name is "netstandard" or "mscorlib" or "Microsoft.CSharp" or "Microsoft.VisualBasic";

    /// <summary>程序集内编译期 XAML（BAML）清单；无 .g.resources 或其中无 BAML 时为空。</summary>
    internal static string[] BamlEntries(Assembly assembly)
    {
        string? resourcesName = assembly.GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith(".g.resources", StringComparison.OrdinalIgnoreCase));
        if (resourcesName is null)
        {
            return Array.Empty<string>();
        }
        using Stream stream = assembly.GetManifestResourceStream(resourcesName)!;
        using var reader = new ResourceReader(stream);
        return reader.Cast<DictionaryEntry>()
            .Select(entry => (string)entry.Key)
            .Where(key => key.EndsWith(".baml", StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }
}
