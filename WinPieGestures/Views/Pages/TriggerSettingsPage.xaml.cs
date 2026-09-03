using CommunityToolkit.Mvvm.Messaging;

namespace WinPieGestures.Views.Pages
{
    /// <summary>
    /// 触发与场景页面 (T19/T21)：全部状态经 Binding 直连 <see cref="BehaviorSettingsViewModel"/>
    /// （滑杆/开关/黑名单列表等均双向绑定，导入重挂由 VM 属性通知自动回填），code-behind 只保留
    /// 黑名单新增条目的滚动适配（ADR-0009 白名单：纯 UI 适配；文本经 T24 语言字典声明式化）。
    /// </summary>
    public partial class TriggerSettingsPage : SettingsPageBase
    {
        public TriggerSettingsPage()
        {
            InitializeComponent();
        }

        protected override void OnPageLoaded()
        {
            // ADR-0009：黑名单新增后的滚动适配是纯 UI 效果；选中已由 VM 写入
            // SelectedBlacklistProcess，列表 SelectedItem 双向绑定自动跟随。
            WeakReferenceMessenger.Default.Register<BlacklistEntryAddedMessage>(this, (_, m) => OnBlacklistEntryAdded(m.ProcessName));
        }

        protected override void OnPageUnloaded()
        {
            WeakReferenceMessenger.Default.Unregister<BlacklistEntryAddedMessage>(this);
        }

        private void OnBlacklistEntryAdded(string proc)
        {
            BlacklistListBox.ScrollIntoView(proc);
        }
    }
}
