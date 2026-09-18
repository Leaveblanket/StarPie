using System;
using System.Windows;
using CommunityToolkit.Mvvm.Messaging;
using StarPie.Host.Localization;
using StarPie.Sdk.Services;
using StarPie.Sdk.Wpf.Services.Shell;
using StarPie.Ui.Services.Shell;
using StarPie.Ui.ViewModels.Navigation;
using StarPie.Ui.Views.Navigation;

namespace StarPie.Ui
{
    /// <summary>
    /// 设置台租户：按需创建、关闭即销毁的设置控制台会话——主窗口（<see cref="MainView"/>）
    /// 与其 VM 树（导航区 + 壳区）同生共死。
    /// </summary>
    /// <remarks>
    /// 常驻职责不寄居在本对象上：托盘、手势、单实例接收、退出编排都在常驻壳层
    /// （<see cref="ShellHost"/>）。本对象只承担设置台一段生命期：开窗（含初始主题、对话框 Owner
    /// 绑定、托盘状态信号接线）与关窗收尾（对话框 Owner 解绑 → 成对退订 → 瞬态窗口收尾 →
    /// 释放 VM 树与会话作用域）。关闭即销毁、重开重建：关窗后本对象、窗口、VM 树与会话内的
    /// 页面 VM 一起不可达（会话作用域由组合根交付的工厂开启，由本对象结束）。
    /// </remarks>
    public sealed class SettingsConsole : IDisposable
    {
        private MainViewModel? _main;
        private ShellViewModel? _shell;
        private readonly IThemeService _themeService;
        // 设置台会话作用域：页面 VM 与设置台会话级 VM 的实例边界；释放时结束会话（整批释放）。
        private readonly ConsolePageSession _pageSession;
        private readonly DialogService _dialogs;
        private readonly InterfaceThemeSettingsViewModel _interfaceTheme;
        private readonly SettingsSaveOrchestrator _saveOrchestrator;
        private readonly IIconAssetService _iconAssets;
        private readonly NavigationSuspension _navigationSuspension;
        private readonly IMessenger _messenger;
        private readonly Window _anchor;
        // 退出态探针：退出态归常驻壳层（设置台只是被关闭；Shutdown 触发的关窗不得走托盘态出账序列）。
        private readonly Func<bool> _isExiting;
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
            ConsolePageSession pageSession,
            Func<bool> isExiting)
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
            _pageSession = pageSession;
            _isExiting = isExiting ?? throw new ArgumentNullException(nameof(isExiting));
        }

        /// <summary>
        /// 创建并显示设置台：建窗 → 托盘状态信号接线 → 初始主题 →
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
            RunTraySequence(TrayStateChange.ConsoleOpened);
        }

        /// <summary>显示并激活（托盘直达/单实例恢复）：已开着走淡入激活，否则先建后显示。</summary>
        public void ShowAndActivate()
        {
            MainView view = EnsureView();
            bool wasOpen = view.IsVisible;
            view.ShowAndActivate();

            // 已开着的窗口再激活不是"重开"：重复发恢复序列会把用户点选的页重放回上次停驻页。
            if (!wasOpen)
            {
                RunTraySequence(TrayStateChange.ConsoleOpened);
            }
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

        /// <summary>
        /// 托盘状态信号：设置台开/关 → 有序动作（关闭走 Flush → 导航出账 → 图标缓存出账 →
        /// Minimized 消息 → GC；重开按最后导航槽位重放导航；退出态不发）。
        /// 输入是控制台开/关而非窗口可见性：设置台是瞬态窗口，新建窗口首次 Show() 也产生可见性变化，
        /// 按可见性判读会把"首次打开"误判成"从托盘恢复"。
        /// </summary>
        private void RunTraySequence(TrayStateChange change)
        {
            foreach (TraySignalStep step in TrayStateSignal.Resolve(change, _isExiting()))
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
            _dialogs.SetOwner(null);

            // 关闭序列先于窗口收尾与会话释放执行：落盘冲刷与导航出账都要求会话内页面 VM 还在
            //（顺序即语义，见 TrayStateSignal）；退出态不走出账动作，此处兜底出账，
            // 否则导航状态会滞留已随会话释放的页面 VM，重开时"同类型已停驻"短路会让页面渲染已释放实例。
            RunTraySequence(TrayStateChange.ConsoleClosed);
            _navigationSuspension.Release();

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
            // 结束会话作用域：会话内的页面 VM 与设置子 VM 随作用域释放（成对退订在此执行）。
            _pageSession.End();
        }
    }
}
