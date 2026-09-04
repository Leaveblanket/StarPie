using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace WinPieGestures.Views.Pages
{
    /// <summary>
    /// 外观与形态页面 (T19/T21)：状态经 Binding 直连 <see cref="AppearanceSettingsViewModel"/>
    /// （配色下拉 ItemsSource 化、核圆面板可见性 DataTrigger 化、核圆图标预览/名称与图片缩略图
    /// Converter 绑定化），code-behind 只保留实时预览画布渲染等
    /// ADR-0009 白名单项（页面文本经 T24 语言字典声明式化）。页面 VM 是单例：
    /// PreviewInvalidated/PageConfigReloaded 视图消息在
    /// Loaded/Unloaded 成对订阅退订，防过期页面引用泄漏。
    /// #54：界面主题卡 DataContext 指向 <see cref="InterfaceThemeSettingsViewModel"/>（经
    /// <see cref="AppearanceSettingsViewModel.InterfaceTheme"/> 绑定），主题应用改消息驱动——
    /// 由壳层主窗口订阅 <see cref="AppThemeChangedMessage"/> 执行，本页面不再有主题
    /// SelectionChanged 处理器；导入后的窗口主题应用路径同步移出页面。
    /// </summary>
    public partial class AppearanceSettingsPage : SettingsPageBase
    {
        private readonly WheelPreviewRenderer _previewRenderer = new();

        // 页面 VM 在 Loaded 时缓存(Unloaded 阶段 DataContext 已置空,见 SettingsPageBase 约定)。
        private AppearanceSettingsViewModel _vm = null!;

        private AppearanceSettingsViewModel Vm => _vm;

        public AppearanceSettingsPage()
        {
            InitializeComponent();
        }

        protected override void OnPageLoaded()
        {
            _vm = (AppearanceSettingsViewModel)DataContext;
            WeakReferenceMessenger.Default.Register<AppearancePreviewInvalidatedMessage>(this, (_, _) => OnAppearancePreviewInvalidated());
            WeakReferenceMessenger.Default.Register<PageConfigReloadedMessage>(this, (_, m) =>
            {
                if (m.ViewModelType == typeof(AppearanceSettingsViewModel)) OnConfigReloaded();
            });

            RenderLiveWheelPreview();
        }

        protected override void OnPageUnloaded()
        {
            WeakReferenceMessenger.Default.Unregister<AppearancePreviewInvalidatedMessage>(this);
            WeakReferenceMessenger.Default.Unregister<PageConfigReloadedMessage>(this);
            _vm = null!;
        }

        private void OnConfigReloaded()
        {
            // ADR-0009 白名单：#54 起导入后只剩预览重绘这一 View 效果（主题应用由界面主题子 VM
            // 发 AppThemeChangedMessage、壳层主窗口订阅执行）；状态、配色下拉项与核圆面板/文本
            // 均声明式绑定，随 VM 通知自动刷新。
            RenderLiveWheelPreview();
        }

        private void ShowCoreIconCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_vm == null) return;
            // VM ShowCoreIcon 只上报落盘不报预览事件；本处理器只剩实时预览重绘这一 View 效果（ADR-0009）。
            RenderLiveWheelPreview();
        }

        #region 60FPS Live Preview Canvas Rendering

        private void OnAppearancePreviewInvalidated()
        {
            RenderLiveWheelPreview();
        }

        private void RenderLiveWheelPreview()
        {
            if (LiveWheelPreviewCanvas == null || _vm == null) return;
            _previewRenderer.Render(LiveWheelPreviewCanvas, Vm, Window.GetWindow(this) as MainView);
        }

        private void LiveWheelPreviewCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (_vm == null) return;
            _previewRenderer.HandleMouseMove(LiveWheelPreviewCanvas, e, Vm);
        }

        private void LiveWheelPreviewCanvas_MouseLeave(object sender, MouseEventArgs e)
        {
            _previewRenderer.HandleMouseLeave();
        }

        #endregion
    }
}
