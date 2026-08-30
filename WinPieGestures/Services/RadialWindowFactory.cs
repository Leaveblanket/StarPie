using System;
using System.Windows;
using System.Windows.Threading;
using Point = System.Windows.Point;

namespace WinPieGestures
{
    /// <summary>
    /// First-version wheel factory: wraps the existing RadialWindow and marshals
    /// every wheel interaction onto the UI thread, so the gesture engine stays
    /// thread-agnostic and WPF-free (ADR-0002). Closing also trims the working
    /// set, mirroring the pre-refactor hide path.
    /// </summary>
    public sealed class RadialWindowFactory : IRadialWindowFactory
    {
        public IRadialWindow Create(GesturePoint center, WheelProfile profile)
        {
            Dispatcher dispatcher = Application.Current.Dispatcher;
            RadialWindow? window = null;
            dispatcher.Invoke(() => window = new RadialWindow(new Point(center.X, center.Y), profile));
            return new RadialWindowHandle(window!, dispatcher);
        }

        private sealed class RadialWindowHandle : IRadialWindow
        {
            private readonly RadialWindow _window;
            private readonly Dispatcher _dispatcher;

            public RadialWindowHandle(RadialWindow window, Dispatcher dispatcher)
            {
                _window = window;
                _dispatcher = dispatcher;
            }

            public void Show() => _dispatcher.Invoke(_window.Show);

            public void HighlightSector(int sectorIndex) =>
                _dispatcher.Invoke(() => _window.HighlightSector(sectorIndex));

            public void SetOuterEscapeState(bool isEscaped) =>
                _dispatcher.Invoke(() => _window.SetOuterEscapeState(isEscaped));

            public void Close() => _dispatcher.Invoke(() =>
            {
                _window.Close();
                MemoryOptimizer.TrimMemory();
            });
        }
    }
}
