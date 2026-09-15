using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using StarPie.Services.Icons;
using StarPie.Wheel;

namespace StarPie.Views.Renderers
{
    /// <summary>
    /// 扇区图标的两处机械映射（内容 → 元素、图标键 → 自定义图标条目）：运行时轮盘与外观页预览
    /// 共用同一份，避免同一映射在两个视图里各写一遍。
    /// </summary>
    /// <remarks>
    /// 只承载与绘图无关的映射：画刷、动画与 Canvas 绘制仍归各自的渲染方。
    /// </remarks>
    internal static class WheelSectorIconFactory
    {
        /// <summary>把扇区内容内核给出的图标内容画成元素：矢量画 <see cref="System.Windows.Shapes.Path"/>、
        /// 自定义位图与程序图标画 <see cref="Image"/>。
        /// 内容为 <see cref="WheelIconKind.None"/> 或程序图标提取不到时返回 null（该扇区不画图标元素）。</summary>
        public static FrameworkElement? Create(WheelSectorContent content, Brush? brush, IIconAssetService iconAssets)
        {
            double bottomMargin = content.IconBottomMargin;

            switch (content.Icon.Kind)
            {
                case WheelIconKind.SvgPath:
                    return new System.Windows.Shapes.Path
                    {
                        Data = Geometry.Parse(content.Icon.Data),
                        Fill = brush,
                        Stretch = Stretch.Uniform,
                        Width = content.IconSize,
                        Height = content.IconSize,
                        Margin = new Thickness(0, 0, 0, bottomMargin),
                        HorizontalAlignment = HorizontalAlignment.Center
                    };

                case WheelIconKind.CustomImageFile:
                    return new Image
                    {
                        Source = iconAssets.GetCustomImageSource(content.Icon.Data),
                        Width = content.IconSize,
                        Height = content.IconSize,
                        Stretch = Stretch.Uniform,
                        Margin = new Thickness(0, 0, 0, bottomMargin),
                        HorizontalAlignment = HorizontalAlignment.Center
                    };

                case WheelIconKind.ProgramIcon:
                    BitmapSource? iconSource = iconAssets.GetIcon(content.Icon.Data);
                    if (iconSource == null)
                    {
                        return null;
                    }
                    return new Image
                    {
                        Source = iconSource,
                        Width = content.IconSize,
                        Height = content.IconSize,
                        Stretch = Stretch.Uniform,
                        Margin = new Thickness(0, 0, 0, bottomMargin),
                        HorizontalAlignment = HorizontalAlignment.Center
                    };

                default:
                    return null;
            }
        }

        /// <summary>图标键命中自定义图标目录时的条目；只有 <c>custom:</c> 前缀才查目录
        /// （目录查询是服务调用，键前缀规则在内核）。</summary>
        public static CustomIconItem? ResolveCustomIcon(string? iconKey, IIconAssetService iconAssets)
            => WheelSectorContentKernel.IsCustomIconKey(iconKey)
                ? iconAssets.GetCustomIcons().FirstOrDefault(c => c.Key == iconKey)
                : null;
    }
}
