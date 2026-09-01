using System.Windows;
using System.Windows.Input;
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

        // 初始为 true:抑制 XAML 解析期(扇区数 8 键默认 IsChecked="True")与回填期的事件风暴
        // (与迁移前窗口 _isUpdatingUi 语义一致)。
        private bool _isUpdatingUi = true;

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
            _vm.ConfigReloaded -= OnConfigReloaded;
            _vm.ConfigReloaded += OnConfigReloaded;

            _isUpdatingUi = true;
            try
            {
                // 迁移前 SwitchToTab(2) 的选中兜底原样搬迁：无选中回落第一项，再同步单选钮与槽位。
                if (Vm.SelectedProfile == null && Vm.Profiles.Count > 0)
                {
                    ProfilesListBox.SelectedItem = Vm.Profiles[0];
                }

                if (Vm.SelectedProfile != null)
                {
                    ProfilesListBox.SelectedItem = Vm.SelectedProfile;
                    UpdateSectorCountRadios();
                    Vm.RebuildSlots();
                }
            }
            finally
            {
                _isUpdatingUi = false;
            }
        }

        protected override void OnPageUnloaded()
        {
            if (_vm != null)
            {
                _vm.ConfigReloaded -= OnConfigReloaded;
            }
            _vm = null;
        }

        private void OnConfigReloaded()
        {
            // 导入后列表选中回落到第一项，使扇区数、槽位与导入内容一致（迁移前语义保留）。
            if (_vm == null) return;
            _isUpdatingUi = true;
            try
            {
                if (Vm.Profiles.Count > 0)
                {
                    ProfilesListBox.SelectedIndex = 0;
                }
            }
            finally
            {
                _isUpdatingUi = false;
            }
        }

        /// <summary>把选中方案的扇区数（原始值）同步到 4/8/12 单选钮（迁移前原样搬迁）。</summary>
        private void UpdateSectorCountRadios()
        {
            int count = Vm.SelectedProfile?.Model.SectorCount ?? 0;
            SectorCount4Radio.IsChecked = count == 4;
            SectorCount8Radio.IsChecked = count == 8;
            SectorCount12Radio.IsChecked = count == 12;
        }

        // --- 事件处理器（迁移前窗口处理器原样搬迁；编排与落盘已收编进 VM） ---

        private void ProfilesListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (_isUpdatingUi) return;
            if (!Vm.SelectProfile(ProfilesListBox.SelectedItem as ProfileItemViewModel)) return;
            UpdateSectorCountRadios();
        }

        private void SectorCountRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUi) return;
            int newCount = 8;
            if (SectorCount4Radio?.IsChecked == true) newCount = 4;
            else if (SectorCount8Radio?.IsChecked == true) newCount = 8;
            else if (SectorCount12Radio?.IsChecked == true) newCount = 12;

            if (!Vm.ApplySectorCount(newCount)) return;
            UpdateSectorCountRadios();
        }

        private void AddProfileButton_Click(object sender, RoutedEventArgs e)
        {
            var added = Vm.AddProfileFromAppPicker();
            if (added != null)
            {
                ProfilesListBox.SelectedItem = added;
            }
        }

        private void AddCustomProfileButton_Click(object sender, RoutedEventArgs e)
        {
            var added = Vm.AddCustomProfileViaDialog();
            if (added != null)
            {
                ProfilesListBox.SelectedItem = added;
            }
        }

        private void RenameProfileButton_Click(object sender, RoutedEventArgs e)
        {
            Vm.RenameSelectedProfileViaDialog();
        }

        private void ProfilesListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ProfilesListBox.SelectedItem is ProfileItemViewModel item && !item.Model.ProcessName.Equals("Global", System.StringComparison.OrdinalIgnoreCase))
            {
                Vm.RenameSelectedProfileViaDialog();
            }
        }

        private void DeleteProfileButton_Click(object sender, RoutedEventArgs e)
        {
            if (Vm.DeleteSelectedProfileViaDialog())
            {
                ProfilesListBox.SelectedIndex = 0;
            }
        }

        private void PickIcon_Click(object sender, RoutedEventArgs e)
        {
            // 图标选取对话框编排已迁 SlotViewModel (T12)；此处只剩 View 层效果：外观页可见时刷新预览——
            // T19 起外观页 View 未挂载即不存在订阅，无需跨页可见性判断。
            if (sender is FrameworkElement elem && elem.DataContext is SlotViewModel vm)
            {
                vm.PickIcon();
            }
        }
    }
}
