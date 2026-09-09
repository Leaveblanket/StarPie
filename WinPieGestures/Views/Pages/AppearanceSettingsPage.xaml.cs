using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace StarPie.Views.Pages
{
    /// <summary>
    /// 外观与形态页面：页面整体 DataContext 是薄聚合
    /// <see cref="AppearanceSettingsViewModel"/>；各设置卡 DataContext 指向对应子 VM——
    /// 界面主题卡 = InterfaceTheme，其余轮盘外观卡 = WheelAppearance（配色下拉 ItemsSource 化、
    /// 核圆面板可见性 DataTrigger 化、核圆图标预览/名称与图片缩略图 Converter 绑定化）。
    /// code-behind 只保留实时预览画布渲染等 View 白名单项（页面文本经运行时语言字典声明式化）。
    /// </summary>
    /// <remarks>
    /// 页面 VM 是单例：PreviewInvalidated/PageConfigReloaded 视图消息在 Loaded/Unloaded
    /// 成对订阅退订，防过期页面引用泄漏。
    /// 实时预览渲染器（View 层无 DI 构造）经已批准预览桥取得共享图标资产实例服务：
    /// 聚合 VM <see cref="AppearanceSettingsViewModel.IconAssetService"/> 暴露
    /// <see cref="IIconAssetService"/>，页面在 Loaded 阶段读取并装配渲染器
    /// （ADR-0019/#87 决策 4，layering Views 例外登记）。
    /// 界面主题卡 DataContext 指向 <see cref="InterfaceThemeSettingsViewModel"/>（经
    /// <see cref="AppearanceSettingsViewModel.InterfaceTheme"/> 绑定）；主题应用改消息驱动，
    /// 由壳层主窗口订阅 <see cref="AppThemeChangedMessage"/> 执行，本页面不挂主题选择处理器，
    /// 导入后的窗口主题应用路径同样在壳层。
    /// 实时预览渲染/交互路径只依赖轮盘模块只读状态接口 <see cref="IWheelAppearanceState"/>；
    /// 具体聚合 VM 引用仅用于 DataContext 桥接（取 WheelAppearance 子 VM）。
    /// </remarks>
    public partial class AppearanceSettingsPage : UserControl
    {
        private WheelPreviewRenderer? _previewRenderer;

        // 预览桥接在 Loaded 时缓存（Unloaded 阶段 DataContext 已置空）为
        // IWheelAppearanceState：渲染/交互代码路径只读该接口，具体实现是聚合 VM 暴露的轮盘外观子 VM。
        private IWheelAppearanceState _previewState = null!;

        private IWheelAppearanceState PreviewState => _previewState;

        public AppearanceSettingsPage()
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
            // 页面整体 DataContext 是薄聚合 VM；预览状态经其 WheelAppearance 子 VM 取得。
            _previewState = ((AppearanceSettingsViewModel)DataContext).WheelAppearance;
            // 预览桥（ADR-0019/#87）：聚合 VM 暴露共享图标资产实例服务，页面据此装配
            // 无 DI 构造的渲染器；DataContext 在 Unloaded 阶段置空前不会再次读取。
            _previewRenderer ??= new WheelPreviewRenderer(((AppearanceSettingsViewModel)DataContext).IconAssetService);
            WeakReferenceMessenger.Default.Register<AppearancePreviewInvalidatedMessage>(this, (_, _) => OnAppearancePreviewInvalidated());
            WeakReferenceMessenger.Default.Register<PageConfigReloadedMessage>(this, (_, m) =>
            {
                if (m.ViewModelType == typeof(AppearanceSettingsViewModel)) OnConfigReloaded();
            });

            RenderLiveWheelPreview();
        }

        private void OnPageUnloaded(object sender, RoutedEventArgs e)
        {
            WeakReferenceMessenger.Default.Unregister<AppearancePreviewInvalidatedMessage>(this);
            WeakReferenceMessenger.Default.Unregister<PageConfigReloadedMessage>(this);
            _previewState = null!;
            _previewRenderer = null;
        }

        private void OnConfigReloaded()
        {
            // 导入后只剩预览重绘这一 View 效果（主题应用由界面主题子 VM 发消息、壳层主窗口
            // 订阅执行）；状态、配色下拉项与核圆面板/文本均声明式绑定，随 VM 通知自动刷新。
            RenderLiveWheelPreview();
        }

        #region 60FPS 实时预览画布渲染

        private void OnAppearancePreviewInvalidated()
        {
            RenderLiveWheelPreview();
        }

        private void RenderLiveWheelPreview()
        {
            if (LiveWheelPreviewCanvas == null || _previewState == null || _previewRenderer == null) return;
            // 深浅色探测由壳层主窗口执行——渲染器不反向引用宿主，
            // 调用方把探测结果以 bool 传入（无壳窗口时回落 false）。
            bool windowsInDarkMode = Window.GetWindow(this) is MainView mainView && mainView.IsWindowsInDarkTheme();
            _previewRenderer.Render(LiveWheelPreviewCanvas, PreviewState, windowsInDarkMode);
        }

        private void LiveWheelPreviewCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (_previewState == null || _previewRenderer == null) return;
            _previewRenderer.HandleMouseMove(LiveWheelPreviewCanvas, e, PreviewState);
        }

        private void LiveWheelPreviewCanvas_MouseLeave(object sender, MouseEventArgs e)
        {
            _previewRenderer?.HandleMouseLeave();
        }

        #endregion
    }
}
