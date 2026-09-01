using System;
using System.Windows;
using WinPieGestures.Services;
using WinPieGestures.ViewModels;

namespace WinPieGestures.Views.Pages
{
    /// <summary>
    /// 触发与场景页面 (T19)：迁移前 SettingsWindow PAGE 0 原样搬迁。页面 ViewModel 为
    /// <see cref="BehaviorSettingsViewModel"/>（容器单例）；滑杆/开关为非绑定控件（清欠边界外），
    /// 挂载与导入重挂时从 VM 回填，事件处理器只做"写 VM + View 效果"。
    /// </summary>
    public partial class TriggerSettingsPage : SettingsPageBase
    {
        // 初始为 true：抑制 XAML 初始化与回填期间的值变更风暴（与迁移前窗口 _isUpdatingUi 语义一致）。
        private bool _isUpdatingUi = true;

        private BehaviorSettingsViewModel Vm => (BehaviorSettingsViewModel)DataContext;

        public TriggerSettingsPage()
        {
            InitializeComponent();
        }

        protected override void ApplyLocalization()
        {
            TriggerPageHeader.Text = I18n.T("TriggerHeader");
            TriggerPageSubheader.Text = I18n.T("TriggerSubheader");
            SensitivityTitleText.Text = I18n.T("SensitivityTitle");
            SensitivityDescText.Text = I18n.T("SensitivityDesc");
            OuterEscapeTitleText.Text = I18n.T("OuterEscapeTitle");
            OuterEscapeDescText.Text = I18n.T("OuterEscapeDesc");
            OuterEscapeCheckboxTitleText.Text = I18n.T("OuterEscapeCheckbox");
            OuterEscapeDistanceTitleText.Text = I18n.T("OuterEscapeDistanceTitle");
            OuterEscapeDistanceDescText.Text = I18n.T("OuterEscapeDistanceDesc");
            SceneIsolationTitleText.Text = I18n.T("SceneIsolationTitle");
            SceneIsolationDescText.Text = I18n.T("SceneIsolationDesc");
            FullScreenOptionTitleText.Text = I18n.T("FullScreenOption");
            FullScreenOptionDescText.Text = I18n.T("FullScreenOptionDesc");
            ModifierPassTitleText.Text = I18n.T("ModifierPassTitle");
            CtrlModifierCheckBox.Content = I18n.T("ModifierCtrl");
            ShiftModifierCheckBox.Content = I18n.T("ModifierShift");
            AltModifierCheckBox.Content = I18n.T("ModifierAlt");
            BlacklistTitleText.Text = I18n.T("BlacklistTitle");
            BlacklistDescText.Text = I18n.T("BlacklistDesc");
            BrowseBlacklistButton.Content = I18n.T("BtnPickProcess");
            AddBlacklistButton.Content = I18n.T("BtnAddProcess");
            DeleteBlacklistButton.Content = I18n.T("BtnDeleteProcess");
            NewBlacklistProcessTextBox.ToolTip = I18n.T("BlacklistPlaceholder");
        }

        protected override void OnPageLoaded()
        {
            Vm.ConfigReloaded += OnConfigReloaded;
            Vm.BlacklistEntryAdded += OnBlacklistEntryAdded;
            SyncControlsFromVm();
        }

        protected override void OnPageUnloaded()
        {
            Vm.ConfigReloaded -= OnConfigReloaded;
            Vm.BlacklistEntryAdded -= OnBlacklistEntryAdded;
        }

        /// <summary>从 VM 回填非绑定控件（页面挂载与导入广播后；迁移前窗口构造块/导入同步原样搬迁）。</summary>
        private void SyncControlsFromVm()
        {
            _isUpdatingUi = true;
            try
            {
                ThresholdSlider.Value = Vm.DragThreshold;
                ThresholdValueLabel.Text = Vm.DragThreshold.ToString("0");

                DisableOnFullScreenCheckBox.IsChecked = Vm.DisableOnFullScreen;
                CtrlModifierCheckBox.IsChecked = Vm.DisableOnCtrl;
                ShiftModifierCheckBox.IsChecked = Vm.DisableOnShift;
                AltModifierCheckBox.IsChecked = Vm.DisableOnAlt;

                // 外圈逃逸：开关与面板可见性、滑杆同步（迁移前构造漏设修正的语义保留）
                EnableOuterEscapeCheckBox.IsChecked = Vm.EnableOuterEscapeCancel;
                OuterEscapeDistancePanel.Visibility = Vm.EnableOuterEscapeCancel ? Visibility.Visible : Visibility.Collapsed;
                OuterEscapeDistanceSlider.Value = Vm.OuterEscapeDistance;
                OuterEscapeDistanceLabel.Text = $"{Math.Round(Vm.OuterEscapeDistance):0} px";
            }
            finally
            {
                _isUpdatingUi = false;
            }
        }

        private void OnConfigReloaded() => SyncControlsFromVm();

        private void OnBlacklistEntryAdded(string proc)
        {
            BlacklistListBox.SelectedItem = proc;
            BlacklistListBox.ScrollIntoView(proc);
        }

        // --- 事件处理器（迁移前窗口处理器原样搬迁；落盘由 VM 消息上报组合根编排） ---

        private void ThresholdSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (ThresholdValueLabel != null)
            {
                ThresholdValueLabel.Text = e.NewValue.ToString("0");
            }
            if (_isUpdatingUi) return;
            Vm.DragThreshold = e.NewValue;
        }

        private void OuterEscapeDistanceSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdatingUi) return;
            Vm.OuterEscapeDistance = e.NewValue;
            OuterEscapeDistanceLabel.Text = $"{Math.Round(e.NewValue):0} px";
        }

        private void OuterEscapeCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUi) return;
            OuterEscapeDistancePanel.Visibility = Visibility.Visible;
            Vm.EnableOuterEscapeCancel = true;
        }

        private void OuterEscapeCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUi) return;
            OuterEscapeDistancePanel.Visibility = Visibility.Collapsed;
            Vm.EnableOuterEscapeCancel = false;
        }

        private void DisableOnFullScreenCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUi) return;
            Vm.DisableOnFullScreen = DisableOnFullScreenCheckBox.IsChecked == true;
        }

        private void ModifierCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUi) return;
            Vm.DisableOnCtrl = CtrlModifierCheckBox.IsChecked == true;
            Vm.DisableOnShift = ShiftModifierCheckBox.IsChecked == true;
            Vm.DisableOnAlt = AltModifierCheckBox.IsChecked == true;
        }

        /// <summary>把输入框文本与 VM 同步后执行黑名单命令，再按 VM 状态回写输入框（迁移前原样搬迁）。</summary>
        private void RunBlacklistCommand(CommunityToolkit.Mvvm.Input.IRelayCommand command)
        {
            Vm.NewBlacklistProcess = NewBlacklistProcessTextBox.Text;
            command.Execute(null);
            NewBlacklistProcessTextBox.Text = Vm.NewBlacklistProcess;
        }

        private void BrowseBlacklistButton_Click(object sender, RoutedEventArgs e)
        {
            RunBlacklistCommand(Vm.BrowseBlacklistCommand);
        }

        private void AddBlacklistButton_Click(object sender, RoutedEventArgs e)
        {
            RunBlacklistCommand(Vm.AddBlacklistFromInputCommand);
        }

        private void NewBlacklistProcessTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                RunBlacklistCommand(Vm.AddBlacklistFromInputCommand);
                e.Handled = true;
            }
        }

        private void BlacklistListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            // 列表选中态与 VM 双向同步（AddBlacklistProcess 经 BlacklistEntryAdded 回设选中为同值，无循环）。
            Vm.SelectedBlacklistProcess = BlacklistListBox.SelectedItem as string;
        }

        private void BlacklistListBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Delete || e.Key == System.Windows.Input.Key.Back)
            {
                Vm.DeleteBlacklistProcessCommand.Execute(null);
                e.Handled = true;
            }
        }

        private void DeleteBlacklistButton_Click(object sender, RoutedEventArgs e)
        {
            Vm.DeleteBlacklistProcessCommand.Execute(null);
        }
    }
}
