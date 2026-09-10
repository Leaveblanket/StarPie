using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;

namespace P0.Host;

/// <summary>
/// P0 打样：collectible 插件 ALC。语义对齐 plugins.md §5 装载管线：
/// SDK/Sdk.Wpf/框架程序集一律回退默认 ALC；其余走 AssemblyDependencyResolver 私有加载。
/// </summary>
internal sealed class PluginLoadContext : AssemblyLoadContext
{
    /// <summary>共享契约集与框架程序集名单：这些名字一律返回 null（回退默认 ALC）。</summary>
    private static readonly HashSet<string> SharedAssemblies = new(StringComparer.OrdinalIgnoreCase)
    {
        "P0.Contracts",
        "WindowsBase",
        "PresentationCore",
        "PresentationFramework",
        "System.Xaml",
        "ReachFramework",
        "UIAutomationProvider",
        "UIAutomationTypes",
        "WindowsFormsIntegration",
        "mscorlib",
        "netstandard"
    };

    private readonly AssemblyDependencyResolver _resolver;

    public PluginLoadContext(string pluginPath)
        : base($"P0.Plugin:{Path.GetFileNameWithoutExtension(pluginPath)}:{Guid.NewGuid():N}", isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(pluginPath);
    }

    /// <summary>被本 ALC 私有加载的程序集（诊断用；注意：读取本属性会产生强引用）。</summary>
    public IReadOnlyList<string> LoadedPrivateAssemblies =>
        Assemblies.Select(a => a.GetName().Name ?? "?").ToArray();

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var name = assemblyName.Name ?? string.Empty;

        if (IsShared(name))
        {
            return null; // 回退默认 ALC —— 类型身份唯一（§5.1 约束 1）
        }

        var path = _resolver.ResolveAssemblyToPath(assemblyName);
        return path is null ? null : LoadFromAssemblyPath(path);
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return path is null ? IntPtr.Zero : LoadUnmanagedDllFromPath(path);
    }

    private static bool IsShared(string name) =>
        SharedAssemblies.Contains(name)
        || name.StartsWith("System.", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase);
}
