using System;
using System.Windows.Threading;
using StarPie.Kernel.ShellIntegration;

namespace StarPie.Services.Shell
{
    /// <summary>
    /// 高权限窗口前台的常驻取样节拍：按固定间隔取前台窗口的完整性级别，一旦满足
    /// <see cref="ElevatedWindowNotice.ShouldReport"/> 即报一次气泡并自停。
    /// </summary>
    /// <remarks>
    /// 本类只是机制（节拍 + 自停），判据全部在共享内核的 <see cref="ElevatedWindowNotice"/>；
    /// 系统调用与配置标记均经注入委托（可测缝是委托，探测本身不单测）。
    /// 取样间隔 1 秒：用户切进高权限窗口后即时报到，而探测只是几次轻量系统调用。
    /// 实例创建在 UI 线程，DispatcherTimer 亦在 UI 线程触发。
    /// 调用方（壳层）以 <see cref="ElevatedWindowNotice.ShouldWatch"/> 为创建门：
    /// 提权态或已提示过时根本不取样，故不会出现"注定无果的常驻空转"。
    /// </remarks>
    internal sealed class ForegroundElevationWatcher : IDisposable
    {
        private static readonly TimeSpan SampleInterval = TimeSpan.FromSeconds(1);

        private readonly DispatcherTimer _timer;
        private readonly Func<bool> _isElevated;
        private readonly Func<bool?> _probeHigherIntegrity;
        private readonly Func<bool> _isReported;
        private readonly Action _report;
        private bool _disposed;

        /// <param name="isElevated">本进程是否已提权。</param>
        /// <param name="probeHigherIntegrity">前台窗口完整性级别探测；返回 null 表示未知。</param>
        /// <param name="isReported">本安装是否已提示过（config.json 标记）。</param>
        /// <param name="report">报出这一次气泡。</param>
        public ForegroundElevationWatcher(
            Func<bool> isElevated,
            Func<bool?> probeHigherIntegrity,
            Func<bool> isReported,
            Action report)
        {
            _isElevated = isElevated ?? throw new ArgumentNullException(nameof(isElevated));
            _probeHigherIntegrity = probeHigherIntegrity ?? throw new ArgumentNullException(nameof(probeHigherIntegrity));
            _isReported = isReported ?? throw new ArgumentNullException(nameof(isReported));
            _report = report ?? throw new ArgumentNullException(nameof(report));

            _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = SampleInterval };
            _timer.Tick += OnTick;
        }

        /// <summary>开始取样。</summary>
        public void Start()
        {
            if (_disposed) return;
            _timer.Start();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _timer.Stop();
            _timer.Tick -= OnTick;
        }

        private void OnTick(object? sender, EventArgs e)
        {
            bool? higher = _probeHigherIntegrity();

            // 探测失败（null）按未知处理：不提示，也不停表——前台窗口随时会换，未知不是终局。
            if (!ElevatedWindowNotice.ShouldReport(_isElevated(), _isReported(), higher)) return;

            _timer.Stop();
            _report();
        }
    }
}
