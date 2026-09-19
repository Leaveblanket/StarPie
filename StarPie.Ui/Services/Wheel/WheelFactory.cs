using System;
using System.Windows;
using System.Windows.Threading;
using StarPie.Host.Localization;
using StarPie.Sdk.ViewModels.Wheel;
using StarPie.Ui.ViewModels.Wheel;
using StarPie.Sdk.Services.Wheel;

namespace StarPie.Ui.Services.Wheel
{
    /// <summary>
    /// 轮盘工厂：每次轮盘交互交给引擎一个句柄，句柄把这轮盘交互的视图模型与窗口的构建、
    /// 显示与状态变更依次排进 UI 线程队列。
    /// </summary>
    /// <remarks>
    /// 调用方就是钩子线程（ADR-0052）：钩子线程只做抑制决策，因此这里一律异步投放、
    /// 绝不阻塞等待 UI 线程。同一轮盘的构建与各次状态变更落在同一队列上，FIFO 保序
    /// （构建先于显示；关闭先于紧随其后的动作执行）。消费方（轮盘交互引擎）只依赖
    /// <see cref="IWheelFactory"/> 接口。
    /// </remarks>
    public sealed class WheelFactory : IWheelFactory
    {
        /// <summary>预热用的全局方案名（无专属方案时的兜底方案）。</summary>
        private const string GlobalProfileName = "Global";

        /// <summary>预热的虚拟触发点：只用于构造视图模型，不参与窗口定位。</summary>
        private static readonly ScreenPoint WarmupCenter = new ScreenPoint(200, 200);

        private readonly IConfigService _config;
        private readonly Func<bool> _windowsInDarkModeProbe;
        private readonly ILocalizationService _localization;
        private readonly IIconAssetService _iconAssets;

        public WheelFactory(
            IConfigService config,
            Func<bool> windowsInDarkModeProbe,
            ILocalizationService localization,
            IIconAssetService iconAssets)
        {
            _config = config;
            _windowsInDarkModeProbe = windowsInDarkModeProbe ?? throw new ArgumentNullException(nameof(windowsInDarkModeProbe));
            _localization = localization ?? throw new ArgumentNullException(nameof(localization));
            _iconAssets = iconAssets ?? throw new ArgumentNullException(nameof(iconAssets));
        }

        public IWheelViewModel Create(ScreenPoint center, WheelProfile profile)
        {
            Dispatcher dispatcher = Application.Current.Dispatcher;
            // 构建延后到 UI 线程队列的队首工作项：本方法由钩子线程调用，不能在这里等 UI 线程。
            // 每次轮盘交互从运行态配置快照组装投影：轮盘弹出期间改配置不回流。
            return new DispatchedWheelViewModel(
                dispatcher,
                () => new WheelViewModel(center, profile, WheelViewData.FromConfig(_config.Current), _localization),
                viewModel => new RadialWindow(viewModel, _windowsInDarkModeProbe, _localization, _iconAssets));
        }

        /// <summary>启动期预热：本方法装配预热所需的一切——取全局方案（缺失即空方案）构造
        /// 与轮盘交互同形的视图模型，再离屏渲染一次。调用方（常驻壳层）不必知道 Profile 查找语义
        /// 与预热方式，调用方不必知道这些装配细节。</summary>
        public void Warmup()
        {
            WheelProfile profile = _config.Current.Profiles.Find(p => p.ProcessName == GlobalProfileName) ?? new WheelProfile();
            var viewModel = new WheelViewModel(WarmupCenter, profile, WheelViewData.FromConfig(_config.Current), _localization);
            WheelWarmup.Run(viewModel, _windowsInDarkModeProbe, _localization, _iconAssets);
        }

        /// <summary>把一轮轮盘交互的构建、生命周期与状态变更排进 UI 线程队列；
        /// 构建延后到第一个工作项，因此从钩子线程调用也不阻塞。状态变更（高亮 / 逃逸）
        /// 走最新态覆盖：未消费期间新值只覆盖待应用值，队列里至多留一个工作项，
        /// 输入速率因此不会在 UI 线程上积成队列。</summary>
        private sealed class DispatchedWheelViewModel : IWheelViewModel
        {
            /// <summary>「无待应用高亮」哨兵（合法扇区索引为 -1 到 N-1）。</summary>
            private const int NoPendingHighlight = int.MinValue;

            private readonly Dispatcher _dispatcher;
            private readonly Func<WheelViewModel> _createViewModel;
            private readonly Func<WheelViewModel, RadialWindow> _createWindow;

            // 钩子线程写、UI 线程读（应用时清空）：待应用状态与在途标记由本锁串行化。
            private readonly object _pendingGate = new();
            private bool _stateQueued;
            private bool _isClosed;
            private int _pendingHighlight = NoPendingHighlight;
            private bool _pendingEscape;

            // 只在 UI 线程读写（构建与使用都排在同一队列里）。窗口随本字段存活：
            // 它是这一轮轮盘交互的 GC 根，构建与首次 Show 之间窗口不会被回收。
            private (WheelViewModel ViewModel, RadialWindow Window)? _wheel;

            public DispatchedWheelViewModel(
                Dispatcher dispatcher,
                Func<WheelViewModel> createViewModel,
                Func<WheelViewModel, RadialWindow> createWindow)
            {
                _dispatcher = dispatcher;
                _createViewModel = createViewModel;
                _createWindow = createWindow;
            }

            public void Show() => Post(viewModel => viewModel.Show());

            public void HighlightSector(int sectorIndex) => QueueState(highlight: sectorIndex);

            public void SetOuterEscapeState(bool isEscaped) => QueueState(escape: isEscaped);

            public void Close()
            {
                lock (_pendingGate)
                {
                    // 关闭后仍在途的拖动事件不再入队：窗口已收，余下的移动没有可应用的视图。
                    _isClosed = true;
                }

                Post(viewModel => viewModel.Close());
            }

            /// <summary>记下最新待应用状态，并在没有在途工作项时投放一个；在途时由该工作项一次取走。</summary>
            private void QueueState(int highlight = NoPendingHighlight, bool? escape = null)
            {
                lock (_pendingGate)
                {
                    if (_isClosed) return;

                    if (highlight != NoPendingHighlight) _pendingHighlight = highlight;
                    if (escape is { } value) _pendingEscape = value;

                    if (_stateQueued) return;
                    _stateQueued = true;
                }

                _dispatcher.BeginInvoke(ApplyPendingState);
            }

            /// <summary>UI 线程：把最新待应用状态一次落到视图模型（与轮盘交互生命周期同队列，先于关闭）。</summary>
            private void ApplyPendingState()
            {
                int highlight;
                bool escape;
                lock (_pendingGate)
                {
                    _stateQueued = false;
                    highlight = _pendingHighlight;
                    _pendingHighlight = NoPendingHighlight;
                    escape = _pendingEscape;
                }

                WheelViewModel viewModel = EnsureCreated();
                viewModel.SetOuterEscapeState(escape);
                if (highlight != NoPendingHighlight)
                {
                    viewModel.HighlightSector(highlight);
                }
            }

            private void Post(Action<WheelViewModel> work) =>
                _dispatcher.BeginInvoke(() => work(EnsureCreated()));

            private WheelViewModel EnsureCreated()
            {
                if (_wheel is not { } wheel)
                {
                    var viewModel = _createViewModel();
                    wheel = (viewModel, _createWindow(viewModel));
                    _wheel = wheel;
                }

                return wheel.ViewModel;
            }
        }
    }
}
