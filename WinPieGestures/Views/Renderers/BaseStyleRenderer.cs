using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace WinPieGestures
{
    public abstract class BaseStyleRenderer : IRadialStyleRenderer
    {
        public Brush DefaultSectorBrush { get; protected set; }
        public Brush HighlightSectorBrush { get; protected set; }
        public Brush SectorBorderBrush { get; protected set; }
        public Brush HighlightBorderBrush { get; protected set; }
        public Brush TextColorBrush { get; protected set; }
        public Brush CoreBgBrush { get; protected set; }
        public Brush CoreBorderBrush { get; protected set; }

        public double BorderThickness { get; protected set; } = 1.0;
        public double HighlightBorderThickness { get; protected set; } = 1.5;

        public bool IsLightTheme { get; protected set; } = false;
        protected AppConfig? _config;

        public virtual void Initialize(string theme, AppConfig config)
        {
            _config = config;
            BorderThickness = 1.0;
            HighlightBorderThickness = 1.5;

            string effectiveTheme = theme;
            if (string.Equals(theme, "System", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(theme))
            {
                effectiveTheme = AppThemeManager.IsWindowsInDarkTheme() ? "Dark" : "Light";
            }

            IsLightTheme = string.Equals(effectiveTheme, "Light", StringComparison.OrdinalIgnoreCase);

            string sectorBgHex, sectorBorderHex, highlightBgHex, highlightBorderHex, textHex;
            GetDefaultColors(effectiveTheme, out sectorBgHex, out sectorBorderHex, out highlightBgHex, out highlightBorderHex, out textHex);

            string coreBgHex = sectorBgHex;
            string coreBorderHex = sectorBorderHex;

            if (theme == "Light" && UseStandardLightThemeFallback())
            {
                sectorBgHex = "#F0F8FAFC";
                sectorBorderHex = "#3064748B";
                highlightBgHex = "#FF2563EB";
                highlightBorderHex = "#FF60A5FA";
                textHex = "#FF0F172A";
                coreBgHex = "#FFF8FAFC";
                coreBorderHex = "#3064748B";
            }
            else if (theme == "MatchaForest")
            {
                sectorBgHex = "#E6142E1F";
                sectorBorderHex = "#4034D399";
                highlightBgHex = "#FF10B981";
                highlightBorderHex = "#FF6EE7B7";
                textHex = "#FFF0FDF4";
                coreBgHex = "#F0142E1F";
                coreBorderHex = "#4034D399";
            }
            else if (theme == "GlacialIce")
            {
                sectorBgHex = "#E0E0F2FE";
                sectorBorderHex = "#6038BDF8";
                highlightBgHex = "#FF0284C7";
                highlightBorderHex = "#FFBAE6FD";
                textHex = "#FF0C4A6E";
                coreBgHex = "#F0E0F2FE";
                coreBorderHex = "#6038BDF8";
            }
            else if (theme == "MorandiMuted")
            {
                sectorBgHex = "#E62C302E";
                sectorBorderHex = "#409CA3AF";
                highlightBgHex = "#FF78716C";
                highlightBorderHex = "#FFD6D3D1";
                textHex = "#FFF5F5F4";
                coreBgHex = "#F02C302E";
                coreBorderHex = "#409CA3AF";
            }
            else if (theme.StartsWith("CustomPreset_") || (config.CustomColorPresets != null && config.CustomColorPresets.Exists(p => p.Id == theme || p.Name == theme)))
            {
                var preset = config.CustomColorPresets?.Find(p => p.Id == theme || p.Name == theme || ("CustomPreset_" + p.Id) == theme);
                if (preset != null)
                {
                    sectorBgHex = preset.SectorBg;
                    sectorBorderHex = preset.SectorBorder;
                    highlightBgHex = preset.HighlightBg;
                    highlightBorderHex = preset.HighlightBorder;
                    textHex = preset.TextColor;
                }
            }
            else if (theme == "Custom")
            {
                sectorBgHex = config.CustomSectorBg ?? sectorBgHex;
                sectorBorderHex = config.CustomSectorBorder ?? sectorBorderHex;
                highlightBgHex = config.CustomHighlightBg ?? highlightBgHex;
                highlightBorderHex = config.CustomHighlightBorder ?? highlightBorderHex;
                textHex = config.CustomText ?? textHex;
                coreBgHex = sectorBgHex;
                coreBorderHex = sectorBorderHex;
            }

            coreBgHex = sectorBgHex;
            coreBorderHex = sectorBorderHex;

            try
            {
                DefaultSectorBrush = CreateSolidBrush(sectorBgHex);
                HighlightSectorBrush = CreateSolidBrush(highlightBgHex);
                SectorBorderBrush = CreateSolidBrush(sectorBorderHex);
                HighlightBorderBrush = CreateSolidBrush(highlightBorderHex);
                TextColorBrush = CreateSolidBrush(textHex);
                CoreBgBrush = CreateSolidBrush(coreBgHex);
                CoreBorderBrush = CreateSolidBrush(coreBorderHex);
            }
            catch
            {
                DefaultSectorBrush = CreateSolidBrush("#E618181B");
                HighlightSectorBrush = CreateSolidBrush("#FF3B82F6");
                SectorBorderBrush = CreateSolidBrush("#35FFFFFF");
                HighlightBorderBrush = CreateSolidBrush("#A0FFFFFF");
                TextColorBrush = CreateSolidBrush("#F8FAFC");
                CoreBgBrush = CreateSolidBrush("#F018181B");
                CoreBorderBrush = CreateSolidBrush("#30FFFFFF");
            }

            PostInitialize();
        }

        protected SolidColorBrush CreateSolidBrush(string hex)
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        }

        protected virtual void GetDefaultColors(string theme, out string sectorBgHex, out string sectorBorderHex, out string highlightBgHex, out string highlightBorderHex, out string textHex)
        {
            // Modern Dark Neutral Base with Electric Blue Accent
            sectorBgHex = "#EB18181B";     // Dark slate-zinc
            sectorBorderHex = "#30FFFFFF"; // Subtle hairline
            highlightBgHex = "#FF2563EB";  // Pure vivid Cobalt/Blue
            highlightBorderHex = "#FF60A5FA";
            textHex = "#FFF8FAFC";
        }

        protected virtual bool UseStandardLightThemeFallback()
        {
            return true;
        }

        protected virtual void PostInitialize()
        {
        }

        public virtual Color GetEffectiveGlowColor()
        {
            if (_config != null && !string.IsNullOrEmpty(_config.HighlightGlowColor))
            {
                try
                {
                    return (Color)ColorConverter.ConvertFromString(_config.HighlightGlowColor);
                }
                catch { }
            }

            // Fallback to HighlightBorderBrush or HighlightSectorBrush color
            if (HighlightBorderBrush is SolidColorBrush hbb && hbb.Color.A > 0)
            {
                return hbb.Color;
            }
            if (HighlightSectorBrush is SolidColorBrush hsb && hsb.Color.A > 0)
            {
                return hsb.Color;
            }
            return Color.FromRgb(168, 85, 247);
        }

        public virtual double GetEffectiveGlowRadius(double defaultRadius = 24.0)
        {
            if (_config != null && _config.HighlightGlowRadius > 0)
            {
                return _config.HighlightGlowRadius;
            }
            return defaultRadius;
        }

        public virtual double GetEffectiveGlowOpacity(double defaultOpacity = 0.85)
        {
            if (_config != null && _config.HighlightGlowOpacity >= 0)
            {
                return _config.HighlightGlowOpacity;
            }
            return defaultOpacity;
        }

        public abstract void RenderDecorations(Canvas canvas, Grid coreGrid, double cx, double cy, double wheelRadius, double coreRadius, int insertIndex);

        public virtual void ApplySectorHighlight(Path path, bool isHighlighted)
        {
        }

        public virtual void ApplyExitHighlight(Path exitIcon, bool isHighlighted)
        {
        }
    }
}
