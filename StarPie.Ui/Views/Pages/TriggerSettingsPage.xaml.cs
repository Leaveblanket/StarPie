using System.Windows;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.Messaging;

namespace StarPie.Views.Pages
{
    /// <summary>
    /// 触发与场景页面：全部状态经 Binding 直连 <see cref="BehaviorSettingsViewModel"/>
    /// （滑杆/开关/黑名单列表等均双向绑定，导入重挂由 VM 属性通知自动回填），code-behind 只保留
    /// 黑名单新增条目的滚动适配（纯 UI 适配；文本经语言字典声明式化）。
    /// </summary>
    public partial class TriggerSettingsPage : UserControl
    {
        public TriggerSettingsPage()
        {
            InitializeComponent();

            // ADR-0009 白名单第 1 条（生命周期接线）：页面挂载/卸载成对订阅退订 View 效果
            // 消息（共享页面基类 SettingsPageBase 随 ADR-0022/#94 删除后改自订阅，
            // 与 InputDialog/RadialWindow 同款成对纪律）。
            Loaded += OnPageLoaded;
            Unloaded += OnPageUnloaded;
        }

        private void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            // 黑名单新增后的滚动适配是纯 UI 效果；选中已由 VM 写入
            // SelectedBlacklistProcess，列表 SelectedItem 双向绑定自动跟随。
            WeakReferenceMessenger.Default.Register<BlacklistEntryAddedMessage>(this, (_, m) => OnBlacklistEntryAdded(m.ProcessName));
        }

        private void OnPageUnloaded(object sender, RoutedEventArgs e)
        {
            WeakReferenceMessenger.Default.Unregister<BlacklistEntryAddedMessage>(this);
        }

        private void OnBlacklistEntryAdded(string proc)
        {
            BlacklistListBox.ScrollIntoView(proc);
        }
    }
}
