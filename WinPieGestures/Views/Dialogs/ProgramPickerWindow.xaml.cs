﻿
using System.Windows;
using System.ComponentModel;

namespace WinPieGestures.Views.Dialogs
{
    /// <summary>
    /// 程序选择器窗口 (T06)：逻辑全部在 <see cref="ProgramPickerViewModel"/>——扫描编排、
    /// 搜索过滤与选择结果；code-behind 只负责把 VM 的关闭请求落成 DialogResult
    /// 以及未选中时的提示框。由 <see cref="DialogService"/> 创建，Owner 归设置窗口。
    /// </summary>
    public partial class ProgramPickerWindow : Window
    {
        private readonly ProgramPickerViewModel _vm;

        public ProgramPickerWindow(IThemeService themeService, ProgramPickerViewModel viewModel, ILocalizationService localization)
        {
            InitializeComponent();
            themeService.ApplyTheme(this, themeService.CurrentEffectiveTheme);
            _vm = viewModel;
            DataContext = _vm;
            _vm.PropertyChanged += OnViewModelPropertyChanged;
            Title = $"{localization.GetString("ProgramPickerTitle")} - StarPie"; // ADR-0010 例外:窗口标题品牌后缀拼接(XAML 表达不了),对话框每次 Show* 新建即时取词
        }

        /// <summary>确认结果（仅在 DialogResult == true 时非空）。</summary>
        public ProgramPickResult? BuildResult() => _vm.BuildResult();

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ProgramPickerViewModel.IsCompleted))
            {
                DialogResult = true;
                Close();
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

    }
}
