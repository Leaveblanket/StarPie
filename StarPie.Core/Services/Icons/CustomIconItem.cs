namespace StarPie.Services.Icons
{
    /// <summary>自定义图标条目：自用户图标目录导入的 SVG 路径数据或位图文件的描述。</summary>
    public class CustomIconItem
    {
        public string Key { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string FilePath { get; set; } = "";
        public string SvgData { get; set; } = "";
        public bool IsSvg => !string.IsNullOrEmpty(SvgData);
    }
}
