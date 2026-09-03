using System.Windows;
using CommunityToolkit.Mvvm.Messaging;
using WinPieGestures.Services.Messages;

namespace WinPieGestures.Views.Pages
{
    /// <summary>
    /// 高级与系统页面 (T19)：迁移前 SettingsWindow PAGE 3 原样搬迁。语言切换/自启/提权/导入导出编排
    /// 已住 VM（容器单例），本视图只做弹窗映射（<see cref="GeneralNoticeRequestedMessage"/>）与
    /// 语言下拉等 View 效果；语言状态经 XAML 双向绑定直达 VM，code-behind 不引用 VM 类型
    /// （ADR-0008 严格边界）。
    /// </summary>
    public partial class AdvancedSettingsPage : SettingsPageBase
    {
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
            WeakReferenceMessenger.Default.Register<GeneralNoticeRequestedMessage>(this, (_, m) => ShowNotice(m.Notice));
        }

        protected override void OnPageUnloaded()
        {
            WeakReferenceMessenger.Default.Unregister<GeneralNoticeRequestedMessage>(this);
        }

        private void ShowNotice(NoticeRequest notice)
        {
            var image = notice.Kind switch
            {
                NoticeKind.Error => MessageBoxImage.Error,
                NoticeKind.Warning => MessageBoxImage.Warning,
                _ => MessageBoxImage.Information
            };
            MessageBox.Show(notice.Message, notice.Title, MessageBoxButton.OK, image);
        }
    }
}