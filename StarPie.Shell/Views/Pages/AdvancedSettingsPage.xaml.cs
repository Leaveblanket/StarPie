using System.Windows;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.Messaging;
using StarPie.Services.Messages;

namespace StarPie.Views.Pages
{
    /// <summary>
    /// 高级与系统页面：语言切换/自启/提权/导入导出编排已住 VM（容器单例），本视图只做
    /// 弹窗映射（<see cref="GeneralNoticeRequestedMessage"/>）与语言下拉等 View 效果；
    /// 语言状态经 XAML 双向绑定直达 VM，code-behind 不引用 VM 类型。
    /// </summary>
    public partial class AdvancedSettingsPage : UserControl
    {
        public AdvancedSettingsPage()
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
            WeakReferenceMessenger.Default.Register<GeneralNoticeRequestedMessage>(this, (_, m) => ShowNotice(m.Notice));
        }

        private void OnPageUnloaded(object sender, RoutedEventArgs e)
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
