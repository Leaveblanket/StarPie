namespace WinPieGestures.Services.Icons
{
    /// <summary>
    /// 矢量图标条目（模块 S1「图标资产」的数据描述）：键/分类/显示名/SVG 路径数据。
    /// 与 <see cref="IconAssets"/> 同属共享「图标资产」出口；T3d/#68 随旧入口收口迁入本命名空间。
    /// </summary>
    public class VectorIconItem
    {
        public string Key { get; set; } = "";
        public string Category { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string SvgData { get; set; } = "";
    }
}
