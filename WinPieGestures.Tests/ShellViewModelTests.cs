using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.Messaging;
using WinPieGestures.Services;

namespace WinPieGestures.Tests;

/// <summary>
/// 主框架壳层 VM 的行为覆盖 (B1/D3，ADR-0016 决策 7)：<see cref="ShellViewModel"/>
/// 承接原 MainViewModel 的壳层职责——WindowTitle 随 I18n 刷新并成对退订、IsExiting 退出
/// 放行置位、Save 落盘请求与成功提示。只测外部行为，直接 new + 替身，不经容器。
/// </summary>
public sealed class ShellViewModelTests
{
    private static readonly LocalizationService Localization = new();

    private static (ShellViewModel Vm, SaveSpy Spy, TestDialogService Dialogs) Create()
    {
        var (messenger, spy) = SaveSpy.Create();
        var dialogs = new TestDialogService();
        var vm = new ShellViewModel(messenger, dialogs, Localization);
        return (vm, spy, dialogs);
    }

    [Fact]
    public void IsExiting_DefaultsFalse_AndIsSettable()
    {
        // B1/D3：App 退出状态归壳层 VM（AppHost 置位、MainView.Closing 读取），View 不反向依赖 Composition。
        var (vm, _, _) = Create();

        Assert.False(vm.IsExiting);

        vm.IsExiting = true;

        Assert.True(vm.IsExiting);
    }

    [Fact]
    public void WindowTitle_ReflectsI18nAndDevSuffix()
    {
        var (vm, _, _) = Create();

        Assert.Equal(Localization.GetString("WindowTitle") + DevInstance.Suffix, vm.WindowTitle);
    }

    [Fact]
    public void LanguageChanged_RaisesWindowTitlePropertyChanged_UntilDisposed()
    {
        // ADR-0010 第 3 条：WindowTitle 由壳层 VM 订阅 I18n 刷新；Dispose 后不再订阅静态事件。
        var (vm, _, _) = Create();
        var original = Localization.CurrentLanguage;
        var changes = new List<string?>();
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ShellViewModel.WindowTitle)) changes.Add(e.PropertyName);
        };
        try
        {
            Localization.SetLanguage("en");

            Assert.Contains(nameof(ShellViewModel.WindowTitle), changes);
            Assert.Equal(Localization.GetString("WindowTitle") + DevInstance.Suffix, vm.WindowTitle);

            changes.Clear();
            vm.Dispose();
            Localization.SetLanguage("ja");
            Assert.DoesNotContain(nameof(ShellViewModel.WindowTitle), changes);
        }
        finally
        {
            Localization.SetLanguage(original);
        }
    }

    [Fact]
    public void Save_SendsImmediateSaveRequest_AndShowsSuccessInfo()
    {
        // B1/D3：Save() 自 MainViewModel 迁入壳层 VM，行为不变——立即落盘请求 + 成功提示。
        var (vm, spy, dialogs) = Create();

        vm.SaveCommand.Execute(null);

        Assert.Equal(1, spy.Immediate);
        Assert.Equal(0, spy.Debounced);
        var call = Assert.Single(dialogs.InfoCalls);
        Assert.Equal(Localization.GetString("Notice"), call.Title);
        Assert.Equal(Localization.GetString("MsgSaveSuccess"), call.Message);
    }
}
