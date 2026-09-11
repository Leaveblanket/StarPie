using System;
using System.Windows;
using System.Windows.Threading;
using StarPie.Kernel.Localization;

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
        private readonly IConfigService _config;
        private readonly IThemeService _themeService;
        private readonly ILocalizationService _localization;
        private readonly IIconAssetService _iconAssets;

        public WheelFactory(
            IConfigService config,
            IThemeService themeService,
            ILocalizationService localization,
            IIconAssetService iconAssets)
        {
            _config = config;
            _themeService = themeService;
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
                viewModel = new WheelViewModel(center, profile, _config.Current, _localization);
                window = new RadialWindow(viewModel, _themeService, _localization, _iconAssets);
            });
            return new DispatchedWheelViewModel(viewModel!, window!, dispatcher);
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
