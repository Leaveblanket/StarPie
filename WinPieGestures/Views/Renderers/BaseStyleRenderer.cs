using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using WinPieGestures.Models;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace WinPieGestures.Views.Renderers
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

        /// <summary>轮盘配色目录中的风格键（与 <see cref="StyleRendererFactory"/> 分支同名），
        /// 决定该风格的默认深浅观感与 Light 方案是否套用标准浅色表。</summary>
        protected abstract string WheelStyleName { get; }

        public virtual void Initialize(string theme, AppConfig config, bool windowsInDarkMode)
        {
            _config = config;
            BorderThickness = 1.0;
            HighlightBorderThickness = 1.5;

            string effectiveTheme = WheelPaletteParser.ResolveEffectiveTheme(theme, windowsInDarkMode);
            IsLightTheme = string.Equals(effectiveTheme, "Light", StringComparison.OrdinalIgnoreCase);

            // ADR-0014 决策 10：方案名→色值组只在解析层发生；渲染器只消费解析结果构造画刷。
            WheelPalette palette = WheelPaletteParser.Resolve(theme, config, windowsInDarkMode, WheelStyleName);

            DefaultSectorBrush = CreateBrush(palette.SectorBg);
            HighlightSectorBrush = CreateBrush(palette.HighlightBg);
            SectorBorderBrush = CreateBrush(palette.SectorBorder);
            HighlightBorderBrush = CreateBrush(palette.HighlightBorder);
            TextColorBrush = CreateBrush(palette.TextColor);
            CoreBgBrush = CreateBrush(palette.CoreBg);
            CoreBorderBrush = CreateBrush(palette.CoreBorder);

            PostInitialize();
        }

        protected static SolidColorBrush CreateBrush(RgbColor color)
            => new SolidColorBrush(Color.FromArgb(color.A, color.R, color.G, color.B));

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

        public abstract void RenderDecorations(Canvas canvas, Grid coreGrid, double cx, double cy, double wheelRadius, double coreRadius, int insertIndex, bool showCoreIcon);

        public virtual void ApplySectorHighlight(Path path, bool isHighlighted)
        {
        }

        public virtual void ApplyExitHighlight(Path exitIcon, bool isHighlighted)
        {
        }
    }
}
