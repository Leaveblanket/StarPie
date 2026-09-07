using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace StarPie.Views.Converters
{
    /// <summary>按核图标类型/自定义图标键/SVG 路径解析核圆预览 Geometry。</summary>
    /// <remarks>纯视觉几何解析：经轮盘视觉几何出口 <see cref="WheelGeometry"/> 取值，
    /// 不经组合根/服务/配置。</remarks>
    public sealed class CoreIconGeometryConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            string type = values.Length > 0 ? values[0] as string ?? "" : "";
            string key = values.Length > 1 ? values[1] as string ?? "" : "";
            string svg = values.Length > 2 ? values[2] as string ?? "" : "";
            return WheelGeometry.GetCoreIconGeometry(type, key, svg);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
