using System;

namespace WinPieGestures.Services.Configuration
{
    /// <summary>
    /// 落盘防抖器 (T17)：把连续的自动保存请求折叠为一次延迟执行——每次 <see cref="Schedule"/>
    /// 重新计时并以最新动作为准，<see cref="CancelPending"/> 取消挂起的执行。
    /// 实现限定 UI 线程使用（自动保存请求全部源自设置界面的绑定/事件管线）。
    /// </summary>
    public interface ISaveDebouncer
    {
        /// <summary>调度（或重排）延迟执行；同一时刻至多一个挂起动作，后到的替换先到的。</summary>
        void Schedule(Action action, TimeSpan delay);

        /// <summary>取消挂起的动作（无挂起时为空操作）。</summary>
        void CancelPending();
    }
}
