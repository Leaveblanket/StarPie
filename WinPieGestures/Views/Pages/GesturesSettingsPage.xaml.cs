using System.Windows;
using CommunityToolkit.Mvvm.Messaging;
using WinPieGestures.Services;
using WinPieGestures.ViewModels;

namespace WinPieGestures.Views.Pages
{
    /// <summary>
    /// 手势与动作页面 (T19)：迁移前 SettingsWindow PAGE 2 原样搬迁。页面 ViewModel 为
    /// <see cref="ProfileListViewModel"/>（容器单例）：方案增删改与扇区数切换的编排（含对话框）
    /// 已收编进 VM，本视图只做列表选中同步与 View 效果；跨页预览重绘已无意义
    /// （外观页 View 未挂载时不重绘，挂载时全量重绘）。
    /// </summary>
    public partial class GesturesSettingsPage : SettingsPageBase
    {
        // 页面 VM 在 Loaded 时缓存(Unloaded 阶段 DataContext 已置空,见 SettingsPageBase 约定)。
        private ProfileListViewModel? _vm;

        private ProfileListViewModel Vm => _vm!;

        public GesturesSettingsPage()
        {
            InitializeComponent();
        }

        protected override void ApplyLocalization()
        {
            GesturesPageHeader.Text = I18n.T("GesturesHeader");
            ProfileCardTitleText.Text = I18n.T("ProfileCardTitle");
            ProfileCardDescText.Text = I18n.T("ProfileCardDesc");
            AddProfileButton.Content = I18n.T("BtnAddAppProfile");
            AddCustomProfileButton.Content = I18n.T("BtnAddCustomProfile");
            RenameProfileButton.Content = I18n.T("BtnRenameProfile");
            DeleteProfileButton.Content = I18n.T("BtnDeleteProfile");
            SectorCountTitleText.Text = I18n.T("SectorCountOptionTitle");
            SectorCountDescText.Text = I18n.T("SectorCountOptionDesc");
            SectorCount4Radio.Content = I18n.T("SectorCount4");
            SectorCount8Radio.Content = I18n.T("SectorCount8");
            SectorCount12Radio.Content = I18n.T("SectorCount12");
            SectorActionListTitleText.Text = I18n.T("SectorActionListTitle");
            SectorActionListDescText.Text = I18n.T("SectorActionListDesc");
        }

        protected override void OnPageLoaded()
        {
            _vm = (ProfileListViewModel)DataContext;
            WeakReferenceMessenger.Default.Register<PageConfigReloadedMessage>(this, (_, m) =>
            {
                if (m.ViewModelType == typeof(ProfileListViewModel)) OnConfigReloaded();
            });

            if (Vm.SelectedProfile == null && Vm.Profiles.Count > 0)
            {
                Vm.SelectedProfile = Vm.Profiles[0];
            }
            ProfilesListBox.SelectedItem = Vm.SelectedProfile;
        }

        protected override void OnPageUnloaded()
        {
            if (_vm != null)
            {
                WeakReferenceMessenger.Default.Unregister<PageConfigReloadedMessage>(this);
            }
            _vm = null;
        }

        private void OnConfigReloaded()
        {
            // 导入后列表选中回落到第一项，使扇区数、槽位与导入内容一致（迁移前语义保留）。
            if (_vm == null) return;
            if (Vm.Profiles.Count > 0)
            {
                Vm.SelectedProfile = Vm.Profiles[0];
                ProfilesListBox.SelectedIndex = 0;
            }
        }

    }
}
