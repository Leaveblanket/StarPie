using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
using CommunityToolkit.Mvvm.Messaging;
using StarPie.Kernel.Localization;
using StarPie.PluginHosting;
using StarPie.PluginRuntime.Diagnostics;
using StarPie.PluginRuntime.Hosting;
using StarPie.Services;
using StarPie.Services.Shell;
using StarPie.Views.Navigation;

namespace StarPie
{
    /// <summary>
    /// 常驻壳层：进程存活期内一直存在的编排面——鼠标钩子、语言资源字典、托盘、插件运行时、
    /// 单实例恢复接收、退出协调，以及设置台租户（<see cref="SettingsConsole"/>）的按需创建与释放。
    /// </summary>
    /// <remarks>
    /// 单进程内按生命周期划分：本类常驻至进程结束；设置台是按需创建、关闭即销毁的租户；
    /// 轮盘维持每次手势一个实例。常驻职责不寄居在瞬态对象上——单实例恢复消息的接收端驻托盘
    /// 消息窗口（常驻 HWND），退出编排与托盘气泡归本类，设置台窗口只是它创建的瞬态窗口。
    /// <see cref="Application.MainWindow"/> 由常驻锚窗口（<see cref="ShellAnchorWindow"/>）兜底持有，
    /// 使任何瞬态窗口都不可能被自动赋值钉住。
    /// DI 注册与解析仍归 <see cref="Composition"/>（组合根），本类不接触 ServiceProvider；
    /// 设置台的对象图经组合根交付的工厂创建，解析点未离开组合根。
    /// 生命周期：App.OnStartup 经 <see cref="Composition.CreateShellHost"/> 取得本对象后调用
    /// Run；App.OnExit 先保存配置再释放本对象（释放设置台、托盘、停钩、退订语言），
    /// Composition 最后释放容器。
    /// </remarks>
    internal sealed class ShellHost : IDisposable
    {
        private readonly IMessenger _messenger;
        private readonly MouseHook _mouseHook;
        private readonly DialogService _dialogService;
        private readonly ThemeService _themeService;
        private readonly ILocalizationService _localization;
        private readonly IConfigService _config;
        private readonly IIconAssetService _iconAssets;
        private readonly SettingsSaveOrchestrator _saveOrchestrator;
        // 目录执行缝按槽位导航——壳层不持有任何页面类型。
        private readonly INavigationExecutor _navigation;
        private readonly AppHostDelegates _hostDelegates;
        // 插件 UI 托管：托盘/设置面的插件条目由它提供（无插件时为空，菜单与设置面不出现空壳）。
        private readonly PluginUiCoordinator _pluginUi;
        // 插件运行时：启动扫描（发现/校验/准入 + 启动报告落盘）后按宿主状态装载启用插件，
        // 并持有停用/再启用入口。
        private readonly PluginRuntimeHost _pluginRuntime;
        // 设置台工厂：组合根交付，按需产出一次性设置台租户（对象图解析仍收在组合根）；
        // 参数是常驻锚窗口——设置台关闭时把 Application.MainWindow 回退给它。
        private readonly Func<Window, Func<bool>, SettingsConsole> _createSettingsConsole;
        // 导航视图出账与恢复重放（设置台关闭时出账、重开时按最后导航槽位重放；幂等）。
        private readonly NavigationSuspension _navigationSuspension;
        // 后台/静默模式（--background，e2e 用）：窗口固定在屏幕左上角 + 不可激活 + 点击穿透 +
        // 无任务栏项，且不启全局鼠标钩子——用户同机工作时键鼠不受打扰，窗口仍真实可见可截图。
        private readonly bool _background;
        // 测试实例（命令行 --allow-multiple/--test-instance）：受理测试实例退出消息，
        // 使 e2e 能以真实退出路径收尾——硬杀不执行用户态收尾，托盘图标会以死条目留在通知区。
        private readonly bool _testInstance;
        // 常驻锚窗口：永不显示，专门长期持有 Application.MainWindow。
        private readonly ShellAnchorWindow _anchor;
        // 退出态：进程退出编排归壳层（设置台关闭不是退出；退出时的关窗不走托盘态出账序列）。
        private bool _isExiting;
        private TrayIconManager? _trayIcon;
        private SettingsConsole? _settingsConsole;
        // 托盘消息窗口上的恢复消息钩子（常驻；释放时成对摘除）。
        private HwndSourceHook? _restoreMessageHook;
        // 让位请求接收端（只装在非提权首实例上；见 StartHandoverListener）。
        private InstanceHandoverListener? _handoverListener;

        public ShellHost(
            IMessenger messenger,
            MouseHook mouseHook,
            DialogService dialogService,
            ThemeService themeService,
            ILocalizationService localization,
            IConfigService config,
            IIconAssetService iconAssets,
            SettingsSaveOrchestrator saveOrchestrator,
            INavigationExecutor navigation,
            AppHostDelegates hostDelegates,
            PluginRuntimeHost pluginRuntime,
            PluginUiCoordinator pluginUi,
            NavigationSuspension navigationSuspension,
            Func<Window, Func<bool>, SettingsConsole> createSettingsConsole,
            bool background = false,
            bool testInstance = false)
        {
            _messenger = messenger;
            _mouseHook = mouseHook;
            _dialogService = dialogService;
            _themeService = themeService;
            _localization = localization;
            _config = config;
            _iconAssets = iconAssets;
            _saveOrchestrator = saveOrchestrator;
            _navigation = navigation;
            _hostDelegates = hostDelegates;
            _pluginRuntime = pluginRuntime;
            _pluginUi = pluginUi;
            _navigationSuspension = navigationSuspension;
            _createSettingsConsole = createSettingsConsole;
            _background = background;
            _testInstance = testInstance;

            // 锚窗口在任何其它窗口之前实例化并占住 Application.MainWindow：
            // 进程内第一个实例化的 Window 会被该属性长期强引用，先占位可保证瞬态窗口
            // （设置台/轮盘/对话框/插件窗口）永远不可能被自动赋值钉住。
            _anchor = new ShellAnchorWindow();
            if (Application.Current is { } application)
            {
                application.MainWindow = _anchor;
            }

            // 回填宿主回调：模块注册器装配页面 VM 时持转发委托，此刻起托盘气泡与
            // 退出动作指向本壳层实例。
            _hostDelegates.ShowTrayBalloonTip = ShowTrayBalloonTip;
            _hostDelegates.ExitApplication = ExitApplication;

            // 后台模式回填到对话框服务：提示框不呈现、确认框取"是"（见 DialogService）。
            _dialogService.SetBackgroundMode(background);
        }

        /// <summary>当前设置台租户（未打开时为 null）——仅诊断与测试口径。</summary>
        internal SettingsConsole? SettingsConsole => _settingsConsole;
        /// <summary>
        /// 启动编排：先在工作线程上完成插件装载（界面插件的 UI 注册要回到 UI 线程），
        /// 再启动鼠标钩子、换入语言字典、创建托盘与设置台并显示——顺序显式可控。
        /// </summary>
        /// <remarks>
        /// 装载不能阻塞 UI 线程：界面插件经 <c>IPluginUiModule.RegisterUi</c> 在 UI 线程注册资产，
        /// 阻塞式等待会让注册永远排不上队。因此窗口在插件就位之后创建，依赖插件来源的消费方
        ///（程序选择器）不必与装载抢时序。
        /// </remarks>
        public void Run()
            => _ = RunAfterPluginStartupAsync();

        /// <summary>装载插件，随后在本线程（UI 线程）完成其余启动编排。</summary>
        private async Task RunAfterPluginStartupAsync()
        {
            try
            {
                await RunCoreAsync();
            }
            catch (Exception ex)
            {
                // 启动编排为 fire-and-forget：异常不能无人观察（会进 TaskScheduler.UnobservedTaskException），
                // 与插件启动失败同路记入调试日志后按可运行态收场。
                Debug.WriteLine($"Startup failed: {ex.Message}");
            }
        }

        private async Task RunCoreAsync()
        {
            // 插件运行时先于窗口与钩子：扫描刷新宿主状态（启用/停用/版本/路径/准入来源/隔离）并落盘
            // 启动报告（准入四态可见），随后按宿主状态装载启用插件。插件代码跑在工作线程上
            //（不进 UI 线程的同步上下文），UI 注册与清理由宿主调度器回到本线程执行。
            // 扫描与装载失败都不阻断启动——插件缺席、插件出错与插件停用都是可运行态。
            try
            {
                await Task.Run(() => _pluginRuntime.StartAsync(CancellationToken.None));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Plugin startup failed: {ex.Message}");
            }

            StartCore();
        }

        /// <summary>UI 线程上的启动编排：钩子、语言字典、托盘、设置台与初始导航。</summary>
        private void StartCore()
        {
            // 后台模式不启全局鼠标钩子：钩子属产品交互，e2e 不覆盖它，
            // 却可能在用户操作鼠标时把轮盘弹到屏幕上。
            if (!_background)
            {
                _mouseHook.Start();
            }

            // 语言资源字典换入——页面 XAML DynamicResource 的运行时数据源。
            // 订阅与首次应用先于任何页面创建（语言切换经服务事件同步重建，换入不累积）。
            _localization.LanguageChanged += ApplyLanguageDictionary;
            // 语言切换按当前暂停态即时刷新托盘 tooltip；
            // 托盘菜单每次打开时重建，无需在此刷新。
            _localization.LanguageChanged += RefreshTrayTooltip;
            ApplyLanguageDictionary();

            // 托盘驻留气泡：壳层动作（不是设置页职责）——设置台的页面 VM 是会话内的、此刻也可能没开，
            // 气泡一律由壳层直接呈现。
            _messenger.Register<MinimizedToTrayMessage>(this, (_, _) =>
                ShowTrayBalloonTip("StarPie", MinimizedToTrayBalloonText));

            // 设置台会话（页面 VM 与设置台 VM 的宿主）先于初始导航建立：
            // 页面 VM 只存在于设置台会话内，导航执行缝在无会话时会拒绝解析。
            SettingsConsole console = EnsureSettingsConsole();

            // 初始页为“触发与场景”槽位。
            _navigation.Navigate(NavigationSlot.Trigger);

            // 初始主题就绪后监听 Windows 深浅色变化（System 模式自动跟随）——进程级主题状态随壳层，
            // 不随设置台开关反复启停（实现内部幂等，重入安全）。
            _themeService.EnableSystemThemeTracking();

            // 托盘深色配色由壳层以委托注入深色探针，壳层模块不反向引用宿主/主题模块。
            // 静默形态也建托盘：通知区入口保留（人工观察/退出），不影响测试侧驱动。
            _trayIcon = new TrayIconManager(
                windowsInDarkModeProbe: () => _themeService.IsWindowsInDarkTheme(),
                onDoubleClick: () => NavigateAndShow(NavigationSlot.Trigger),
                menuProvider: BuildTrayMenuEntries);
            _trayIcon.SetTooltip(CurrentTooltip());
            // 单实例恢复消息的接收端驻常驻侧：设置台窗口关着时也要能受理"打开设置台"请求。
            _restoreMessageHook = OnResidentWindowMessage;
            _trayIcon.AddHook(_restoreMessageHook);
            // 提权实例持有托盘窗口时，非提权实例的恢复消息会被 UIPI 按完整性级别拦下：
            // 接收端显式放行本进程自有的注册消息，双击图标才置得前已运行的提权实例。
            if (ProcessElevation.IsRunningAsAdministrator())
            {
                SingleInstanceRestore.AllowFromLowerIntegrity(_trayIcon.Handle);
            }

            console.Show();

            // 启动编排末尾：预热轮盘核心路径（BAML/样式渲染器工厂/调色板与画刷构造踩热，
            // 首次手势弹出免付一次性成本），随后兜底内存整理——预热在前、GC 在后，
            // 预热的一次性分配由紧随的 force GC 顺带回收，不等硬顶压力另行触发（#150）。
            WarmUpWheelCorePath();
            RunStartupMemoryHousekeeping();

            StartHandoverListener();
        }

        /// <summary>
        /// 装上让位接收端：等提权新实例"以管理员身份重启/右键运行"时的让位请求，收到即走既有退出编排
        /// （落盘 → 释托盘 → 关闭，互斥体由 App.OnExit 释放）——新实例这才接得上手。
        /// </summary>
        /// <remarks>
        /// 只装在**非提权**首实例上：判定严格单向，提权实例永不让位（<see cref="SingleInstanceGate"/>），
        /// 故它不装接收端，也就不会被任何信号关停；测试实例不持单实例互斥体，同样不装。
        /// 装在编排末尾：让位请求事件由本进程在拿到互斥体时就已发布，置位会**留存**在事件对象上
        /// （ManualReset），故启动期间到达的请求在这里被立刻看到，不会丢。
        /// </remarks>
        private void StartHandoverListener()
        {
            if (_testInstance || !SingleInstanceGate.AcceptsHandoverRequest(ProcessElevation.IsRunningAsAdministrator()))
            {
                return;
            }

            _handoverListener = new InstanceHandoverListener(
                onYieldRequested: () => _ = Application.Current?.Dispatcher.BeginInvoke(ExitApplication));
            _handoverListener.Start();
        }

        /// <summary>启动兜底内存整理 + 堆硬顶生效值日志（GC.GetConfigurationVariables 为运行时
        /// 生效口径，被运行时钳制时以此记录为准；预算值 256 MiB 见 runtimeconfig.template.json）。
        /// 生效值经 <see cref="Debug.WriteLine(string)"/> 记载，只在 Debug 构建/附加调试器时可见
        /// ——正式版核对以产物 StarPie.runtimeconfig.json 的 configProperties 为准。</summary>
        private static void RunStartupMemoryHousekeeping()
        {
            if (GC.GetConfigurationVariables().TryGetValue("GCHeapHardLimit", out object? hardLimit))
            {
                Debug.WriteLine($"[Startup] GC Heap HardLimit 生效值: {hardLimit}");
            }

            MemoryOptimizer.CollectGarbage(true);
        }

        /// <summary>轮盘核心路径离屏预热：以全局方案构造视图模型并渲染一次后放弃产物；
        /// 失败吞异常记调试日志，不影响启动。</summary>
        private void WarmUpWheelCorePath()
        {
            try
            {
                WheelProfile profile = _config.Current.Profiles.Find(p => p.ProcessName == "Global") ?? new WheelProfile();
                var viewModel = new WheelViewModel(new GesturePoint(200, 200), profile, _config.Current, _localization);
                WheelWarmup.Run(viewModel, _themeService, _localization, _iconAssets);
                Debug.WriteLine("[Startup] Wheel core path warmed up");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Startup] Wheel warmup failed (ignored): {ex.Message}");
            }
        }

        public void Dispose()
        {
            // 成对退订语言字典换入（订阅在 Run()），避免壳层释放后事件仍持有引用。
            _localization.LanguageChanged -= ApplyLanguageDictionary;
            _localization.LanguageChanged -= RefreshTrayTooltip;

            // 让位接收端先于托盘释放：退出收尾期间不再受理新的让位请求（进程已经在退出了）。
            _handoverListener?.Dispose();
            _handoverListener = null;

            if (_trayIcon != null)
            {
                DisposeTray();
            }

            // 设置台租户先于常驻件释放：关窗收尾 + VM 树成对退订。
            _settingsConsole?.Dispose();
            _settingsConsole = null;

            _mouseHook.Stop();
        }

        /// <summary>释放托盘与挂在其消息窗口上的常驻钩子（成对摘除；幂等）。</summary>
        private void DisposeTray()
        {
            if (_trayIcon is not { } tray)
            {
                return;
            }

            _trayIcon = null;
            if (_restoreMessageHook is { } hook)
            {
                _restoreMessageHook = null;
                tray.RemoveHook(hook);
            }

            tray.Dispose();
        }

        // 运行时语言字典：本地化服务的 XAML 投影，只持当前语言一份；
        // 原地清空重建，不向合并字典累积旧语言。
        private static readonly ResourceDictionary LanguageDictionary = new();

        /// <summary>用当前语言重建 Application 级语言字典（设置页文本 DynamicResource 的数据源）。</summary>
        private void ApplyLanguageDictionary()
        {
            if (Application.Current is not { } app)
            {
                return;
            }

            if (!app.Resources.MergedDictionaries.Contains(LanguageDictionary))
            {
                app.Resources.MergedDictionaries.Add(LanguageDictionary);
            }

            LanguageDictionary.Clear();
            foreach ((string key, string value) in _localization.EnumerateCurrentEntries())
            {
                LanguageDictionary[key] = value;
            }
        }

        /// <summary>
        /// 显示设置台（未创建则创建）：<paramref name="activate"/> 为真时走淡入激活
        ///（托盘直达/单实例恢复），为假时按启动呈现。关闭后的设置台在此重建。
        /// </summary>
        /// <summary>
        /// 取当前设置台（未开则新建）：设置台是瞬态租户，已关闭（或从未创建）时新建会话，
        /// 已关闭的旧会话先出账。会话作用域（页面 VM 的宿主）在此开启。
        /// </summary>
        private SettingsConsole EnsureSettingsConsole()
        {
            SettingsConsole? current = _settingsConsole;
            if (current is not { IsOpen: true })
            {
                current?.Dispose();
                current = _createSettingsConsole(_anchor, () => _isExiting);
                _settingsConsole = current;
            }

            return current;
        }

        /// <summary>显示设置台：<paramref name="activate"/> 为真时走淡入激活（托盘直达/单实例恢复）。</summary>
        private SettingsConsole ShowSettingsConsole(bool activate)
        {
            SettingsConsole console = EnsureSettingsConsole();
            if (activate)
            {
                console.ShowAndActivate();
            }
            else
            {
                console.Show();
            }

            return console;
        }

        /// <summary>
        /// 按目录槽位导航 + 显示设置台并激活（托盘直达）：先开设置台再导航——
        /// 开窗会先按最后导航槽位重放（恢复序列），重放之后的目标槽位才是用户点选的页；
        /// 页面 VM 只存在于设置台会话内，故导航必须在会话建立之后。
        /// </summary>
        private void NavigateAndShow(NavigationSlot slot)
        {
            ShowSettingsConsole(activate: true);
            _navigation.Navigate(slot);
        }

        private List<TrayMenuEntry> BuildTrayMenuEntries()
        {
            var entries = new List<TrayMenuEntry>
            {
                // 托盘 Header 为品牌/版本名（StarPie v1.4.1 + Dev 标记），不参与翻译。
                TrayMenuEntry.Header("StarPie v1.4.1" + DevInstance.Suffix),
                TrayMenuEntry.Separator()
            };

            string pauseText = _mouseHook.IsPaused ? _localization.GetString("TrayResume") : _localization.GetString("TrayPause");
            entries.Add(TrayMenuEntry.Item(pauseText, TogglePauseGestures));
            // 托盘直达项经目录槽位导航（触发/外观/手势）。
            entries.Add(TrayMenuEntry.Item(_localization.GetString("TrayPreferences"), () => NavigateAndShow(NavigationSlot.Trigger)));
            entries.Add(TrayMenuEntry.Item(_localization.GetString("TrayAppearance"), () => NavigateAndShow(NavigationSlot.Appearance)));
            entries.Add(TrayMenuEntry.Item(_localization.GetString("TrayGestures"), () => NavigateAndShow(NavigationSlot.Gestures)));
            entries.Add(TrayMenuEntry.Separator());
            entries.Add(TrayMenuEntry.Item(_localization.GetString("TrayExit"), ExitApplication));

            // 插件菜单项追加在内置条目之后；无插件菜单项时不追加分隔线（降级不留空壳）。
            return TrayMenuComposer.Compose(entries, _pluginUi, _localization).ToList();
        }

        /// <summary>
        /// 常驻窗口消息：单实例恢复请求 → 创建设置台并显示（设置台关着时也受理）；
        /// 测试实例退出请求 → 走真实退出编排。钩子挂在托盘消息窗口（常驻 HWND）上，
        /// 不依赖设置台窗口是否存在。
        /// </summary>
        private IntPtr OnResidentWindowMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == SingleInstanceRestore.MessageId)
            {
                handled = true;
                _ = Application.Current?.Dispatcher.BeginInvoke(() => ShowSettingsConsole(activate: true));
                return IntPtr.Zero;
            }

            // 注册窗口消息全机可投递：退出只对测试实例受理，正式实例不会被任意进程一刀关停。
            if (_testInstance && msg == TestInstanceExit.MessageId)
            {
                handled = true;
                // 退出收尾要销毁窗口（含本消息窗口），排到当前消息之后执行，不在 WndProc 内重入。
                _ = Application.Current?.Dispatcher.BeginInvoke(() => ExitApplication());
                return IntPtr.Zero;
            }

            return IntPtr.Zero;
        }

        private void ShowTrayBalloonTip(string title, string text)
        {
            _trayIcon?.ShowBalloonTip(title, text);
        }

        private void TogglePauseGestures()
        {
            _mouseHook.IsPaused = !_mouseHook.IsPaused;
            _trayIcon?.SetTooltip(CurrentTooltip());
        }

        /// <summary>当前暂停态对应的托盘 tooltip；语言切换时由壳层按暂停态刷新。</summary>
        private string CurrentTooltip()
        {
            return _mouseHook.IsPaused ? $"StarPie ({_localization.GetString("TrayPause")})" : DefaultTooltip;
        }

        private void RefreshTrayTooltip()
        {
            if (_trayIcon != null)
            {
                _trayIcon.SetTooltip(CurrentTooltip());
            }
        }

        private string DefaultTooltip => _localization.GetString("TrayTooltip") + DevInstance.Suffix;

        /// <summary>最小化到托盘的驻留气泡文案（壳层动作，不经页面 VM）。</summary>
        private const string MinimizedToTrayBalloonText = "应用已最小化至系统托盘，将在后台继续运行鼠标笔势监视。";

        /// <summary>
        /// 托盘退出：按 <see cref="ShellExitSequence"/> 的固定顺序执行（落盘 → 释壳 → 应用关闭）。
        /// 不依赖设置台是否存在——无控制台时同样走完（退出态先置位，Shutdown 触发的关窗不再走
        /// 托盘态出账序列，设置台租户随之销毁）；互斥体与容器的释放由 `App.OnExit` 收尾。
        /// </summary>
        private void ExitApplication()
        {
            foreach (ShellExitStep step in ShellExitSequence.Resolve())
            {
                switch (step)
                {
                    case ShellExitStep.FlushPendingSave:
                        try
                        {
                            // 兜底落盘：直调编排订阅者冲刷挂起防抖并立即落盘。
                            _saveOrchestrator.FlushPendingSave();
                        }
                        catch { }
                        break;
                    case ShellExitStep.ReleaseTray:
                        DisposeTray();
                        break;
                    case ShellExitStep.ShutdownApplication:
                        // 退出态先置位：Shutdown 触发的关窗不走托盘态出账序列。
                        _isExiting = true;
                        Application.Current.Shutdown();
                        break;
                }
            }
        }
    }
}
