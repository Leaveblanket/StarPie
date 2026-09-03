using System.Windows;
using System.ComponentModel;
using System.Windows.Input;

namespace WinPieGestures.Views.Dialogs
{
    /// <summary>
    /// 输入对话框窗口 (T07)：确认与验证逻辑全部在 <see cref="InputViewModel"/>（DataContext 绑定）——
    /// 标题/提示/输入文本走绑定，确认按钮绑 ConfirmCommand；code-behind 只剩主题应用、VM 事件接线、
    /// 本地化按钮文案、键盘路由（Enter=确认 / Esc=取消）、提示框与焦点行为。
    /// 由 <see cref="DialogService"/> 创建，Owner 归设置窗口。
    /// </summary>
    public partial class InputDialog : Window
    {
        private readonly InputViewModel _vm;

        public InputDialog(IThemeService themeService, InputViewModel viewModel)
        {
            InitializeComponent();
            themeService.ApplyTheme(this, themeService.CurrentEffectiveTheme);
            _vm = viewModel;
            DataContext = _vm;

            _vm.PropertyChanged += OnViewModelPropertyChanged;

            if (OkButton != null) OkButton.Content = I18n.T("BtnConfirm");
            if (CancelButton != null) CancelButton.Content = I18n.T("BtnCancel");

            Loaded += (s, e) =>
            {
                InputTextBox.Focus();
                InputTextBox.SelectAll();
            };
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(InputViewModel.IsCompleted))
            {
                DialogResult = true;
                Close();
                return;
            }

            if (e.PropertyName == nameof(InputViewModel.RejectedText))
            {
                InputTextBox.Focus();
                if (!string.IsNullOrEmpty(_vm.RejectedText))
                {
                    InputTextBox.SelectAll();
                }
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
