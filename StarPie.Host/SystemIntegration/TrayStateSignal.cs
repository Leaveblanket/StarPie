using System;
using System.Collections.Generic;

namespace StarPie.Host.SystemIntegration
{
    /// <summary>
    /// 主框架可见性 → 托盘状态信号的纯决策：动作序列即语义——
    /// 进托盘按固定顺序 FlushPendingSave → 导航视图出账 → 发 MinimizedToTrayMessage
    /// （订阅方同步出账）→ CollectGarbage 后台执行（出账先于 GC）；恢复按最后导航槽位
    /// 重放导航重建视图后发 RestoredFromTrayMessage；退出态两者都不发。
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

    /// <summary>控制台状态变化：设置台开窗 / 关闭——托盘状态信号的输入。</summary>
    public enum TrayStateChange
    {
        /// <summary>设置台已打开（启动时建立，或关闭后重开）。</summary>
        ConsoleOpened,

        /// <summary>设置台已关闭（关窗即销毁，托盘驻留由常驻壳层承担）。</summary>
        ConsoleClosed,
    }

    public static class TrayStateSignal
    {
        /// <summary>解析一次控制台状态变化为有序动作序列（无状态、无副作用；调用方持有全部动作）。</summary>
        /// <remarks>
        /// 输入是**控制台开/关**而不是窗口可见性：设置台是瞬态窗口，新建窗口首次 <c>Show()</c>
        /// 同样产生可见性变化，按可见性判读会把"首次打开"误判成"从托盘恢复"。
        /// </remarks>
        public static IReadOnlyList<TraySignalStep> Resolve(TrayStateChange change, bool isExiting)
        {
            if (isExiting)
            {
                return Array.Empty<TraySignalStep>();
            }

            if (change == TrayStateChange.ConsoleOpened)
            {
                // 重开：先按最后导航槽位重放导航重建视图（选中态回灌），再发恢复信号。
                return new[] { TraySignalStep.RestoreNavigation, TraySignalStep.SendRestored };
            }

            // 进托盘：固定顺序。
            return new[] { TraySignalStep.FlushPendingSave, TraySignalStep.ReleaseNavigation, TraySignalStep.ReleaseIconCaches, TraySignalStep.SendMinimized, TraySignalStep.CollectGarbage };
        }
    }
}
