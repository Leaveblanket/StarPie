using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.Messaging;
using WinPieGestures.ViewModels.Wheel;

namespace WinPieGestures.Tests;

/// <summary>
/// 外观聚合页 VM 的薄页壳行为覆盖（#56/ADR-0014 决策 6）：只暴露两个设置子 VM（界面主题
/// InterfaceTheme + 轮盘外观 WheelAppearance）、配置导入后的页面级重挂编排（子 VM 各自自订阅
/// ConfigImportedMessage 重挂；聚合壳广播 PageConfigReloadedMessage 通知页面 View）、Dispose 链
/// （释放两子 VM，成对退订语言事件）。轮盘外观状态/命令的逐项行为测试已迁移到
/// <see cref="WheelAppearanceSettingsViewModelTests"/>。
/// </summary>
public sealed class AppearanceSettingsViewModelTests
{
    /// <summary>
    /// 页面级消息记录器：PageConfigReloadedMessage（外观页 View 重绘实时预览的广播目标）与
    /// AppThemeChangedMessage（壳层主窗口主题应用的广播目标）。
    /// </summary>
    private sealed class PageReloadLog
    {
        public List<Type> ReloadedPages { get; } = new();
        public List<string> AppliedThemes { get; } = new();

        public static PageReloadLog Attach(WeakReferenceMessenger messenger)
        {
            var log = new PageReloadLog();
            messenger.Register<PageConfigReloadedMessage>(log, (_, m) => log.ReloadedPages.Add(m.ViewModelType));
            messenger.Register<AppThemeChangedMessage>(log, (_, m) => log.AppliedThemes.Add(m.Theme));
            return log;
        }
    }

    private sealed class Harness
    {
        public TestConfigService ConfigService { get; }
        public TestDialogService Dialogs { get; } = new();
        public LocalizationService Localization { get; } = new();
        public WeakReferenceMessenger Messenger { get; }
        public SaveSpy Spy { get; }
        public PageReloadLog Reload { get; }
        public ProfileListViewModel ProfileList { get; }
        public InterfaceThemeSettingsViewModel InterfaceTheme { get; }
        public WheelAppearanceSettingsViewModel WheelAppearance { get; }
        public AppearanceSettingsViewModel Vm { get; }

        public Harness(AppConfig? config = null)
        {
            ConfigService = new TestConfigService { Current = config ?? new AppConfig() };
            var (messenger, spy) = SaveSpy.Create();
            Messenger = messenger;
            Spy = spy;
            Reload = PageReloadLog.Attach(messenger);

            ProfileList = new ProfileListViewModel(
                ConfigService.Current.Profiles, Dialogs, messenger, new TestActionExecutor(), Localization);
            InterfaceTheme = new InterfaceThemeSettingsViewModel(ConfigService, messenger, Localization);
            WheelAppearance = new WheelAppearanceSettingsViewModel(
                ConfigService, Dialogs, messenger, ProfileList, Localization);
            Vm = new AppearanceSettingsViewModel(messenger, InterfaceTheme, WheelAppearance);
        }
    }

    // --- 子 VM 暴露与薄壳形态 -------------------------------------------------------

    [Fact]
    public void Constructor_InjectsAndExposesBothChildViewModels()
    {
        // #54/#56（ADR-0014 决策 6）：外观聚合 VM 只暴露界面主题子 VM 与轮盘外观子 VM 单例——
        // 页面各设置卡 DataContext 经本壳取对应子 VM。
        var h = new Harness();

        Assert.Same(h.InterfaceTheme, h.Vm.InterfaceTheme);
        Assert.Same(h.WheelAppearance, h.Vm.WheelAppearance);
    }

    [Fact]
    public void AggregateShell_NoLongerImplementsPreviewStateInterface_WheelChildDoes()
    {
        // #56（ADR-0014 决策 8）：预览只读状态接口实现随轮盘外观状态迁移到子 VM——聚合壳不再
        // 实现 IWheelAppearanceState，页面预览 code-behind 经 WheelAppearance 子 VM 取只读状态。
        var h = new Harness();

        Assert.Null(h.Vm as IWheelAppearanceState);
        Assert.IsAssignableFrom<IWheelAppearanceState>(h.Vm.WheelAppearance);
    }

    // --- 配置导入后的重挂编排与页面级消息广播 ----------------------------------------

    [Fact]
    public void ConfigImport_OrchestratesChildrenReload_BroadcastsPageReload_AndSingleThemeApply()
    {
        // #54/#56：导入成功 → 界面主题子 VM 重挂并只发一条 AppThemeChangedMessage（壳层执行窗口
        // 主题应用）；轮盘外观子 VM 自订阅重挂（重建配色下拉/恢复选中）；聚合壳广播
        // PageConfigReloadedMessage(typeof 外观聚合 VM) 通知页面 View 重绘实时预览。
        var h = new Harness(new AppConfig { AppTheme = "Dark", Theme = "CustomPreset_p1" });
        var imported = new AppConfig
        {
            AppTheme = "RoyalViolet",
            Theme = "CustomPreset_p9",
            CustomColorPresets = new List<CustomColorPreset>
            {
                new() { Id = "p9", Name = "导入预设" }
            }
        };
        h.ConfigService.Current = imported;

        h.Messenger.Send(new ConfigImportedMessage(imported));

        // 主题应用只发一条（子 VM 独占；聚合壳自身不发布主题应用）。
        var apply = Assert.Single(h.Reload.AppliedThemes);
        Assert.Equal("RoyalViolet", apply);
        // 页面级收尾广播：外观页 View 订阅 PageConfigReloadedMessage 后重绘预览。
        Assert.Contains(typeof(AppearanceSettingsViewModel), h.Reload.ReloadedPages);
        // 轮盘子 VM 已从新配置重挂：重建 ThemeOptions 并恢复选中。
        Assert.Equal("CustomPreset_p9", h.WheelAppearance.SelectedTheme);
        Assert.Contains(h.WheelAppearance.ThemeOptions, o => o.Tag == "CustomPreset_p9" && o.Label.Contains("导入预设"));
        // 重挂只是视图/壳层路径：不触发落盘请求。
        Assert.Equal(0, h.Spy.Debounced);
        Assert.Equal(0, h.Spy.Immediate);
        Assert.Equal(0, h.ConfigService.SaveCalls);
    }

    [Fact]
    public void ConfigImport_BroadcastsPageReload_WithoutRequiringAggregateState()
    {
        // #56 薄壳语义：聚合壳自身不持有轮盘外观状态——导入广播的处理只做页面级收尾，状态重挂
        // 全部由子 VM 自订阅完成。
        var h = new Harness();
        var imported = new AppConfig { AppTheme = "MidnightNavy", Theme = "Dark" };
        h.ConfigService.Current = imported;

        h.Messenger.Send(new ConfigImportedMessage(imported));

        Assert.Contains(typeof(AppearanceSettingsViewModel), h.Reload.ReloadedPages);
        // 界面主题子 VM 自订阅重挂：导入后补发主题应用消息由壳层执行窗口主题应用（#54 语义）。
        Assert.Equal("MidnightNavy", Assert.Single(h.Reload.AppliedThemes));
        Assert.Equal("Dark", h.WheelAppearance.SelectedTheme);
    }

    // --- Dispose 链 ----------------------------------------------------------------

    [Fact]
    public void Dispose_ReleasesBothChildViewModels_LanguageUnsubscribed()
    {
        // #56/ADR-0014 决策 6（Dispose 链）：聚合壳 Dispose 释放两个设置子 VM——各自成对退订
        // ADR-0010 语言事件；幂等（重复 Dispose/容器再释放安全）。
        var h = new Harness(new AppConfig { AppTheme = "Dark" });
        int themeNotifications = 0;
        int wheelNotifications = 0;
        h.InterfaceTheme.PropertyChanged += (_, _) => themeNotifications++;
        h.WheelAppearance.PropertyChanged += (_, _) => wheelNotifications++;
        var original = h.Localization.CurrentLanguage;
        try
        {
            // 前置验证：子 VM 已订阅语言事件（切语重建驻留目录并补发选中通知）。
            h.Localization.SetLanguage(LanguageCode.En);
            Assert.True(themeNotifications > 0);
            Assert.True(wheelNotifications > 0);
            string enThemeLabel = h.InterfaceTheme.AppThemeOptions[2].Label;
            string enWheelLabel = h.WheelAppearance.ThemeOptions[1].Label;

            themeNotifications = 0;
            wheelNotifications = 0;
            h.Vm.Dispose();
            h.Vm.Dispose(); // 幂等

            h.Localization.SetLanguage(LanguageCode.Ja);

            // 退订后切语不再重建驻留目录/补发选中通知。
            Assert.Equal(enThemeLabel, h.InterfaceTheme.AppThemeOptions[2].Label);
            Assert.Equal(enWheelLabel, h.WheelAppearance.ThemeOptions[1].Label);
            Assert.Equal(0, themeNotifications);
            Assert.Equal(0, wheelNotifications);
        }
        finally
        {
            h.Localization.SetLanguage(original);
        }
    }
}
