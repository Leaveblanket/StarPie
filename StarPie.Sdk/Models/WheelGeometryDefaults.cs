namespace StarPie.Models
{
    /// <summary>
    /// 轮盘几何默认值的唯一来源：配置模型默认值与外观设置页「一键重置几何」同源。
    /// </summary>
    /// <remarks>
    /// 这是<b>外观默认观感</b>，不是运行期兜底：配置里半径写成 0/负值时，手势引擎用自己那份
    /// 兜底常量（外甩逃逸距离的计算基准），语义不同、不随本类改动（ADR-0043 决策 3）。
    /// </remarks>
    public static class WheelGeometryDefaults
    {
        /// <summary>轮盘外半径（像素）。</summary>
        public const double Radius = 138.0;

        /// <summary>扇区环内半径（像素）。</summary>
        public const double InnerRadius = 52.0;

        /// <summary>中心核圆半径（像素）。</summary>
        public const double CoreRadius = 50.0;
    }
}
