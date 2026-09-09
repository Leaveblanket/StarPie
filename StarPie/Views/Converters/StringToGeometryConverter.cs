using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace StarPie.Views.Converters
{
    /// <summary>把 ViewModel 提供的 SVG 路径数据（d 字符串）解析为 WPF Geometry；解析失败回退空几何。</summary>
    public sealed class StringToGeometryConverter : IValueConverter
    {
        /// <summary>路径字符串 → <see cref="Geometry"/>；空/非法数据返回 <see cref="Geometry.Empty"/>（不破坏绑定）。</summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string data && !string.IsNullOrWhiteSpace(data))
            {
                try
                {
                    return Geometry.Parse(data);
                }
                catch
                {
                    // 非法路径数据不应让绑定崩溃，回退到空几何。
                }
            }

            return Geometry.Empty;
        }

        /// <summary>单向转换，反向转换不支持。</summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
