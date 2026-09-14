using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using CommunityToolkit.Mvvm.Messaging;
using StarPie.Kernel.Localization;
using StarPie.Services;
using StarPie.Services.Shell;
using StarPie.ViewModels.Navigation;
using StarPie.Views.Navigation;

namespace StarPie
{
    /// <summary>
    /// 设置台租户：按需创建、关闭即销毁的设置控制台会话——主窗口（<see cref="MainView"/>）
    /// 与其 VM 树（导航区 + 壳区）同生共死。
    /// </summary>
    /// <remarks>
    /// 常驻职责不寄居在本对象上：托盘、手势、单实例接收、退出编排都在常驻壳层
    /// （<see cref="ShellHost"/>）。本对象只承担设置台一段生命期：开窗（含初始主题、对话框 Owner
    /// 绑定、托盘状态信号接线）与关窗收尾（对话框 Owner 解绑 → 成对退订 → 瞬态窗口收尾 →
    /// 释放 VM 树）。关闭即销毁、重开重建：关窗后本对象、窗口与 VM 树一起不可达。
    /// </remarks>
    public sealed class SettingsConsole : IDisposable
    {
        private MainViewModel? _main;
        private ShellViewModel? _shell;
        private readonly IThemeService _themeService;
        private readonly DialogService _dialogs;
        private readonly InterfaceThemeSettingsViewModel _interfaceTheme;
        private readonly SettingsSaveOrchestrator _saveOrchestrator;
        private readonly IIconAssetService _iconAssets;
        private readonly NavigationSuspension _navigationSuspension;
        private readonly IMessenger _messenger;
        private readonly Window _anchor;
        private readonly bool _background;
        private MainView? _view;
        private bool _disposed;

        /// <summary>设置台是否已创建（有窗口即开着）。</summary>
        public bool IsOpen => _view is not null;

        public SettingsConsole(
            MainViewModel main,
            ShellViewModel shell,
            IThemeService themeService,
            DialogService dialogs,
            InterfaceThemeSettingsViewModel interfaceTheme,
            SettingsSaveOrchestrator saveOrchestrator,
            IIconAssetService iconAssets,
            NavigationSuspension navigationSuspension,
            IMessenger messenger,
            Window anchor,
            bool background)
        {
            _main = main;
            _shell = shell;
            _themeService = themeService;
            _dialogs = dialogs;
            _interfaceTheme = interfaceTheme;
            _saveOrchestrator = saveOrchestrator;
            _iconAssets = iconAssets;
            _navigationSuspension = navigationSuspension;
            _messenger = messenger;
            _anchor = anchor;
            _background = background;
        }

        /// <summary>
        /// 创建并显示设置台：建窗 → 静默形态（如启用）→ 托盘状态信号接线 → 初始主题 →
        /// 对话框 Owner 绑定 → 主窗口属性指向本窗口 → 显示并激活。
        /// </summary>
        public void Show()
        {
            MainView view = EnsureView();
            if (view.IsVisible)
            {
                view.Activate();
                return;
            }

            view.Show();
            view.WindowState = WindowState.Normal;
            view.Activate();
        }

        /// <summary>显示并激活（托盘直达/单实例恢复）：已开着走淡入激活，否则先建后显示。</summary>
        public void ShowAndActivate()
        {
            MainView view = EnsureView();
            view.ShowAndActivate();
        }

        /// <summary>创建窗口并完成接线（幂等：窗口已存在时直接返回）。</summary>
        private MainView EnsureView()
        {
            if (_view is { } existing)
            {
                return existing;
            }

            var view = new MainView(
                _main ?? throw new InvalidOperationException("设置台已释放，不能再开窗"),
                _shell ?? throw new InvalidOperationException("设置台已释放，不能再开窗"),
                _themeService);
            if (_background)
            {
                ConfigureBackgroundWindow(view);
            }

            // 托盘状态信号：可见性变化 → 有序动作（进入托盘态 Flush → 导航出账 → 图标缓存出账
            // → Minimized 消息；重开按最后导航槽位重放导航；退出态不发；后台形态出账禁用、消息照发）。
            view.IsVisibleChanged += OnViewVisibilityChanged;

            // 关窗即租户生命期结束：任何关窗路径（关闭按钮、Alt+F4、WM_CLOSE、进程退出）都收敛到 Closed，
            // 在此完成窗口收尾与 VM 树释放——否则壳层持有的设置台引用会停在已关闭窗口上，
            // 重开时既开不了新窗（旧窗口不能 Show）也释放不了旧对象图。
            view.Closed += OnViewClosed;

            // 初始主题在窗口呈现前就位；此后主题变更经消息由窗口订阅执行。
            view.ApplyAppTheme(_interfaceTheme.AppTheme);

            // Owner 随设置台开/关绑定与解绑：关闭时解绑，已关闭窗口不被对话框服务长期强引用。
            _dialogs.SetOwner(view);

            // Application.MainWindow 指向当前设置台：常驻锚窗口保证它不可能被自动赋值钉在
            // 某个已关闭的瞬态窗口上；此处显式接管，使依赖主窗口的消费方（插件泄漏验证等）
            // 读到的是活动的设置台窗口。
            if (Application.Current is { } application)
            {
                application.MainWindow = view;
            }

            _view = view;
            return view;
        }

        /// <summary>退出编排置位：进程退出中的关窗不走托盘态出账序列。</summary>
        public void MarkExiting()
        {
            if (_shell is { } shell)
            {
                shell.IsExiting = true;
            }
        }

        private void OnViewVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (_view is not { } view)
            {
                return;
            }

            foreach (TraySignalStep step in TrayVisibilitySignal.Resolve(
                view.IsVisible, _shell?.IsExiting ?? true, _background))
            {
                switch (step)
                {
                    case TraySignalStep.FlushPendingSave:
                        _saveOrchestrator.FlushPendingSave();
                        break;
                    case TraySignalStep.ReleaseNavigation:
                        _navigationSuspension.Release();
                        break;
                    case TraySignalStep.ReleaseIconCaches:
                        _iconAssets.ReleaseTransientCaches();
                        break;
                    case TraySignalStep.SendMinimized:
                        _messenger.Send(MinimizedToTrayMessage.Instance);
                        break;
                    case TraySignalStep.CollectGarbage:
                        // MemoryOptimizer 内部 Task.Run：GC 后台执行，不占 Send 调用线程。
                        MemoryOptimizer.CollectGarbage();
                        break;
                    case TraySignalStep.RestoreNavigation:
                        _navigationSuspension.Restore();
                        break;
                    case TraySignalStep.SendRestored:
                        _messenger.Send(RestoredFromTrayMessage.Instance);
                        break;
                }
            }
        }

        /// <summary>窗口已关闭（任何关窗路径）：收敛到会话释放。</summary>
        private void OnViewClosed(object? sender, EventArgs e)
        {
            ReleaseView(alreadyClosed: true);
            Dispose();
        }

        /// <summary>关闭设置台窗口（幂等）：未开时 no-op。</summary>
        public void Close() => ReleaseView(alreadyClosed: false);

        /// <summary>窗口侧收尾：解绑订阅与对话框 Owner，走瞬态窗口收尾纪律并丢弃窗口引用。</summary>
        private void ReleaseView(bool alreadyClosed)
        {
            if (_view is not { } view)
            {
                return;
            }

            _view = null;
            view.Closed -= OnViewClosed;
            view.IsVisibleChanged -= OnViewVisibilityChanged;
            _dialogs.SetOwner(null);

            // 瞬态窗口收尾纪律集中一处：清动画 → 丢弃内容与 DataContext → Close →
            // 排空 Dispatcher → 主窗口属性回退到常驻锚窗口；随后丢弃强引用。
            TransientWindowTeardown.Complete(view, _anchor, alreadyClosed);
        }

        /// <summary>释放设置台会话：先关窗收尾，再成对退订并丢弃 VM 树（幂等）。</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            // 从 Closed 路径进来时窗口已在关闭流程中（ReleaseView(alreadyClosed: true) 已执行），
            // 此处 no-op；显式释放路径则由它关窗。
            ReleaseView(alreadyClosed: false);
            // 丢弃 VM 树强引用：释放后本对象（可能仍被壳层字段引用）不再钉住会话对象图。
            _main?.Dispose();
            _shell?.Dispose();
            _main = null;
            _shell = null;
        }

        // ==== 后台/静默模式（--background，e2e 用）====

        /// <summary>静默形态主窗口定位：屏幕左上角（窗口真实可见、被 DWM 合成，失败截图可抓真实内容）。</summary>
        private const int SilentWindowLeft = 0;
        private const int SilentWindowTop = 0;
        /// <summary>静默形态界面缩放：0.9 线性 → 窗口 954×648；再小正文会掉到 9px 以下、截图不可读。</summary>
        private const double SilentWindowScale = 0.9;
        private const int GwlExStyle = -20;
        private const int WsExNoActivate = 0x08000000;
        private const int WsExTransparent = 0x00000020;
        private const int WmNcHitTest = 0x0084;
        private const int HtTransparent = -1;

        /// <summary>
        /// 把设置台窗口切成"静默形态"：屏幕左上角定位、不可激活（WS_EX_NOACTIVATE）、
        /// 点击穿透（WS_EX_TRANSPARENT + WM_NCHITTEST→HTTRANSPARENT，用户点击落到下层窗口）、
        /// 不进任务栏。语义只影响窗口呈现/激活/命中测试，不影响导航/配置/渲染，UIA 仍可完整驱动。
        /// </summary>
        private static void ConfigureBackgroundWindow(MainView view)
        {
            view.ShowActivated = false;
            view.ShowInTaskbar = false;
            view.WindowStartupLocation = WindowStartupLocation.Manual;
            view.ApplyLayoutScale(SilentWindowScale);
            view.Left = SilentWindowLeft;
            view.Top = SilentWindowTop;

            // HWND 在 Show 时创建：SourceInitialized 早于窗口出现在屏幕上，此刻挂扩展样式最稳。
            view.SourceInitialized += (_, _) =>
            {
                IntPtr hwnd = new WindowInteropHelper(view).Handle;
                if (hwnd == IntPtr.Zero)
                {
                    return;
                }

                int exStyle = GetWindowLong(hwnd, GwlExStyle);
                SetWindowLong(hwnd, GwlExStyle, exStyle | WsExNoActivate | WsExTransparent);

                // 命中测试一律 HTTRANSPARENT：鼠标点击穿透到下层窗口（跨进程亦生效），
                // 配合 WS_EX_NOACTIVATE 让静默形态对用户键鼠完全无感。
                HwndSource.FromHwnd(hwnd)?.AddHook((IntPtr h, int msg, IntPtr w, IntPtr l, ref bool handled) =>
                {
                    if (msg == WmNcHitTest)
                    {
                        handled = true;
                        return new IntPtr(HtTransparent);
                    }

                    return IntPtr.Zero;
                });
            };
        }

        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    }
}
