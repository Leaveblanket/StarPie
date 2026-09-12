using System;
using System.IO;
using System.Reflection;
using StarPie.Compatibility;
using StarPie.Plugins;
using StarPie.PluginRuntime.Loading;

namespace StarPie.Tests;

/// <summary>
/// 装载上下文缝：共享契约与框架程序集回退默认 ALC（类型身份唯一），包内私有依赖经
/// <see cref="System.Runtime.Loader.AssemblyDependencyResolver"/> 私有加载；判定常量与
/// Sdk.Wpf 的装载政策一致。
/// </summary>
public sealed class PluginLoadContextTests : IDisposable
{
    private const string PluginId = "com.example.alc";

    private readonly string _tempRoot;
    private readonly string _packageDirectory;
    private readonly string _entryAssemblyPath;

    public PluginLoadContextTests()
    {
        _tempRoot = Directory.CreateTempSubdirectory("starpie-plugin-alc-tests").FullName;
        _packageDirectory = Path.Combine(_tempRoot, PluginId);
        Directory.CreateDirectory(_packageDirectory);
        _entryAssemblyPath = Path.Combine(_packageDirectory, "StarPie.Tests.dll");
        File.Copy(typeof(PluginLoadContextTests).Assembly.Location, _entryAssemblyPath);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempRoot, recursive: true); } catch { }
    }

    [Fact]
    public void 装载上下文_以插件id命名且可回收()
    {
        var context = new PluginLoadContext(PluginId, _entryAssemblyPath);

        Assert.True(context.IsCollectible);
        Assert.Equal("StarPie.Plugin." + PluginId, context.Name);
    }

    [Fact]
    public void 共享程序集判定_只含SDK共享契约与框架程序集()
    {
        // 与 Sdk.Wpf 装载政策的两个共享契约名逐一对齐。
        Assert.True(PluginSharedAssemblyPolicy.MustResolveFromDefaultAlc(DefaultAlcPolicy.SdkAssemblyName));
        Assert.True(PluginSharedAssemblyPolicy.MustResolveFromDefaultAlc(DefaultAlcPolicy.SdkWpfAssemblyName));

        Assert.True(PluginSharedAssemblyPolicy.MustResolveFromDefaultAlc("System.Runtime"));
        Assert.True(PluginSharedAssemblyPolicy.MustResolveFromDefaultAlc("System.Private.CoreLib"));
        Assert.True(PluginSharedAssemblyPolicy.MustResolveFromDefaultAlc("netstandard"));
        Assert.True(PluginSharedAssemblyPolicy.MustResolveFromDefaultAlc("mscorlib"));
        Assert.True(PluginSharedAssemblyPolicy.MustResolveFromDefaultAlc("WindowsBase"));
        Assert.True(PluginSharedAssemblyPolicy.MustResolveFromDefaultAlc("PresentationFramework"));

        Assert.False(PluginSharedAssemblyPolicy.MustResolveFromDefaultAlc("CommunityToolkit.Mvvm"));
        Assert.False(PluginSharedAssemblyPolicy.MustResolveFromDefaultAlc("Example.Plugin"));
        Assert.False(PluginSharedAssemblyPolicy.MustResolveFromDefaultAlc(null));
        Assert.False(PluginSharedAssemblyPolicy.MustResolveFromDefaultAlc(string.Empty));
    }

    [Fact]
    public void 共享契约与框架_回退默认ALC取同一实例()
    {
        var context = new PluginLoadContext(PluginId, _entryAssemblyPath);

        Assert.Same(typeof(IPlugin).Assembly, context.LoadFromAssemblyName(new AssemblyName("StarPie.Sdk")));
        Assert.Same(Assembly.Load("System.Runtime"), context.LoadFromAssemblyName(new AssemblyName("System.Runtime")));
    }

    [Fact]
    public void 包内出现共享契约副本_仍由默认ALC解析()
    {
        // 包内容违规由发现期拦下；此处的回退政策是第二道闸：即使副本在包内也不得私有加载。
        File.Copy(typeof(IPlugin).Assembly.Location, Path.Combine(_packageDirectory, "StarPie.Sdk.dll"));
        var context = new PluginLoadContext(PluginId, _entryAssemblyPath);

        Assert.Same(typeof(IPlugin).Assembly, context.LoadFromAssemblyName(new AssemblyName("StarPie.Sdk")));
    }

    [Fact]
    public void 包内私有依赖_私有加载_不与宿主同名程序集共享()
    {
        string privateDependencyName = "CommunityToolkit.Mvvm.dll";
        File.Copy(
            Path.Combine(AppContext.BaseDirectory, privateDependencyName),
            Path.Combine(_packageDirectory, privateDependencyName));
        var context = new PluginLoadContext(PluginId, _entryAssemblyPath);

        Assembly privateCopy = context.LoadFromAssemblyName(new AssemblyName("CommunityToolkit.Mvvm"));

        Assert.NotSame(Assembly.Load("CommunityToolkit.Mvvm"), privateCopy);
        Assert.Equal(_packageDirectory, Path.GetDirectoryName(privateCopy.Location));
    }
}
