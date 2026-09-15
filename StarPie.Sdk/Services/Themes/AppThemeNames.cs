using System;
using System.Collections.Frozen;

namespace StarPie.Services.Themes
{
    /// <summary>
    /// 界面主题名（AppTheme）的唯一来源：配置取值、解析分支、主题字典文件名与设置面选项目录
    /// 共用的字面量。
    /// </summary>
    /// <remarks>
    /// 与 <see cref="StarPie.Services.Wheel.WheelPaletteNames"/> 分列：<c>System</c>/<c>Dark</c>/<c>Light</c>
    /// 在界面主题与轮盘配色两边同名不同义（CONTEXT.md 两词条互标 <c>_Avoid_</c>），混成一个类会让这三个值
    /// 再次语义模糊。刻意不用枚举：主题名在 <c>config.json</c> 里是字符串，枚举会把序列化兼容面绑上类型名。
    /// </remarks>
    public static class AppThemeNames
    {
        /// <summary>跟随系统深浅色（解析时按实时 OS 主题换成 Dark/Light）。</summary>
        public const string System = "System";

        public const string Light = "Light";

        public const string Dark = "Dark";

        public const string MidnightNavy = "MidnightNavy";

        public const string RoyalViolet = "RoyalViolet";

        public const string TitaniumGray = "TitaniumGray";

        /// <summary>五套具体主题名的声明序（不含 <see cref="System"/>）：与 <c>Themes/*.xaml</c>
        /// 字典、设置面选项目录逐一同名。</summary>
        private static readonly string[] Concrete =
        {
            Light,
            Dark,
            MidnightNavy,
            RoyalViolet,
            TitaniumGray
        };

        /// <summary>深色主题集合（窗口暗色标题栏判定查表）：五套里除 <see cref="Light"/> 外均为深底令牌集。
        /// 显式列举而不写成「<see cref="Concrete"/> 减去 Light」：将来新增一套浅色主题时，显式列举会
        /// 失败安全（不误判为暗色），派生写法会把新主题静默算成暗色。</summary>
        public static readonly FrozenSet<string> DarkThemes = new[]
        {
            Dark,
            MidnightNavy,
            RoyalViolet,
            TitaniumGray
        }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

        /// <summary>已知具体主题名的规范形：大小写不敏感命中返回常量原形（与主题字典文件名同形），
        /// 否则返回 null——含 <see cref="System"/>（无对应字典）、遗留别名与未知名。</summary>
        public static string? CanonicalOrNull(string? themeName)
        {
            if (string.IsNullOrEmpty(themeName)) return null;

            foreach (string name in Concrete)
            {
                if (string.Equals(name, themeName, StringComparison.OrdinalIgnoreCase)) return name;
            }

            return null;
        }
    }
}
