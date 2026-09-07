using System;
using System.Windows.Threading;

namespace StarPie.Services.Configuration
{
    /// <summary>
    /// <see cref="ISaveDebouncer"/> 的 WPF 实现：用 DispatcherTimer 承载计时。
    /// </summary>
    /// <remarks>
    /// Tick 落在 UI 线程；自动保存的请求方（页面 ViewModel 管线）与落盘点
    /// （组合根的 <see cref="SettingsSaveOrchestrator"/>）都在 UI 线程，无需跨线程封送，
    /// 本实现仅限 UI 线程使用。
    /// </remarks>
    public sealed class DispatcherSaveDebouncer : ISaveDebouncer
    {
        private DispatcherTimer? _timer;
        private Action? _pending;

        public void Schedule(Action action, TimeSpan delay)
        {
            _pending = action ?? throw new ArgumentNullException(nameof(action));
            if (_timer == null)
            {
                _timer = new DispatcherTimer();
                _timer.Tick += OnTick;
            }

            // 惰性创建 + 每次重排：连续请求只保留最后一次（防抖语义）
            _timer.Stop();
            _timer.Interval = delay;
            _timer.Start();
        }

        public void CancelPending()
        {
            _timer?.Stop();
            _pending = null;
        }

        private void OnTick(object? sender, EventArgs e)
        {
            _timer?.Stop();
            var action = _pending;
            _pending = null;
            action?.Invoke();
        }
    }
}
