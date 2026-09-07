using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;
using StarPie.Models;
using Brush = System.Windows.Media.Brush;

namespace StarPie.Views.Renderers
{
    /// <summary>纯视觉渲染契约：渲染器只消费主题/配置与绘制参数,
    /// 不订阅事件、不读写 VM、不反向依赖 Composition/服务; 实例按窗口/预览随用随建。</summary>
    public interface IRadialStyleRenderer
    {
        Brush DefaultSectorBrush { get; }
        Brush HighlightSectorBrush { get; }
        Brush SectorBorderBrush { get; }
        Brush HighlightBorderBrush { get; }
        Brush TextColorBrush { get; }
        Brush CoreBgBrush { get; }
        Brush CoreBorderBrush { get; }

        double BorderThickness { get; }
        double HighlightBorderThickness { get; }

        /// <summary>配置当前主题并解析色值：主题为 "System"/空值时按调用方提供的实时
        /// Windows 深色标志解析；方案→色值换算在 <see cref="WheelPaletteParser"/>，
        /// 渲染器只把解析后的 <see cref="WheelPalette"/> 转成画刷。</summary>
        void Initialize(string theme, AppConfig config, bool windowsInDarkMode);

        /// <summary>绘制当前样式的装饰；轮盘状态（几何、是否显示核图标）由调用方
        /// 从轮盘视图模型传入。</summary>
        void RenderDecorations(Canvas canvas, Grid coreGrid, double cx, double cy, double wheelRadius, double coreRadius, int insertIndex, bool showCoreIcon);
        void ApplySectorHighlight(Path path, bool isHighlighted);
        void ApplyExitHighlight(Path exitIcon, bool isHighlighted);
    }
}
