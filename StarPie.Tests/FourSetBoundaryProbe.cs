using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Xml.Linq;

namespace StarPie.Tests;

/// <summary>
/// 应用程序集边界断言（四集与 SDK 收口）的共享探针：仓库根定位、csproj 读取与
/// 程序集元数据读取。服务 <see cref="FourSetBoundaryTests"/>、
/// <see cref="RuntimeNoCrossReferenceTests"/> 与 <see cref="SdkBoundaryTests"/>；
/// 断言本体留在各测试文件中。
/// </summary>
internal static class FourSetBoundaryProbe
{
    /// <summary>四集程序集名（Ui 集程序集名保持 StarPie，发布产物 StarPie.exe）。</summary>
    internal static readonly string[] FourSetAssemblyNames =
    {
        "StarPie", "StarPie.Sdk", "StarPie.Sdk.Wpf", "StarPie.Host",
    };

    /// <summary>
    /// 旧集程序集名黑名单：这些名字不得出现在产物、解决方案或任何工程的引用面里。
    /// 本清单是这条约束的机械拦截面（XAML/入口同理：不存在即无从携带）。
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

    /// <summary>旧集的工程路径（相对仓库根）——解决方案登记面不得有它们。</summary>
    internal static readonly string[] LegacyProjectPaths = LegacyAssemblyNames
        .Select(name => name + "\\" + name + ".csproj")
        .ToArray();

    /// <summary>WPF 桌面程序集名（含 System.Xaml 这类 System.* 命名但属 WPF 栈者）。</summary>
    internal static readonly string[] WpfAssemblyNames =
    {
        "PresentationFramework", "PresentationCore", "WindowsBase", "System.Xaml",
        "System.Windows.Forms", "System.Windows.Extensions", "System.Drawing.Common", "ReachFramework",
    };

    /// <summary>
    /// Ui 集允许直接引用的全部工程名——白名单只有 SDK 面与宿主内核；
    /// 任何旧集引用（含测试工程与反向引用）都不在白名单内。
    /// </summary>
    internal static readonly string[] KnownProjectNames =
    {
        "StarPie.Sdk", "StarPie.Sdk.Wpf", "StarPie.Host",
    };

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

    /// <summary>仓库根统一构建属性文件（Directory.Build.props）。</summary>
    internal static XDocument LoadRootBuildProps()
        => XDocument.Load(Path.Combine(RepoRoot, "Directory.Build.props"));

    /// <summary>取 csproj 属性值（按 MSBuild 语义取最后一个同名属性；缺失返回 null）。</summary>
    internal static string? GetProperty(XDocument csproj, string name)
        => csproj.Descendants("PropertyGroup")
            .SelectMany(group => group.Elements())
            .Where(element => element.Name.LocalName == name)
            .Select(element => element.Value.Trim())
            .LastOrDefault();

    /// <summary>
    /// 取工程属性在 MSBuild 导入语义下的生效值：先读仓库根 Directory.Build.props，
    /// 再以 csproj 自身声明覆盖（工程级优先）——断言不因属性留在根 props 或下移到 csproj 而失真。
    /// </summary>
    internal static string? GetEffectiveProperty(string projectRelativePath, string name)
        => GetProperty(LoadProject(projectRelativePath), name)
            ?? GetProperty(LoadRootBuildProps(), name);

    /// <summary>取 csproj 布尔属性（缺失/非 true 均视为 false）。</summary>
    internal static bool GetBoolProperty(XDocument csproj, string name)
        => string.Equals(GetProperty(csproj, name), "true", StringComparison.OrdinalIgnoreCase);

    /// <summary>取工程布尔属性在 MSBuild 导入语义下的生效值（工程级覆盖根 props）。</summary>
    internal static bool GetEffectiveBoolProperty(string projectRelativePath, string name)
        => string.Equals(GetEffectiveProperty(projectRelativePath, name), "true", StringComparison.OrdinalIgnoreCase);

    /// <summary>csproj 直接声明的 ProjectReference 工程名（不含路径与扩展名）。</summary>
    internal static string[] ProjectReferences(string relativePath)
        => LoadProject(relativePath)
            .Descendants("ProjectReference")
            .Select(element => Path.GetFileNameWithoutExtension(((string)element.Attribute("Include")!).Replace('\\', '/')))
            .ToArray();

    /// <summary>按简单名加载应用程序集（四集或旧集；测试工程已显式引用，产物随输出目录就位）。</summary>
    internal static Assembly LoadAppAssembly(string simpleName)
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

    /// <summary>
    /// 程序集内的全部类型全名（含非导出类型），按序数序排序——“空壳检查”必须覆盖全部类型，
    /// 只看 public 面会把只塞内部类型的空壳放行。
    /// </summary>
    internal static string[] AllTypeNames(Assembly assembly)
        => assembly.GetTypes()
            .Select(type => type.FullName ?? type.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

    /// <summary>程序集导出（public）类型的全名，按序数序排序（收口/唯一性断言的比较基准）。</summary>
    internal static string[] ExportedTypeNames(Assembly assembly)
        => assembly.GetExportedTypes()
            .Select(type => type.FullName!)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

    /// <summary>
    /// 测试输出目录（应用产物所在）里全部应用 <c>StarPie*.dll</c> 的简单程序集名，
    /// 按序数序排序——产物级面：(a) 恰为四集、(b) 旧集文件不存在。
    /// 测试工程产物（<c>StarPie.Tests</c>，测试运行器本体，自带入口与 XAML 运行上下文）
    /// 不属于应用产物面，排除在外。
    /// </summary>
    internal static string[] AppAssembliesOnDisk()
        => Directory.EnumerateFiles(AppContext.BaseDirectory, "StarPie*.dll")
            .Select(path => Path.GetFileNameWithoutExtension(path))
            .Where(name => name != "StarPie.Tests")
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

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

    /// <summary>类型声明面（字段/属性/事件/方法与构造的参数与返回值）出现的全部类型。</summary>
    internal static Type[] SignatureTypes(Type type)
    {
        const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic
            | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;

        return type.GetFields(All).Select(field => field.FieldType)
            .Concat(type.GetProperties(All).Select(property => property.PropertyType))
            .Concat(type.GetEvents(All).Select(declaredEvent => declaredEvent.EventHandlerType!))
            .Concat(type.GetMethods(All).SelectMany(method => method.GetParameters()
                .Select(parameter => parameter.ParameterType)
                .Prepend(method.ReturnType)))
            .Concat(type.GetConstructors(All).SelectMany(ctor => ctor.GetParameters()
                .Select(parameter => parameter.ParameterType)))
            .ToArray();
    }

    /// <summary>递归判定类型（含数组/指针/泛型实参）是否来自 WPF 程序集。</summary>
    internal static bool TouchesWpf(Type type)
    {
        if (type.IsGenericParameter) return false;
        if (type.HasElementType) return TouchesWpf(type.GetElementType()!);
        if (WpfAssemblyNames.Contains(type.Assembly.GetName().Name)) return true;
        return type.IsGenericType && type.GetGenericArguments().Any(TouchesWpf);
    }

    /// <summary>
    /// 从根类型出发、沿声明面与基类型/接口可传递到达的**本仓**类型集合（含根自身），按序数序排序。
    /// </summary>
    /// <param name="roots">起点类型（可达面的入口）。</param>
    /// <remarks>
    /// 只展开四集里的类型：平台与 BCL 类型不是宿主提供给插件的能力面，展开它们既无意义也让集合发散。
    /// 「插件可达面」一类判定以此为基准——导出面决定哪些类型可被引用，本闭包决定插件实际能拿到什么。
    /// </remarks>
    internal static Type[] ReachableSignatureTypes(params Type[] roots)
    {
        var reached = new HashSet<Type>();
        var pending = new Queue<Type>();
        foreach (Type root in roots)
        {
            if (reached.Add(root))
            {
                pending.Enqueue(root);
            }
        }

        while (pending.Count > 0)
        {
            foreach (Type next in Edges(pending.Dequeue()))
            {
                if (reached.Add(next))
                {
                    pending.Enqueue(next);
                }
            }
        }

        return reached
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>一个类型的可达边：声明面 + 基类型 + 接口，拆到元素与泛型实参后只留四集类型。</summary>
    private static IEnumerable<Type> Edges(Type type)
    {
        IEnumerable<Type> edges = SignatureTypes(type);
        if (type.BaseType is not null)
        {
            edges = edges.Append(type.BaseType);
        }

        foreach (Type edge in edges.Concat(type.GetInterfaces()))
        {
            foreach (Type flat in Flatten(edge))
            {
                if (!flat.IsGenericParameter && FourSetAssemblyNames.Contains(flat.Assembly.GetName().Name))
                {
                    yield return flat;
                }
            }
        }
    }

    /// <summary>把数组/指针/泛型类型拆到元素类型与泛型实参。</summary>
    private static IEnumerable<Type> Flatten(Type type)
    {
        if (type.HasElementType)
        {
            foreach (Type inner in Flatten(type.GetElementType()!))
            {
                yield return inner;
            }

            yield break;
        }

        yield return type;
        if (!type.IsGenericType)
        {
            yield break;
        }

        foreach (Type argument in type.GetGenericArguments())
        {
            foreach (Type inner in Flatten(argument))
            {
                yield return inner;
            }
        }
    }
}
