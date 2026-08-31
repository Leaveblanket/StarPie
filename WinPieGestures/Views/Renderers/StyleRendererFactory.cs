namespace WinPieGestures.Views.Renderers
{
    public static class StyleRendererFactory
    {
        /// <summary>
        /// Instantiates the appropriate style renderer for the given style name.
        /// </summary>
        public static IRadialStyleRenderer CreateRenderer(string style)
        {
            if (string.IsNullOrEmpty(style))
            {
                return new ClassicRingRenderer();
            }

            switch (style.Trim())
            {
                case "CatPaw":
                    return new CatPawRenderer();
                case "Glassmorphism":
                    return new GlassmorphismRenderer();
                case "CleanSectors":
                    return new CleanSectorsRenderer();
                case "ClassicRing":
                default:
                    return new ClassicRingRenderer();
            }
        }
    }
}
