using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace WinPieGestures.Views.Converters
{
    /// <summary>本地图片路径 → 缩略图（T21：核圆图片缩略图随绑定刷新，替换 code-behind 回填；缺文件返回空）。</summary>
    public sealed class FilePathToImageConverter : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string path && !string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                try
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = new Uri(path, UriKind.Absolute);
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    bmp.Freeze();
                    return bmp;
                }
                catch
                {
                    // 无效/损坏图片不 crash 绑定，回退空白缩略图。
                }
            }

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
