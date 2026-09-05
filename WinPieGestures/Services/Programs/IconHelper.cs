using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WinPieGestures.Services.Programs
{
    /// <summary>
    /// 图标/几何/程序解析旧入口（R6 三分 · T3a 扩展，ADR-0015）：public 成员签名与行为保持不变，
    /// 全量委托新归属出口——图标资产 → <c>WinPieGestures.Services.Icons.IconAssets</c>（S1）、
    /// 轮盘几何 → <c>WinPieGestures.Services.Wheel.WheelGeometry</c>（M2）、快捷方式解析 →
    /// <see cref="ShortcutResolver"/>（M3）。调用方暂不改动；待 T3b/T3c 接线迁移后由 T3d 删除本入口。
    /// </summary>
    public static class IconHelper
    {
        /// <summary>
        /// 自定义图标兼容条目：新形态使用 <see cref="IconAssets.CustomIconItem"/>（S1），
        /// 本子类仅作 T3a 过渡期源码兼容，待调用方迁移后随旧入口删除。
        /// </summary>
        public class CustomIconItem : IconAssets.CustomIconItem
        {
        }

        /// <summary>矢量图标清单：与 <see cref="IconAssets.VectorIconList"/> 同一列表对象，委托保证目录一致。</summary>
        public static readonly List<VectorIconItem> VectorIconList = IconAssets.VectorIconList;

        /// <summary>SVG 键目录取值（委托 S1 共享图标资产目录）。</summary>
        public static string? GetSvgPathByKey(string? key) => IconAssets.GetSvgPathByKey(key);

        /// <summary>扇区切削几何（委托 M2 轮盘视觉几何出口）。</summary>
        public static Geometry CreateAdvancedSectorGeometry(
            double cx, double cy,
            double startAngle, double endAngle,
            double innerR, double outerR,
            string shape, double gap = 0, double cornerRadius = 0)
            => WheelGeometry.CreateAdvancedSectorGeometry(cx, cy, startAngle, endAngle, innerR, outerR, shape, gap, cornerRadius);

        /// <summary>中心核图标几何（委托 M2 轮盘视觉几何出口）。</summary>
        public static Geometry GetCoreIconGeometry(string? coreIconType, string? customKey = null, string? customSvg = null)
            => WheelGeometry.GetCoreIconGeometry(coreIconType, customKey, customSvg);

        /// <summary>快捷方式目标解析（委托 M3 程序快捷方式解析出口）。</summary>
        public static bool ResolveShortcutTarget(string lnkPath, out string targetPath, out string iconPath, out int iconIndex)
            => ShortcutResolver.ResolveShortcutTarget(lnkPath, out targetPath, out iconPath, out iconIndex);

        /// <summary>文件/程序图标提取（委托 S1 共享图标资产出口）。</summary>
        public static BitmapSource? GetIcon(string path) => IconAssets.GetIcon(path);

        /// <summary>自定义图标目录（委托 S1 共享图标资产出口）。</summary>
        public static string GetCustomIconsDirectory() => IconAssets.GetCustomIconsDirectory();

        /// <summary>自定义图标列表（委托 S1，条目适配回兼容类型）。</summary>
        public static List<CustomIconItem> GetCustomIcons()
            => IconAssets.GetCustomIcons().Select(ToLegacyCustomItem).ToList();

        /// <summary>SVG 路径数据提取（委托 S1 共享图标资产出口）。</summary>
        public static string ExtractSvgPathData(string svgContent) => IconAssets.ExtractSvgPathData(svgContent);

        /// <summary>导入自定义图标文件（委托 S1，条目适配回兼容类型）。</summary>
        public static CustomIconItem? ImportCustomIcon(string sourceFilePath, string? customName = null)
        {
            IconAssets.CustomIconItem? imported = IconAssets.ImportCustomIcon(sourceFilePath, customName);
            return imported == null ? null : ToLegacyCustomItem(imported);
        }

        /// <summary>导入自定义 SVG 路径数据（委托 S1，条目适配回兼容类型）。</summary>
        public static CustomIconItem? ImportCustomSvgData(string svgPathData, string iconName)
        {
            IconAssets.CustomIconItem? imported = IconAssets.ImportCustomSvgData(svgPathData, iconName);
            return imported == null ? null : ToLegacyCustomItem(imported);
        }

        /// <summary>删除自定义图标（委托 S1 共享图标资产出口）。</summary>
        public static bool DeleteCustomIcon(string key) => IconAssets.DeleteCustomIcon(key);

        /// <summary>自定义图标图像源（委托 S1 共享图标资产出口）。</summary>
        public static ImageSource? GetCustomImageSource(string iconKeyOrPath) => IconAssets.GetCustomImageSource(iconKeyOrPath);

        private static CustomIconItem ToLegacyCustomItem(IconAssets.CustomIconItem item) => new CustomIconItem
        {
            Key = item.Key,
            DisplayName = item.DisplayName,
            FilePath = item.FilePath,
            SvgData = item.SvgData,
        };
    }
}
