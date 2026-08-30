using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using Color = System.Windows.Media.Color;
using Panel = System.Windows.Controls.Panel;
using Point = System.Windows.Point;

namespace WinPieGestures
{
    /// <summary>
    /// Glassmorphism Style: Apple Liquid Glass & Windows Fluent Acrylic/Mica Floating Wheel.
    /// Pure standalone frosted translucent glass sectors with delicate specular hairline borders,
    /// soft ambient floating drop shadow, and glowing ethereal lilac outer glow on hover.
    /// </summary>
    public class GlassmorphismRenderer : BaseStyleRenderer
    {
        protected override void GetDefaultColors(string theme, out string sectorBgHex, out string sectorBorderHex, out string highlightBgHex, out string highlightBorderHex, out string textHex)
        {
            if (theme == "Light")
            {
                sectorBgHex = "#45FFFFFF";     // Translucent frosted crystalline white (40% opacity)
                sectorBorderHex = "#85FFFFFF"; // Specular glass refraction rim
                highlightBgHex = "#D86366F1";  // Luminous indigo-violet crystal
                highlightBorderHex = "#FFFFFFFF"; // Pure specular white rim
                textHex = "#FF0F172A";         // High-contrast slate text
            }
            else
            {
                sectorBgHex = "#40181E32";     // Deep midnight acrylic frosted glass (translucent deep blue-violet)
                sectorBorderHex = "#50E2E8F0"; // Specular glass hairline rim (semi-transparent highlight)
                highlightBgHex = "#D07C3AED";  // Rich royal amethyst lilac crystal on hover
                highlightBorderHex = "#FFF5F3FF"; // Glowing lilac-white rim
                textHex = "#FFF8FAFC";         // Pure morning snow white text
            }
        }

        protected override void PostInitialize()
        {
            BorderThickness = 0.9;
            HighlightBorderThickness = 1.8;

            CoreBgBrush = new SolidColorBrush(Color.FromArgb(60, 20, 24, 40));
            CoreBorderBrush = new SolidColorBrush(Color.FromArgb(70, 255, 255, 255));
        }

        public override void RenderDecorations(Canvas canvas, Grid coreGrid, double cx, double cy, double wheelRadius, double coreRadius, int insertIndex, bool showCoreIcon)
        {
            // Pure floating aesthetic: No bulky background disc!
            // Sectors float directly over the desktop/app with individual glass refraction and shadows.
            
            Color ringColor = IsLightTheme ? Color.FromArgb(40, 100, 116, 139) : Color.FromArgb(35, 255, 255, 255);
            var innerGlassRing = new Ellipse
            {
                Width = coreRadius * 2.0 + 4.0,
                Height = coreRadius * 2.0 + 4.0,
                Stroke = new SolidColorBrush(ringColor),
                StrokeThickness = 0.8,
                Tag = "Deco_InnerGlassRing",
                IsHitTestVisible = false
            };
            Canvas.SetLeft(innerGlassRing, cx - (coreRadius + 2.0));
            Canvas.SetTop(innerGlassRing, cy - (coreRadius + 2.0));
            Panel.SetZIndex(innerGlassRing, 0);
            canvas.Children.Add(innerGlassRing);
        }

        public override void ApplySectorHighlight(Path path, bool isHighlighted)
        {
            if (isHighlighted)
            {
                Color glowColor = GetEffectiveGlowColor();
                double blurRadius = GetEffectiveGlowRadius(26.0);
                double opacity = GetEffectiveGlowOpacity(0.95);

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
                // Soft Ambient Drop Shadow for unselected floating frosted glass sectors
                path.Effect = new DropShadowEffect
                {
                    Color = Color.FromRgb(0, 0, 0),
                    BlurRadius = 14,
                    ShadowDepth = 2,
                    Opacity = 0.40,
                    Direction = 270
                };
            }
        }

        public override void ApplyExitHighlight(Path exitIcon, bool isHighlighted)
        {
            if (isHighlighted)
            {
                exitIcon.Effect = new DropShadowEffect
                {
                    Color = Color.FromRgb(244, 63, 94),
                    BlurRadius = 16,
                    ShadowDepth = 0,
                    Opacity = 0.9
                };
            }
            else
            {
                exitIcon.Effect = null;
            }
        }
    }
}
