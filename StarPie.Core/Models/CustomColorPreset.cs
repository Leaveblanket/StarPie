using System;

namespace StarPie.Models
{
    /// <summary>
    /// 自定义配色预设：用户在轮盘外观设置中保存/切换的一套颜色方案。
    /// 颜色值统一使用 "#AARRGGBB" 十六进制字符串（属数据而非界面文案），默认种子由生产方提供。
    /// </summary>
    public class CustomColorPreset
    {
        /// <summary>预设唯一标识（默认每次新建时生成 GUID）。</summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>用户定义的预设名称（属数据而非界面文案，由生产方提供默认名）。</summary>
        public string Name { get; set; } = "";

        /// <summary>普通扇区的背景色。</summary>
        public string SectorBg { get; set; } = "#9016161A";

        /// <summary>普通扇区的边框色。</summary>
        public string SectorBorder { get; set; } = "#35FFFFFF";

        /// <summary>高亮（悬停/当前）扇区的背景色。</summary>
        public string HighlightBg { get; set; } = "#E06C4DFF";

        /// <summary>高亮（悬停/当前）扇区的边框色。</summary>
        public string HighlightBorder { get; set; } = "#A0FFFFFF";

        /// <summary>扇区文字颜色。</summary>
        public string TextColor { get; set; } = "#E0FFFFFF";

        /// <summary>下拉框/列表按预设名展示。</summary>
        public override string ToString() => Name;
    }
}
