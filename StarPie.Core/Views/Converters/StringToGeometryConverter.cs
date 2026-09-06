using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace WinPieGestures.Views.Converters
{
    /// <summary>Converts SVG path data from a ViewModel into a WPF Geometry.</summary>
    public sealed class StringToGeometryConverter : IValueConverter
    {
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
                    // Invalid path data should not crash binding; fall through to empty geometry.
                }
            }

            return Geometry.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
