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
            OpenChangelogButton.Content = I18n.T("BtnOpenChangelog");
        }
    }
}
