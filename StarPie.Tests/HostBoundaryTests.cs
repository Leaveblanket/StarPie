using System;
using System.IO;
using System.Linq;
using System.Reflection;
using StarPie.Kernel.Configuration;
using StarPie.Kernel.Localization;

namespace StarPie.Tests;

/// <summary>
/// 宿主内核边界基线：内核运行时（配置读写/防抖落盘接缝/本地化实现）归 <see cref="StarPie.Host"/>——
/// 导出面 = 恰为内核清单（新增 public 类型须同步本表），导出面签名不触碰 WPF 与旧集 runtime，
/// 核心件在无 WPF 依赖的程序集里可直接构造；遗留的 <c>StarPie.Core</c> 只余设计期投影字典
/// （零导出类型、零运行时件）。与 <see cref="FourSetBoundaryTests"/>（工程面）、
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
    };

    /// <summary>设计期字符串字典的唯一来源（Page 编译、签入生成物；pack URI 由 UI 工程资源锚合并）。</summary>
    private const string DesignTimeDictionaryPackUri =
        "pack://application:,,,/StarPie.Core;component/Services/Localization/DesignTimeStrings.xaml";

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
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public void Core_只余设计期投影_无导出类型且仅一份惰性字典()
    {
        Assembly core = FourSetBoundaryProbe.LoadAppAssembly("StarPie.Core");

        // 遗留集不再导出任何类型（无双份定义、无运行时 WPF 类型泄漏）。
        Assert.Empty(FourSetBoundaryProbe.ExportedTypeNames(core));

        // 唯一 XAML = 设计期字符串字典（惰性 BAML，仅设计期合并、运行时永不合并）。
        Assert.Equal(
            new[] { "services/localization/designtimestrings.baml" },
            FourSetBoundaryProbe.BamlEntries(core));
    }

    [Fact]
    public void 设计期资源锚_五个UI工程指向Core投影字典()
    {
        foreach (string project in new[] { "StarPie.Ui", "StarPie.Shell", "StarPie.Gestures", "StarPie.Dialogs", "StarPie.Wheel" })
        {
            string path = Path.Combine(FourSetBoundaryProbe.RepoRoot, project, "Properties", "DesignTimeResources.xaml");
            Assert.True(File.Exists(path), $"设计期资源锚缺失: {project}");
            Assert.Contains(DesignTimeDictionaryPackUri, File.ReadAllText(path));
        }
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
