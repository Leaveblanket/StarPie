using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.Messaging;
using WinPieGestures.Services;

namespace WinPieGestures.Tests;

/// <summary>
/// 落盘/导入消息记录器 (T19 测试基建)：注册在测试专用 WeakReferenceMessenger 上，
/// 供页面 ViewModel 测试断言"发过哪些落盘请求/导入广播"——替代迁移前的
/// SaveRequested / SaveDebounceRequested / ConfigImported 事件订阅断言。
/// </summary>
public sealed class SaveSpy
{
    /// <summary>收到的立即落盘请求数（迁移前 SaveRequested 事件计数）。</summary>
    public int Immediate;

    /// <summary>收到的防抖落盘请求数（迁移前 SaveDebounceRequested / AutoSaveRequested 事件计数）。</summary>
    public int Debounced;

    /// <summary>收到的导入广播（携带新配置实例）。</summary>
    public List<ConfigImportedMessage> Imported { get; } = new();

    /// <summary>建一对（专用消息总线, 记录器）：VM 与记录器共用同一总线。</summary>
    public static (WeakReferenceMessenger Messenger, SaveSpy Spy) Create()
    {
        var messenger = TestHub.NewMessenger();
        var spy = new SaveSpy();
        Attach(messenger, spy);
        return (messenger, spy);
    }

    /// <summary>把记录器挂到既有总线（VM 构造需专用总线而记录器由外层测试持有的场景）。</summary>
    public static void Attach(WeakReferenceMessenger messenger, SaveSpy spy)
    {
        messenger.Register<ImmediateSaveRequestedMessage>(spy, (_, _) => spy.Immediate++);
        messenger.Register<DebouncedSaveRequestedMessage>(spy, (_, _) => spy.Debounced++);
        messenger.Register<ConfigImportedMessage>(spy, (_, m) => spy.Imported.Add(m));
    }
}

/// <summary>测试专用消息总线工厂（工程约定：mock 直接 new，不用容器与 Default 单例）。</summary>
public static class TestHub
{
    public static WeakReferenceMessenger NewMessenger() => new();
}

/// <summary>
/// IActionExecutorService 的测试替身 (T19)：不执行真实动作，仅记录请求，
/// 供槽位"测试动作"命令的编排断言。
/// </summary>
public sealed class TestActionExecutor : IActionExecutorService
{
    public List<ActionItem> Executed { get; } = new();

    public void Execute(ActionItem action) => Executed.Add(action);
}
