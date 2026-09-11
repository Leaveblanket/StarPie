using System;

namespace StarPie.Models
{
    /// <summary>
    /// 不依赖 WPF 的颜色值（ARGB 字节结构）。ViewModel 与纯逻辑层使用该类型而非
    /// System.Windows.Media.Color / SolidColorBrush；视图层只在呈现边界把它
    /// 转换为 WPF 画刷。
    /// </summary>
    public readonly struct RgbColor
    {
        /// <summary>Alpha 通道（0–255）。</summary>
        public byte A { get; }

        /// <summary>红色通道（0–255）。</summary>
        public byte R { get; }

        /// <summary>绿色通道（0–255）。</summary>
        public byte G { get; }

        /// <summary>蓝色通道（0–255）。</summary>
        public byte B { get; }

        /// <summary>按 ARGB 通道顺序构造颜色。</summary>
        public RgbColor(byte a, byte r, byte g, byte b)
        {
            A = a;
            R = r;
            G = g;
            B = b;
        }

        /// <summary>格式化为 "#AARRGGBB" 十六进制字符串（Alpha 在前）。</summary>
        public string ToHex() => $"#{A:X2}{R:X2}{G:X2}{B:X2}";

        /// <summary>
        /// 解析 "#RRGGBB" 或 "#AARRGGBB" 十六进制字符串：解析成功返回 true 并输出颜色；
        /// 字符串为空、长度非法或含非法字符时返回 false。
        /// </summary>
        public static bool TryParseHex(string? hex, out RgbColor color)
        {
            color = default;
            if (string.IsNullOrWhiteSpace(hex)) return false;

            var text = hex.Trim();
            if (text.StartsWith("#", StringComparison.Ordinal)) text = text.Substring(1);
            if (text.Length != 6 && text.Length != 8) return false;

            try
            {
                int value = Convert.ToInt32(text, 16);
                if (text.Length == 6)
                {
                    // 6 位格式 "#RRGGBB" 没有 Alpha 位，按完全不透明（A=255）处理。
                    color = new RgbColor(255, (byte)((value >> 16) & 0xFF), (byte)((value >> 8) & 0xFF), (byte)(value & 0xFF));
                    return true;
                }

                // 8 位格式 "#AARRGGBB"：最高 8 位是 Alpha，其后依次为 R/G/B。
                color = new RgbColor((byte)((value >> 24) & 0xFF), (byte)((value >> 16) & 0xFF), (byte)((value >> 8) & 0xFF), (byte)(value & 0xFF));
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>HSV 与 RGB 互转的纯函数工具，供颜色对话框与外观设置 ViewModel 使用（无 UI 依赖）。</summary>
    public static class ColorMath
    {
        /// <summary>
        /// 把 HSV 颜色（色相 0–360°、饱和度/明度 0–1）转为 RGB，可指定 Alpha（默认 255）。
        /// 色相越界等非法输入按标准六扇区算法就近处理，结果分量始终钳制在 0–255。
        /// </summary>
        public static RgbColor HsvToRgb(double h, double s, double v, byte alpha = 255)
        {
            // 色相按 60° 一区划分，确定落在六个扇区中的哪一个（hi）及其内部偏移量 f。
            int hi = (int)Math.Floor(h / 60) % 6;
            double f = (h / 60) - Math.Floor(h / 60);

            // 预计算三个辅助分量：v（明度）、p（v·(1-s)）、q、t，再按扇区组合成 RGB。
            v *= 255;
            byte vVal = (byte)Math.Max(0, Math.Min(255, v));
            byte p = (byte)Math.Max(0, Math.Min(255, v * (1 - s)));
            byte q = (byte)Math.Max(0, Math.Min(255, v * (1 - f * s)));
            byte t = (byte)Math.Max(0, Math.Min(255, v * (1 - (1 - f) * s)));

            return hi switch
            {
                0 => new RgbColor(alpha, vVal, t, p),
                1 => new RgbColor(alpha, q, vVal, p),
                2 => new RgbColor(alpha, p, vVal, t),
                3 => new RgbColor(alpha, p, q, vVal),
                4 => new RgbColor(alpha, t, p, vVal),
                _ => new RgbColor(alpha, vVal, p, q)
            };
        }

        /// <summary>把 RGB 颜色转为 HSV 元组（Hue 0–360°，Saturation/Value 0–1）；灰色（无饱和度）时 Hue 为 0。</summary>
        public static (double Hue, double Saturation, double Value) RgbToHsv(RgbColor color)
        {
            double r = color.R / 255.0;
            double g = color.G / 255.0;
            double b = color.B / 255.0;

            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double delta = max - min;

            double v = max;
            double s = max <= 0 ? 0 : delta / max;

            // 通道间最大差值决定色相所在区间；差值为 0（灰色）时无意义，取 0。
            double h;
            if (delta <= 0)
            {
                h = 0;
            }
            else
            {
                if (Math.Abs(r - max) < 0.0001) h = (g - b) / delta;
                else if (Math.Abs(g - max) < 0.0001) h = 2 + (b - r) / delta;
                else h = 4 + (r - g) / delta;

                // 把区间内偏移换算为角度，负值转正（色相环 360° 回绕）。
                h *= 60;
                if (h < 0) h += 360;
            }

            return (h, s, v);
        }
    }
}
