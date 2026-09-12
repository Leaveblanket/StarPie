using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using StarPie.Abstractions;
using StarPie.HostServices;
using StarPie.Manifest;
using StarPie.PluginRuntime.Admission;
using StarPie.PluginRuntime.Lifecycle;
using StarPie.PluginRuntime.Loading;
using StarPie.PluginRuntime.Manifest;
using StarPie.PluginRuntime.Registry;
using StarPie.Services.Messages;
using StarPie.Services.Programs;

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
        var pipeline = new PluginLoadPipeline(new CapabilityRegistry());

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

        PluginLoadResult result = await new PluginLoadPipeline(new CapabilityRegistry())
            .LoadAsync(request, cancellation.Token);

        Assert.Equal(PluginLoadStatus.Quarantined, result.Status);
        Assert.Contains("被取消", result.FailureReason);
        Assert.Null(result.Plugin);
        Assert.NotNull(result.LoadContext);
        Assert.Equal(PluginLifecycleState.Starting, result.Lifecycle.Transitions[^1].From);
    }

    [Fact]
    public async Task 插件启动期注册能力_活动态后消费者经守卫取用()
    {
        CapabilityRegistry registry = PluginCapabilityTestDoubles.CreateProgramSourceRegistry();
        PluginLoadRequest request = CreateRequest(
            PluginId,
            typeof(ProgramSourceTestPlugin),
            capabilitiesJson: """[{ "id": "program-source", "abi": 1 }]""");

        PluginLoadResult result = await new PluginLoadPipeline(registry)
            .LoadAsync(request, CancellationToken.None);

        Assert.Equal(PluginLoadStatus.Active, result.Status);
        Assert.NotNull(result.Scope);
        Assert.Equal(PluginId, result.Scope!.PluginId);
        Assert.Equal(0, result.Scope.HandleCount);

        // 消费者短租用：每次取用都是新的守卫适配器，实例留在插件作用域内。
        IProgramScanner scanner = Assert.Single(registry.GetAll<IProgramScanner>());
        ProgramEntry entry = Assert.Single(scanner.ScanInstalledPrograms());
        Assert.Equal(PluginId, entry.Name);
        Assert.Equal(0, result.Scope.Guard.InFlightCount);
    }

    [Fact]
    public async Task 清单priority_决定插件能力在表中的顺序()
    {
        CapabilityRegistry registry = PluginCapabilityTestDoubles.CreateProgramSourceRegistry();
        var pipeline = new PluginLoadPipeline(registry);
        const string capabilities = """[{ "id": "program-source", "abi": 1 }]""";

        // 注册顺序与最终顺序相反：priority 小者靠前（内置为空时即插件之间排序）。
        PluginLoadResult late = await pipeline.LoadAsync(
            CreateRequest(
                "com.example.late",
                typeof(ProgramSourceTestPlugin),
                capabilitiesJson: capabilities,
                priority: 5),
            CancellationToken.None);
        PluginLoadResult early = await pipeline.LoadAsync(
            CreateRequest(
                "com.example.early",
                typeof(ProgramSourceTestPlugin),
                capabilitiesJson: capabilities,
                priority: -1),
            CancellationToken.None);

        Assert.Equal(PluginLoadStatus.Active, late.Status);
        Assert.Equal(PluginLoadStatus.Active, early.Status);
        string[] order = registry.GetAll<IProgramScanner>()
            .Select(scanner => Assert.Single(scanner.ScanInstalledPrograms()).Name)
            .ToArray();
        Assert.Equal(new[] { "com.example.early", "com.example.late" }, order);
    }

    [Fact]
    public async Task 插件自定义异常经日志后不root插件集()
    {
        var sink = new RecordingPluginLogSink();
        CapabilityRegistry registry = PluginCapabilityTestDoubles.CreateProgramSourceRegistry();
        PluginLoadRequest request = CreateRequest(
            PluginId,
            typeof(CustomExceptionProgramSourceTestPlugin),
            capabilitiesJson: """[{ "id": "program-source", "abi": 1 }]""");
        PluginLoadResult result = await new PluginLoadPipeline(registry, sink)
            .LoadAsync(request, CancellationToken.None);
        IProgramScanner scanner = Assert.Single(registry.GetAll<IProgramScanner>());

        WeakReference probe = CapturePluginExceptionWeakReference(scanner);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.False(probe.IsAlive, "插件自定义异常实例不得被日志/守卫 root");
        PluginLogEntry entry = Assert.Single(sink.Entries);
        Assert.Contains(nameof(PluginCustomException), entry.ExceptionType);
        Assert.Equal("插件自定义异常", entry.ExceptionMessage);
    }

    /// <summary>
    /// 在独立方法内调用并留存异常弱引用：异常类型来自插件 ALC 的 StarPie.Tests 副本，
    /// 断言宿主日志与守卫没有把该实例留成长生命周期引用。
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CapturePluginExceptionWeakReference(IProgramScanner scanner)
    {
        Exception thrown = Record.Exception(() => scanner.ScanInstalledPrograms())!;
        Assert.NotNull(thrown);
        Assert.Equal(nameof(PluginCustomException), thrown.GetType().Name);
        Assert.NotSame(typeof(PluginCustomException).Assembly, thrown.GetType().Assembly);
        return new WeakReference(thrown);
    }

    [Fact]
    public async Task 插件能力连续失败_熔断隔离_能力表条目隐藏()
    {
        CapabilityRegistry registry = PluginCapabilityTestDoubles.CreateProgramSourceRegistry();
        PluginLoadRequest request = CreateRequest(
            PluginId,
            typeof(ThrowingProgramSourceTestPlugin),
            capabilitiesJson: """[{ "id": "program-source", "abi": 1 }]""");
        PluginLoadResult result = await new PluginLoadPipeline(registry)
            .LoadAsync(request, CancellationToken.None);
        IProgramScanner scanner = Assert.Single(registry.GetAll<IProgramScanner>());

        // 默认阈值：连续 3 次失败熔断。
        Assert.Throws<InvalidOperationException>(() => scanner.ScanInstalledPrograms());
        Assert.Throws<InvalidOperationException>(() => scanner.ScanInstalledPrograms());
        Assert.Throws<InvalidOperationException>(() => scanner.ScanInstalledPrograms());

        Assert.Equal(PluginLifecycleState.Quarantined, result.Lifecycle.Current);
        Assert.True(result.Scope!.Guard.IsCircuitOpen);
        Assert.Empty(registry.GetAll<IProgramScanner>());
    }

    [Fact]
    public async Task 事件订阅_作用域记账_释放后强制断订阅()
    {
        var registry = new CapabilityRegistry();
        PluginLoadRequest request = CreateRequest(PluginId, typeof(EventSubscriberTestPlugin));
        PluginLoadResult result = await new PluginLoadPipeline(registry)
            .LoadAsync(request, CancellationToken.None);

        Assert.Equal(PluginLoadStatus.Active, result.Status);
        Assert.Equal(1, result.Scope!.HandleCount);
        Assert.Equal(0, ReadPluginStaticInt(result, "EventSubscriberTestPlugin", "ReceivedCount"));

        result.Scope.Publish(MinimizedToTrayMessage.Instance);
        Assert.Equal(1, ReadPluginStaticInt(result, "EventSubscriberTestPlugin", "ReceivedCount"));

        // 释放作用域 = 强制枚举清理订阅：账本清零，之后不再投递。
        result.Scope.Dispose();
        Assert.True(result.Scope.IsDisposed);
        Assert.Equal(0, result.Scope.HandleCount);
        result.Scope.Publish(MinimizedToTrayMessage.Instance);
        Assert.Equal(1, ReadPluginStaticInt(result, "EventSubscriberTestPlugin", "ReceivedCount"));
    }

    [Fact]
    public async Task 启动失败_作用域不流出_能力表无残留()
    {
        CapabilityRegistry registry = PluginCapabilityTestDoubles.CreateProgramSourceRegistry();
        PluginLoadRequest request = CreateRequest(
            PluginId,
            typeof(ThrowingTestPlugin),
            capabilitiesJson: """[{ "id": "program-source", "abi": 1 }]""");

        PluginLoadResult result = await new PluginLoadPipeline(registry)
            .LoadAsync(request, CancellationToken.None);

        Assert.Equal(PluginLoadStatus.Quarantined, result.Status);
        Assert.Null(result.Scope);
        Assert.Empty(registry.GetAll<IProgramScanner>());
    }

    /// <summary>读取插件 ALC 内程序集类型的静态计数（宿主与插件各持一份 StarPie.Tests 副本）。</summary>
    private static int ReadPluginStaticInt(
        PluginLoadResult result,
        string typeName,
        string fieldName)
    {
        Assembly entryAssembly = result.LoadContext!.Assemblies
            .Single(assembly => assembly.GetName().Name == "StarPie.Tests");
        Type entryType = entryAssembly.GetType($"StarPie.Tests.{typeName}", throwOnError: true)!;
        FieldInfo counter = entryType.GetField(
            fieldName,
            BindingFlags.Public | BindingFlags.Static)!;
        return (int)counter.GetValue(null)!;
    }

    private static Task<PluginLoadResult> LoadAsync(PluginLoadRequest request)
        => new PluginLoadPipeline(new CapabilityRegistry()).LoadAsync(request, CancellationToken.None);

    private PluginLoadRequest CreateRequest(
        string pluginId,
        Type entryType,
        string? entryTypeName = null,
        bool copyPrivateDependency = false,
        string? capabilitiesJson = null,
        int priority = 0)
    {
        string packageDirectory = Path.Combine(_pluginsRoot, pluginId);
        Directory.CreateDirectory(packageDirectory);

        string entryAssemblyName = Path.GetFileName(typeof(RecordingTestPlugin).Assembly.Location);
        string manifestJson = PluginTestPackage.Manifest(
            pluginId,
            entryAssembly: entryAssemblyName,
            entryType: entryTypeName ?? entryType.FullName!,
            priority: priority,
            capabilitiesJson: capabilitiesJson);
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
