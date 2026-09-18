using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using StarPie.Sdk.Services.Wheel;
using StarPie.Ui.Services.Wheel;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace StarPie.Ui.Views.Renderers
{
    public abstract class BaseStyleRenderer : IRadialStyleRenderer
    {
        // 由 Initialize 依据解析出的 WheelPalette 构造画刷后填充（调用方保证先 Initialize 再使用）。
        public Brush DefaultSectorBrush { get; protected set; } = null!;
        public Brush HighlightSectorBrush { get; protected set; } = null!;
        public Brush SectorBorderBrush { get; protected set; } = null!;
        public Brush HighlightBorderBrush { get; protected set; } = null!;
        public Brush TextColorBrush { get; protected set; } = null!;
        public Brush CoreBgBrush { get; protected set; } = null!;
        public Brush CoreBorderBrush { get; protected set; } = null!;

        public double BorderThickness { get; protected set; } = 1.0;
        public double HighlightBorderThickness { get; protected set; } = 1.5;

        public bool IsLightPalette { get; protected set; } = false;
        // 配色微调与光晕的窄输入快照；未 Initialize 时为 null。
        protected WheelPaletteInput? _paletteInput;

        /// <summary>轮盘配色目录中的风格键（与 <see cref="StyleRendererFactory"/> 分支同名），
        /// 决定该风格的默认深浅观感与 Light 方案是否套用标准浅色表。</summary>
        protected abstract string WheelStyleName { get; }

        public virtual void Initialize(string palette, WheelPaletteInput paletteInput, bool windowsInDarkMode)
        {
            _paletteInput = paletteInput;
            BorderThickness = 1.0;
            HighlightBorderThickness = 1.5;

            string effectivePalette = WheelPaletteParser.ResolveEffectivePalette(palette, windowsInDarkMode);
            IsLightPalette = string.Equals(effectivePalette, "Light", StringComparison.OrdinalIgnoreCase);

            // 方案名→色值组只在解析层发生；渲染器只消费解析结果构造画刷。
            WheelPalette wheelPalette = WheelPaletteParser.Resolve(palette, paletteInput, windowsInDarkMode, WheelStyleName);

            DefaultSectorBrush = CreateBrush(wheelPalette.SectorBg);
            HighlightSectorBrush = CreateBrush(wheelPalette.HighlightBg);
            SectorBorderBrush = CreateBrush(wheelPalette.SectorBorder);
            HighlightBorderBrush = CreateBrush(wheelPalette.HighlightBorder);
            TextColorBrush = CreateBrush(wheelPalette.TextColor);
            CoreBgBrush = CreateBrush(wheelPalette.CoreBg);
            CoreBorderBrush = CreateBrush(wheelPalette.CoreBorder);

            PostInitialize();
        }

        protected static SolidColorBrush CreateBrush(RgbColor color)
            => new SolidColorBrush(Color.FromArgb(color.A, color.R, color.G, color.B));

        protected virtual void PostInitialize()
        {
        }

        public virtual Color GetEffectiveGlowColor()
        {
            if (_paletteInput != null && !string.IsNullOrEmpty(_paletteInput.HighlightGlowColor))
            {
                try
                {
                    return (Color)ColorConverter.ConvertFromString(_paletteInput.HighlightGlowColor);
                }
                catch { }
            }

            // 回退到高亮边框/高亮扇区画刷颜色
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
            if (_paletteInput != null && _paletteInput.HighlightGlowRadius > 0)
            {
                return _paletteInput.HighlightGlowRadius;
            }
            return defaultRadius;
        }

        public virtual double GetEffectiveGlowOpacity(double defaultOpacity = 0.85)
        {
            if (_paletteInput != null && _paletteInput.HighlightGlowOpacity >= 0)
            {
                return _paletteInput.HighlightGlowOpacity;
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
