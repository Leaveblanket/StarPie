using System;
using System.Collections.Generic;

namespace StarPie.ShellIntegration
{
    /// <summary>托盘退出的编排步骤。</summary>
    public enum ShellExitStep
    {
        /// <summary>兜底落盘：冲刷挂起的防抖保存并立即落盘（释放序列的第一步）。</summary>
        FlushPendingSave,

        /// <summary>释放托盘（含摘除挂在托盘消息窗口上的常驻钩子）。</summary>
        ReleaseTray,

        /// <summary>请求 WPF 应用关闭（`ShutdownMode=OnExplicitShutdown`：这一步才是真退出）。</summary>
        ShutdownApplication,
    }

    /// <summary>
    /// 托盘退出的固定顺序（纯决策，无输入参数）：落盘 → 释壳 → 应用关闭。
    /// </summary>
    /// <remarks>
    /// 无输入参数即语义：退出编排不依赖设置台是否存在（无控制台时必须同样能走完），
    /// 也不依赖任何运行态分支——退出只有这一条路。落盘严格在释放动作之前：托盘释放会摘掉
    /// 常驻钩子，应用关闭后 `App.OnExit` 才兜底保存配置并释放容器与单实例互斥体，那时页面 VM
    /// 已不可达，挂起的页内改动只有这一步能保住。
    /// </remarks>
    public static class ShellExitSequence
    {
        /// <summary>托盘退出的有序步骤（调用方持有全部动作）。</summary>
        public static IReadOnlyList<ShellExitStep> Resolve() => new[]
        {
            ShellExitStep.FlushPendingSave,
            ShellExitStep.ReleaseTray,
            ShellExitStep.ShutdownApplication,
        };
    }
}
