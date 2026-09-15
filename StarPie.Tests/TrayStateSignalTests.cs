namespace StarPie.Tests;

/// <summary>
/// 托盘状态信号纯决策测试（#151，输入于 #158 由窗口可见性改为控制台开/关）：
/// 关闭设置台固定顺序 Flush → 导航出账 → 图标缓存出账 → Minimized → GC（出账先于 GC，
/// 且落盘严格先于任何释放动作）；重开读 Restored 并先重放导航；退出态不发任何信号；
/// 后台静默形态（--background，e2e）出账动作禁用、消息照发。
/// </summary>
public sealed class TrayStateSignalTests
{
    [Fact]
    public void 关闭设置台_固定顺序_落盘先于任何释放动作且出账先于GC()
    {
        IReadOnlyList<TraySignalStep> steps = TrayStateSignal.Resolve(
            TrayStateChange.ConsoleClosed, isExiting: false, background: false);

        Assert.Equal(
            new[] { TraySignalStep.FlushPendingSave, TraySignalStep.ReleaseNavigation, TraySignalStep.ReleaseIconCaches, TraySignalStep.SendMinimized, TraySignalStep.CollectGarbage },
            steps);
    }

    [Fact]
    public void 关闭设置台_后台形态_出账禁用消息照发()
    {
        IReadOnlyList<TraySignalStep> steps = TrayStateSignal.Resolve(
            TrayStateChange.ConsoleClosed, isExiting: false, background: true);

        Assert.Equal(new[] { TraySignalStep.SendMinimized }, steps);
    }

    [Fact]
    public void 重开设置台_先重放导航再发Restored()
    {
        IReadOnlyList<TraySignalStep> steps = TrayStateSignal.Resolve(
            TrayStateChange.ConsoleOpened, isExiting: false, background: false);

        Assert.Equal(new[] { TraySignalStep.RestoreNavigation, TraySignalStep.SendRestored }, steps);
    }

    [Fact]
    public void 重开设置台_后台形态_不重放只发Restored()
    {
        IReadOnlyList<TraySignalStep> steps = TrayStateSignal.Resolve(
            TrayStateChange.ConsoleOpened, isExiting: false, background: true);

        Assert.Equal(new[] { TraySignalStep.SendRestored }, steps);
    }

    [Fact]
    public void 退出态_开关两种信号都不发()
    {
        Assert.Empty(TrayStateSignal.Resolve(TrayStateChange.ConsoleClosed, isExiting: true, background: false));
        Assert.Empty(TrayStateSignal.Resolve(TrayStateChange.ConsoleOpened, isExiting: true, background: false));
        Assert.Empty(TrayStateSignal.Resolve(TrayStateChange.ConsoleClosed, isExiting: true, background: true));
        Assert.Empty(TrayStateSignal.Resolve(TrayStateChange.ConsoleOpened, isExiting: true, background: true));
    }
}
