using System;

namespace WinPieGestures.Models
{
    /// <summary>
    /// WPF-free color value. ViewModels and pure logic use this type instead of
    /// System.Windows.Media.Color / SolidColorBrush. The View layer converts it to
    /// a WPF brush only at the presentation boundary.
    /// </summary>
    public readonly struct RgbColor
    {
        public byte A { get; }
        public byte R { get; }
        public byte G { get; }
        public byte B { get; }

        public RgbColor(byte a, byte r, byte g, byte b)
        {
            A = a;
            R = r;
            G = g;
            B = b;
        }

        public string ToHex() => $"#{A:X2}{R:X2}{G:X2}{B:X2}";

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
                    color = new RgbColor(255, (byte)((value >> 16) & 0xFF), (byte)((value >> 8) & 0xFF), (byte)(value & 0xFF));
                    return true;
                }

                color = new RgbColor((byte)((value >> 24) & 0xFF), (byte)((value >> 16) & 0xFF), (byte)((value >> 8) & 0xFF), (byte)(value & 0xFF));
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>Pure HSV/RGB helpers used by dialog and appearance ViewModels.</summary>
    public static class ColorMath
    {
        public static RgbColor HsvToRgb(double h, double s, double v, byte alpha = 255)
        {
            int hi = (int)Math.Floor(h / 60) % 6;
            double f = (h / 60) - Math.Floor(h / 60);

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

                h *= 60;
                if (h < 0) h += 360;
            }

            return (h, s, v);
        }
    }
}
