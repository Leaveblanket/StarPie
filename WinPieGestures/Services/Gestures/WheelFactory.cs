using System;
using System.Windows;
using System.Windows.Threading;

namespace WinPieGestures.Services.Gestures
{
    /// <summary>
    /// Wheel factory (T05): builds the per-gesture view-model and its window on the
    /// UI thread, then returns a thread-safe handle — every engine call is marshaled
    /// onto the dispatcher and lands as a view-model state mutation the window
    /// observes (ADR-0002: implementations own the UI-thread marshaling; callers may
    /// be on the hook thread).
    /// </summary>
    public sealed class WheelFactory : IWheelFactory
    {
        private readonly IConfigService _config;
        private readonly IThemeService _themeService;

        public WheelFactory(IConfigService config, IThemeService themeService)
        {
            _config = config;
            _themeService = themeService;
        }

        public IWheelViewModel Create(GesturePoint center, WheelProfile profile)
        {
            Dispatcher dispatcher = Application.Current.Dispatcher;
            WheelViewModel? viewModel = null;
            RadialWindow? window = null;
            dispatcher.Invoke(() =>
            {
                viewModel = new WheelViewModel(center, profile, _config.Current);
                window = new RadialWindow(viewModel, _themeService);
            });
            return new DispatchedWheelViewModel(viewModel!, window!, dispatcher);
        }

        /// <summary>Marshals every wheel interaction onto the UI thread as a
        /// view-model mutation; the window reacts to the state change itself.</summary>
        private sealed class DispatchedWheelViewModel : IWheelViewModel
        {
            private readonly WheelViewModel _viewModel;
            // GC root: keeps the not-yet-shown window reachable between Create and
            // the first Show dispatch (the view-model does not reference the window).
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
