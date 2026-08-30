using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;
using Brush = System.Windows.Media.Brush;

namespace WinPieGestures
{
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
