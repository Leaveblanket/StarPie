namespace StarPie.Services.Icons
{
    /// <summary>矢量图标条目：键、分类、显示名与 SVG 路径数据，是「图标资产」静态纯目录清单的条目类型。</summary>
    public class VectorIconItem
    {
        public string Key { get; set; } = "";
        public string Category { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string SvgData { get; set; } = "";
    }
}
