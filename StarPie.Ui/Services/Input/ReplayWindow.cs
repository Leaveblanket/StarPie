using System;
using System.Threading;

namespace StarPie.Ui.Services.Input
{
    /// <summary>
    /// 回放窗口：补发的点击由本进程注入，注入事件会再回到输入栈；
    /// 窗口内到达的注入事件不参与手势，直接放行。
    /// </summary>
    /// <remarks>
    /// SharpHook 的 <c>UioHookEvent</c> 不暴露 <c>dwExtraInfo</c>，社区通行的注入戳记
    /// （注入时写魔数、捕获侧见戳记即跳过）在捕获侧不可移植（ADR-0052），故以
    /// 「期望事件数 + 截止时刻」代替戳记。一次回放是「按下 + 抬起」两件事。
    /// 已知残差风险：外部注入同样带模拟标记，窗口内恰好到达会被误吞。
    /// </remarks>
    internal sealed class ReplayWindow
    {
        /// <summary>一次回放期望回到输入栈的事件数（触发键按下 + 抬起）。</summary>
        private const int ExpectedEvents = 2;

        /// <summary>窗口存活上限：注入事件丢失时窗口不会永久敞开。</summary>
        private const long LifetimeMilliseconds = 1000;

        private int _remaining;
        private long _deadline;

        /// <summary>开窗：必须在注入之前调用，覆盖紧随其后的按下与抬起。</summary>
        public void Open()
        {
            Volatile.Write(ref _deadline, Environment.TickCount64 + LifetimeMilliseconds);
            Interlocked.Exchange(ref _remaining, ExpectedEvents);
        }

        /// <summary>
        /// 吃掉一个配额并报告该事件是否属于本窗口。只认注入来源（模拟标记）——
        /// 窗口内到达的真实输入必须照常参与手势。
        /// </summary>
        public bool TryConsume(bool isSimulated)
        {
            if (!isSimulated) return false;
            if (Volatile.Read(ref _remaining) == 0) return false;

            if (Environment.TickCount64 > Volatile.Read(ref _deadline))
            {
                Interlocked.Exchange(ref _remaining, 0);
                return false;
            }

            Interlocked.Decrement(ref _remaining);
            return true;
        }
    }
}
