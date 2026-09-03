using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;
using Brush = System.Windows.Media.Brush;

namespace WinPieGestures.Views.Renderers
{
    /// <summary>纯视觉渲染契约(ADR-0009 白名单 3): 渲染器只消费主题/配置与绘制参数,
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

        /// <summary>The theme name as configured; when it is "System"/empty the
        /// renderer resolves with the caller-provided live Windows dark-mode flag.</summary>
        void Initialize(string theme, AppConfig config, bool windowsInDarkMode);

        /// <summary>Draws the style's decorations; wheel state (geometry, whether the
        /// core icon is shown) flows in from the wheel view-model via the caller.</summary>
        void RenderDecorations(Canvas canvas, Grid coreGrid, double cx, double cy, double wheelRadius, double coreRadius, int insertIndex, bool showCoreIcon);
        void ApplySectorHighlight(Path path, bool isHighlighted);
        void ApplyExitHighlight(Path exitIcon, bool isHighlighted);
    }
}
