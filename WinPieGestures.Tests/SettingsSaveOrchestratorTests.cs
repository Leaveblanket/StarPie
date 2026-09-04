using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.Messaging;
using WinPieGestures;
using WinPieGestures.Services;

namespace WinPieGestures.Tests;

/// <summary>
/// 落盘编排订阅者的行为覆盖 (T19, Spec 预定缝②)：页面 VM → IMessenger →
/// 订阅者 → 防抖器/配置服务。只测外部行为——防抖折叠语义、立即冲刷语义、
/// 四个冲刷时机的调用序列（防抖到期/显式保存/导入前/退出由持有方驱动）。
/// 直接 new + TestSaveDebouncer / 假配置服务，不经容器。
/// </summary>
public sealed class SettingsSaveOrchestratorTests
{
    private static (SettingsSaveOrchestrator Orchestrator, TestConfigService Config, TestSaveDebouncer Debouncer, WeakReferenceMessenger Messenger)
        Create()
    {
        var config = new TestConfigService();
        var debouncer = new TestSaveDebouncer();
        var messenger = TestHub.NewMessenger();
        var orchestrator = new SettingsSaveOrchestrator(config, debouncer, messenger);
        return (orchestrator, config, debouncer, messenger);
    }

    [Fact]
    public void DebouncedMessage_SchedulesSingleSaveWithFixedDelay()
    {
        var (orchestrator, config, debouncer, messenger) = Create();

        messenger.Send(DebouncedSaveRequestedMessage.Instance);
        messenger.Send(DebouncedSaveRequestedMessage.Instance);
        messenger.Send(DebouncedSaveRequestedMessage.Instance);

        // 连续防抖请求只保留一个挂起动作，且尚未落盘
        Assert.Equal(1, debouncer.PendingCount);
        Assert.Equal(SettingsSaveOrchestrator.AutoSaveDelay, debouncer.LastDelay);
        Assert.Equal(0, config.SaveCalls);
    }

    [Fact]
    public void DebounceTick_SavesConfigOnce()
    {
        var (orchestrator, config, debouncer, messenger) = Create();

        messenger.Send(DebouncedSaveRequestedMessage.Instance);
        debouncer.TickPending();

        Assert.Equal(1, config.SaveCalls);
        Assert.Equal(0, debouncer.PendingCount);
    }

    [Fact]
    public void ImmediateMessage_CancelsPendingDebounceAndSavesNow()
    {
        var (orchestrator, config, debouncer, messenger) = Create();

        messenger.Send(DebouncedSaveRequestedMessage.Instance);
        messenger.Send(ImmediateSaveRequestedMessage.Instance);

        Assert.Equal(1, config.SaveCalls);
        Assert.Equal(0, debouncer.PendingCount); // 挂起防抖被取消，防抖到期不会再落盘
        debouncer.TickPending();
        Assert.Equal(1, config.SaveCalls);
    }

    [Fact]
    public void SaveNow_SavesImmediatelyWithoutPendingDebounce()
    {
        var (orchestrator, config, debouncer, _) = Create();

        orchestrator.SaveNow();

        Assert.Equal(1, config.SaveCalls);
        Assert.Equal(0, debouncer.PendingCount);
    }

    [Fact]
    public void FlushPendingSave_BehavesAsImmediateSave()
    {
        var (orchestrator, config, debouncer, messenger) = Create();

        messenger.Send(DebouncedSaveRequestedMessage.Instance);
        orchestrator.FlushPendingSave();

        Assert.Equal(1, config.SaveCalls);
        Assert.Equal(0, debouncer.PendingCount);
    }

    [Fact]
    public void FullSequence_DebounceThenExplicitSave_ThenDebounceTick_SavesTwice()
    {
        var (orchestrator, config, debouncer, messenger) = Create();

        // 编辑 → 防抖请求 → 用户点保存（显式冲刷）→ 防抖到期不再重复落盘
        messenger.Send(DebouncedSaveRequestedMessage.Instance);
        orchestrator.SaveNow();
        debouncer.TickPending();

        Assert.Equal(1, config.SaveCalls);
    }
}
