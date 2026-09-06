namespace WinPieGestures.Models
{
    /// <summary>
    /// 轮盘配色解析结果（WPF-free 色值组）：扇区底色/边框、高亮/高亮边框、文字，
    /// 以及同源导出的核底色/核边框。渲染层只把这些值构造成画刷（ADR-0014 决策 3/4/10）。
    /// </summary>
    public sealed class WheelPalette
    {
        public RgbColor SectorBg { get; }
        public RgbColor SectorBorder { get; }
        public RgbColor HighlightBg { get; }
        public RgbColor HighlightBorder { get; }
        public RgbColor TextColor { get; }
        public RgbColor CoreBg { get; }
        public RgbColor CoreBorder { get; }

        public WheelPalette(
            RgbColor sectorBg,
            RgbColor sectorBorder,
            RgbColor highlightBg,
            RgbColor highlightBorder,
            RgbColor textColor,
            RgbColor coreBg,
            RgbColor coreBorder)
        {
            SectorBg = sectorBg;
            SectorBorder = sectorBorder;
            HighlightBg = highlightBg;
            HighlightBorder = highlightBorder;
            TextColor = textColor;
            CoreBg = coreBg;
            CoreBorder = coreBorder;
        }

        /// <summary>常规流：核底色/核边框与扇区底色/边框同源（坏值全局回落除外）。</summary>
        public static WheelPalette Create(
            RgbColor sectorBg,
            RgbColor sectorBorder,
            RgbColor highlightBg,
            RgbColor highlightBorder,
            RgbColor textColor)
            => new WheelPalette(sectorBg, sectorBorder, highlightBg, highlightBorder, textColor, sectorBg, sectorBorder);
    }
}
