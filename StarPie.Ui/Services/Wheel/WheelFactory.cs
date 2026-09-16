using System;
using System.Windows;
using System.Windows.Threading;
using StarPie.Localization;
using StarPie.ViewModels.Wheel;

namespace StarPie.Services.Wheel
{
    /// <summary>
    /// 轮盘工厂：在 UI 线程构建每次手势的视图模型与窗口，再返回线程安全句柄——
    /// 引擎的每次调用都经调度器转发，落地为窗口观察的视图模型状态变更。
    /// </summary>
    /// <remarks>
    /// 实现方负责 UI 线程调度，调用方可能位于钩子线程；消费方（手势引擎）只依赖
    /// <see cref="IWheelFactory"/> 接口。
    /// </remarks>
    public sealed class WheelFactory : IWheelFactory
    {
        /// <summary>预热用的全局方案名（无专属方案时的兜底方案）。</summary>
        private const string GlobalProfileName = "Global";

        /// <summary>预热的虚拟触发点：只用于构造视图模型，不参与窗口定位。</summary>
        private static readonly GesturePoint WarmupCenter = new GesturePoint(200, 200);

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

        public IWheelViewModel Create(GesturePoint center, WheelProfile profile)
        {
            Dispatcher dispatcher = Application.Current.Dispatcher;
            WheelViewModel? viewModel = null;
            RadialWindow? window = null;
            dispatcher.Invoke(() =>
            {
                // 每次手势从运行态配置快照组装投影：轮盘弹出期间改配置不回流。
                viewModel = new WheelViewModel(center, profile, WheelViewData.FromConfig(_config.Current), _localization);
                window = new RadialWindow(viewModel, _windowsInDarkModeProbe, _localization, _iconAssets);
            });
            return new DispatchedWheelViewModel(viewModel!, window!, dispatcher);
        }

        /// <summary>启动期预热：本方法装配预热所需的一切——取全局方案（缺失即空方案）构造
        /// 与手势同形的视图模型，再离屏渲染一次。调用方（壳层）不必知道 Profile 查找语义
        /// 与预热方式，调用方不必知道这些装配细节。</summary>
        public void Warmup()
        {
            WheelProfile profile = _config.Current.Profiles.Find(p => p.ProcessName == GlobalProfileName) ?? new WheelProfile();
            var viewModel = new WheelViewModel(WarmupCenter, profile, WheelViewData.FromConfig(_config.Current), _localization);
            WheelWarmup.Run(viewModel, _windowsInDarkModeProbe, _localization, _iconAssets);
        }

        /// <summary>把每次轮盘交互经调度器转发到 UI 线程，落地为视图模型状态变更；
        /// 窗口自行响应状态变化。</summary>
        private sealed class DispatchedWheelViewModel : IWheelViewModel
        {
            private readonly WheelViewModel _viewModel;
            // GC 根：在 Create 与首次 Show 派发之间保持尚未显示的窗口可达
            // （视图模型不引用窗口）。
            private readonly RadialWindow _window;
            private readonly Dispatcher _dispatcher;

            public DispatchedWheelViewModel(WheelViewModel viewModel, RadialWindow window, Dispatcher dispatcher)
            {
                _viewModel = viewModel;
                _window = window;
                _dispatcher = dispatcher;
            }

            public void Show() => _dispatcher.Invoke(_viewModel.Show);

            public void HighlightSector(int sectorIndex) =>
                _dispatcher.Invoke(() => _viewModel.HighlightSector(sectorIndex));

            public void SetOuterEscapeState(bool isEscaped) =>
                _dispatcher.Invoke(() => _viewModel.SetOuterEscapeState(isEscaped));

            public void Close() => _dispatcher.Invoke(_viewModel.Close);
        }
    }
}
