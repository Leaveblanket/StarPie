using System;
using System.Collections.Generic;

namespace StarPie.Kernel.ShellIntegration
{
    /// <summary>
    /// 主框架可见性 → 托盘状态信号的纯决策：动作序列即语义——
    /// 进托盘按固定顺序 FlushPendingSave → 导航视图出账 → 发 MinimizedToTrayMessage
    /// （订阅方同步出账）→ CollectGarbage 后台执行（出账先于 GC）；恢复按最后导航槽位
    /// 重放导航重建视图后发 RestoredFromTrayMessage；退出态两者都不发；
    /// 后台静默形态（--background，e2e）出账动作禁用、消息照发。
    /// </summary>
    public enum TraySignalStep
    {
        FlushPendingSave,
        ReleaseNavigation,
        ReleaseIconCaches,
        SendMinimized,
        CollectGarbage,
        RestoreNavigation,
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
                // 恢复：先按最后导航槽位重放导航重建视图（选中态回灌），再发恢复信号。
                return background
                    ? new[] { TraySignalStep.SendRestored }
                    : new[] { TraySignalStep.RestoreNavigation, TraySignalStep.SendRestored };
            }

            // 进托盘：固定顺序；后台形态出账动作（落盘/导航出账/图标缓存/GC）禁用，消息照发。
            return background
                ? new[] { TraySignalStep.SendMinimized }
                : new[] { TraySignalStep.FlushPendingSave, TraySignalStep.ReleaseNavigation, TraySignalStep.ReleaseIconCaches, TraySignalStep.SendMinimized, TraySignalStep.CollectGarbage };
        }
    }
}
