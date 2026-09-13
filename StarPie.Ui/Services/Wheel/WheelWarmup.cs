using System;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace StarPie.Services.Wheel
{
    /// <summary>
    /// 轮盘核心路径启动预热：离屏构造 <see cref="RadialWindow"/> 并渲染一次，踩热 BAML 装载、
    /// 样式渲染器工厂、调色板与画刷构造路径，使首次手势弹出不再付这些一次性成本。
    /// 不 Show（静默形态无闪窗）、不 Close（未显示窗口无 HWND）。诚实边界：
    /// <c>RadialWindow_Loaded</c> 挂的绘制路径依赖窗口显示，离屏预热不到，首手势仍付一次该路径成本。
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static class WheelWarmup
    {
        /// <summary>执行一次离屏预热，返回窗口的 <see cref="WeakReference"/> 供测试判定可回收；
        /// 窗口与位图在方法返回后即不可达（预热产物不驻留）。</summary>
        public static WeakReference Run(
            WheelViewModel viewModel,
            IThemeService themeService,
            ILocalizationService localization,
            IIconAssetService iconAssets)
        {
            var window = new RadialWindow(viewModel, themeService, localization, iconAssets);

            // 离屏走一次完整布局与渲染：Measure/Arrange 触发布局与样式查找，
            // RenderTargetBitmap 触发绘制管线；窗口未显示，不创建 HWND、不触发 Loaded。
            window.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Size size = window.DesiredSize;
            if (size.IsEmpty || size.Width < 1 || size.Height < 1)
            {
                // 未显示的 Window 其 DesiredSize 为空：回退到 XAML 声明的显式尺寸
                size = new Size(window.Width, window.Height);
            }
            window.Arrange(new Rect(default, size));
            var bitmap = new RenderTargetBitmap(
                (int)Math.Ceiling(size.Width),
                (int)Math.Ceiling(size.Height),
                96, 96, PixelFormats.Pbgra32);
            bitmap.Render(window);

            // 关闭以解除 Application.Windows 集合的强引用（窗口构造即入集合，Closed 才出）；
            // 随后排空 Dispatcher 上遗留的布局/渲染清理载荷——它们持有刚关闭的窗口，
            // 不排空则预热产物滞留。未显示窗口无 HWND，Close 无视觉/系统副作用。
            window.Close();
            window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);

            return new WeakReference(window);
        }
    }
}
