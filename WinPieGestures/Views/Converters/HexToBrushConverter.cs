using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using WinPieGestures.Models;

namespace WinPieGestures.Views.Converters
{
    /// <summary>Converts a hex color string from a ViewModel into a WPF brush.</summary>
    public sealed class HexToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string hex && RgbColor.TryParseHex(hex, out var color))
            {
                return new SolidColorBrush(System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B));
            }

            return Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
