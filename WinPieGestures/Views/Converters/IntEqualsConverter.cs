using System;
using System.Globalization;
using System.Windows.Data;

namespace WinPieGestures.Views.Converters
{
    public sealed class IntEqualsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => int.TryParse(value?.ToString(), out var actual) && int.TryParse(parameter?.ToString(), out var expected) && actual == expected;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool selected && selected && int.TryParse(parameter?.ToString(), out var expected)
                ? expected
                : Binding.DoNothing;
    }
}
