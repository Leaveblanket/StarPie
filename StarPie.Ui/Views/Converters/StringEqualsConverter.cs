using System;
using System.Globalization;
using System.Windows.Data;

namespace StarPie.Ui.Views.Converters
{
    /// <summary>
    /// 字符串相等转换器：把绑定值与参数（均按字符串）比较是否相等。
    /// 用于 RadioButton/单选场景——与 VM 的字符串选择项绑定选中态（参数传配置键名，
    /// 如触发键的 "LeftButton"）；与 <see cref="IntEqualsConverter"/> 同族同语义。
    /// </summary>
    public sealed class StringEqualsConverter : IValueConverter
    {
        /// <summary>绑定值与参数按序数忽略大小写相等时返回 true，否则 false。</summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.OrdinalIgnoreCase);

        /// <summary>反向：仅当选中的 true 且参数为字符串时回填该值，否则保持绑定原值（DoNothing）。</summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool selected && selected && parameter is string expected
                ? expected
                : Binding.DoNothing;
    }
}
