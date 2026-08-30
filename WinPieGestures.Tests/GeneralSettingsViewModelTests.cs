using System;
using System.Collections.Generic;
using WinPieGestures;

namespace WinPieGestures.Tests;

/// <summary>
/// 通用分区 ViewModel 的行为覆盖 (T13, ADR-0001)：界面语言切换（写配置 + I18n 切换 +
/// 落盘请求）、开机自启（注册表读写经注入委托）、退出/提权重启编排、托盘驻留气泡提示
/// 与配置导入/导出——全部锁定迁移前 SettingsWindow code-behind 的外部行为。
/// 直接 new 被测对象并注入记录型委托，不触碰 ConfigManager 静态态。
/// </summary>
public sealed class GeneralSettingsViewModelTests
{
    /// <summary>常用装配：记录型托盘/退出/自启/提权委托（export/import 默认成功）。</summary>
    private static GeneralSettingsViewModel Create(
        AppConfig config,
        TestDialogService? dialogs = null,
        List<string>? exitCalls = null,
        List<string>? balloonCalls = null,
        List<bool>? autoStartCalls = null,
        bool autoStartEnabled = false,
        Action<string>? startElevated = null,
        Func<string, bool>? exportConfig = null,
        Func<string, bool>? importConfig = null,
        List<GeneralSettingsViewModel.NoticeRequest>? notices = null)
    {
        var vm = new GeneralSettingsViewModel(
            config,
            dialogs ?? new TestDialogService(),
            (title, text) => balloonCalls?.Add($"{title}|{text}"),
            () => exitCalls?.Add("exit"),
            () => autoStartEnabled,
            enable => autoStartCalls?.Add(enable),
            exportConfig ?? (_ => true),
            importConfig ?? (_ => true),
            startElevated);
        if (notices != null)
        {
            vm.NoticeRequested += notices.Add;
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
    [InlineData("en", LanguageCode.En)]
    [InlineData("zh-TW", LanguageCode.ZhTw)]
    [InlineData("ja", LanguageCode.Ja)]
    [InlineData("zh-CN", LanguageCode.ZhCn)]
    public void ApplyLanguage_WritesConfigSwitchesI18nAndRequestsSave(string code, LanguageCode expected)
    {
        var config = MakeConfig();
        var original = I18n.CurrentLanguage;
        var saves = 0;
        var vm = Create(config);
        vm.SaveRequested += () => saves++;
        try
        {
            vm.ApplyLanguage(code);

            Assert.Equal(code, config.Language);
            Assert.Equal(expected, I18n.CurrentLanguage);
            Assert.Equal(1, saves);
        }
        finally
        {
            I18n.CurrentLanguage = original;
        }
    }

    [Fact]
    public void ApplyLanguage_BroadcastsLanguageChanged()
    {
        var config = MakeConfig();
        var original = I18n.CurrentLanguage;
        var fired = 0;
        void Handler() => fired++;
        I18n.LanguageChanged += Handler;
        var vm = Create(config);
        try
        {
            // 基准化，避免静态语言状态受其他测试影响
            I18n.SetLanguage("zh-CN");
            fired = 0;

            vm.ApplyLanguage("en");

            Assert.Equal(1, fired);
        }
        finally
        {
            I18n.LanguageChanged -= Handler;
            I18n.CurrentLanguage = original;
        }
    }

    [Fact]
    public void ApplyLanguage_EmptyCode_MakesNoChanges()
    {
        var config = MakeConfig();
        var saves = 0;
        var vm = Create(config);
        vm.SaveRequested += () => saves++;

        vm.ApplyLanguage("");
        vm.ApplyLanguage(null!);

        Assert.Equal("Auto", config.Language);
        Assert.Equal(0, saves);
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
        var calls = new List<bool>();
        var saves = 0;
        var vm = Create(config, autoStartCalls: calls);
        vm.SaveRequested += () => saves++;

        vm.SetAutoStart(true);

        // 注册表读写经注入委托（transitional 保持 ConfigManager 调用点不变）
        Assert.Equal(new[] { true }, calls);
        Assert.Equal(1, saves);
    }

    // --- 托盘驻留提示 ----------------------------------------------------------------

    [Fact]
    public void NotifyMinimizedToTray_UsesCompositionRootDelegateWithFixedText()
    {
        var balloon = new List<string>();
        var vm = Create(MakeConfig(), balloonCalls: balloon);

        vm.NotifyMinimizedToTray();

        var request = Assert.Single(balloon);
        Assert.Equal("WinPieGestures", request.Split('|')[0]);
        Assert.Equal("应用已最小化至系统托盘，将在后台继续运行鼠标笔势监视。", request.Split('|')[1]);
    }

    // --- 提权重启 / 退出 -------------------------------------------------------------

    [Fact]
    public void ElevateAndRestart_StartsElevatedThenExits()
    {
        var config = MakeConfig();
        var exitCalls = new List<string>();
        var started = new List<string>();
        var notices = new List<GeneralSettingsViewModel.NoticeRequest>();
        var vm = Create(config, startElevated: path => started.Add(path), exitCalls: exitCalls, notices: notices);

        vm.ElevateAndRestart();

        // 启动成功后才经组合根委托退出，失败弹窗不出现
        //（测试宿主中 ProcessPath 指向 testhost，非空即可）
        var start = Assert.Single(started);
        Assert.False(string.IsNullOrWhiteSpace(start));
        Assert.Equal(new[] { "exit" }, exitCalls);
        Assert.Empty(notices);
    }

    [Fact]
    public void ElevateAndRestart_FailureOrCancel_NoticesAndDoesNotExit()
    {
        var config = MakeConfig();
        var exitCalls = new List<string>();
        var notices = new List<GeneralSettingsViewModel.NoticeRequest>();
        var vm = Create(
            config,
            startElevated: _ => throw new Exception("已取消"),
            exitCalls: exitCalls,
            notices: notices);

        vm.ElevateAndRestart();

        Assert.Empty(exitCalls);
        var notice = Assert.Single(notices);
        Assert.Equal("管理员提权", notice.Title);
        Assert.Equal("提权重启失败或已取消: 已取消", notice.Message);
        Assert.Equal(GeneralSettingsViewModel.NoticeKind.Warning, notice.Kind);
    }

    // --- 配置导出/导入 ---------------------------------------------------------------

    [Fact]
    public void ExportConfig_Cancelled_DoesNotExport()
    {
        var dialogs = new TestDialogService();
        var exported = new List<string>();
        var notices = new List<GeneralSettingsViewModel.NoticeRequest>();
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
        var notices = new List<GeneralSettingsViewModel.NoticeRequest>();
        var vm = Create(MakeConfig(), dialogs: dialogs, exportConfig: path => { exported.Add(path); return true; }, notices: notices);

        vm.ExportConfigCommand.Execute(null);

        var call = Assert.Single(dialogs.SaveFileDialogCalls);
        Assert.Equal("JSON 配置文件 (*.json)|*.json", call.Filter);
        Assert.Equal("导出配置文件", call.Title);
        // 备份文件名含日期前缀
        Assert.Matches(@"^WinPieGestures_Config_Backup_\d{8}\.json$", call.FileName ?? "");
        Assert.Equal(new[] { @"D:\backup\config.json" }, exported);
        var notice = Assert.Single(notices);
        Assert.Equal("提示", notice.Title);
        Assert.Equal("配置导出成功！", notice.Message);
        Assert.Equal(GeneralSettingsViewModel.NoticeKind.Info, notice.Kind);
    }

    [Fact]
    public void ExportConfig_Failure_NoticesError()
    {
        var dialogs = new TestDialogService();
        dialogs.SaveFileToPick = new FilePickResult(@"D:\backup\config.json");
        var notices = new List<GeneralSettingsViewModel.NoticeRequest>();
        var vm = Create(MakeConfig(), dialogs: dialogs, exportConfig: _ => false, notices: notices);

        vm.ExportConfigCommand.Execute(null);

        var notice = Assert.Single(notices);
        Assert.Equal("错误", notice.Title);
        Assert.Equal("配置导出失败，请检查写入权限。", notice.Message);
        Assert.Equal(GeneralSettingsViewModel.NoticeKind.Error, notice.Kind);
    }

    [Fact]
    public void ImportConfig_Cancelled_DoesNotImport()
    {
        var dialogs = new TestDialogService();
        var imported = new List<string>();
        var notices = new List<GeneralSettingsViewModel.NoticeRequest>();
        var importedEvents = 0;
        var vm = Create(MakeConfig(), dialogs: dialogs, importConfig: path => { imported.Add(path); return true; }, notices: notices);
        vm.ConfigImported += () => importedEvents++;

        vm.ImportConfigCommand.Execute(null);

        Assert.Empty(imported);
        Assert.Empty(notices);
        Assert.Equal(0, importedEvents);
    }

    [Fact]
    public void ImportConfig_Success_NoticesInfoThenRaisesConfigImported()
    {
        var dialogs = new TestDialogService();
        dialogs.OpenFileToPick = new FilePickResult(@"D:\backup\config.json");
        var imported = new List<string>();
        var notices = new List<GeneralSettingsViewModel.NoticeRequest>();
        var importedEvents = 0;
        var vm = Create(MakeConfig(), dialogs: dialogs, importConfig: path => { imported.Add(path); return true; }, notices: notices);
        vm.ConfigImported += () => importedEvents++;

        vm.ImportConfigCommand.Execute(null);

        var call = Assert.Single(dialogs.OpenFileDialogCalls);
        Assert.Equal("JSON 配置文件 (*.json)|*.json", call.Filter);
        Assert.Equal("选择要导入的配置文件", call.Title);
        Assert.Equal(new[] { @"D:\backup\config.json" }, imported);
        // 与迁移前一致：先提示导入成功，再由窗口重载各分区 UI
        var notice = Assert.Single(notices);
        Assert.Equal("提示", notice.Title);
        Assert.Equal("配置导入成功！正在应用新设置...", notice.Message);
        Assert.Equal(GeneralSettingsViewModel.NoticeKind.Info, notice.Kind);
        Assert.Equal(1, importedEvents);
    }

    [Fact]
    public void ImportConfig_Failure_NoticesErrorWithoutConfigImported()
    {
        var dialogs = new TestDialogService();
        dialogs.OpenFileToPick = new FilePickResult(@"D:\backup\config.json");
        var notices = new List<GeneralSettingsViewModel.NoticeRequest>();
        var importedEvents = 0;
        var vm = Create(MakeConfig(), dialogs: dialogs, importConfig: _ => false, notices: notices);
        vm.ConfigImported += () => importedEvents++;

        vm.ImportConfigCommand.Execute(null);

        var notice = Assert.Single(notices);
        Assert.Equal("错误", notice.Title);
        Assert.Equal("导入失败：文件格式不匹配或已损坏。", notice.Message);
        Assert.Equal(GeneralSettingsViewModel.NoticeKind.Error, notice.Kind);
        Assert.Equal(0, importedEvents);
    }
}
