using System;
using System.Collections.Generic;
using WinPieGestures;

namespace WinPieGestures.Tests;

/// <summary>
/// 根设置 ViewModel 的行为覆盖 (T14, ADR-0001)：聚合各分区子 ViewModel 并以同一运行态配置
/// 播种；配置导入替换运行态配置实例后由根统一重挂各分区（方案列表/行为/通用），分区间
/// 共享状态不残留旧实例。直接 new 被测对象 + mock 依赖，不触碰任何静态配置状态。
/// </summary>
public sealed class RootSettingsViewModelTests
{
    /// <summary>内存配置服务假实现：直接持有 POCO，记录 Save 调用。</summary>
    private sealed class FakeConfigService : IConfigService
    {
        public AppConfig Current { get; set; } = new();
        public int SaveCalls;
        public void Load() { }
        public void Save() => SaveCalls++;
        public WheelProfile GetProfileForProcess(string processName) => Current.Profiles[0];
        public WheelProfile GetGlobalProfile() => Current.Profiles[0];
    }

    private static AppConfig MakeConfig(string language = "zh-CN") => new()
    {
        Language = language,
        DragThreshold = 25.0,
        WheelRadius = 138.0,
        BlacklistedProcesses = new List<string> { "mstsc.exe" },
        Profiles = new List<WheelProfile>
        {
            new WheelProfile { ProcessName = "Global", SectorCount = 8, Actions = new List<ActionItem>() }
        }
    };

    /// <summary>组合根委托的默认桩：与本票无关的副作用全部空转并记录。</summary>
    private sealed class HostStubs
    {
        public AppConfig Current = new();
        public List<string> BalloonTips { get; } = new();
        public int ExitCalls;
        public bool AutoStart = false;
        public List<bool> AutoStartWrites { get; } = new();
        public List<string> ImportedPaths { get; } = new();
        public List<string> ExportedPaths { get; } = new();
        public Func<string, bool> OnImport { get; set; } = _ => false;

        public RootSettingsViewModel CreateRoot(IDialogService dialogs, IConfigService configService)
            => new(
                configService,
                dialogs,
                () => Current,
                (title, text) => BalloonTips.Add(title + text),
                () => ExitCalls++,
                isAutoStartEnabled: () => AutoStart,
                setAutoStart: enable => AutoStartWrites.Add(enable),
                exportConfig: path => { ExportedPaths.Add(path); return true; },
                importConfig: path => { ImportedPaths.Add(path); return OnImport(path); });
    }

    // --- 构造聚合 -----------------------------------------------------------------

    [Fact]
    public void Constructor_AggregatesPartitionsSeededFromSameRuntimeConfig()
    {
        var config = MakeConfig();
        var stubs = new HostStubs { Current = config };
        var root = stubs.CreateRoot(new TestDialogService(), new FakeConfigService { Current = config });

        Assert.NotNull(root.Appearance);
        Assert.NotNull(root.ProfileList);
        Assert.NotNull(root.Behavior);
        Assert.NotNull(root.General);

        // 各分区以同一运行态配置播种
        Assert.Equal(25.0, root.Behavior.DragThreshold);
        Assert.Equal(new[] { "mstsc.exe" }, root.Behavior.BlacklistProcesses);
        Assert.Single(root.ProfileList.Profiles);
        Assert.Equal("Global", root.ProfileList.Profiles[0].ProcessName);
        Assert.Equal("zh-CN", root.General.LanguageCode);
        Assert.Equal(138.0, root.Appearance.WheelRadius);

        // 外观分区 live-apply 写穿同一配置实例（分区间共享状态）
        root.Appearance.WheelRadius = 150.0;
        Assert.Equal(150.0, config.WheelRadius);
    }

    // --- 配置导入 → 分区间经根协调重挂 ---------------------------------------------

    [Fact]
    public void ImportConfig_ThroughGeneral_ReloadsAllPartitionsWithNewInstance()
    {
        var configV1 = MakeConfig();
        var configV2 = MakeConfig(language: "en");
        configV2.DragThreshold = 40.0;
        configV2.BlacklistedProcesses = new List<string> { "paint.exe" };
        configV2.Profiles.Add(new WheelProfile { ProcessName = "myapp.exe", SectorCount = 4, Actions = new List<ActionItem>() });

        var stubs = new HostStubs { Current = configV1 };
        stubs.OnImport = _ => { stubs.Current = configV2; return true; };
        var dialogs = new TestDialogService { OpenFileToPick = new FilePickResult("D:\\backup.json") };
        var root = stubs.CreateRoot(dialogs, new FakeConfigService { Current = configV1 });

        int reloaded = 0;
        root.PartitionsReloaded += () => reloaded++;

        root.General.ImportConfigCommand.Execute(null);

        Assert.Equal(new[] { "D:\\backup.json" }, stubs.ImportedPaths);
        Assert.Equal(1, reloaded);

        // 方案列表分区重挂到导入实例
        Assert.Equal(2, root.ProfileList.Profiles.Count);
        Assert.Equal("Global", root.ProfileList.Profiles[0].ProcessName);
        Assert.Equal("myapp.exe", root.ProfileList.Profiles[1].ProcessName);
        // 行为分区重挂到导入实例
        Assert.Equal(40.0, root.Behavior.DragThreshold);
        Assert.Equal(new[] { "paint.exe" }, root.Behavior.BlacklistProcesses);
        // 通用分区重挂到导入实例
        Assert.Equal("en", root.General.LanguageCode);

        // 导入后切换语言写入新实例而非残留旧实例（T13 遗留的旧实例滞留回归锁）
        root.General.ApplyLanguage("ja");
        Assert.Equal("ja", configV2.Language);
    }

    [Fact]
    public void ImportConfig_Failure_RaisesErrorNoticeAndKeepsPartitions()
    {
        var configV1 = MakeConfig();
        var stubs = new HostStubs { Current = configV1 };
        var dialogs = new TestDialogService { OpenFileToPick = new FilePickResult("D:\\broken.json") };
        var root = stubs.CreateRoot(dialogs, new FakeConfigService { Current = configV1 });

        var notices = new List<GeneralSettingsViewModel.NoticeRequest>();
        root.General.NoticeRequested += notices.Add;
        int reloaded = 0;
        root.PartitionsReloaded += () => reloaded++;

        root.General.ImportConfigCommand.Execute(null);

        Assert.Equal(new[] { "D:\\broken.json" }, stubs.ImportedPaths);
        Assert.Equal(0, reloaded);
        Assert.Single(notices);
        Assert.Equal(GeneralSettingsViewModel.NoticeKind.Error, notices[0].Kind);
        Assert.Equal(25.0, root.Behavior.DragThreshold);
        Assert.Single(root.ProfileList.Profiles);
    }

    // --- 运行态配置访问与落盘（T16 新增根 VM 表面） ---------------------------------

    [Fact]
    public void CurrentConfig_ExposesLiveConfigInstance_AndTracksImportedInstance()
    {
        var configV1 = MakeConfig();
        var configV2 = MakeConfig(language: "en");
        var stubs = new HostStubs { Current = configV1 };
        stubs.OnImport = _ => { stubs.Current = configV2; return true; };
        var dialogs = new TestDialogService { OpenFileToPick = new FilePickResult("D:\\backup.json") };
        var root = stubs.CreateRoot(dialogs, new FakeConfigService { Current = configV1 });

        Assert.Same(configV1, root.CurrentConfig);

        root.General.ImportConfigCommand.Execute(null);

        // 导入替换运行态实例后，访问器取到新实例（纯 View 读取与预览绘制消费此入口）
        Assert.Same(configV2, root.CurrentConfig);
    }

    [Fact]
    public void SaveConfig_DrivesInjectedConfigServiceSave()
    {
        var config = MakeConfig();
        var configService = new FakeConfigService { Current = config };
        var stubs = new HostStubs { Current = config };
        var root = stubs.CreateRoot(new TestDialogService(), configService);

        root.SaveConfig();
        root.SaveConfig();

        Assert.Equal(2, configService.SaveCalls);
    }
}
