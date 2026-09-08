using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace StarPie.Services.Icons
{
    /// <summary>
    /// 「图标资产」实例服务契约：自定义图标存储（目录/列表缓存/导入/删除/图像源）与
    /// 文件/程序图标提取（<see cref="GetIcon"/>）。内置矢量图标清单与 SVG 键目录等
    /// 无状态纯目录见 <see cref="IconCatalog"/>（静态）。
    /// </summary>
    public interface IIconAssetService
    {
        /// <summary>返回自定义图标目录（应用数据目录/CustomIcons），不存在时先创建。</summary>
        string GetCustomIconsDirectory();

        /// <summary>扫描自定义图标目录并返回全部条目（首次调用后缓存）：.svg 解析路径数据，
        /// 其余位图（png/jpg/jpeg/ico/bmp/webp）仅记录文件路径。</summary>
        IReadOnlyList<CustomIconItem> GetCustomIcons();

        /// <summary>导入自定义图标文件到自定义图标目录：文件名清洗非法字符后追加时间戳避免重名，
        /// 复制成功后使缓存失效并返回新条目。源文件不存在或复制失败返回 null。</summary>
        CustomIconItem? ImportCustomIcon(string sourceFilePath, string? customName = null);

        /// <summary>把一段 SVG 路径数据保存为自定义 .svg 图标文件（名称清洗并追加时间戳），成功后返回新条目。</summary>
        CustomIconItem? ImportCustomSvgData(string svgPathData, string iconName);

        /// <summary>按键删除自定义图标（同时删除磁盘文件并使缓存失效）；未找到或删除失败返回 false。</summary>
        bool DeleteCustomIcon(string key);

        /// <summary>取自定义图标的位图源：入参可为 "custom:xxx" 键（解析为文件路径）或直接的文件路径；
        /// 仅位图可返回 <see cref="BitmapImage"/>，SVG 交由 XAML 几何绑定，此处返回 null。</summary>
        ImageSource? GetCustomImageSource(string iconKeyOrPath);

        /// <summary>提取干净的高分辨率程序/文件图标（不含 Windows 快捷方式的小箭头角标叠加）。
        /// 解析顺序：快捷方式先解析目标（自定义图标文件优先，其次目标程序自身）→
        /// 直接按文件/目录路径提取 → 文件不存在时退回按属性取系统关联图标。</summary>
        BitmapSource? GetIcon(string path);
    }
}
