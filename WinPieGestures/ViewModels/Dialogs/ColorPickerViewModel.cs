using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinPieGestures.Models;

namespace WinPieGestures.ViewModels.Dialogs
{
    /// <summary>色盘取点归一化坐标（Saturation/Value ∈ [0,1]；View 附加行为翻译像素坐标后传入 VM 命令）。</summary>
    public readonly record struct SpectrumPoint(double Saturation, double Value);

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

        [ObservableProperty]
        private double _saturation = 1; // 0 ~ 1

        [ObservableProperty]
        private double _value = 1;      // 0 ~ 1
        private bool _isUpdating;

        /// <summary>当前结果色（#AARRGGBB，大写）。</summary>
        public string SelectedHexColor { get; private set; } = DefaultHex;

        [ObservableProperty]
        private double _hue; // 0 ~ 360

        [ObservableProperty]
        private double _alpha = 255; // 0 ~ 255

        /// <summary>十六进制输入框内容（确认前与 <see cref="SelectedHexColor"/> 同步刷新）。</summary>
        [ObservableProperty]
        private string _hexText = DefaultHex;

        /// <summary>色盘底色（当前色相的纯色），View 经 HexToBrushConverter 转为画刷。</summary>
        [ObservableProperty]
        private string _spectrumHex = DefaultHex;

        /// <summary>预览块颜色（当前完整颜色，含透明度），View 经 HexToBrushConverter 转为画刷。</summary>
        [ObservableProperty]
        private string _previewHex = DefaultHex;

        /// <summary>确认后变为 true，视图据此关闭窗口。</summary>
        [ObservableProperty]
        private bool _isCompleted;

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
                if (!RgbColor.TryParseHex(text, out var color)) return;

                ApplyParsedColor(color);

                UpdateSpectrumBrush();
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
                if (!RgbColor.TryParseHex(hex, out var color)) return;

                _isUpdating = true;
                HexText = hex.ToUpper();
                _isUpdating = false;

                ApplyParsedColor(color);

                UpdateSpectrumBrush();
                UpdatePreview();
            }
            catch { }
        }

        [RelayCommand]
        private void SetColorFromHexAction(string hex) => SetColorFromHex(hex);

        /// <summary>应用已解析的颜色：同步 HSV 状态与滑杆（抑制回环，与迁移前一致）。</summary>
        private void ApplyParsedColor(RgbColor color)
        {
            var (h, s, v) = ColorMath.RgbToHsv(color);

            _isUpdating = true;
            Saturation = s;
            Value = v;
            Hue = h;
            Alpha = color.A;
            _isUpdating = false;
        }

        /// <summary>色盘取点：饱和度/明度归一化值越界夹紧后更新结果色。</summary>
        public void SetSpectrumPoint(double saturation, double value)
        {
            Saturation = Math.Max(0, Math.Min(1, saturation));
            Value = Math.Max(0, Math.Min(1, value));
            UpdatePreview();
        }

        /// <summary>色盘取点命令：View 附加行为把 Canvas 像素坐标翻译成归一化点后经此进入（ADR-0009）。</summary>
        [RelayCommand]
        private void SetSpectrumPointAction(SpectrumPoint point) => SetSpectrumPoint(point.Saturation, point.Value);

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

        /// <summary>确认：请求关窗；取消由视图直接关窗。</summary>
        [RelayCommand]
        private void Confirm() => IsCompleted = true;

        private void UpdateSpectrumBrush() => SpectrumHex = ColorMath.HsvToRgb(Hue, 1, 1).ToHex();

        private void UpdatePreview()
        {
            var finalColor = ColorMath.HsvToRgb(Hue, Saturation, Value, (byte)Alpha);
            SelectedHexColor = finalColor.ToHex();

            PreviewHex = SelectedHexColor;

            // 与迁移前一致：程序化写输入框时抑制回环解析。
            if (!_isUpdating)
            {
                _isUpdating = true;
                HexText = SelectedHexColor;
                _isUpdating = false;
            }
        }

        #region HSV / RGB Conversion（迁移自 ColorPickerWindow，算法不变）

        /// <summary>HSV → RGB：h ∈ [0, 360)，s/v ∈ [0, 1]。</summary>
        public static RgbColor HsvToRgb(double h, double s, double v) => ColorMath.HsvToRgb(h, s, v);

        /// <summary>RGB → HSV：h ∈ [0, 360)，s/v ∈ [0, 1]；返回色相便于一行接线。</summary>
        public static (double Hue, double Saturation, double Value) ColorToHsv(RgbColor color) => ColorMath.RgbToHsv(color);

        #endregion
    }
}
