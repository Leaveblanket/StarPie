using System;
using System.Globalization;
using System.Windows.Data;

namespace StarPie.Views.Converters
{
    /// <summary>
    /// 整数相等转换器：把绑定值与参数（均按整数解析）比较是否相等。
    /// 用于 RadioButton/单选场景——用 Tag 传数值，与 VM 的整型选择项绑定选中态。
    /// </summary>
    public sealed class IntEqualsConverter : IValueConverter
    {
        /// <summary>绑定值 == 参数（均解析为 int）时返回 true，否则 false。</summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => int.TryParse(value?.ToString(), out var actual) && int.TryParse(parameter?.ToString(), out var expected) && actual == expected;

        /// <summary>反向：仅当选中的 true 且参数可解析时回填该整数值，否则保持绑定原值（DoNothing）。</summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool selected && selected && int.TryParse(parameter?.ToString(), out var expected)
                ? expected
                : Binding.DoNothing;
    }
}
