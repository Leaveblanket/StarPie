using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.Messaging;

namespace WinPieGestures.Tests;

/// <summary>
/// 界面主题设置子 ViewModel 的行为覆盖 (#54, ADR-0014 决策 6/7)：AppTheme 透传（读穿配置 /
/// 写穿 + 防抖落盘 + 主题应用消息）、驻留选项目录（切语重建 / 选中恢复 / Dispose 退订）、
/// 配置导入后重挂路径（补发选中通知 + 主题应用消息由壳层订阅执行）。
/// </summary>
public sealed class InterfaceThemeSettingsViewModelTests
{
    /// <summary>主题应用消息记录器（壳层主窗口订阅语义的测试投影）。</summary>
    private sealed class ThemeApplyLog
    {
        public List<string> Themes { get; } = new();

        public static ThemeApplyLog Attach(WeakReferenceMessenger messenger)
        {
            var log = new ThemeApplyLog();
            messenger.Register<AppThemeChangedMessage>(log, (_, m) => log.Themes.Add(m.Theme));
            return log;
        }
    }

    private sealed class Harness
    {
        public TestConfigService ConfigService { get; } = new();
        public LocalizationService Localization { get; } = new();
        public WeakReferenceMessenger Messenger { get; } = TestHub.NewMessenger();
        public SaveSpy Spy { get; }
        public ThemeApplyLog Applied { get; }
        public List<string?> Notified { get; } = new();
        public InterfaceThemeSettingsViewModel Vm { get; }

        public Harness(AppConfig? config = null)
        {
            if (config != null)
            {
                ConfigService.Current = config;
            }

            Spy = new SaveSpy();
            SaveSpy.Attach(Messenger, Spy);
            Applied = ThemeApplyLog.Attach(Messenger);
            Vm = new InterfaceThemeSettingsViewModel(ConfigService, Messenger, Localization);
            Vm.PropertyChanged += (_, e) => Notified.Add(e.PropertyName);
        }
    }

    // --- 构造播种：选项目录 ---------------------------------------------------------

    [Fact]
    public void Constructor_SeedsOptionsFromLocalization_WithoutSideEffects()
    {
        var h = new Harness();

        Assert.Equal("System", h.Vm.AppTheme);
        Assert.Equal(6, h.Vm.AppThemeOptions.Count);
        Assert.Equal(
            new[] { "System", "Light", "Dark", "MidnightNavy", "RoyalViolet", "TitaniumGray" },
            h.Vm.AppThemeOptions.Select(o => o.Tag));
        // 标签即时取词（迁移前 XAML 静态项 Content 的 DynamicResource 同键）
        Assert.Equal(h.Localization.GetString("ThemeSystem"), h.Vm.AppThemeOptions[0].Label);
        Assert.Equal(h.Localization.GetString("ThemeDark"), h.Vm.AppThemeOptions[2].Label);
        Assert.Equal(h.Localization.GetString("ThemeGray"), h.Vm.AppThemeOptions[5].Label);
        // 构造不写穿配置、不发落盘/主题应用消息
        Assert.Empty(h.Applied.Themes);
        Assert.Equal(0, h.Spy.Debounced);
        Assert.Equal(0, h.Spy.Immediate);
        Assert.Equal(0, h.ConfigService.SaveCalls);
    }

    // --- AppTheme 透传 -------------------------------------------------------------

    [Fact]
    public void AppTheme_ReadsThroughLiveConfig_WithSystemFallback()
    {
        var h = new Harness(new AppConfig { AppTheme = "Dark" });

        Assert.Equal("Dark", h.Vm.AppTheme);

        // 运行态配置替换实例后读直取（透传属性不持副本）；空值回落 System
        h.ConfigService.Current.AppTheme = null!;
        Assert.Equal("System", h.Vm.AppTheme);
    }

    [Fact]
    public void AppTheme_Set_WritesThroughConfig_NotifiesAndPublishesAutoSaveAndApply()
    {
        var h = new Harness();

        h.Vm.AppTheme = "MidnightNavy";

        Assert.Equal("MidnightNavy", h.ConfigService.Current.AppTheme);
        Assert.Contains(nameof(InterfaceThemeSettingsViewModel.AppTheme), h.Notified);
        var apply = Assert.Single(h.Applied.Themes);
        Assert.Equal("MidnightNavy", apply);
        // 落盘语义与迁移前一致：防抖请求，非立即
        Assert.Equal(1, h.Spy.Debounced);
        Assert.Equal(0, h.Spy.Immediate);
        Assert.Equal(0, h.ConfigService.SaveCalls);

        // 同值写入不再通知、不再触发管线
        h.Vm.AppTheme = "MidnightNavy";
        Assert.Single(h.Notified, n => n == nameof(InterfaceThemeSettingsViewModel.AppTheme));
        Assert.Single(h.Applied.Themes);
        Assert.Equal(1, h.Spy.Debounced);
    }

    [Fact]
    public void AppTheme_Set_NullOrEmpty_IsIgnored()
    {
        // ItemsSource 化后下拉重建期间绑定回推 null：不得清掉当前主题（迁移前对 null 同样短路）
        var h = new Harness(new AppConfig { AppTheme = "Dark" });

        h.Vm.AppTheme = null!;
        h.Vm.AppTheme = "";

        Assert.Equal("Dark", h.ConfigService.Current.AppTheme);
        Assert.Empty(h.Applied.Themes);
        Assert.Equal(0, h.Spy.Debounced);
        Assert.Empty(h.Notified);
    }

    // --- 切语重建与选中恢复 ---------------------------------------------------------

    [Fact]
    public void LanguageChanged_RebuildsOptionLabels_AndNotifiesSelectionRestore()
    {
        var h = new Harness(new AppConfig { AppTheme = "Dark" });
        string zhLabel = h.Vm.AppThemeOptions[2].Label;

        h.Localization.SetLanguage(LanguageCode.En);

        // 目录保持六项、Tag 不变；标签随语言刷新
        Assert.Equal(6, h.Vm.AppThemeOptions.Count);
        Assert.Equal("Dark", h.Vm.AppThemeOptions[2].Tag);
        Assert.Equal(h.Localization.GetString("ThemeDark"), h.Vm.AppThemeOptions[2].Label);
        Assert.NotEqual(zhLabel, h.Vm.AppThemeOptions[2].Label);
        // 选中恢复通知：让 ComboBox 从新目录恢复选中
        Assert.Contains(nameof(InterfaceThemeSettingsViewModel.AppTheme), h.Notified);
        Assert.Equal("Dark", h.Vm.AppTheme);
        // 切语只重建目录：不写配置、不发落盘/主题应用消息
        Assert.Equal("Dark", h.ConfigService.Current.AppTheme);
        Assert.Empty(h.Applied.Themes);
        Assert.Equal(0, h.Spy.Debounced);
    }

    [Fact]
    public void Dispose_UnsubscribesLanguageChanged()
    {
        var h = new Harness(new AppConfig { AppTheme = "Dark" });
        string zhLabel = h.Vm.AppThemeOptions[2].Label;

        h.Vm.Dispose();
        h.Vm.Dispose(); // 幂等
        h.Localization.SetLanguage(LanguageCode.En);

        // 退订后切语不再重建目录、不再补发选中通知（ADR-0010 单例 VM 成对退订）
        Assert.Equal(zhLabel, h.Vm.AppThemeOptions[2].Label);
        Assert.DoesNotContain(nameof(InterfaceThemeSettingsViewModel.AppTheme), h.Notified);
    }

    // --- 配置导入后重挂路径 ---------------------------------------------------------

    [Fact]
    public void ConfigImport_ReloadsAndPublishesApplyMessage_WithoutSaveRequests()
    {
        var h = new Harness(new AppConfig { AppTheme = "Dark" });
        var imported = new AppConfig { AppTheme = "RoyalViolet" };
        h.ConfigService.Current = imported;

        h.Messenger.Send(new ConfigImportedMessage(imported));

        // 补发选中通知（绑定拉取新值恢复 ComboBox 选中）并发布主题应用消息（壳层执行窗口主题应用）
        Assert.Contains(nameof(InterfaceThemeSettingsViewModel.AppTheme), h.Notified);
        var apply = Assert.Single(h.Applied.Themes);
        Assert.Equal("RoyalViolet", apply);
        Assert.Equal("RoyalViolet", h.Vm.AppTheme);
        // 重挂只是视图/壳层路径：不触发落盘请求
        Assert.Equal(0, h.Spy.Debounced);
        Assert.Equal(0, h.Spy.Immediate);
        Assert.Equal(0, h.ConfigService.SaveCalls);
    }
}
