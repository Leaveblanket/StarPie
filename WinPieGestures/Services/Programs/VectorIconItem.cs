namespace WinPieGestures.Services.Programs
{
    /// <summary>
    /// 矢量图标条目（模块 S1「图标资产」的数据描述）：键/分类/显示名/SVG 路径数据。
    /// T3a 扩展阶段为保持旧入口 public 签名与调用方零改动，本类型暂居旧命名空间
    /// <c>WinPieGestures.Services.Programs</c>；待 T3c/T3d 接线迁移时随调用方收编至
    /// <c>WinPieGestures.Services.Icons</c>。
    /// </summary>
    public class VectorIconItem
    {
        public string Key { get; set; } = "";
        public string Category { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string SvgData { get; set; } = "";
    }
}
