using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace WinPieGestures.Views.Converters
{
    /// <summary>按 CoreIconType/自定义图标键/SVG 路径解析核圆预览 Geometry（T21 文本绑定化配套）。
    /// 纯视觉几何解析（ADR-0009 #3）：IconHelper 是 View/Renderer 共用的静态几何辅助，非组合根/服务/配置。</summary>
    public sealed class CoreIconGeometryConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            string type = values.Length > 0 ? values[0] as string ?? "" : "";
            string key = values.Length > 1 ? values[1] as string ?? "" : "";
            string svg = values.Length > 2 ? values[2] as string ?? "" : "";
            return IconHelper.GetCoreIconGeometry(type, key, svg);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
