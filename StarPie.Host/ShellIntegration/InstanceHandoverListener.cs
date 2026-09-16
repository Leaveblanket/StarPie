using System;
using System.Runtime.Versioning;
using System.Threading;

namespace StarPie.ShellIntegration
{
    /// <summary>
    /// 接管握手的接收端（常驻）：在后台线程上等提权新实例的两个信号——让位请求与"提权未生效"——
    /// 收到即回调（壳层接的是既有退出编排与既有气泡通道）。托盘先于单实例互斥体释放，
    /// 故新实例接手时通知区里不会有两个图标。
    /// </summary>
    /// <remarks>
    /// 只装在**非提权**首实例上：判定严格单向，提权实例永不让位（见
    /// <see cref="SingleInstanceGate.AcceptsHandoverRequest"/>），故它不装接收端，也就不会被任何信号关停。
    /// 两个事件对象都由首实例在拿到单实例互斥体时发布（见
    /// <see cref="InstanceHandover.PublishOwnerMarker"/> / <see cref="InstanceHandover.PublishElevationFailedEvent"/>），
    /// 本类只按名字打开它们——置位方（提权新实例）打开的是同一个对象。
    /// 让位只受理一次（随即走退出编排）；"提权未生效"受理后继续等——用户解卡后可以再试一次。
    /// 回调在后台线程上触发，切回工作线程是调用方的事（壳层走 Dispatcher）。
    /// 等待用 <see cref="WaitHandle.WaitAny(WaitHandle[])"/> 与停止信号配对：收尾时不误触让位。
    /// </remarks>
    [SupportedOSPlatform("windows")]
    public sealed class InstanceHandoverListener : IDisposable
    {
        /// <summary>让位请求在等待数组中的下标（恒为第一项）。</summary>
        private const int YieldIndex = 0;

        /// <summary>"提权未生效"在等待数组中的下标（事件缺席时该下标即停止信号）。</summary>
        private const int FailureIndex = 1;

        private readonly Action _onYieldRequested;
        private readonly Action _onElevationNotApplied;
        private readonly EventWaitHandle _stop = new(false, EventResetMode.ManualReset);
        private EventWaitHandle? _yieldRequest;
        private EventWaitHandle? _elevationFailed;
        private WaitHandle[]? _waitHandles;
        private bool _hasFailureChannel;
        private Thread? _thread;
        private bool _disposed;

        /// <summary>构造接收端（尚未开始等待，见 <see cref="Start"/>）。</summary>
        /// <param name="onYieldRequested">收到让位请求时回调（壳层在此走既有退出编排）。</param>
        /// <param name="onElevationNotApplied">收到"提权未生效"时回调（壳层在此报气泡）。</param>
        public InstanceHandoverListener(Action onYieldRequested, Action onElevationNotApplied)
        {
            _onYieldRequested = onYieldRequested ?? throw new ArgumentNullException(nameof(onYieldRequested));
            _onElevationNotApplied = onElevationNotApplied ?? throw new ArgumentNullException(nameof(onElevationNotApplied));
        }

        /// <summary>
        /// 开始等待（幂等）。标记事件尚未发布时静默不等待——退回"让位不可达"，新实例那条路会以超时
        /// 收场，本实例行为不变；"提权未生效"事件缺席只让那一条提示降级，不影响让位。
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

            _elevationFailed = InstanceHandover.OpenElevationFailedEvent();
            _hasFailureChannel = _elevationFailed != null;
            _waitHandles = _hasFailureChannel
                ? new WaitHandle[] { _yieldRequest, _elevationFailed!, _stop }
                : new WaitHandle[] { _yieldRequest, _stop };

            _thread = new Thread(Listen) { IsBackground = true, Name = "StarPie.InstanceHandover" };
            _thread.Start();
        }

        private void Listen()
        {
            try
            {
                while (true)
                {
                    int signaled = WaitHandle.WaitAny(_waitHandles!);
                    if (signaled == YieldIndex)
                    {
                        _onYieldRequested();
                        return;
                    }

                    if (_hasFailureChannel && signaled == FailureIndex)
                    {
                        _onElevationNotApplied();
                        continue;
                    }

                    return;   // 停止信号
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
            _elevationFailed?.Dispose();
            _elevationFailed = null;
            _waitHandles = null;
            _stop.Dispose();
        }
    }
}
