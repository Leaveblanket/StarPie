using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using Color = System.Windows.Media.Color;
using Panel = System.Windows.Controls.Panel;
using Point = System.Windows.Point;

namespace WinPieGestures.Views.Renderers
{
    /// <summary>
    /// Classic Ring Style: Vision Pro Spatial Ring HUD.
    /// Features concentric spatial orbits, geometric compass ticks, and high-contrast sapphire pop-out.
    /// </summary>
    public class ClassicRingRenderer : BaseStyleRenderer
    {
        protected override void GetDefaultColors(string theme, out string sectorBgHex, out string sectorBorderHex, out string highlightBgHex, out string highlightBorderHex, out string textHex)
        {
            if (theme == "Light")
            {
                sectorBgHex = "#F5F8FAFC";
                sectorBorderHex = "#3564748B";
                highlightBgHex = "#FF2563EB";  // Vivid Royal Cobalt
                highlightBorderHex = "#FF93C5FD";
                textHex = "#FF0F172A";
            }
            else
            {
                sectorBgHex = "#F018181B";     // Deep obsidian zinc
                sectorBorderHex = "#40FFFFFF"; // Fine crisp ring border
                highlightBgHex = "#FF2563EB";  // Pure vivid Cobalt Blue
                highlightBorderHex = "#FF93C5FD";
                textHex = "#FFF8FAFC";
            }
        }

        protected override void PostInitialize()
        {
            BorderThickness = 1.0;
            HighlightBorderThickness = 2.0;
        }

        public override void RenderDecorations(Canvas canvas, Grid coreGrid, double cx, double cy, double wheelRadius, double coreRadius, int insertIndex, bool showCoreIcon)
        {
            Color orbitColor = IsLightTheme ? Color.FromArgb(70, 100, 116, 139) : Color.FromArgb(45, 255, 255, 255);
            Color tickColor = IsLightTheme ? Color.FromArgb(100, 71, 85, 105) : Color.FromArgb(70, 255, 255, 255);

            // 1. Concentric Spatial Outer Orbit (外层悬浮导引虚线轨道)
            double outerOrbitRadius = wheelRadius + 8.0;
            var outerOrbit = new Ellipse
            {
                Width = outerOrbitRadius * 2.0,
                Height = outerOrbitRadius * 2.0,
                Stroke = new SolidColorBrush(orbitColor),
                StrokeThickness = 1.0,
                StrokeDashArray = new DoubleCollection { 3, 5 },
                Tag = "Deco_SpatialOuterOrbit"
            };
            Canvas.SetLeft(outerOrbit, cx - outerOrbitRadius);
            Canvas.SetTop(outerOrbit, cy - outerOrbitRadius);
            Panel.SetZIndex(outerOrbit, 0);
            canvas.Children.Add(outerOrbit);

            // 2. 4-Axis Compass / Spatial Radar Ticks (空间罗盘标尺)
            double[] tickAngles = { 0, 90, 180, 270 };
            foreach (double deg in tickAngles)
            {
                double rad = deg * Math.PI / 180.0;
                double r1 = wheelRadius + 4.0;
                double r2 = wheelRadius + 11.0;
                var tickLine = new Line
                {
                    X1 = cx + r1 * Math.Cos(rad),
                    Y1 = cy + r1 * Math.Sin(rad),
                    X2 = cx + r2 * Math.Cos(rad),
                    Y2 = cy + r2 * Math.Sin(rad),
                    Stroke = new SolidColorBrush(tickColor),
                    StrokeThickness = 1.2,
                    Tag = "Deco_CompassTick"
                };
                Panel.SetZIndex(tickLine, 0);
                canvas.Children.Add(tickLine);
            }

            // 3. Concentric Inner Energy Ring (内同心环)
            var innerRing = new Ellipse
            {
                Width = coreRadius * 2.0 + 8.0,
                Height = coreRadius * 2.0 + 8.0,
                Stroke = new SolidColorBrush(Color.FromArgb(50, 59, 130, 246)),
                StrokeThickness = 1.2,
                Tag = "Deco_ClassicInnerRing"
            };
            Canvas.SetLeft(innerRing, cx - (coreRadius + 4.0));
            Canvas.SetTop(innerRing, cy - (coreRadius + 4.0));
            Panel.SetZIndex(innerRing, 0);
            canvas.Children.Add(innerRing);
        }

        public override void ApplySectorHighlight(Path path, bool isHighlighted)
        {
            if (isHighlighted)
            {
                Color glowColor = GetEffectiveGlowColor();
                double blurRadius = GetEffectiveGlowRadius(20.0);
                double opacity = GetEffectiveGlowOpacity(0.75);

                path.Effect = new DropShadowEffect
                {
                    Color = glowColor,
                    BlurRadius = blurRadius,
                    ShadowDepth = 2,
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
