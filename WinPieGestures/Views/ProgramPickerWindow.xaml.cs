﻿
using System.Windows;
using MessageBox = System.Windows.MessageBox;

namespace WinPieGestures
{
    /// <summary>
    /// 程序选择器窗口 (T06)：逻辑全部在 <see cref="ProgramPickerViewModel"/>——扫描编排、
    /// 搜索过滤与选择结果；code-behind 只剩本地化文案、把 VM 的关闭请求落成 DialogResult
    /// 以及未选中时的提示框。由 <see cref="DialogService"/> 创建，Owner 归设置窗口。
    /// </summary>
    public partial class ProgramPickerWindow : Window
    {
        private readonly ProgramPickerViewModel _vm;

        public ProgramPickerWindow(IThemeService themeService, IDialogService dialogService)
        {
            InitializeComponent();
            themeService.ApplyTheme(this, themeService.CurrentEffectiveTheme);
            _vm = new ProgramPickerViewModel(ProgramScanner.ScanInstalledPrograms, dialogService);
            DataContext = _vm;
            _vm.CloseRequested += result =>
            {
                if (result == null)
                {
                    MessageBox.Show("请选择一个程序，或者点击“手动浏览文件...”", "未选择", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                DialogResult = true;
                Close();
            };
            ApplyLocalization();
        }

        /// <summary>确认结果（仅在 DialogResult == true 时非空）。</summary>
        public ProgramPickResult? BuildResult() => _vm.BuildResult();

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await _vm.LoadAsync();
        }

        private void ProgramsListView_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _vm.ConfirmCommand.Execute(null);
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ApplyLocalization()
        {
            this.Title = $"{I18n.T("ProgramPickerTitle")} - StarPie";
            if (HeaderTitleText != null) HeaderTitleText.Text = I18n.T("ProgramPickerHeader");
            if (SearchPlaceholderText != null) SearchPlaceholderText.Text = I18n.T("ProgramPickerPlaceholder");
            // StatusTextBlock 的文本走 VM 的 StatusText 绑定，这里不能落本地值覆盖绑定。
            if (ManualBrowseButton != null) ManualBrowseButton.Content = I18n.T("BtnManualBrowse");
            if (OkButton != null) OkButton.Content = I18n.T("BtnConfirm");
            if (CancelButton != null) CancelButton.Content = I18n.T("BtnCancel");
        }
    }
}
