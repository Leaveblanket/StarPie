using System;
using System.IO;
using System.Linq;
using System.Reflection;
using StarPie.Kernel.Configuration;
using StarPie.Kernel.Localization;
using StarPie.HostServices;
using StarPie.PluginRuntime;
using StarPie.PluginRuntime.Admission;
using StarPie.PluginRuntime.Diagnostics;
using StarPie.PluginRuntime.Discovery;
using StarPie.PluginRuntime.Lifecycle;
using StarPie.PluginRuntime.Loading;
using StarPie.PluginRuntime.Manifest;
using StarPie.PluginRuntime.Registry;
using StarPie.PluginRuntime.State;
using StarPie.PluginRuntime.Unloading;
using StarPie.PluginRuntime.Hosting;
using StarPie.PluginRuntime.Ui;

namespace StarPie.Tests;

/// <summary>
/// 宿主内核边界基线：内核运行时（配置读写/防抖落盘接缝/本地化实现）归 <see cref="StarPie.Host"/>——
/// 导出面 = 恰为内核清单（新增 public 类型须同步本表），导出面签名不触碰 WPF 与旧集 runtime，
/// 核心件在无 WPF 依赖的程序集里可直接构造；设计期投影字典随 Ui 集编译（唯一的资源锚指向它），
/// 独立的设计期投影壳 <c>StarPie.Core</c> 已删除。与 <see cref="FourSetBoundaryTests"/>（工程面）、
/// <see cref="RuntimeNoCrossReferenceTests"/>（引用面）、<see cref="SdkBoundaryTests"/>（SDK 导出面）
/// 互补。
/// </summary>
public sealed class HostBoundaryTests
{
    /// <summary>StarPie.Host 的全部导出类型（导出面 = 恰为该清单）。</summary>
    private static readonly Type[] KernelTypes =
    {
        // Kernel/Configuration/
        typeof(IConfigService), typeof(JsonConfigService), typeof(ISaveDebouncer),
        typeof(AppDataPaths), typeof(SettingsSaveOrchestrator),
        // Kernel/Localization/
        typeof(ILocalizationService), typeof(LocalizationService),
        // Kernel/ShellIntegration/（开机自启注册表与内存整理，纯托管 + P/Invoke）
        typeof(AutostartRegistry), typeof(MemoryOptimizer),
        // Icons/（资产目录与自定义图标存储）
        typeof(IconCatalog), typeof(CustomIconStore),
        // Programs/（内置程序来源、程序来源能力契约/聚合与 .lnk 解析）
        typeof(ProgramScanner), typeof(ShortcutResolver), typeof(ProgramSourceCapability),
        typeof(ProgramSourceAggregator),
        // Themes/（界面主题引擎）与 Ports/（宿主→Ui 端口：主题应用）
        typeof(ThemeEngine), typeof(IThemeApplier),
        // Wheel/（轮盘配色目录与色值解析，WPF-free）
        typeof(WheelPalette), typeof(WheelPaletteCatalog), typeof(WheelPaletteParser),
        // Gestures/（手势内核：状态机/释放语义/窗口上下文接缝）与 Actions/（动作路由纯函数）
        typeof(GestureEngine), typeof(GestureState), typeof(GestureReleaseResult),
        typeof(IWindowContext), typeof(WindowContext), typeof(GestureModifierKeys),
        typeof(ActionRouting), typeof(ActionRoute), typeof(KeyStroke),
        typeof(ActionRouting.SystemCommand), typeof(ActionRouting.SystemCommand.Noop),
        typeof(ActionRouting.SystemCommand.SendHotkey), typeof(ActionRouting.SystemCommand.SendKey),
        typeof(ActionRouting.SystemCommand.LockWorkstation), typeof(ActionRouting.SystemCommand.StartProcess),
        // PluginRuntime/（插件运行时首层：路径、发现、清单校验、准入、宿主状态与启动报告）
        typeof(PluginPaths),
        typeof(PluginPackageOrigin), typeof(PluginPackageCandidate), typeof(PluginDiscovery),
        typeof(PluginManifestParseResult), typeof(PluginManifestParser), typeof(PluginManifestValidator),
        typeof(PluginAdmission), typeof(PluginAdmissionDecision), typeof(IPluginReviewCatalog),
        typeof(EmptyPluginReviewCatalog), typeof(PluginAdmissionPolicy), typeof(PluginDeveloperModeService),
        typeof(PluginQuarantineState), typeof(PluginStateEntry), typeof(PluginStateDocument), typeof(PluginStateStore),
        typeof(PluginStartupReportEntry), typeof(PluginStartupReport),
        typeof(PluginStartupReportWriter), typeof(PluginStartupScanner),
        // PluginRuntime/Diagnostics/（残留清单与插件诊断报告）
        typeof(PluginResidualKind), typeof(PluginResidual), typeof(PluginRuntimeStatus),
        typeof(PluginDiagnosticsReport),
        // PluginRuntime/Loading/（collectible ALC 与装载管线）与 Lifecycle/（生命周期状态机）
        typeof(PluginSharedAssemblyPolicy), typeof(PluginLoadContext), typeof(PluginLoadRequest),
        typeof(PluginLoadStatus), typeof(PluginLoadResult), typeof(PluginLoadPipeline),
        typeof(PluginLifecycleState), typeof(PluginLifecycleTransition),
        typeof(PluginLifecycleStateMachine),
        // PluginRuntime/Registry/（能力表与调用守卫）与 HostServices/（插件可见服务作用域）
        typeof(CapabilityContract), typeof(CapabilityRegistry), typeof(CapabilityGuard),
        typeof(CapabilityGuardOptions), typeof(CapabilityUnavailableException),
        typeof(CapabilityCircuitOpenException), typeof(CapabilityCallTimeoutException),
        typeof(PluginServiceScope), typeof(IPluginLogSink), typeof(PluginLogEntry),
        // PluginRuntime/Unloading/（安全点卸载管线与回收判定）
        typeof(PluginUnloadStatus), typeof(PluginUnloadRequest), typeof(PluginUnloadResult),
        typeof(PluginUnloadPipeline), typeof(PluginReclaimPolicy),
        // PluginRuntime/Hosting/（宿主侧插件运行时：启动装载与停用/再启用/重载/更新/彻底移除）
        typeof(PluginRuntimeHost), typeof(PluginUninstallResult), typeof(PluginUninstallOptions),
        // PluginRuntime/Ui/（UI 托管端口：装载期 RegisterUi 调度与卸载期释放编排的纯数据面；
        // Host 零 WPF，实做封送在 Ui 层）
        typeof(IPluginUiCoordinator), typeof(PluginUiAttachRequest),
        typeof(PluginUiAttachResult), typeof(PluginUiReleaseResult),
    };

    /// <summary>设计期字符串字典的 pack URI（Page 编译、签入生成物；由 Ui 工程资源锚设计期合并）。</summary>
    private const string DesignTimeDictionaryPackUri =
        "pack://application:,,,/StarPie;component/Services/Localization/DesignTimeStrings.xaml";

    /// <summary>设计期字符串字典在 Ui 集内的编译落点（Page 项与惰性 BAML）。</summary>
    private const string DesignTimeDictionaryItem = @"Services\Localization\DesignTimeStrings.xaml";

    [Fact]
    public void 迁入类型_全部由StarPieHost定义()
    {
        Assert.All(KernelTypes, type => Assert.Equal("StarPie.Host", type.Assembly.GetName().Name));
    }

    [Fact]
    public void Host导出面_恰为内核清单_零额外类型()
    {
        string[] expected = KernelTypes
            .Select(type => type.FullName!)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected, FourSetBoundaryProbe.ExportedTypeNames(typeof(AppDataPaths).Assembly));
    }

    [Fact]
    public void Host内核_导出面签名_零WPF零旧集runtime()
    {
        foreach (Type type in KernelTypes)
        {
            Assert.All(SignatureTypes(type), member => Assert.False(
                TouchesWpf(member),
                $"{type.FullName} 的成员签名泄漏 WPF 类型: {member}"));

            Assert.All(SignatureTypes(type), member => Assert.DoesNotContain(
                member.Assembly.GetName().Name!,
                FourSetBoundaryProbe.LegacyAssemblyNames));
        }
    }

    [Fact]
    public void 内核件_在无WPF依赖的程序集内可直接构造()
    {
        string tempDir = Directory.CreateTempSubdirectory("starpie-host-kernel-tests").FullName;
        try
        {
            string configPath = Path.Combine(tempDir, "config.json");

            var localization = new LocalizationService();
            var config = new JsonConfigService(configPath, localization);
            var debouncer = new TestSaveDebouncer();
            var orchestrator = new SettingsSaveOrchestrator(config, debouncer, TestHub.NewMessenger());

            config.Load();                 // 文件缺失：播种默认配置并落盘
            config.Save();
            Assert.True(File.Exists(configPath));
            Assert.Equal(25.0, config.Current.DragThreshold);
            Assert.Equal("Global", config.GetProfileForProcess("unknown.exe").ProcessName);

            orchestrator.FlushPendingSave();
            Assert.Equal(0, debouncer.PendingCount);
            Assert.Contains("\"DragThreshold\"", File.ReadAllText(configPath));

            localization.SetLanguage("en");
            Assert.Equal("Confirm", localization.GetString("BtnConfirm"));

            // 主题引擎零 WPF 可 headless 直接构造：注入探针 → 解析 → 经端口换肤。
            var themeEngine = new ThemeEngine(() => true);
            var themeApplier = new TestThemeApplier();
            themeEngine.AttachApplier(themeApplier);
            themeEngine.SetTheme("System");
            Assert.Equal("Dark", themeEngine.CurrentEffectiveTheme);
            Assert.Equal(new[] { "Dark" }, themeApplier.Themes);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public void 设计期投影字典_随Ui集Page编译_壳工程已删除()
    {
        // 字典 = Ui 集（程序集名 StarPie）内的惰性 BAML：只此一份，运行时永不自动合并。
        Assert.Contains(
            "services/localization/designtimestrings.baml",
            FourSetBoundaryProbe.BamlEntries(typeof(App).Assembly));
        Assert.Contains(
            DesignTimeDictionaryItem,
            File.ReadAllText(Path.Combine(FourSetBoundaryProbe.RepoRoot, "StarPie.Ui", "StarPie.Ui.csproj")));

        // 旧设计期投影壳 StarPie.Core 已删除：仓库、解决方案与产物三处都不再存在。
        Assert.Contains("StarPie.Core", FourSetBoundaryProbe.LegacyAssemblyNames);
        Assert.DoesNotContain("StarPie.Core", FourSetBoundaryProbe.AppAssembliesOnDisk());
        Assert.False(File.Exists(Path.Combine(FourSetBoundaryProbe.RepoRoot, "StarPie.Core", "StarPie.Core.csproj")));
        Assert.DoesNotContain(
            "StarPie.Core",
            File.ReadAllText(Path.Combine(FourSetBoundaryProbe.RepoRoot, "StarPie.slnx")));
    }

    [Fact]
    public void 设计期资源锚_仅Ui工程一份_指向Ui内字典()
    {
        string[] projects = { "StarPie.Ui", "StarPie.Sdk", "StarPie.Sdk.Wpf", "StarPie.Host", "StarPie.Tests" };
        string[] anchors = projects
            .Where(project => File.Exists(Path.Combine(
                FourSetBoundaryProbe.RepoRoot, project, "Properties", "DesignTimeResources.xaml")))
            .ToArray();

        // 五份旧资源锚随归并收敛为一份：只有 Ui 集持有锚，其余工程不得再起第二份。
        Assert.Equal(new[] { "StarPie.Ui" }, anchors);
        Assert.Contains(
            DesignTimeDictionaryPackUri,
            File.ReadAllText(Path.Combine(FourSetBoundaryProbe.RepoRoot, "StarPie.Ui", "Properties", "DesignTimeResources.xaml")));
    }

    /// <summary>导出类型声明面（字段/属性/事件/方法与构造的参数与返回值）出现的全部类型。</summary>
    private static Type[] SignatureTypes(Type type)
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
    private static bool TouchesWpf(Type type)
    {
        if (type.IsGenericParameter) return false;
        if (type.HasElementType) return TouchesWpf(type.GetElementType()!);
        if (FourSetBoundaryProbe.WpfAssemblyNames.Contains(type.Assembly.GetName().Name)) return true;
        return type.IsGenericType && type.GetGenericArguments().Any(TouchesWpf);
    }
}
