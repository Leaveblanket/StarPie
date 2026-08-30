using System;
using System.Windows;
using System.Windows.Input;
using MessageBox = System.Windows.MessageBox;

namespace WinPieGestures
{
    public partial class InputDialog : Window
    {
        public string InputText { get; private set; } = "";
        private readonly Func<string, (bool IsValid, string ErrorMessage)>? _validator;

        public InputDialog(string title, string prompt, string defaultText = "", Func<string, (bool IsValid, string ErrorMessage)>? validator = null)
        {
            InitializeComponent();
            AppThemeManager.ApplyTheme(this, AppThemeManager.CurrentEffectiveTheme);
            Title = title;
            TitleTextBlock.Text = title;
            PromptTextBlock.Text = prompt;
            InputTextBox.Text = defaultText;
            _validator = validator;
            if (OkButton != null) OkButton.Content = I18n.T("BtnConfirm");
            if (CancelButton != null) CancelButton.Content = I18n.T("BtnCancel");

            Loaded += (s, e) =>
            {
                InputTextBox.Focus();
                InputTextBox.SelectAll();
            };
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            string trimmed = InputTextBox.Text.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                MessageBox.Show(I18n.T("InputDialogEmpty"), I18n.T("Notice"), MessageBoxButton.OK, MessageBoxImage.Warning);
                InputTextBox.Focus();
                return;
            }

            if (_validator != null)
            {
                var (isValid, errorMessage) = _validator(trimmed);
                if (!isValid)
                {
                    MessageBox.Show(errorMessage, I18n.T("Notice"), MessageBoxButton.OK, MessageBoxImage.Warning);
                    InputTextBox.Focus();
                    InputTextBox.SelectAll();
                    return;
                }
            }

            InputText = trimmed;
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void InputTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                OkButton_Click(sender, e);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                CancelButton_Click(sender, e);
                e.Handled = true;
            }
        }
    }
}
