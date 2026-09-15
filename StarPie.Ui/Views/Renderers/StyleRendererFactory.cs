using StarPie.Services.Wheel;

namespace StarPie.Views.Renderers
{
    public static class StyleRendererFactory
    {
        /// <summary>按风格名实例化对应的样式渲染器。</summary>
        public static IRadialStyleRenderer CreateRenderer(string style)
        {
            switch (style?.Trim())
            {
                case WheelStyleNames.CatPaw:
                    return new CatPawRenderer();
                case WheelStyleNames.Glassmorphism:
                    return new GlassmorphismRenderer();
                case WheelStyleNames.CleanSectors:
                    return new CleanSectorsRenderer();
                case WheelStyleNames.ClassicRing:
                default:
                    return new ClassicRingRenderer();
            }
        }
    }
}
