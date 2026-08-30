using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using Color = System.Windows.Media.Color;

namespace WinPieGestures
{
    /// <summary>
    /// Clean Sectors Style: Swiss Modern Minimalist.
    /// Features pure geometric negative space, matte cards, and razor-sharp emerald highlight.
    /// </summary>
    public class CleanSectorsRenderer : BaseStyleRenderer
    {
        protected override void GetDefaultColors(string theme, out string sectorBgHex, out string sectorBorderHex, out string highlightBgHex, out string highlightBorderHex, out string textHex)
        {
            if (theme == "Light")
            {
                sectorBgHex = "#F8FFFFFF";     // Pure crisp white card
                sectorBorderHex = "#35CBD5E1"; // Hairline border
                highlightBgHex = "#FF059669";  // Emerald 600
                highlightBorderHex = "#FF10B981";
                textHex = "#FF0F172A";
            }
            else
            {
                sectorBgHex = "#F20F172A";     // Matte Obsidian Slate
                sectorBorderHex = "#35334155"; // Hairline slate border
                highlightBgHex = "#FF10B981";  // Vivid Emerald Green
                highlightBorderHex = "#FF6EE7B7";
                textHex = "#FFF8FAFC";
            }
        }

        protected override void PostInitialize()
        {
            BorderThickness = 0.9;
            HighlightBorderThickness = 1.6;
        }

        public override void RenderDecorations(Canvas canvas, Grid coreGrid, double cx, double cy, double wheelRadius, double coreRadius, int insertIndex, bool showCoreIcon)
        {
            // Pure minimalist style: clean space, no noisy clutter
        }

        public override void ApplySectorHighlight(Path path, bool isHighlighted)
        {
            if (isHighlighted)
            {
                Color glowColor = GetEffectiveGlowColor();
                double blurRadius = GetEffectiveGlowRadius(16.0);
                double opacity = GetEffectiveGlowOpacity(0.70);

                path.Effect = new DropShadowEffect
                {
                    Color = glowColor,
                    BlurRadius = blurRadius,
                    ShadowDepth = 0,
                    Opacity = opacity
                };
            }
            else
            {
                path.Effect = null;
            }
        }
    }
}
