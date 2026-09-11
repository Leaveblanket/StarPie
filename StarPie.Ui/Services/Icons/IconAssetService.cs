using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using StarPie.Icons;
using StarPie.Services.Icons;

namespace StarPie.Services.Icons
{
    /// <summary>
    /// 「图标资产」实例服务（Ui 侧）：在宿主内核的自定义图标目录之上做 WPF 图像构造——
    /// 自定义位图源与文件/程序图标提取（Win32 Shell）；目录扫描/导入/删除委托
    /// <see cref="CustomIconStore"/>。.lnk 目标解析经注入的
    /// <see cref="IShortcutTargetResolver"/>（宿主内核实现）。
    /// </summary>
    public sealed class IconAssetService : IIconAssetService
    {
        private readonly CustomIconStore _store;
        private readonly IShortcutTargetResolver _shortcutTargetResolver;

        public IconAssetService(
            CustomIconStore store,
            IShortcutTargetResolver shortcutTargetResolver)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _shortcutTargetResolver = shortcutTargetResolver ?? throw new ArgumentNullException(nameof(shortcutTargetResolver));
        }

        /// <inheritdoc/>
        public string GetCustomIconsDirectory() => _store.GetCustomIconsDirectory();

        /// <inheritdoc/>
        public IReadOnlyList<CustomIconItem> GetCustomIcons() => _store.GetCustomIcons();

        /// <inheritdoc/>
        public CustomIconItem? ImportCustomIcon(string sourceFilePath, string? customName = null)
            => _store.ImportCustomIcon(sourceFilePath, customName);

        /// <inheritdoc/>
        public CustomIconItem? ImportCustomSvgData(string svgPathData, string iconName)
            => _store.ImportCustomSvgData(svgPathData, iconName);

        /// <inheritdoc/>
        public bool DeleteCustomIcon(string key) => _store.DeleteCustomIcon(key);

        /// <summary>
        /// 取自定义图标的位图源：入参可为 "custom:xxx" 键（解析为文件路径）或直接的文件路径；
        /// 仅位图可返回 <see cref="BitmapImage"/>，SVG 交由 XAML 几何绑定，此处返回 null。
        /// </summary>
        public ImageSource? GetCustomImageSource(string iconKeyOrPath)
        {
            if (string.IsNullOrWhiteSpace(iconKeyOrPath)) return null;

            try
            {
                string path = iconKeyOrPath;
                if (iconKeyOrPath.StartsWith("custom:", StringComparison.OrdinalIgnoreCase))
                {
                    var item = GetCustomIcons().FirstOrDefault(i => i.Key == iconKeyOrPath);
                    if (item != null) path = item.FilePath;
                }

                if (File.Exists(path))
                {
                    string ext = Path.GetExtension(path).ToLower();
                    if (ext != ".svg")
                    {
                        var bi = new BitmapImage();
                        bi.BeginInit();
                        bi.CacheOption = BitmapCacheOption.OnLoad;
                        bi.UriSource = new Uri(path, UriKind.Absolute);
                        bi.EndInit();
                        bi.Freeze();
                        return bi;
                    }
                }
            }
            catch { }

            return null;
        }

        /// <summary>SHGetFileInfo 输出结构（Win32 Shell API）：承载图标句柄、显示名与类型名。</summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        // Win32 SHGetFileInfo 标志：取图标、大图标、对不存在的文件按属性/类型返回图标。
        private const uint SHGFI_ICON = 0x100;
        private const uint SHGFI_LARGEICON = 0x0;
        private const uint SHGFI_USEFILEATTRIBUTES = 0x10;

        /// <summary>Win32 shell32!SHGetFileInfo：按路径/属性取系统图标句柄。</summary>
        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        /// <summary>Win32 shell32!ExtractIconEx：从可执行/图标文件提取大小图标句柄。</summary>
        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern uint ExtractIconEx(string szFileName, int nIconIndex, out IntPtr phiconLarge, out IntPtr phiconSmall, uint nIcons);

        /// <summary>Win32 user32!DestroyIcon：释放由上述 API 取得的图标句柄，防止 GDI 泄漏。</summary>
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        /// <summary>
        /// 提取干净的高分辨率程序/文件图标（不含 Windows 快捷方式的小箭头角标叠加）。
        /// 解析顺序：快捷方式先解析目标（自定义图标文件优先，其次目标程序自身）→
        /// 直接按文件/目录路径提取 → 文件不存在时退回按属性取系统关联图标。
        /// </summary>
        public BitmapSource? GetIcon(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            try
            {
                string resolvedPath = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));

                // 1. 若是快捷方式（.lnk），先解析到实际目标程序或图标文件。
                if (resolvedPath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                {
                    if (_shortcutTargetResolver.ResolveShortcutTarget(resolvedPath, out string targetPath, out string iconPath, out int iconIndex))
                    {
                        // 优先级 A：快捷方式里显式指定的自定义图标文件。
                        if (!string.IsNullOrEmpty(iconPath) && File.Exists(iconPath))
                        {
                            var icon = ExtractPureIconFromFile(iconPath, iconIndex);
                            if (icon != null) return icon;
                        }

                        // 优先级 B：目标程序自身的图标。
                        if (!string.IsNullOrEmpty(targetPath) && File.Exists(targetPath))
                        {
                            var icon = ExtractPureIconFromFile(targetPath, 0);
                            if (icon != null) return icon;
                        }
                    }
                }

                // 2. 普通文件/目录路径直接提取。
                if (File.Exists(resolvedPath) || Directory.Exists(resolvedPath))
                {
                    var icon = ExtractPureIconFromFile(resolvedPath, 0);
                    if (icon != null) return icon;
                }

                // 3. 兜底：文件物理上不存在时，按属性让系统返回关联类型图标。
                SHFILEINFO shinfoAttr = new SHFILEINFO();
                IntPtr hImgAttr = SHGetFileInfo(resolvedPath, 256, ref shinfoAttr, (uint)Marshal.SizeOf(shinfoAttr), SHGFI_ICON | SHGFI_LARGEICON | SHGFI_USEFILEATTRIBUTES);
                if (shinfoAttr.hIcon != IntPtr.Zero)
                {
                    try
                    {
                        BitmapSource bmpSrc = Imaging.CreateBitmapSourceFromHIcon(
                            shinfoAttr.hIcon,
                            Int32Rect.Empty,
                            BitmapSizeOptions.FromEmptyOptions()
                        );
                        bmpSrc.Freeze();
                        return bmpSrc;
                    }
                    finally
                    {
                        DestroyIcon(shinfoAttr.hIcon);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to extract icon for '{path}': {ex.Message}");
            }

            return null;
        }

        /// <summary>从单个文件按索引提取无角标大图标；ExtractIconEx 失败时回退 SHGetFileInfo。</summary>
        private static BitmapSource? ExtractPureIconFromFile(string filePath, int iconIndex)
        {
            try
            {
                // 优先用 ExtractIconEx 取大尺寸无角标图标。
                uint count = ExtractIconEx(filePath, iconIndex, out IntPtr hIconLarge, out IntPtr hIconSmall, 1);
                if (count > 0 && hIconLarge != IntPtr.Zero)
                {
                    try
                    {
                        BitmapSource bmpSrc = Imaging.CreateBitmapSourceFromHIcon(
                            hIconLarge,
                            Int32Rect.Empty,
                            BitmapSizeOptions.FromEmptyOptions()
                        );
                        bmpSrc.Freeze();
                        return bmpSrc;
                    }
                    finally
                    {
                        DestroyIcon(hIconLarge);
                        if (hIconSmall != IntPtr.Zero) DestroyIcon(hIconSmall);
                    }
                }
                else if (count > 0 && hIconSmall != IntPtr.Zero)
                {
                    try
                    {
                        BitmapSource bmpSrc = Imaging.CreateBitmapSourceFromHIcon(
                            hIconSmall,
                            Int32Rect.Empty,
                            BitmapSizeOptions.FromEmptyOptions()
                        );
                        bmpSrc.Freeze();
                        return bmpSrc;
                    }
                    finally
                    {
                        DestroyIcon(hIconSmall);
                    }
                }

                // 兜底：改用 SHGetFileInfo 取系统图标。
                SHFILEINFO shinfo = new SHFILEINFO();
                IntPtr hImg = SHGetFileInfo(filePath, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), SHGFI_ICON | SHGFI_LARGEICON);
                if (shinfo.hIcon != IntPtr.Zero)
                {
                    try
                    {
                        BitmapSource bmpSrc = Imaging.CreateBitmapSourceFromHIcon(
                            shinfo.hIcon,
                            Int32Rect.Empty,
                            BitmapSizeOptions.FromEmptyOptions()
                        );
                        bmpSrc.Freeze();
                        return bmpSrc;
                    }
                    finally
                    {
                        DestroyIcon(shinfo.hIcon);
                    }
                }
            }
            catch { }

            return null;
        }
    }
}
