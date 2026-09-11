using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using StarPie.Models;

namespace StarPie.Views.Converters
{
    /// <summary>把 ViewModel 提供的 "#AARRGGBB" 颜色字符串转为 WPF 画刷；解析失败回退透明画刷。</summary>
    public sealed class HexToBrushConverter : IValueConverter
    {
        /// <summary>字符串颜色 → <see cref="SolidColorBrush"/>；非法/空值返回透明画刷（不破坏绑定）。</summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string hex && RgbColor.TryParseHex(hex, out var color))
            {
                return new SolidColorBrush(System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B));
            }

            return Brushes.Transparent;
        }

        /// <summary>单向转换，反向转换不支持。</summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
