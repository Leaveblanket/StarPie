using System;
using System.Runtime.Versioning;
using System.Threading;

namespace StarPie.Kernel.ShellIntegration
{
    /// <summary>
    /// 让位请求的接收端（常驻）：在后台线程上等提权新实例的让位请求，收到即回调让位动作
    /// （壳层接的是既有退出编排：落盘 → 释托盘 → 关闭——托盘先于单实例互斥体释放，
    /// 故新实例接手时通知区里不会有两个图标）。
    /// </summary>
    /// <remarks>
    /// 只装在**非提权**首实例上：判定严格单向，提权实例永不让位（见
    /// <see cref="SingleInstanceGate.AcceptsHandoverRequest"/>），故它不装接收端，也就不会被任何信号关停。
    /// 标记事件由首实例在拿到单实例互斥体时发布（见 <see cref="InstanceHandover.PublishOwnerMarker"/>），
    /// 本类只按名字打开它——事件对象归首实例所有，置位方（提权新实例）打开的是同一个对象。
    /// 回调在后台线程上触发，切回工作线程是调用方的事（壳层走 Dispatcher）。
    /// 等待用 <see cref="WaitHandle.WaitAny(WaitHandle[], int)"/> 与停止信号配对：收尾时不误触让位。
    /// </remarks>
    [SupportedOSPlatform("windows")]
    public sealed class InstanceHandoverListener : IDisposable
    {
        private readonly Action _onYieldRequested;
        private readonly EventWaitHandle _stop = new(false, EventResetMode.ManualReset);
        private EventWaitHandle? _yieldRequest;
        private Thread? _thread;
        private bool _disposed;

        /// <summary>构造接收端（尚未开始等待，见 <see cref="Start"/>）。</summary>
        public InstanceHandoverListener(Action onYieldRequested)
            => _onYieldRequested = onYieldRequested ?? throw new ArgumentNullException(nameof(onYieldRequested));

        /// <summary>
        /// 开始等待让位请求（幂等）。标记事件尚未发布时静默不等待——退回"让位不可达"，
        /// 新实例那条路会以超时收场，本实例行为不变。
        /// </summary>
        public void Start()
        {
            if (_disposed || _thread != null)
            {
                return;
            }

            _yieldRequest = InstanceHandover.OpenYieldRequestEvent();
            if (_yieldRequest == null)
            {
                return;
            }

            _thread = new Thread(Listen) { IsBackground = true, Name = "StarPie.InstanceHandover" };
            _thread.Start();
        }

        private void Listen()
        {
            try
            {
                // 让位只受理一次：首实例随即走退出编排，进程不再需要接收端。
                bool stopped = WaitHandle.WaitAny(new WaitHandle[] { _yieldRequest!, _stop }) == 1;
                if (!stopped)
                {
                    _onYieldRequested();
                }
            }
            catch
            {
                // 句柄在等待中被释放（进程收尾）：按停止处理，不把异常抛到后台线程外。
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            try { _stop.Set(); } catch { }
            _yieldRequest?.Dispose();
            _yieldRequest = null;
            _stop.Dispose();
        }
    }
}
