namespace StarPie.Services.Wheel
{
    /// <summary>
    /// 轮盘配色方案名的唯一来源：解析器分支键、外观设置页固定配色项与配置默认值共用的字面量。
    /// </summary>
    /// <remarks>
    /// 与 <see cref="WheelStyleNames"/> 分列：配色方案是「用哪套色」，主题风格是「画成什么形状」，
    /// 两者取值集合不同、消费方也不同，混成一个类会让 <c>CatPaw</c> 这类同名值再次语义模糊。
    /// 刻意不用枚举：方案名在 <c>config.json</c> 里是字符串（ADR-0043 决策 3）。
    /// </remarks>
    public static class WheelPaletteNames
    {
        /// <summary>跟随系统深浅色（解析时按实时 OS 主题换成 Dark/Light）。</summary>
        public const string System = "System";

        public const string Dark = "Dark";

        public const string Light = "Light";

        public const string MatchaForest = "MatchaForest";

        public const string GlacialIce = "GlacialIce";

        public const string MorandiMuted = "MorandiMuted";

        /// <summary>逐字段微调（五色取自配置的 Custom* 字段）。</summary>
        public const string Custom = "Custom";

        /// <summary>自定义预设的引用前缀：完整方案名为 <c>CustomPreset_{id}</c>。</summary>
        public const string CustomPresetPrefix = "CustomPreset_";
    }
}
