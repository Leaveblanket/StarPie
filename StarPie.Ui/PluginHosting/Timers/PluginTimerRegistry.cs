using System;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using StarPie.Abstractions.Ui;

namespace StarPie.PluginHosting.Timers
{
    /// <summary>
    /// 定时器与动画托管：宿主签发 <see cref="DispatcherTimer"/>（可整体停止）；
    /// 动画由宿主附着并记账，摘除必须用 <c>Storyboard.Remove</c>（只 <c>Stop</c> 不清零）。
    /// </summary>
    internal sealed class PluginTimerRegistry
    {
        private readonly PluginUiAssetRegistry _assets;
        private readonly string _pluginId;
        private readonly IUiDispatcher _dispatcher;

        internal PluginTimerRegistry(
            PluginUiAssetRegistry assets,
            string pluginId,
            IUiDispatcher dispatcher)
        {
            _assets = assets;
            _pluginId = pluginId;
            _dispatcher = dispatcher;
        }

        /// <summary>创建宿主签发的定时器并登记。</summary>
        internal IDisposable Create(TimeSpan interval, Action tick)
        {
            ArgumentNullException.ThrowIfNull(tick);
            if (interval <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(interval), "定时器间隔必须为正");
            }

            var timer = new DispatcherTimer(DispatcherPriority.Normal, ResolveDispatcher())
            {
                Interval = interval,
            };
            EventHandler handler = (_, _) => tick();
            timer.Tick += handler;
            timer.Start();

            return new PluginUiAssetHandle(
                _assets,
                _assets.Track(
                    _pluginId,
                    PluginUiAssetKind.Timer,
                    $"定时器 {interval}",
                    () =>
                    {
                        timer.Stop();
                        timer.Tick -= handler;
                        return true;
                    }));
        }

        /// <summary>附着并登记宿主中介动画；摘除 = <c>Remove</c>。</summary>
        internal IDisposable AttachAnimation(FrameworkElement target, Storyboard storyboard)
        {
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(storyboard);

            storyboard.Begin(target, isControllable: true);
            return new PluginUiAssetHandle(
                _assets,
                _assets.Track(
                    _pluginId,
                    PluginUiAssetKind.Animation,
                    $"动画 {storyboard.GetHashCode()}",
                    () =>
                    {
                        // 只 Stop 会把时钟与时间线留在元素上（登记表因此不清零）；Remove 才是真摘除。
                        storyboard.Remove(target);
                        return true;
                    }));
        }

        private Dispatcher ResolveDispatcher()
            => _dispatcher is WpfUiDispatcher wpf
                ? wpf.Dispatcher
                : throw new InvalidOperationException("宿主签发定时器需要 WPF 调度器适配器");
    }
}
