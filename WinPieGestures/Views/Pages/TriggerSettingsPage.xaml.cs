using System.Windows;
using CommunityToolkit.Mvvm.Messaging;
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
        // 页面 VM 在 Loaded 时缓存:Unloaded 阶段 DataContext 已随可视树脱离而置空,
        // 届时经缓存退订事件(迁移前窗口持有字段引用,无此问题)。
        private BehaviorSettingsViewModel? _vm;

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
            _vm = (BehaviorSettingsViewModel)DataContext;
            WeakReferenceMessenger.Default.Register<PageConfigReloadedMessage>(this, (_, m) =>
            {
                if (m.ViewModelType == typeof(BehaviorSettingsViewModel)) OnConfigReloaded();
            });
            WeakReferenceMessenger.Default.Register<BlacklistEntryAddedMessage>(this, (_, m) => OnBlacklistEntryAdded(m.ProcessName));
        }

        protected override void OnPageUnloaded()
        {
            if (_vm != null)
            {
                WeakReferenceMessenger.Default.Unregister<PageConfigReloadedMessage>(this);
                WeakReferenceMessenger.Default.Unregister<BlacklistEntryAddedMessage>(this);
            }
            _vm = null;
        }

        private void OnConfigReloaded() { }

        private void OnBlacklistEntryAdded(string proc)
        {
            BlacklistListBox.SelectedItem = proc;
            BlacklistListBox.ScrollIntoView(proc);
        }
    }
}
