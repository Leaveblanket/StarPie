using StarPie.Services.Messages;

namespace StarPie.Tests;

/// <summary>
/// 托盘状态信号纯决策测试（#151）：进托盘固定顺序 Flush → Minimized → GC（出账先于 GC）；
/// 恢复读 Restored；退出态不发任何信号；后台静默形态（--background，e2e）出账动作禁用、消息照发。
/// </summary>
public sealed class TrayVisibilitySignalTests
{
    [Fact]
    public void 隐藏到托盘_固定顺序_出账先于GC()
    {
        IReadOnlyList<TraySignalStep> steps = TrayVisibilitySignal.Resolve(visible: false, isExiting: false, background: false);

        Assert.Equal(
            new[] { TraySignalStep.FlushPendingSave, TraySignalStep.ReleaseNavigation, TraySignalStep.SendMinimized, TraySignalStep.CollectGarbage },
            steps);
    }

    [Fact]
    public void 隐藏到托盘_后台形态_出账禁用消息照发()
    {
        IReadOnlyList<TraySignalStep> steps = TrayVisibilitySignal.Resolve(visible: false, isExiting: false, background: true);

        Assert.Equal(new[] { TraySignalStep.SendMinimized }, steps);
    }

    [Fact]
    public void 恢复显示_先重放导航再发Restored()
    {
        IReadOnlyList<TraySignalStep> steps = TrayVisibilitySignal.Resolve(visible: true, isExiting: false, background: false);

        Assert.Equal(new[] { TraySignalStep.RestoreNavigation, TraySignalStep.SendRestored }, steps);
    }

    [Fact]
    public void 恢复显示_后台形态_不重放只发Restored()
    {
        IReadOnlyList<TraySignalStep> steps = TrayVisibilitySignal.Resolve(visible: true, isExiting: false, background: true);

        Assert.Equal(new[] { TraySignalStep.SendRestored }, steps);
    }

    [Fact]
    public void 退出态_两种信号都不发()
    {
        Assert.Empty(TrayVisibilitySignal.Resolve(visible: false, isExiting: true, background: false));
        Assert.Empty(TrayVisibilitySignal.Resolve(visible: true, isExiting: true, background: false));
        Assert.Empty(TrayVisibilitySignal.Resolve(visible: false, isExiting: true, background: true));
        Assert.Empty(TrayVisibilitySignal.Resolve(visible: true, isExiting: true, background: true));
    }
}
