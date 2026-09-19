using System;
using System.Reflection;
using StarPie.Ui.ViewModels.Navigation;

namespace StarPie.Tests;

/// <summary>
/// 托盘退出的固定顺序（ADR-0039 决策 7/8）：落盘 → 释放托盘 → 应用关闭，
/// 且不依赖设置台是否存在（序列无输入参数即"无控制台也要走完"）。
/// 另锁"退出态归常驻壳层"：退出态不住在设置台会话级的 <see cref="WindowChromeViewModel"/> 上。
/// </summary>
public sealed class ExitSequenceTests
{
    [Fact]
    public void 托盘退出_固定顺序_落盘先于释放托盘与应用关闭()
    {
        IReadOnlyList<ExitStep> steps = ExitSequence.Resolve();

        Assert.Equal(
            new[] { ExitStep.FlushPendingSave, ExitStep.ReleaseTray, ExitStep.ShutdownApplication },
            steps);
    }

    [Fact]
    public void 托盘退出_无控制台也能走完_序列不依赖设置台()
    {
        // 无输入参数：退出编排没有"设置台存在/不存在"的分支——无控制台时同样走完。
        Assert.Empty(typeof(ExitSequence)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.Name == nameof(ExitSequence.Resolve))
            .SelectMany(method => method.GetParameters()));
    }

    [Fact]
    public void 退出态_不在设置台会话级VM上()
    {
        // 退出是常驻壳层的编排：设置台只是被关闭。退出态寄居在会话级 VM 上会让 Shutdown 触发的关窗
        // 读到已销毁会话的状态。
        Assert.Null(typeof(WindowChromeViewModel).GetProperty("IsExiting"));
    }
}
