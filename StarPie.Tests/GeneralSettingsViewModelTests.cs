using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.Messaging;
using StarPie;
using StarPie.Services;

namespace StarPie.Tests;

/// <summary>
/// 通用分区 ViewModel 的行为覆盖：界面语言切换（写配置 + I18n 切换 +
/// 落盘请求）、开机自启（注册表读写经注入委托）与配置导入/导出。
/// 直接 new 被测对象并注入记录型委托，不触碰任何静态配置状态。
/// </summary>
public sealed class GeneralSettingsViewModelTests
{
    private static readonly LocalizationService Localization = new();

    /// <summary>常用装配：记录型自启/通知委托（export/import 默认成功）。
    /// 自启假体带"任务存在"状态：落位成功才改变它，供 VM 的实况回读断言。</summary>
    private static GeneralSettingsViewModel Create(
        AppConfig config,
        TestDialogService? dialogs = null,
        List<(bool Enable, bool AsAdmin)>? applyCalls = null,
        bool autoStartEnabled = false,
        Func<string, bool>? exportConfig = null,
        Func<string, bool>? importConfig = null,
        List<NoticeRequest>? notices = null,
        SaveSpy? save = null,
        bool adminAutoStartEnabled = false,
        bool applyAdminSucceeds = true,
        bool runningElevated = false,
        AppHostDelegates? hostDelegates = null)
    {
        var messenger = TestHub.NewMessenger();
        if (save != null) SaveSpy.Attach(messenger, save);
        bool adminTaskPresent = adminAutoStartEnabled;
        var vm = new GeneralSettingsViewModel(
            config,
            dialogs ?? new TestDialogService(),
            () => autoStartEnabled,
            (enable, asAdmin) =>
            {
                applyCalls?.Add((enable, asAdmin));
                if (asAdmin && !applyAdminSucceeds) return false;
                adminTaskPresent = enable && asAdmin;
                return true;
            },
            exportConfig ?? (_ => true),
            importConfig ?? (_ => true),
            currentConfig: () => config,
            messenger: messenger,
            localization: Localization,
            isAdminAutoStartEnabled: () => adminTaskPresent,
            isRunningElevated: () => runningElevated,
            hostDelegates: hostDelegates);
        if (notices != null)
        {
            messenger.Register<GeneralNoticeRequestedMessage>(vm, (_, m) => notices.Add(m.Notice));
        }
        return vm;
    }

    private static AppConfig MakeConfig() => new() { Language = "Auto" };

    // --- 构造 ---------------------------------------------------------------------

    [Fact]
    public void Constructor_ReadsAutoStartStateAndLanguageCode()
    {
        var vm = Create(MakeConfig(), autoStartEnabled: true);

        Assert.True(vm.AutoStartEnabled);
        Assert.Equal("Auto", vm.LanguageCode);
    }

    [Fact]
    public void LanguageCode_NullFallsBackToAuto()
    {
        var config = MakeConfig();
        config.Language = null!;

        var vm = Create(config);

        Assert.Equal("Auto", vm.LanguageCode);
    }

    // --- 语言切换 -------------------------------------------------------------------

    [Theory]
    [InlineData("en", "en")]
    [InlineData("zh-TW", "zh-TW")]
    [InlineData("ja", "ja")]
    [InlineData("zh-CN", "zh-CN")]
    public void ApplyLanguage_WritesConfigSwitchesI18nAndRequestsSave(string code, string expected)
    {
        var config = MakeConfig();
        var original = Localization.CurrentLanguage;
        var save = new SaveSpy();
        var vm = Create(config, save: save);
        try
        {
            vm.ApplyLanguage(code);

            Assert.Equal(code, config.Language);
            Assert.Equal(expected, Localization.CurrentLanguage);
            Assert.Equal(1, save.Immediate);
        }
        finally
        {
            Localization.SetLanguage(original);
        }
    }

    [Fact]
    public void ApplyLanguage_BroadcastsLanguageChanged()
    {
        var config = MakeConfig();
        var original = Localization.CurrentLanguage;
        var fired = 0;
        void Handler() => fired++;
        Localization.LanguageChanged += Handler;
        var vm = Create(config);
        try
        {
            // 基准化，避免静态语言状态受其他测试影响
            Localization.SetLanguage("zh-CN");
            fired = 0;

            vm.ApplyLanguage("en");

            Assert.Equal(1, fired);
        }
        finally
        {
            Localization.LanguageChanged -= Handler;
            Localization.SetLanguage(original);
        }
    }

    [Fact]
    public void ApplyLanguage_EmptyCode_MakesNoChanges()
    {
        var config = MakeConfig();
        var save = new SaveSpy();
        var vm = Create(config, save: save);

        vm.ApplyLanguage("");
        vm.ApplyLanguage(null!);

        Assert.Equal("Auto", config.Language);
        Assert.Equal(0, save.Immediate);
    }

    [Fact]
    public void Reload_RefreshesLanguageCodeFromNewConfigInstance()
    {
        var vm = Create(MakeConfig());
        var imported = MakeConfig();
        imported.Language = "ja";

        vm.Reload(imported);

        Assert.Equal("ja", vm.LanguageCode);
    }

    // --- 开机自启 -------------------------------------------------------------------

    [Fact]
    public void SetAutoStart_CallsRegistryDelegateAndRequestsSave()
    {
        var config = MakeConfig();
        var calls = new List<(bool, bool)>();
        var save = new SaveSpy();
        var vm = Create(config, applyCalls: calls, save: save);

        vm.SetAutoStart(true);

        // 自启形态落位经注入委托（组合根接线 AutostartRegistry）；未勾提权形态即 (开, 不提权)
        Assert.Equal(new[] { (Enable: true, AsAdmin: false) }, calls);
        Assert.Equal(1, save.Immediate);
    }

    [Fact]
    public void Constructor_ReadsAdminAutoStartFromTaskScheduler()
    {
        // 提权形态状态读自计划任务实况，不读配置
        var vm = Create(MakeConfig(), autoStartEnabled: true, adminAutoStartEnabled: true);

        Assert.True(vm.AdminAutoStartEnabled);
    }

    [Fact]
    public void AdminAutoStart_TurningOnAlsoTurnsOnAutoStartAndPersists()
    {
        var config = MakeConfig();
        var calls = new List<(bool Enable, bool AsAdmin)>();
        var save = new SaveSpy();
        var vm = Create(config, applyCalls: calls, save: save);

        vm.AdminAutoStartEnabled = true;

        // 提权自启蕴含自启：一次落位就是 (开, 提权)
        Assert.True(vm.AutoStartEnabled);
        Assert.Equal(new[] { (Enable: true, AsAdmin: true) }, calls);
        Assert.True(config.AutoStartAsAdmin);
        Assert.Equal(1, save.Immediate);
    }

    [Fact]
    public void AdminAutoStart_ApplyFailure_NotifiesAndSnapsBackToReality()
    {
        // 用户取消 UAC 或账号无管理员凭据：不静默——提示 + 开关拨回实况（系统里并没有那个任务）
        var config = MakeConfig();
        var notices = new List<NoticeRequest>();
        var vm = Create(config, notices: notices, applyAdminSucceeds: false);

        vm.AdminAutoStartEnabled = true;

        Assert.False(vm.AdminAutoStartEnabled);
        Assert.False(config.AutoStartAsAdmin);
        var notice = Assert.Single(notices);
        Assert.Equal(NoticeKind.Warning, notice.Kind);
        Assert.Contains("UAC", notice.Message);
    }

    [Fact]
    public void AutoStart_TurningOffAlsoRevokesAdminShape()
    {
        var config = MakeConfig();
        var calls = new List<(bool Enable, bool AsAdmin)>();
        var vm = Create(config, applyCalls: calls, autoStartEnabled: true, adminAutoStartEnabled: true);

        vm.AutoStartEnabled = false;

        // 关掉自启即提权形态一并撤销（提权自启以自启为前提）
        Assert.False(vm.AdminAutoStartEnabled);
        Assert.False(config.AutoStartAsAdmin);
        Assert.Equal((Enable: false, AsAdmin: false), calls[^1]);
    }

    // --- 立即以管理员身份重启（入口可见性口径） ---------------------------------------

    [Fact]
    public void RestartElevatedEntry_VisibleAndClickable_WhenNotElevatedAndTaskPresent()
    {
        var vm = Create(MakeConfig(), autoStartEnabled: true, adminAutoStartEnabled: true);

        Assert.True(vm.ShowRestartElevated);
        Assert.True(vm.CanRestartElevated);
        Assert.False(vm.ShowRestartElevatedHint);
    }

    [Fact]
    public void RestartElevatedEntry_Hidden_WhenAlreadyRunningElevated()
    {
        // 提权入口只在非提权态出现：对以管理员身份运行的实例没有意义。
        var vm = Create(MakeConfig(), autoStartEnabled: true, adminAutoStartEnabled: true, runningElevated: true);

        Assert.False(vm.ShowRestartElevated);
        Assert.False(vm.ShowRestartElevatedHint);
    }

    [Fact]
    public void RestartElevatedEntry_NotClickableWithReason_WhenAdminAutostartTaskMissing()
    {
        // 入口出现但不可点，且呈现"为什么不可点"——不给一个点了没反应的按钮。
        var vm = Create(MakeConfig());

        Assert.True(vm.ShowRestartElevated);
        Assert.False(vm.CanRestartElevated);
        Assert.True(vm.ShowRestartElevatedHint);
    }

    [Fact]
    public void RestartElevatedEntry_ClickabilityFollowsTheAutostartTaskReality()
    {
        var vm = Create(MakeConfig());
        Assert.False(vm.CanRestartElevated);

        vm.AdminAutoStartEnabled = true;

        // 任务落位成功（实况回读为"在"）后入口即可点，原因说明随之收起。
        Assert.True(vm.CanRestartElevated);
        Assert.False(vm.ShowRestartElevatedHint);
    }

    [Fact]
    public void RestartElevatedNow_ForwardsToHostDelegate()
    {
        // 触发经宿主委托包转发：页面 VM 不反向依赖宿主类，也不给应用内入口开专用通道。
        var hostDelegates = new AppHostDelegates();
        int invoked = 0;
        hostDelegates.RestartElevated = () => invoked++;
        var vm = Create(
            MakeConfig(),
            autoStartEnabled: true,
            adminAutoStartEnabled: true,
            hostDelegates: hostDelegates);

        vm.RestartElevatedNowCommand.Execute(null);

        Assert.Equal(1, invoked);
    }

    [Fact]
    public void RestartElevatedNow_DoesNothing_WhenEntryIsNotClickable()
    {
        // 不可点时不动手（按钮本身也绑 IsEnabled）：守卫写在 VM 里，命令被程序化触发也不会越界。
        var hostDelegates = new AppHostDelegates();
        int invoked = 0;
        hostDelegates.RestartElevated = () => invoked++;
        var vm = Create(MakeConfig(), hostDelegates: hostDelegates);

        vm.RestartElevatedNowCommand.Execute(null);

        Assert.Equal(0, invoked);
    }

    // --- 配置导出/导入 ---------------------------------------------------------------
    [Fact]
    public void ExportConfig_Cancelled_DoesNotExport()
    {
        var dialogs = new TestDialogService();
        var exported = new List<string>();
        var notices = new List<NoticeRequest>();
        var vm = Create(MakeConfig(), dialogs: dialogs, exportConfig: path => { exported.Add(path); return true; }, notices: notices);

        vm.ExportConfigCommand.Execute(null);

        Assert.Empty(exported);
        Assert.Empty(notices);
    }

    [Fact]
    public void ExportConfig_Success_NoticesInfo()
    {
        var dialogs = new TestDialogService();
        dialogs.SaveFileToPick = new FilePickResult(@"D:\backup\config.json");
        var exported = new List<string>();
        var notices = new List<NoticeRequest>();
        var vm = Create(MakeConfig(), dialogs: dialogs, exportConfig: path => { exported.Add(path); return true; }, notices: notices);

        vm.ExportConfigCommand.Execute(null);

        var call = Assert.Single(dialogs.SaveFileDialogCalls);
        Assert.Equal("JSON 配置文件 (*.json)|*.json", call.Filter);
        Assert.Equal("导出配置文件", call.Title);
        // 备份文件名含日期前缀
        Assert.Matches(@"^StarPie_Config_Backup_\d{8}\.json$", call.FileName ?? "");
        Assert.Equal(new[] { @"D:\backup\config.json" }, exported);
        var notice = Assert.Single(notices);
        Assert.Equal("提示", notice.Title);
        Assert.Equal("配置导出成功！", notice.Message);
        Assert.Equal(NoticeKind.Info, notice.Kind);
    }

    [Fact]
    public void ExportConfig_Failure_NoticesError()
    {
        var dialogs = new TestDialogService();
        dialogs.SaveFileToPick = new FilePickResult(@"D:\backup\config.json");
        var notices = new List<NoticeRequest>();
        var vm = Create(MakeConfig(), dialogs: dialogs, exportConfig: _ => false, notices: notices);

        vm.ExportConfigCommand.Execute(null);

        var notice = Assert.Single(notices);
        Assert.Equal("错误", notice.Title);
        Assert.Equal("配置导出失败，请检查写入权限。", notice.Message);
        Assert.Equal(NoticeKind.Error, notice.Kind);
    }

    [Fact]
    public void ImportConfig_Cancelled_DoesNotImport()
    {
        var dialogs = new TestDialogService();
        var imported = new List<string>();
        var notices = new List<NoticeRequest>();
        var save = new SaveSpy();
        var config = MakeConfig();
        var vm = Create(config, dialogs: dialogs, importConfig: path => { imported.Add(path); return true; }, notices: notices, save: save);

        vm.ImportConfigCommand.Execute(null);

        Assert.Empty(imported);
        Assert.Empty(notices);
        Assert.Empty(save.Imported);
    }

    [Fact]
    public void ImportConfig_Success_NoticesInfoThenRaisesConfigImported()
    {
        var config = MakeConfig();
        var dialogs = new TestDialogService();
        dialogs.OpenFileToPick = new FilePickResult(@"D:\backup\config.json");
        var imported = new List<string>();
        var notices = new List<NoticeRequest>();
        var save = new SaveSpy();
        var vm = Create(config, dialogs: dialogs, importConfig: path => { imported.Add(path); return true; }, notices: notices, save: save);

        vm.ImportConfigCommand.Execute(null);

        var call = Assert.Single(dialogs.OpenFileDialogCalls);
        Assert.Equal("JSON 配置文件 (*.json)|*.json", call.Filter);
        Assert.Equal("选择要导入的配置文件", call.Title);
        Assert.Equal(new[] { @"D:\backup\config.json" }, imported);
        // 先提示导入成功，再由窗口重载各分区 UI
        var notice = Assert.Single(notices);
        Assert.Equal("提示", notice.Title);
        Assert.Equal("配置导入成功！正在应用新设置...", notice.Message);
        Assert.Equal(NoticeKind.Info, notice.Kind);
        // 导入成功广播携带新运行态配置实例
        var message = Assert.Single(save.Imported);
        Assert.Same(config, message.ImportedConfig);
    }

    [Fact]
    public void ImportConfig_Failure_NoticesErrorWithoutConfigImported()
    {
        var dialogs = new TestDialogService();
        dialogs.OpenFileToPick = new FilePickResult(@"D:\backup\config.json");
        var notices = new List<NoticeRequest>();
        var save = new SaveSpy();
        var vm = Create(MakeConfig(), dialogs: dialogs, importConfig: _ => false, notices: notices, save: save);

        vm.ImportConfigCommand.Execute(null);

        var notice = Assert.Single(notices);
        Assert.Equal("错误", notice.Title);
        Assert.Equal("导入失败：文件格式不匹配或已损坏。", notice.Message);
        Assert.Equal(NoticeKind.Error, notice.Kind);
        Assert.Empty(save.Imported);
    }
}
