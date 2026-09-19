using System.Collections.Generic;

namespace StarPie.Ui.ViewModels.WheelInteraction
{
    /// <summary>
    /// 扇区方位表：按扇区数为每个扇区给出方位名文案键与语言中立符号
    /// （罗盘缩写 + 角度；12 分位落不到罗盘缩写上，只给角度）。
    /// 方位名经 resx 取词、随 <c>LanguageChanged</c> 刷新，符号本身与语言无关。
    /// </summary>
    public static class DirectionCatalog
    {
        /// <summary>单条方位：resx 文案键 + 语言中立符号。</summary>
        public readonly record struct Direction(string NameKey, string Symbol);

        private static readonly Direction[] Directions4 =
        {
            new("DirectionRight", "E / 0°"),
            new("DirectionDown", "S / 90°"),
            new("DirectionLeft", "W / 180°"),
            new("DirectionUp", "N / 270°"),
        };

        private static readonly Direction[] Directions8 =
        {
            new("DirectionRight", "E / 0°"),
            new("DirectionDownRight", "SE / 45°"),
            new("DirectionDown", "S / 90°"),
            new("DirectionDownLeft", "SW / 135°"),
            new("DirectionLeft", "W / 180°"),
            new("DirectionUpLeft", "NW / 225°"),
            new("DirectionUp", "N / 270°"),
            new("DirectionUpRight", "NE / 315°"),
        };

        private static readonly Direction[] Directions12 =
        {
            new("DirectionRight", "E / 0°"),
            new("DirectionDownRight", "30°"),
            new("DirectionDownRight", "60°"),
            new("DirectionDown", "S / 90°"),
            new("DirectionDownLeft", "120°"),
            new("DirectionDownLeft", "150°"),
            new("DirectionLeft", "W / 180°"),
            new("DirectionUpLeft", "210°"),
            new("DirectionUpLeft", "240°"),
            new("DirectionUp", "N / 270°"),
            new("DirectionUpRight", "300°"),
            new("DirectionUpRight", "330°"),
        };

        /// <summary>取该扇区数对应的方位表；非法扇区数按 8 处理（与槽位重建的规范化一致）。</summary>
        public static IReadOnlyList<Direction> ForSectorCount(int sectorCount) => sectorCount switch
        {
            4 => Directions4,
            12 => Directions12,
            _ => Directions8,
        };
    }
}
