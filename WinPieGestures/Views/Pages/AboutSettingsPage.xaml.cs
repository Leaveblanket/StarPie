using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using WinPieGestures.Services;

namespace WinPieGestures.Views.Pages
{
    /// <summary>
    /// 关于与更新页面 (T19)：迁移前 SettingsWindow PAGE 4 原样搬迁。页面 ViewModel 为
    /// <see cref="ViewModels.AboutViewModel"/> 空壳（纯静态展示内容，无用户状态）。
    /// </summary>
    public partial class AboutSettingsPage : SettingsPageBase
    {
        public AboutSettingsPage()
        {
            InitializeComponent();
        }

        protected override void ApplyLocalization()
        {
            OlderMilestonesExpander.Header = I18n.T("MilestonesOlderExpander");
        }

        private void OpenChangelogButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string changelogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CHANGELOG.md");
                if (File.Exists(changelogPath))
                {
                    Process.Start(new ProcessStartInfo(changelogPath) { UseShellExecute = true });
                }
                else
                {
                    MessageBox.Show("CHANGELOG.md 文件位于根目录。", "提示");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"无法打开文件: {ex.Message}");
            }
        }
    }
}
