namespace StarPie.Services.Icons
{
    /// <summary>矢量图标条目：键、分类、显示名与 SVG 路径数据（属「图标资产」静态纯目录
    /// <see cref="IconCatalog"/> 的清单条目）。</summary>
    public class VectorIconItem
    {
        public string Key { get; set; } = "";
        public string Category { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string SvgData { get; set; } = "";
    }
}
