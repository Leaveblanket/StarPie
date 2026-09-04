using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace WinPieGestures.Views.Dialogs
{
    using Button = System.Windows.Controls.Button;
    using Color = System.Windows.Media.Color;
    using ColorConverter = System.Windows.Media.ColorConverter;

    /// <summary>
    /// 颜色选择器窗口 (T08)：HSV 状态机、十六进制输入解析与确认结果全部在
    /// <see cref="ColorPickerViewModel"/>；code-behind 只剩取色圈定位与把
    /// VM 完成/取消落成 DialogResult——色盘取点像素坐标翻译在
    /// <see cref="SpectrumCanvasBehavior"/>（ADR-0009）。由 <see cref="DialogService"/> 创建，
    /// Owner 归设置窗口。屏上取色覆盖层已拆分到 <see cref="ScreenEyedropperWindow"/>。
    /// </summary>
    public partial class ColorPickerWindow : Window
    {
        private readonly ColorPickerViewModel _vm;

        public ColorPickerWindow(IThemeService themeService, ColorPickerViewModel viewModel)
        {
            InitializeComponent();
            themeService.ApplyTheme(this, themeService.CurrentEffectiveTheme);
            _vm = viewModel;
            _vm.PropertyChanged += OnViewModelPropertyChanged;
            DataContext = _vm;
            PopulateSwatches();
            UpdateSpectrumThumbPosition();
            Title = $"{I18n.T("ColorPickerTitle")} - StarPie"; // ADR-0010 例外:窗口标题品牌后缀拼接(XAML 表达不了),对话框每次 Show* 新建即时取词
        }

        /// <summary>确认结果（仅在 DialogResult == true 时非空）。</summary>
        public ColorPickResult? BuildResult() => _vm.BuildResult();

        private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ColorPickerViewModel.IsCompleted))
            {
                DialogResult = true;
                Close();
                return;
            }

            if (e.PropertyName == nameof(ColorPickerViewModel.Saturation) ||
                e.PropertyName == nameof(ColorPickerViewModel.Value))
            {
                UpdateSpectrumThumbPosition();
            }
        }

        private void PopulateSwatches()
        {
            SwatchesPanel.Children.Clear();
            foreach (var hex in ColorPickerViewModel.PresetColors)
            {
                try
                {
                    var col = (Color)ColorConverter.ConvertFromString(hex);
                    var btn = new Button
                    {
                        Style = (Style)FindResource("ColorSwatchButtonStyle"),
                        Background = new SolidColorBrush(col),
                        ToolTip = hex,
                        Command = _vm.SetColorFromHexActionCommand,
                        CommandParameter = hex
                    };
                    SwatchesPanel.Children.Add(btn);
                }
                catch { }
            }
        }

        private void UpdateSpectrumThumbPosition()
        {
            double w = SpectrumCanvas.ActualWidth > 0 ? SpectrumCanvas.ActualWidth : 440;
            double h = SpectrumCanvas.ActualHeight > 0 ? SpectrumCanvas.ActualHeight : 180;

            Canvas.SetLeft(SpectrumThumb, _vm.Saturation * w);
            Canvas.SetTop(SpectrumThumb, (1.0 - _vm.Value) * h);
        }

        private void SwatchesScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is ScrollViewer scv)
            {
                scv.ScrollToVerticalOffset(scv.VerticalOffset - (e.Delta / 3.0));
                e.Handled = true;
            }
        }

        // ADR-0009：取消无业务语义，Click→DialogResult=false 属 code-behind 白名单。
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
