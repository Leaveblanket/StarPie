﻿
using System.Windows;
using System.ComponentModel;

namespace StarPie.Views.Dialogs
{
    /// <summary>
    /// 程序选择器窗口：逻辑全部在 <see cref="ProgramPickerViewModel"/>——扫描编排、
    /// 搜索过滤与选择结果；code-behind 只负责把 VM 的关闭请求落成 DialogResult
    /// 以及未选中时的提示框。由 <see cref="DialogService"/> 创建，Owner 归设置窗口。
    /// </summary>
    public partial class ProgramPickerWindow : Window
    {
        private readonly ProgramPickerViewModel _vm;

        public ProgramPickerWindow(IThemeService themeService, ProgramPickerViewModel viewModel, ILocalizationService localization)
        {
            InitializeComponent();
            themeService.ApplyWindowTheme(this);
            _vm = viewModel;
            DataContext = _vm;
            _vm.PropertyChanged += OnViewModelPropertyChanged;
            Title = $"{localization.GetString("ProgramPickerTitle")} - StarPie"; // 窗口标题品牌后缀拼接：XAML 表达不了，对话框每次新建即时取词
        }

        /// <summary>
        /// 设计期专用无参构造（ADR-0025/#101）：仅供 VS 设计器实例化根窗口以预览 L1 样例
        /// （d:DataContext 提供 ProgramPickerDesignTimeData）；运行时由
        /// <see cref="DialogService"/> 走带参构造装配，不触碰本构造。
        /// </summary>
        public ProgramPickerWindow()
        {
            InitializeComponent();
            _vm = null!;
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
