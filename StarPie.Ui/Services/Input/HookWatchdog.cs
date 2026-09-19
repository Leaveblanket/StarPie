using System;
using System.Threading;
using StarPie.Sdk.Models;

namespace StarPie.Ui.Services.Input
{
    /// <summary>
    /// 钩子看门狗：周期比对「钩子事件计数」与「系统光标位移」——
    /// 光标动过却一个事件都没收到，说明钩子已被系统静默移除，
    /// 立刻交 <c>recover</c> 就地重注册。
    /// </summary>
    /// <remarks>
    /// 判定在定时器线程上跑，探测本身是只读的；周期由注入的 <see cref="TimeProvider"/> 驱动
    /// （生产为系统时钟；测试注入假时钟推进周期），判定入口不开公开面。计数由捕获侧经
    /// <see cref="CountEvent"/> 递交（暂停态同样计数：暂停只是放行事件，不代表钩子还活着）。
    /// </remarks>
    public sealed class HookWatchdog : IDisposable
    {
        /// <summary>默认探针周期（3 秒）。</summary>
        public static readonly TimeSpan DefaultPeriod = TimeSpan.FromSeconds(3);

        private readonly TimeSpan _period;
        private readonly TimeProvider _timeProvider;
        private readonly Func<ScreenPoint?> _cursorProbe;
        private readonly Action _recover;

        private int _eventsSinceLastCheck;
        private ScreenPoint? _lastCursor;
        private ITimer? _timer;

        /// <param name="period">探针周期。</param>
        /// <param name="cursorProbe">系统光标位置探针（读不到返回 null）。</param>
        /// <param name="recover">判定失效时的重注册动作（由捕获侧提供）。</param>
        /// <param name="timeProvider">时钟（默认系统时钟；测试注入假时钟以确定性推进周期探针）。</param>
        public HookWatchdog(TimeSpan period, Func<ScreenPoint?> cursorProbe, Action recover, TimeProvider? timeProvider = null)
        {
            ArgumentNullException.ThrowIfNull(cursorProbe);
            ArgumentNullException.ThrowIfNull(recover);
            _period = period;
            _timeProvider = timeProvider ?? TimeProvider.System;
            _cursorProbe = cursorProbe;
            _recover = recover;
        }

        /// <summary>记一次钩子事件（捕获侧在回调最前面调用，暂停态也调）。</summary>
        public void CountEvent() => Interlocked.Increment(ref _eventsSinceLastCheck);

        /// <summary>取基准光标位置并起周期探针；重复调用无副作用。</summary>
        public void Start()
        {
            if (_timer != null) return;

            _lastCursor = _cursorProbe();
            Interlocked.Exchange(ref _eventsSinceLastCheck, 0);
            _timer = _timeProvider.CreateTimer(_ => CheckOnce(), null, _period, _period);
        }

        /// <summary>停周期探针；重复调用无副作用。</summary>
        public void Stop()
        {
            _timer?.Dispose();
            _timer = null;
        }

        /// <summary>
        /// 单次探测（定时器每到一个周期调用一次）
        /// </summary>
        private void CheckOnce()
        {
            if (_timer == null) return;

            // 读光标位置，判定是否动过。
            ScreenPoint? current = _cursorProbe();
            if (current is null) return;

            bool moved = _lastCursor is not { } last || current.Value.X != last.X || current.Value.Y != last.Y;
            _lastCursor = current;

            // 光标没动 → 清零计数防误报（没有输入就没有事件，不是死亡证据）。
            if (!moved)
            {
                Interlocked.Exchange(ref _eventsSinceLastCheck, 0);
                return;
            }

            // 光标动过但零事件 → 判失效并重注册。
            if (Interlocked.Exchange(ref _eventsSinceLastCheck, 0) == 0)
            {
                _recover();
            }
        }

        public void Dispose() => Stop();
    }
}
