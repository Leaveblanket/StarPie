using System;
using System.Globalization;
using System.Windows.Data;

namespace WinPieGestures.Views.Converters
{
    /// <summary>核圆自定义图标展示名（T21 文本绑定化）：优先图标键，其次 SVG，缺省提示默认五角星。</summary>
    public sealed class CoreIconNameConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            string key = values.Length > 0 ? values[0] as string ?? "" : "";
            string svg = values.Length > 1 ? values[1] as string ?? "" : "";
            if (!string.IsNullOrEmpty(key)) return key;
            if (!string.IsNullOrEmpty(svg)) return "自定义 SVG 图标";
            return "默认五角星 (点击更换)";
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
