using System;
using System.Windows;
using System.Windows.Controls;
using WinPieGestures.Services;
using WinPieGestures.ViewModels;

namespace WinPieGestures.Views.Pages
{
    /// <summary>
    /// 高级与系统页面 (T19)：迁移前 SettingsWindow PAGE 3 原样搬迁。页面 ViewModel 为
    /// <see cref="GeneralSettingsViewModel"/>（容器单例）：语言切换/自启/提权/导入导出编排已住 VM，
    /// 本视图只做弹窗映射（NoticeRequested）与语言下拉等 View 效果；落盘请求由 VM 消息上报组合根。
    /// </summary>
    public partial class AdvancedSettingsPage : SettingsPageBase
    {
        private bool _isUpdatingUi = true;

        private GeneralSettingsViewModel Vm => (GeneralSettingsViewModel)DataContext;

        public AdvancedSettingsPage()
        {
            InitializeComponent();
        }

        protected override void ApplyLocalization()
        {
            AdvancedPageHeader.Text = I18n.T("AdvancedHeader");
            LanguageTitleText.Text = I18n.T("LanguageTitle");
            LanguageDescText.Text = I18n.T("LanguageDesc");
            StartupTitleText.Text = I18n.T("StartupTitle");
            StartupDescText.Text = I18n.T("StartupDesc");
            ElevateTitleText.Text = I18n.T("ElevateTitle");
            ElevateDescText.Text = I18n.T("ElevateDesc");
            ElevateButton.Content = I18n.T("BtnElevate");
            MemoryOptTitleText.Text = I18n.T("MemoryTitle");
            MemoryOptDescText.Text = I18n.T("MemoryDesc");
            TrimMemoryButton.Content = I18n.T("BtnTrimMemory");
            BackupTitleText.Text = I18n.T("BackupTitle");
            ExportConfigButton.Content = I18n.T("BtnExportConfig");
            ImportConfigButton.Content = I18n.T("BtnImportConfig");
        }

        protected override void OnPageLoaded()
        {
            Vm.NoticeRequested += ShowNotice;
            Vm.ConfigReloaded += OnConfigReloaded;

            _isUpdatingUi = true;
            try
            {
                AutoStartCheckBox.IsChecked = Vm.AutoStartEnabled;
                SetComboBoxSelectedValue(LanguageComboBox, Vm.LanguageCode);
                UacWarningCard.Visibility = IsRunningAsAdmin() ? Visibility.Collapsed : Visibility.Visible;
            }
            finally
            {
                _isUpdatingUi = false;
            }
        }

        protected override void OnPageUnloaded()
        {
            Vm.NoticeRequested -= ShowNotice;
            Vm.ConfigReloaded -= OnConfigReloaded;
        }

        private void OnConfigReloaded()
        {
            _isUpdatingUi = true;
            try
            {
                SetComboBoxSelectedValue(LanguageComboBox, Vm.LanguageCode);
            }
            finally
            {
                _isUpdatingUi = false;
            }
        }

        private void ShowNotice(GeneralSettingsViewModel.NoticeRequest notice)
        {
            var image = notice.Kind switch
            {
                GeneralSettingsViewModel.NoticeKind.Error => MessageBoxImage.Error,
                GeneralSettingsViewModel.NoticeKind.Warning => MessageBoxImage.Warning,
                _ => MessageBoxImage.Information
            };
            MessageBox.Show(notice.Message, notice.Title, MessageBoxButton.OK, image);
        }

        // --- 事件处理器（迁移前窗口处理器原样搬迁；编排已住 VM） ---

        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingUi) return;
            // 语言切换编排（写配置、I18n.SetLanguage 触发广播）进 VM；文本刷新由各页订阅的广播完成。
            if (LanguageComboBox.SelectedItem is ComboBoxItem item && item.Tag is string langCode)
            {
                Vm.ApplyLanguage(langCode);
            }
        }

        private void AutoStartCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUi) return;
            Vm.SetAutoStart(AutoStartCheckBox.IsChecked == true);
        }

        private void ElevatePrivileges_Click(object sender, RoutedEventArgs e)
        {
            Vm.ElevateAndRestart();
        }

        private void ExportConfigButton_Click(object sender, RoutedEventArgs e)
        {
            Vm.ExportConfigCommand.Execute(null);
        }

        private void ImportConfigButton_Click(object sender, RoutedEventArgs e)
        {
            Vm.ImportConfigCommand.Execute(null);
        }

        private void TrimMemoryButton_Click(object sender, RoutedEventArgs e)
        {
            MemoryOptimizer.TrimMemory(true);
            MessageBox.Show(Window.GetWindow(this), "物理工作集内存已深度压缩！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private static bool IsRunningAsAdmin()
        {
            try
            {
                using var id = System.Security.Principal.WindowsIdentity.GetCurrent();
                var principal = new System.Security.Principal.WindowsPrincipal(id);
                return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }
    }
}
