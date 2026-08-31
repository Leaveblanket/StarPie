using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace WinPieGestures.ViewModels
{
    /// <summary>
    /// 颜色选择器 ViewModel (T08, ADR-0001/0004)：HSV 状态机、十六进制输入解析、
    /// 预览/色盘画刷与确认结果全部在此；视图只做色盘取点、滑杆/输入框绑定与本地化文案。
    /// HSV/RGB 换算为静态纯函数，直接可测。
    /// </summary>
    public partial class ColorPickerViewModel : ObservableObject
    {
        private const string DefaultHex = "#FF2563EB";

        /// <summary>预设色卡（与迁移前一致）。</summary>
        public static readonly string[] PresetColors =
        {
            "#EB18181B", "#F0F8FAFC", "#FF2563EB", "#FF3B82F6", "#FF60A5FA", "#FF06B6D4", "#FF0EA5E9",
            "#FF10B981", "#FF22C55E", "#FF84CC16", "#FFEAB308", "#FFF97316", "#FFEF4444", "#FFF43F5E",
            "#FFEC4899", "#FFD946EF", "#FFA855F7", "#FF8B5CF6", "#FF6366F1", "#FF475569", "#FF64748B",
            "#FF94A3B8", "#FFCBD5E1", "#FFF1F5F9", "#FFFFFF", "#FF000000", "#9016161A", "#35FFFFFF",
            "#E06C4DFF", "#A0FFFFFF", "#E0FFFFFF", "#E60F172A", "#E61E1B4B", "#E6142E1F", "#E6181111"
        };

        private readonly IDialogService _dialogs;

        private double _saturation = 1; // 0 ~ 1
        private double _value = 1;      // 0 ~ 1
        private bool _isUpdating;

        /// <summary>色盘当前点的饱和度（0 ~ 1），视图重定位取色圈用。</summary>
        public double Saturation => _saturation;

        /// <summary>色盘当前点的明度（0 ~ 1），视图重定位取色圈用。</summary>
        public double Value => _value;

        /// <summary>当前结果色（#AARRGGBB，大写）。</summary>
        public string SelectedHexColor { get; private set; } = DefaultHex;

        [ObservableProperty]
        private double _hue; // 0 ~ 360

        [ObservableProperty]
        private double _alpha = 255; // 0 ~ 255

        /// <summary>十六进制输入框内容（确认前与 <see cref="SelectedHexColor"/> 同步刷新）。</summary>
        [ObservableProperty]
        private string _hexText = DefaultHex;

        /// <summary>色盘底色（当前色相的纯色）。</summary>
        [ObservableProperty]
        private SolidColorBrush _spectrumBrush = new(System.Windows.Media.Color.FromRgb(255, 0, 0));

        /// <summary>预览块画刷（当前完整颜色，含透明度）。</summary>
        [ObservableProperty]
        private SolidColorBrush _previewBrush = new(System.Windows.Media.Color.FromRgb(37, 99, 235));

        /// <summary>色相/饱和度/明度变化后通知视图重定位取色圈（画布尺寸归视图）。</summary>
        public event Action? SpectrumChanged;

        public ColorPickerViewModel(IDialogService dialogs, string initialHex = DefaultHex)
        {
            _dialogs = dialogs;
            SetColorFromHex(string.IsNullOrWhiteSpace(initialHex) ? DefaultHex : initialHex);
        }

        partial void OnHueChanged(double value)
        {
            if (_isUpdating) return;
            UpdateSpectrumBrush();
            UpdatePreview();
        }

        partial void OnAlphaChanged(double value)
        {
            if (_isUpdating) return;
            UpdatePreview();
        }

        partial void OnHexTextChanged(string value)
        {
            if (_isUpdating) return;
            var text = value?.Trim() ?? "";
            if (text.Length != 7 && text.Length != 9) return;
            try
            {
                if (!text.StartsWith("#")) text = "#" + text;
                var color = (Color)ColorConverter.ConvertFromString(text);

                ApplyParsedColor(color);

                UpdateSpectrumBrush();
                RaiseSpectrumChanged();
                UpdatePreview();
            }
            catch { }
        }

        /// <summary>按十六进制串整体设置颜色（滑杆/输入框/预览同步，与迁移前一致）。</summary>
        public void SetColorFromHex(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex)) return;
            try
            {
                if (!hex.StartsWith("#")) hex = "#" + hex;
                var color = (Color)ColorConverter.ConvertFromString(hex);

                _isUpdating = true;
                HexText = hex.ToUpper();
                _isUpdating = false;

                ApplyParsedColor(color);

                UpdateSpectrumBrush();
                RaiseSpectrumChanged();
                UpdatePreview();
            }
            catch { }
        }

        /// <summary>应用已解析的颜色：同步 HSV 状态与滑杆（抑制回环，与迁移前一致）。</summary>
        private void ApplyParsedColor(Color color)
        {
            var (h, s, v) = ColorToHsv(color);

            _isUpdating = true;
            _saturation = s;
            _value = v;
            Hue = h;
            Alpha = color.A;
            _isUpdating = false;
        }

        /// <summary>色盘取点：饱和度/明度归一化值越界夹紧后更新结果色。</summary>
        public void SetSpectrumPoint(double saturation, double value)
        {
            _saturation = Math.Max(0, Math.Min(1, saturation));
            _value = Math.Max(0, Math.Min(1, value));
            RaiseSpectrumChanged();
            UpdatePreview();
        }

        /// <summary>屏上取色：经对话框服务开全屏取色器，取回后应用到当前色（取消则不动）。</summary>
        [RelayCommand]
        private void Eyedropper()
        {
            var picked = _dialogs.ShowEyedropper();
            if (picked != null)
            {
                SetColorFromHex(picked.HexColor);
            }
        }

        /// <summary>确认结果：未得到有效色时返回 null，调用方只判一次 null。</summary>
        public ColorPickResult? BuildResult()
            => string.IsNullOrEmpty(SelectedHexColor) ? null : new ColorPickResult(SelectedHexColor);

        private void UpdateSpectrumBrush() => SpectrumBrush = new SolidColorBrush(HsvToRgb(Hue, 1, 1));

        private void UpdatePreview()
        {
            var rgb = HsvToRgb(Hue, _saturation, _value);
            var finalColor = Color.FromArgb((byte)Alpha, rgb.R, rgb.G, rgb.B);
            SelectedHexColor = $"#{finalColor.A:X2}{finalColor.R:X2}{finalColor.G:X2}{finalColor.B:X2}";

            PreviewBrush = new SolidColorBrush(finalColor);

            // 与迁移前一致：程序化写输入框时抑制回环解析。
            if (!_isUpdating)
            {
                _isUpdating = true;
                HexText = SelectedHexColor;
                _isUpdating = false;
            }
        }

        private void RaiseSpectrumChanged() => SpectrumChanged?.Invoke();

        #region HSV / RGB Conversion（迁移自 ColorPickerWindow，算法不变）

        /// <summary>HSV → RGB：h ∈ [0, 360)，s/v ∈ [0, 1]。</summary>
        public static Color HsvToRgb(double h, double s, double v)
        {
            int hi = (int)Math.Floor(h / 60) % 6;
            double f = (h / 60) - Math.Floor(h / 60);

            v *= 255;
            byte vVal = (byte)Math.Max(0, Math.Min(255, v));
            byte p = (byte)Math.Max(0, Math.Min(255, v * (1 - s)));
            byte q = (byte)Math.Max(0, Math.Min(255, v * (1 - f * s)));
            byte t = (byte)Math.Max(0, Math.Min(255, v * (1 - (1 - f) * s)));

            switch (hi)
            {
                case 0: return Color.FromRgb(vVal, t, p);
                case 1: return Color.FromRgb(q, vVal, p);
                case 2: return Color.FromRgb(p, vVal, t);
                case 3: return Color.FromRgb(p, q, vVal);
                case 4: return Color.FromRgb(t, p, vVal);
                default: return Color.FromRgb(vVal, p, q);
            }
        }

        /// <summary>RGB → HSV：h ∈ [0, 360)，s/v ∈ [0, 1]；返回色相便于一行接线。</summary>
        public static (double Hue, double Saturation, double Value) ColorToHsv(Color color)
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

        #endregion
    }
}
