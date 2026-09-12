using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using StarPie.Abstractions;
using StarPie.Manifest;
using StarPie.PluginRuntime.Admission;
using StarPie.PluginRuntime.Lifecycle;
using StarPie.PluginRuntime.Loading;
using StarPie.PluginRuntime.Manifest;

namespace StarPie.Tests;

/// <summary>
/// 装载管线缝：校验 → 建 collectible ALC → 载入口程序集并实例化 → 启动；成功进入活动态，
/// 失败与拒绝分别进入隔离与拒绝路径（不创建 ALC），入口类型不进入宿主长生命周期结构。
/// </summary>
public sealed class PluginLoadPipelineTests : IDisposable
{
    private const string PluginId = "com.example.recording";

    private readonly string _tempRoot;
    private readonly string _pluginsRoot;

    public PluginLoadPipelineTests()
    {
        _tempRoot = Directory.CreateTempSubdirectory("starpie-plugin-load-tests").FullName;
        _pluginsRoot = Path.Combine(_tempRoot, "plugins");
        Directory.CreateDirectory(_pluginsRoot);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempRoot, recursive: true); } catch { }
    }

    [Fact]
    public async Task 装载成功_状态机完整转移至活动_入口实例可用()
    {
        PluginLoadRequest request = CreateRequest(PluginId, typeof(RecordingTestPlugin));

        PluginLoadResult result = await LoadAsync(request);

        Assert.Equal(PluginLoadStatus.Active, result.Status);
        Assert.Null(result.FailureReason);
        Assert.Equal(PluginId, result.PluginId);
        Assert.NotNull(result.Plugin);
        Assert.NotNull(result.LoadContext);
        Assert.True(result.LoadContext!.IsCollectible);
        Assert.Equal(PluginLifecycleState.Active, result.Lifecycle.Current);
        Assert.Equal(
            new[]
            {
                PluginLifecycleState.Validated,
                PluginLifecycleState.Loading,
                PluginLifecycleState.Starting,
                PluginLifecycleState.Active,
            },
            result.Lifecycle.Transitions.Select(transition => transition.To));

        // 宿主把清单 id 经 IPluginContext 交给插件。
        string? observed = (string?)result.Plugin!
            .GetType()
            .GetProperty(nameof(RecordingTestPlugin.ObservedPluginId))!
            .GetValue(result.Plugin);
        Assert.Equal(PluginId, observed);
    }

    [Fact]
    public async Task 跨ALC只共享SDK与框架程序集_私有依赖随包私有加载()
    {
        PluginLoadRequest request = CreateRequest(
            PluginId,
            typeof(RecordingTestPlugin),
            copyPrivateDependency: true);

        PluginLoadResult result = await LoadAsync(request);
        PluginLoadContext context = result.LoadContext!;

        // 入口程序集是 ALC 私有副本：与测试进程内的同名程序集不是同一份。
        Assert.NotSame(typeof(RecordingTestPlugin).Assembly, result.Plugin!.GetType().Assembly);

        // 共享契约与框架程序集：与默认 ALC 同一实例（跨 ALC 类型身份唯一）。
        Assert.Same(typeof(IPlugin).Assembly, context.LoadFromAssemblyName(new AssemblyName("StarPie.Sdk")));
        Assert.Same(Assembly.Load("System.Runtime"), context.LoadFromAssemblyName(new AssemblyName("System.Runtime")));

        // 包内私有依赖：从包目录私有加载，与宿主默认 ALC 的同名程序集不共享。
        Assembly privateDependency = context.LoadFromAssemblyName(new AssemblyName("CommunityToolkit.Mvvm"));
        Assert.Equal(request.PackageDirectory, Path.GetDirectoryName(privateDependency.Location));
        Assert.NotSame(Assembly.Load("CommunityToolkit.Mvvm"), privateDependency);
    }

    [Fact]
    public async Task 装载期不缓存入口Type_两次装载各自解析()
    {
        PluginLoadRequest request = CreateRequest(PluginId, typeof(RecordingTestPlugin));
        var pipeline = new PluginLoadPipeline();

        PluginLoadResult first = await pipeline.LoadAsync(request, CancellationToken.None);
        PluginLoadResult second = await pipeline.LoadAsync(request, CancellationToken.None);

        Assert.NotSame(first.Plugin!.GetType(), second.Plugin!.GetType());
        Assert.NotSame(first.LoadContext, second.LoadContext);
    }

    [Fact]
    public void 装载管线与结果_声明面不含Type类型成员()
    {
        var types = new[]
        {
            typeof(PluginLoadPipeline),
            typeof(PluginLoadRequest),
            typeof(PluginLoadResult),
            typeof(PluginLoadContext),
            typeof(PluginLifecycleStateMachine),
        };

        foreach (Type type in types)
        {
            Assert.DoesNotContain(
                DeclaredMemberTypes(type),
                memberType => ContainsTypeOfType(memberType));
        }

        // 非空壳：结果声明面仍被真实扫描到（过滤编译器生成成员后不得成为空集合）。
        Assert.Contains(typeof(IPlugin), DeclaredMemberTypes(typeof(PluginLoadResult)));
        Assert.Contains(typeof(PluginLoadContext), DeclaredMemberTypes(typeof(PluginLoadResult)));
    }

    [Fact]
    public async Task 入口类型未找到_隔离且不保留插件对象()
    {
        PluginLoadRequest request = CreateRequest(
            PluginId,
            typeof(RecordingTestPlugin),
            entryTypeName: "StarPie.Tests.NoSuchTestPlugin");

        PluginLoadResult result = await LoadAsync(request);

        Assert.Equal(PluginLoadStatus.Quarantined, result.Status);
        Assert.Contains("入口类型未找到", result.FailureReason);
        Assert.Null(result.Plugin);
        Assert.NotNull(result.LoadContext);
        Assert.Equal(PluginLifecycleState.Quarantined, result.Lifecycle.Current);
        Assert.Equal(PluginLifecycleState.Loading, result.Lifecycle.Transitions[^1].From);
        Assert.Equal(PluginLifecycleState.Quarantined, result.Lifecycle.Transitions[^1].To);
        Assert.Equal(result.FailureReason, result.Lifecycle.QuarantineReason);
    }

    [Fact]
    public async Task 入口类型未实现IPlugin_隔离()
    {
        PluginLoadRequest request = CreateRequest(PluginId, typeof(NotAnEntryTestPlugin));

        PluginLoadResult result = await LoadAsync(request);

        Assert.Equal(PluginLoadStatus.Quarantined, result.Status);
        Assert.Contains("未实现 IPlugin", result.FailureReason);
        Assert.Null(result.Plugin);
        Assert.Equal(PluginLifecycleState.Quarantined, result.Lifecycle.Current);
    }

    [Fact]
    public async Task 启动抛异常_隔离且原因含插件异常信息()
    {
        PluginLoadRequest request = CreateRequest(PluginId, typeof(ThrowingTestPlugin));

        PluginLoadResult result = await LoadAsync(request);

        Assert.Equal(PluginLoadStatus.Quarantined, result.Status);
        Assert.Contains("夹具启动爆炸", result.FailureReason);
        Assert.Null(result.Plugin);
        Assert.Equal(PluginLifecycleState.Starting, result.Lifecycle.Transitions[^1].From);
        Assert.Equal(PluginLifecycleState.Quarantined, result.Lifecycle.Transitions[^1].To);
    }

    [Fact]
    public async Task 准入拒绝_不创建ALC_状态停在已发现()
    {
        PluginLoadRequest request = CreateRequest(PluginId, typeof(RecordingTestPlugin)) with
        {
            Admission = new PluginAdmissionDecision(PluginAdmission.Rejected, "拒绝：未命中审核清单"),
        };

        PluginLoadResult result = await LoadAsync(request);

        Assert.Equal(PluginLoadStatus.Rejected, result.Status);
        Assert.Contains("未命中审核清单", result.FailureReason);
        Assert.Null(result.Plugin);
        Assert.Null(result.LoadContext);
        Assert.Equal(PluginLifecycleState.Discovered, result.Lifecycle.Current);
        Assert.Empty(result.Lifecycle.Transitions);
    }

    [Fact]
    public async Task 装载前重校验不通过_拒绝且不创建ALC()
    {
        PluginLoadRequest request = CreateRequest(PluginId, typeof(RecordingTestPlugin));
        File.Delete(Path.Combine(request.PackageDirectory, "StarPie.Tests.dll"));

        PluginLoadResult result = await LoadAsync(request);

        Assert.Equal(PluginLoadStatus.Rejected, result.Status);
        Assert.Contains("entryAssembly 文件不存在于包内", result.FailureReason);
        Assert.Null(result.Plugin);
        Assert.Null(result.LoadContext);
        Assert.Equal(PluginLifecycleState.Discovered, result.Lifecycle.Current);
    }

    [Fact]
    public async Task 启动被取消_按中止隔离且不保留半启动实例()
    {
        PluginLoadRequest request = CreateRequest(PluginId, typeof(TokenAwareTestPlugin));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        PluginLoadResult result = await new PluginLoadPipeline().LoadAsync(request, cancellation.Token);

        Assert.Equal(PluginLoadStatus.Quarantined, result.Status);
        Assert.Contains("被取消", result.FailureReason);
        Assert.Null(result.Plugin);
        Assert.NotNull(result.LoadContext);
        Assert.Equal(PluginLifecycleState.Starting, result.Lifecycle.Transitions[^1].From);
    }

    private static Task<PluginLoadResult> LoadAsync(PluginLoadRequest request)
        => new PluginLoadPipeline().LoadAsync(request, CancellationToken.None);

    private PluginLoadRequest CreateRequest(
        string pluginId,
        Type entryType,
        string? entryTypeName = null,
        bool copyPrivateDependency = false)
    {
        string packageDirectory = Path.Combine(_pluginsRoot, pluginId);
        Directory.CreateDirectory(packageDirectory);

        string entryAssemblyName = Path.GetFileName(typeof(RecordingTestPlugin).Assembly.Location);
        string manifestJson = PluginTestPackage.Manifest(
            pluginId,
            entryAssembly: entryAssemblyName,
            entryType: entryTypeName ?? entryType.FullName!);
        File.WriteAllText(Path.Combine(packageDirectory, "plugin.json"), manifestJson);
        File.Copy(
            typeof(RecordingTestPlugin).Assembly.Location,
            Path.Combine(packageDirectory, entryAssemblyName),
            overwrite: true);

        if (copyPrivateDependency)
        {
            const string privateDependencyName = "CommunityToolkit.Mvvm.dll";
            File.Copy(
                Path.Combine(AppContext.BaseDirectory, privateDependencyName),
                Path.Combine(packageDirectory, privateDependencyName));
        }

        PluginManifest manifest = PluginManifestParser.Parse(manifestJson).Manifest!;
        return new PluginLoadRequest(
            manifest,
            packageDirectory,
            new PluginAdmissionDecision(PluginAdmission.DeveloperMode, "开发者模式：未命中审核清单，经开发者模式放行"));
    }

    private static IEnumerable<Type> DeclaredMemberTypes(Type type)
    {
        const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic
            | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        // 编译器为 record 生成的成员（如 EqualityContract）不属于手写声明面，过滤后再核对。
        return type.GetFields(Declared).Where(IsDeclared).Select(field => field.FieldType)
            .Concat(type.GetProperties(Declared).Where(IsDeclared).Select(property => property.PropertyType))
            .Concat(type.GetEvents(Declared).Where(IsDeclared).Select(declaredEvent => declaredEvent.EventHandlerType!))
            .Concat(type.GetMethods(Declared).Where(IsDeclared)
                .SelectMany(method => method.GetParameters()
                .Select(parameter => parameter.ParameterType)
                .Append(method.ReturnType)))
            .Concat(type.GetConstructors(Declared).Where(IsDeclared).SelectMany(ctor => ctor.GetParameters()
                .Select(parameter => parameter.ParameterType)));
    }

    private static bool IsDeclared(MemberInfo member)
        => !member.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), inherit: false);

    private static bool ContainsTypeOfType(Type type)
        => type == typeof(Type)
            || (type.HasElementType && ContainsTypeOfType(type.GetElementType()!))
            || (type.IsGenericType && type.GetGenericArguments().Any(ContainsTypeOfType));
}
