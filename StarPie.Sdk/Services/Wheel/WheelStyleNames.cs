namespace StarPie.Services.Wheel
{
    /// <summary>
    /// 轮盘主题风格名的唯一来源：配置值、渲染器自报名与工厂分派共用的字面量集中于此。
    /// </summary>
    /// <remarks>
    /// 刻意不用枚举：风格名在 <c>config.json</c> 里是字符串，枚举会把序列化兼容面绑上类型名，
    /// 收益不抵代价。核图标类型另有同名值 <c>CatPaw</c>（<c>AppConfig.CoreIconType</c>），
    /// 与本类不是同一概念，勿合并。
    /// </remarks>
    public static class WheelStyleNames
    {
        public const string ClassicRing = "ClassicRing";

        public const string CleanSectors = "CleanSectors";

        public const string Glassmorphism = "Glassmorphism";

        public const string CatPaw = "CatPaw";

        /// <summary>缺省风格：配置缺失/空值时的回落，也是渲染器工厂的兜底分支。</summary>
        public const string Default = ClassicRing;
    }
}
