using System.Threading;
using StarPie.Kernel.ShellIntegration;

namespace StarPie.Tests;

/// <summary>
/// 接管握手信道的可自动覆盖面（#170）：命名对象的**名字形状**与**就绪判据的语义**。
/// 跨完整性级别的真实接管不提权的 xUnit 环境复现不了（沿用 ADR-0040 的验收口径，进人工验收清单），
/// 故这里也**不触碰真实标记事件**——对全机命名对象置位会误伤同机正在运行的实例。
/// </summary>
public sealed class InstanceHandoverTests
{
    // --- 名字形状：形态与权限态都编码在名字里，且 dev 绝不与正式版共用 -------------------

    [Fact]
    public void BuildOwnerMarkerName_EncodesPrivilegeAndStaysInTheGlobalNamespace()
    {
        // 全局命名空间：接管的两个实例可能处于不同完整性级别，会话内对象（Local\）不够用。
        Assert.Equal(@"Global\StarPie_InstanceOwner_Normal", InstanceHandover.BuildOwnerMarkerName(devInstance: false, elevated: false));
        Assert.Equal(@"Global\StarPie_InstanceOwner_High", InstanceHandover.BuildOwnerMarkerName(devInstance: false, elevated: true));
    }

    [Fact]
    public void BuildOwnerMarkerName_DevInstanceCarriesItsOwnSuffix()
    {
        // 与计划任务名、配置目录同口径：dev 的握手对象绝不与正式版共用——
        // 共用会让 dev 的提权尝试去接管正式版实例（反之亦然）。
        string dev = InstanceHandover.BuildOwnerMarkerName(devInstance: true, elevated: false);
        string release = InstanceHandover.BuildOwnerMarkerName(devInstance: false, elevated: false);

        Assert.EndsWith("_Dev", dev);
        Assert.StartsWith("StarPie_InstanceOwner", dev.Substring(@"Global\".Length));
        Assert.NotEqual(release, dev);
    }

    [Fact]
    public void YieldTimeout_IsAMatterOfSeconds()
    {
        // 超时取一个明确的、数秒量级的上限：太长则失败要用户干等，太短则正常的落盘让位来不及。
        Assert.Equal(TimeSpan.FromSeconds(5), InstanceHandover.YieldTimeout);
    }

    // --- 就绪判据：只看互斥体是否已可取得，不看任何触发命令的退出码 ---------------------

    [Fact]
    public void WaitForSingleInstanceRelease_TimesOutWhileTheFirstInstanceStillHoldsTheGate()
    {
        using var mutex = new Mutex();
        mutex.WaitOne();   // 首实例（本线程）持有单实例互斥体

        bool acquired = true;
        var newInstance = new Thread(() =>
            acquired = InstanceHandover.WaitForSingleInstanceRelease(mutex, TimeSpan.FromMilliseconds(150)));
        newInstance.Start();
        newInstance.Join();

        // 等不到让位即失败——不因为"请求已经发出去了"而当成接管成立。
        Assert.False(acquired);
        mutex.ReleaseMutex();
    }

    [Fact]
    public void WaitForSingleInstanceRelease_SucceedsOnceTheFirstInstanceLetsGo()
    {
        using var mutex = new Mutex();
        mutex.WaitOne();

        bool acquired = false;
        var newInstance = new Thread(() =>
        {
            acquired = InstanceHandover.WaitForSingleInstanceRelease(mutex, TimeSpan.FromSeconds(2));
            if (acquired)
            {
                mutex.ReleaseMutex();
            }
        });
        newInstance.Start();

        Thread.Sleep(80);          // 首实例走完落盘与释壳，这才释放闸门
        mutex.ReleaseMutex();
        newInstance.Join();

        Assert.True(acquired);
    }

    // --- 接收端的降级：让位不可达时静默不等待，收尾可重入 -----------------------------

    [Fact]
    public void Listener_StartAndDispose_AreSafeAndIdempotent()
    {
        // 标记未发布（或本进程不是首实例）时 Start 静默降级成"让位不可达"，不抛也不阻塞；
        // Dispose 可重入——壳层收尾与 App.OnExit 都可能碰到它。
        var listener = new InstanceHandoverListener(onYieldRequested: static () => { });

        listener.Start();
        listener.Dispose();
        listener.Dispose();
    }
}
