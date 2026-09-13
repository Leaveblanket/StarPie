using System;
using System.Collections.Generic;

namespace StarPie.Kernel.ShellIntegration
{
    /// <summary>
    /// 主框架可见性 → 托盘状态信号的纯决策：动作序列即语义——
    /// 进托盘按固定顺序 FlushPendingSave → 发 MinimizedToTrayMessage（订阅方同步出账）→
    /// CollectGarbage 后台执行（出账先于 GC）；恢复发 RestoredFromTrayMessage；退出态两者都不发；
    /// 后台静默形态（--background，e2e）出账动作禁用、消息照发。
    /// </summary>
    public enum TraySignalStep
    {
        FlushPendingSave,
        SendMinimized,
        CollectGarbage,
        SendRestored,
    }

    public static class TrayVisibilitySignal
    {
        /// <summary>解析一次可见性变化为有序动作序列（无状态、无副作用；调用方持有全部动作）。</summary>
        public static IReadOnlyList<TraySignalStep> Resolve(bool visible, bool isExiting, bool background)
        {
            if (isExiting)
            {
                return Array.Empty<TraySignalStep>();
            }

            if (visible)
            {
                return new[] { TraySignalStep.SendRestored };
            }

            // 进托盘：固定顺序；后台形态出账动作（落盘/GC）禁用，消息照发。
            return background
                ? new[] { TraySignalStep.SendMinimized }
                : new[] { TraySignalStep.FlushPendingSave, TraySignalStep.SendMinimized, TraySignalStep.CollectGarbage };
        }
    }
}
