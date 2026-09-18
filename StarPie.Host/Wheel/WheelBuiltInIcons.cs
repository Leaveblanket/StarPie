using StarPie.Host.Icons;

namespace StarPie.Host.Wheel
{
    /// <summary>
    /// 扇区内置向量图标：动作类型/系统参数到内置矢量目录的映射，以及目录体外的字面量。
    /// 运行时轮盘与外观页预览共用这一份，两侧不再各持实现。
    /// </summary>
    public static class WheelBuiltInIcons
    {
        /// <summary>热键动作的键盘图标（矢量目录体外的字面量，故留在此处集中）。</summary>
        public const string Hotkey =
            "M19,15H5V5H19M19,3H5C3.89,3 3,3.89 3,5V15C3,16.1 3.89,17 5,17H19C20.1,17 21,16.1 21,15V5C21,3.89 20.1,3 19,3M2,18H22V20H2V18Z";

        /// <summary>系统动作参数 → 内置矢量目录键（忽略大小写与首尾空白）；未收录返回 null。</summary>
        /// <remarks>参数取值大小写混用（配置默认值与界面预设写法不一致），故归一后匹配。</remarks>
        public static string? SystemParameterIconKey(string? parameter)
        {
            if (string.IsNullOrWhiteSpace(parameter)) return null;

            switch (parameter.Trim().ToLowerInvariant())
            {
                case "lock": return "Lock";
                case "volumeup": return "VolumeUp";
                case "volumedown": return "VolumeDown";
                case "volumemute": return "VolumeMute";
                case "showdesktop": return "ShowDesktop";
                case "screenshot": return "Screenshot";
                default: return null;
            }
        }

        /// <summary>按动作类型与参数取内置向量的 SVG 路径数据；该类型无内置向量返回 null。</summary>
        public static string? ResolveSvgPath(string? type, string? parameter)
        {
            switch (type)
            {
                case "Folder":
                case "OpenFolder":
                    return IconCatalog.GetSvgPathByKey("Folder");
                case "Hotkey":
                    return Hotkey;
                case "System":
                    return IconCatalog.GetSvgPathByKey(SystemParameterIconKey(parameter));
                default:
                    return null;
            }
        }
    }
}
