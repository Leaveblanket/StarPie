using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace WinPieGestures.Views.Pages
{
    /// <summary>
    /// 外观与形态页面 (T19/T21)：状态经 Binding 直连 <see cref="AppearanceSettingsViewModel"/>
    /// （配色下拉 ItemsSource 化、核圆面板可见性 DataTrigger 化、核圆图标预览/名称与图片缩略图
    /// Converter 绑定化），code-behind 只保留实时预览画布渲染与窗口主题应用等
    /// ADR-0009 白名单项（页面文本经 T24 语言字典声明式化）。页面 VM 是单例：
    /// PreviewInvalidated/PageConfigReloaded 视图消息在
    /// Loaded/Unloaded 成对订阅退订，防过期页面引用泄漏。
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
            // ADR-0009 白名单：导入后只剩 View 效果（主题应用到窗口 + 预览重绘）；
            // 状态、配色下拉项与核圆面板/文本均声明式绑定，随 VM 通知自动刷新。
            (Window.GetWindow(this) as MainView)?.ApplyAppTheme(Vm.AppTheme);
            RenderLiveWheelPreview();
        }

        private void ShowCoreIconCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_vm == null) return;
            // VM ShowCoreIcon 只上报落盘不报预览事件；本处理器只剩实时预览重绘这一 View 效果（ADR-0009）。
            RenderLiveWheelPreview();
        }

        private void AppThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_vm == null) return;
            // 状态经 SelectedValue 双向绑定写穿外观 VM（落盘由 VM 管线上报）；主题应用（窗口视觉）是 View 效果。
            (Window.GetWindow(this) as MainView)?.ApplyAppTheme(Vm.AppTheme);
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
