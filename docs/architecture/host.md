# 模块：宿主与组合根（App + ShellHost + SettingsConsole + Composition）

> 本文是 [docs/architecture.md](../architecture.md) 的拆分文档；涉及启动顺序、DI 注册、退出/隐藏流程时读本篇。

## 职责

- `App`：进程级生命周期——单实例闸门、全局异常、启动/退出兜底（不做业务）。`App.xaml` 设
  `ShutdownMode="OnExplicitShutdown"`：设置台是瞬态窗口，关窗不代表进程退出，退出只走托盘退出。
- `Composition`：唯一 DI 组合根——`ServiceCollection` 注册、`BuildServiceProvider`、解析常驻依赖并创建
  `ShellHost`，另交付**设置台会话工厂**（工厂闭包在组合根内构造会话对象图，解析点未离开组合根）；
  不持有托盘/主窗口/语言字典等宿主状态。
- `ShellHost`（常驻壳层）：进程存活期内一直存在的编排面——鼠标钩子启停、语言字典投影（H1 消费 S3）、
  托盘创建与菜单、高权限窗口的一次性告知节拍（#165）、插件运行时驱动、单实例恢复消息接收（驻常驻侧）、
  让位请求接收端（非提权首实例上，见 §单实例闸门）、设置台的按需创建与释放、
  驻留与真退出协调（[ADR-0011](../adr/0011-composition-apphost-split.md)、
  [ADR-0039](../adr/0039-resident-shell-and-transient-settings-console.md)）。
- `SettingsConsole`（设置台租户）：按需创建、关闭即销毁的设置控制台会话——主窗口（`MainView`）与其
  VM 树（导航区 `MainViewModel` + 壳区 `ShellViewModel`）同生共死；开窗时应用初始主题、绑定对话框
  Owner、接线托盘状态信号，关窗收尾解绑 Owner 并走瞬态窗口收尾纪律。

## 组成文件

`App.xaml(.cs)`、`Composition.cs`、`ShellHost.cs`、`SettingsConsole.cs`、`DevInstance.cs`（R2：归 H1，驻工程根）、
`SingleInstanceRestore.cs` 与 `TestInstanceExit.cs`（进程生命周期窗口消息：单实例重激活 / 测试实例退出）。

单实例闸门的判定与握手信道驻宿主内核（`StarPie.Host/Kernel/ShellIntegration/`，命名空间
`StarPie.Kernel.ShellIntegration`）：`SingleInstanceGate.cs`（处置决策纯函数）、`InstanceHandover.cs`
（命名内核对象握手：首实例标记/让位请求、提权未生效、就绪判据）、`InstanceHandoverListener.cs`
（让位请求接收端）。行为规范见 §单实例闸门。

瞬态窗口收尾的**唯一实现**：`Services/Shell/TransientWindowTeardown.cs`（清动画 → 丢弃内容与
DataContext → `Close()` → 排空 Dispatcher → 处理 `Application.MainWindow`；设置台与轮盘预热共用）；
常驻锚窗口 `Views/Navigation/ShellAnchorWindow.cs`（永不显示，长期持有 `Application.MainWindow`）。

导航运行时（归 H1，命名空间不变）：

- `Services/Navigation/`：`NavigationStore`、`NavigationExecutor`（含 `INavigationExecutor`）；
- `ViewModels/Navigation/`：`MainViewModel`、`NavigationItemViewModel`（与 `ShellViewModel` 同目录族）。

> 归属边界（[modules.md](modules.md) §5 D4）：`ShellHost` 的语言字典投影与壳外文案刷新是 H1 对 S3 的消费，
> 不是本地化组成文件（见 [localization.md](localization.md)）。

## 生命周期与关键流程

1. `App.OnStartup`：
   - dev 实例标记：`AppDataPaths.IsDevInstance` 按构建配置在编译期定死——Debug 构建
     （`dotnet run` 默认）即 dev 沙箱，Release 构建即正式形态
     （见 [ADR-0037](../adr/0037-dev-instance-flag-by-build-config.md)）。
   - 单实例互斥（全机命名互斥 `Global\StarPie_SingleInstance_Mutex_…`，dev 与正式实例
     同闸、不并行运行）；命令行含 `--allow-multiple`/`--test-instance` 即**测试实例**：
     跳过互斥，并让常驻壳层受理测试实例退出消息（测试运行器用，见下条）。
   - 后台/静默模式：命令行含 `--background` 时，设置窗口固定在屏幕左上角（`0,0`）、界面 `0.9` 缩放（954×648）+ 挂
     `WS_EX_NOACTIVATE`/`WS_EX_TRANSPARENT` 并对 `WM_NCHITTEST` 返回 `HTTRANSPARENT`（不抢焦点、点击穿透）
     + 不进任务栏，且不启全局鼠标钩子；托盘照常创建（人工观察/退出入口）。
     `DialogService` 回填后台模式后提示框不呈现、确认框取"是"，自定义对话框（程序/图标/颜色选择器、
     输入框）仍离屏 + 不可激活。仅影响窗口呈现/激活/命中测试与对话框可见性，导航、配置与渲染语义不变
     （e2e 静默跑用，见 [ADR-0031](../adr/0031-e2e-silent-background-run.md) / [ADR-0032](../adr/0032-e2e-silent-visible-window.md)）。
   - 非首实例：先按纯决策 `SingleInstanceGate.Resolve(本实例是否提权, 既有实例是否提权)` 取处置方式
     （真值表见 §单实例闸门）。**置前退出**：按托盘消息窗口标题（`TrayIconManager.WindowName` =
     `StarPieTrayWindow`，进程存活期内恒在的常驻 HWND）找到既有实例并投递单实例恢复消息
     `SingleInstanceRestore.Send`——接收端在常驻壳层，设置台关着时也受理（创建设置台并显示）；
     随后 `Shutdown(0)`。不按设置台窗口标题查找：设置台是瞬态窗口，关闭后该窗口不存在。
     消息窗口的窗口类名由 WPF 生成（`HwndWrapper[…]`），外部只能按标题定位。
   - 跨完整性级别（提权实例在跑）：UIPI 默认拦截值大于 `WM_USER` 的窗口消息，而注册消息必大于之，
     故提权实例在创建托盘窗口后经 `SingleInstanceRestore.AllowFromLowerIntegrity` 放行**本进程自有的
     这一个注册消息**（`ChangeWindowMessageFilterEx`，按窗口生效、只在提权态执行）——否则非提权实例的
     置前请求会被静默丢掉（"双击图标没反应"）。互斥体方向：更高完整性级别的对象带"不向上写"强制策略，
     本次打开因写访问被拒抛 `UnauthorizedAccessException`，该失败**按"已有实例"处理**并走上面的恢复
     消息路径（退回新实例会得到两个托盘图标与两条全局鼠标钩子）；其余失败才保守退回新实例。
     该路径无法被不提权的 xUnit/e2e 环境复现，验收只能靠真实提权实例 + 非提权双击
     （见 [ADR-0040](../adr/0040-startup-privilege-policy.md)）。
   - 测试实例退出消息（`TestInstanceExit`，`StarPie_TestInstance_Exit`）：测试实例在托盘消息窗口
     受理退出请求，走 `ExitApplication` 的真实退出编排（落盘 → 释放托盘 → 关闭应用）。e2e fixture
     以此收尾：硬杀（`TerminateProcess`）不执行用户态收尾，`NIM_DELETE` 不执行，shell 的通知区会
     留下宿主窗口已失效的死条目（幽灵托盘图标），直到通知区收到鼠标输入才被摘除。注册窗口消息
     全机可投递，故正式实例不受理。
   - 注册全局异常处理器（Dispatcher + AppDomain，均不崩溃）。
   - 首实例（含接管成功者）发布命名标记与"提权未生效"事件并持有到进程结束：后启动的实例由此读得
     既有实例的形态与权限态，接管没成时也有地方留话（见 §单实例闸门）。
   - `new Composition()` → `Config.Load()` → `Composition.CreateShellHost()` → `ShellHost.Run()`
     （启动编排末尾：轮盘预热 → 内存整理兜底 `MemoryOptimizer.CollectGarbage(true)` → 装让位接收端，
     见 [shell.md](shell.md)）；失败弹错误框并退出。
2. `Composition` 注册期（**内置贡献者有序清单驱动**，全部单例；注册顺序 ≠ 解析时机）：
   - 阶段 1｜注册期：`BuiltInContributors.CreateAll(_hostDelegates)` 得到按 `Order` 升序的清单
     （HostCore/HostPage/Theme/Wheel/Gestures/Shell/Dialogs 七个内置贡献者）；先集中调各贡献者
     `RegisterNavigation(catalog)` 并 `Validate()`，再集中调 `RegisterServices(services)`。
   - `HostCoreContributor` 基础设施：`JsonConfigService`（具体类，配置路径经内核 `AppDataPaths.GetAppDataFolder()` 构造）+
     `IConfigService` 别名、`IMessenger` = `WeakReferenceMessenger.Default`、
     `NavigationStore` + `INavigationExecutor`→`NavigationExecutor`（导航运行时主体归 Host，
     目录执行缝为 Host 内部件）以及 `MainViewModel`/`ShellViewModel`。M4 的 `ThemeService`（具体类）+
     `IThemeService` 别名注册由 `ThemeContributor.RegisterServices` 登记（StarPie.Ui；
     `IThemeService` 契约驻 StarPie.Sdk.Wpf，ADR-0023）；M2 的
     轮盘工厂（`IWheelFactory` → `WheelFactory`）与轮盘外观设置子 VM 注册由
     `WheelContributor.RegisterServices` 登记（驻 StarPie.Ui，D5；契约驻 `StarPie.Sdk`，ADR-0023，
     P1.3/#112 收口，M1 手势侧只经契约接口消费）。
   - 程序扫描由 `HostCoreContributor` 登记——`IShortcutTargetResolver→ShortcutResolver` 与
     `IProgramScanner→ProgramScanner`（契约驻 `StarPie.Sdk/Services/Programs|Icons/`，
     实现驻宿主内核 `StarPie.Host/Programs/`，ADR-0023；组合根无静态扫描委托行）。
   - 插件运行时首层由 `HostCoreContributor` 登记——`PluginStateStore`（宿主状态）、
     `PluginAdmissionPolicy`（准入判定，审核清单先接入空实现）、`PluginDeveloperModeService`
     （开发者模式开关与风险披露）、`PluginDiscovery`（安装目录 + 用户目录）与
     `PluginStartupScanner`（发现 → 清单校验 → 准入 → 状态与报告落盘）；路径经
     `PluginPaths` 单一来源推导（见 [plugins.md](plugins.md) §2/§3）。
     宿主侧运行时 `PluginRuntimeHost` 的「彻底移除」另接三条接缝：配置段删除走
     `IConfigService`、插件数据目录走 `PluginPaths.DataDirectory`、删除后立即
     `FlushPendingSave()` 冲刷（见 [config.md](config.md)）。
   - 图标资产由 `HostCoreContributor` 登记——内核 `CustomIconStore`（`StarPie.Host/Icons/`，目录默认
     `AppDataPaths.GetAppDataFolder`）与 Ui 侧 `IIconAssetService→IconAssetService`
     （`StarPie.Ui/Services/Icons/`，实现 Sdk.Wpf 契约并惰性解析 `IShortcutTargetResolver`）。
    - `NavigationCatalog` 由 `HostPageContributor`（槽位 1）、`GesturesContributor`（槽位 0/2）
     与 `ShellContributor`（槽位 3）各自 `RegisterNavigation` 写入，组合根在清单遍历后
     `Validate()` 并单例注册——导航装配/解析清单不硬编码页面类型（运行时
     类型与执行缝的注册见上段基础设施）。
   - 服务：`DialogService`（构造注入图标资产服务、.lnk 解析契约与程序扫描契约——
     程序扫描/.lnk 契约在 `StarPie.Sdk`、图标资产服务契约在 `StarPie.Sdk.Wpf`，
     实现均在组合根登记（ADR-0023）；
     `DialogService` 与 `IDialogService` 的注册随 S6 实现由 `DialogsContributor.RegisterServices`
     登记（Ui 集 `Services/Dialogs/`）；对话框服务另注入共享图标资产实例服务与解析契约，
     供图标/程序选择器使用）、`ISaveDebouncer`（实现 = Ui 适配器 `DispatcherSaveDebouncer`）、
     `SettingsSaveOrchestrator`（宿主内核）。（M1 手势管线
     `MouseHook`/`IActionExecutorService`/`IWindowContext`/`GestureEngine`/`GestureController`
     的注册由 `GesturesContributor.RegisterServices` 登记 Ui 集 M1；`IWheelFactory`
     的注册见 WheelContributor 注。）
   - 页面 VM 工厂注册（单例）：M4 主题服务与界面主题设置子 VM 由
     `ThemeContributor.RegisterServices` 登记 `StarPie.Ui`（模块无导航页，只登记 DI
     注册）；M5 页面（`GeneralSettingsViewModel`）由
     `ShellContributor.RegisterServices` 登记（ADR-0016 决策 8，见
     [assemblies.md](assemblies.md) §6）；M1 两页（`BehaviorSettingsViewModel`/
     `ProfileListViewModel`）由 `GesturesContributor.RegisterServices` 登记
     Ui 集 M1；`AppearanceSettingsViewModel`（薄聚合页壳，构造注入两个
     设置子 VM——`InterfaceThemeSettingsViewModel`（由 ThemeContributor 登记）与
     `WheelAppearanceSettingsViewModel`（由 WheelContributor 登记），
     均另行注册单例）、`MainViewModel`（目录驱动：导航项/选中态全部来自目录注册；运行时主体
     在 Host `ViewModels/Navigation/`，命名空间不变；页面 VM 的 DI 注册已全部
     下放所属贡献者，导航 VM 与外观聚合页 VM 分别由 HostCore/HostPage 贡献者登记；
     `ProfileListViewModel` 另以 M1 只读 `IProfilePreviewSource` 注册别名的动作由
     GesturesContributor 登记（契约随实现方 M1、P1.3/#112 收口入 `StarPie.Sdk`，ADR-0023，
     供轮盘外观设置子 VM 经契约边消费））、`ShellViewModel`（D3：Host 壳窗口壳层 VM——窗口
     标题/退出态/保存，主框架分区 DataContext 的壳区，见 [shell.md](shell.md)）。
   - `AppHostDelegates` 为 SDK 公开契约（`StarPie.Sdk/Services/AppHostDelegates.cs`，P1.3/#112
     收口）并以单例注册进容器，
`ShellHost` 构造后回填；`ShellContributor` 的 VM 工厂经容器惰性解析该委托包，只依赖 SDK。
    - `ThemeContributor.RegisterServices` 在注册期调用（M4 → Host 内核 + Sdk.Wpf
      单向），主题服务/主题设置子 VM 的工厂只解析内核/SDK 契约（`IThemeService` 契约驻
      StarPie.Sdk.Wpf，ADR-0023）；`AppThemePaletteManager` 不经容器，由 `ShellHost` 构造时
      直接 `new` 并经内核端口 `IThemeApplier` 接到主题服务（同集适配器，装配面在 Ui 内）。
    - `WheelContributor.RegisterServices` 在注册期调用（M2 → Sdk + Host 内核 +
      Sdk.Wpf 契约面），轮盘工厂
      `IWheelFactory→WheelFactory` 与轮盘外观设置子 VM 的工厂只解析契约程序集（`IThemeService`
      经 Sdk.Wpf，ADR-0023）；RadialWindow 不经 Host
      直接 new——由 WheelFactory 在 StarPie.Ui 内创建。
    - `GesturesContributor.RegisterServices` 在注册期调用（M1 → Sdk + Host 内核 + Sdk.Wpf
      契约面），手势管线/页面 VM/`IProfilePreviewSource` 别名的工厂只解析内核/SDK 契约与
      SDK 接口（IWheelFactory/IWheelViewModel，M1→M2 runtime 允许边清零，
      ADR-0023；P1.3/#112 收口），M1 不反向引用宿主。
   - `ShellContributor.RegisterServices` 在注册期调用（M5 → Sdk + Host 内核 + Sdk.Wpf
     契约面），自启注册表经本集 `AutostartRegistry` 静态委托接线，页面 VM 不反向引用宿主类
     （托盘气泡与退出已归壳层直接呈现/执行）。
   - **Views 不注册**（页面无参构造；`MainView` 由 `SettingsConsole` 显式 `new`；对话框 Window 由
     `DialogService` 在 Ui 集内显式 `new`）。
3. 阶段 2｜容器构建：唯一 `BuildServiceProvider`，解析点仍只在组合根。
4. 阶段 3｜`Composition.CreateShellHost`（解析点仍集中在组合根，[ADR-0005](../adr/0005-di-container-for-navigation.md)/[0011](../adr/0011-composition-apphost-split.md)）：
   - 解析 `IMessenger`、`MouseHook`、`DialogService`、`IThemeService`、`SettingsSaveOrchestrator`、
     `INavigationExecutor`、`NavigationCatalog`、`GestureController`、`NavigationStore`；
   - 页面 VM **不在启动期解析**：它们的作用域是设置台会话，首次进入该页时由导航执行缝经
     `ConsolePageSession` 构造（scoped 注册，作用域 = 会话；会话结束整批释放）；壳层直持的常驻 VM
     在此解析（`InterfaceThemeSettingsViewModel` 由设置台会话工厂从会话作用域取，故此处只解析
     常驻件）；
   - 构造并交付**设置台会话工厂** `Func<Window, SettingsConsole>`：每次开窗时开启设置台会话作用域
     （`ConsolePageSession.Begin`：页面 VM 与设置子 VM 的实例边界），新建导航区 `MainViewModel`
     与壳区 `ShellViewModel`（不注册进容器——它们随设置台开关生灭），并从会话作用域解析
     `InterfaceThemeSettingsViewModel`（初始主题），与主题服务、对话框服务、图标资产、导航出账、
     消息总线、锚窗口、会话缓存一起构造 `SettingsConsole`；
   - 构造 `ShellHost`（持有常驻件、设置台工厂与常驻锚窗口）并回填 `AppHostDelegates`
     （托盘气泡、退出）。
5. `ShellHost.Run`（顺序固定，[ADR-0003](../adr/0003-application-host-restructure.md)）：
   - 插件启动扫描（发现/清单校验/准入 + 宿主状态与启动报告落盘，见 [plugins.md](plugins.md) §3；
     不装载插件代码，失败不阻断启动）→ `_mouseHook.Start()` → 订阅
     `ILocalizationService.LanguageChanged`（重建语言字典、刷新托盘 tooltip）并
    首次应用语言字典（投影见 [localization.md](localization.md)）→
     `EnsureSettingsConsole()`：经工厂建设置台租户与会话作用域（页面 VM 的宿主，必须先于初始导航）→
     初始导航 `INavigationExecutor.Navigate(NavigationSlot.Trigger)`（触发与场景，目录槽位）→
     创建 `TrayIconManager` 并挂常驻窗口消息钩子（单实例恢复 + 测试实例退出，见 [shell.md](shell.md)）→
     `console.Show()` → `new MainView(...)` + 应用初始界面主题
      （`MainView.ApplyAppTheme`，见 [interface-theme.md](interface-theme.md)）→
      `_dialogService.SetOwner(view)`（Ui 集 public 装配面）→ `Application.MainWindow = view` →
     `view.Show()`。
6. 退出：托盘退出（正式实例的唯一入口；测试实例另受理退出消息，见 §1）→
   `ShellHost.ExitApplication` 按 `ShellExitSequence` 固定顺序执行：
   冲刷挂起保存 → 释放托盘（含摘除恢复消息钩子）→ 置退出态并 `Application.Shutdown()`
   （`ShutdownMode=OnExplicitShutdown`，关窗不自行结束进程）。顺序无输入参数即语义：
   退出编排不依赖设置台是否存在（无控制台时同样走完）。
   `App.OnExit`：`Config.Save()` 兜底 → `ShellHost.Dispose()`（退订语言服务、释放让位接收端、释放设置台
   租户、托盘 dispose、`_mouseHook.Stop()`）→ `Composition.Dispose()`（容器 dispose）→ 释放互斥体与命名标记。
7. 设置台关闭（`MinimizedToTray` 语义）：关窗即销毁，托盘驻留由常驻壳层承担。
   托盘状态信号的输入是**设置台开/关**（不是窗口可见性：新建窗口首次 `Show()` 同样产生可见性变化，
   按可见性判读会把「首次打开」误判成「从托盘恢复」），经 `TrayStateSignal` 有序决策执行：
   关闭 → 冲刷保存 → 导航视图出账 → 图标缓存出账 → 发 `MinimizedToTrayMessage`（订阅方同步出账）→
   内存整理 `MemoryOptimizer.CollectGarbage()` 后台执行；重开 → 按最后导航槽位重放导航 → 发
   `RestoredFromTrayMessage`（见 [shell.md](shell.md)）。关闭序列先于窗口收尾执行（落盘与导航出账
   都要求会话内页面 VM 还在）；随后走 `TransientWindowTeardown`（清动画 → 丢弃内容与 DataContext →
   `Close()` → 排空 Dispatcher → `Application.MainWindow` 回退锚窗口）、解绑对话框 Owner、
   结束会话作用域（会话内页面 VM 与设置子 VM 整批释放）。
   托盘直达项与单实例恢复都经 `ShellHost.ShowSettingsConsole` 创建设置台；
   托盘直达先开窗（触发重放）再导航到目标槽位，避免重放覆盖用户点选的页。
   进托盘不再弹驻留气泡（该提示已移除）；`MinimizedToTrayMessage` 仍照发，作为出账信号由订阅方消费，不由壳层呈现任何用户可见提示。

## 宿主委托包

`AppHostDelegates`（SDK 公开契约，`StarPie.Sdk/Services/AppHostDelegates.cs`，P1.3/#112 收口）承载页面 VM
注册所需的宿主回调：`ShowTrayBalloonTip`、`ExitApplication`、`RestartElevated`。
组合根持有该实例并由 `HostCoreContributor` 登记单例，`ShellContributor` 装配 `GeneralSettingsViewModel` 时持稳定转发委托，
`ShellHost` 构造后回填实现（`_hostDelegates.ShowTrayBalloonTip/ExitApplication/RestartElevated = …`）；VM 不反向依赖宿主类。
委托包名保持 `AppHostDelegates`（SDK 公开契约面，ABI additive-only，改名破坏第三方插件编译兼容）。

## 单实例闸门

闸门只在 `App.OnStartup` 一处做判定（`SingleInstanceGate`，纯函数 + `SingleInstanceGateTests` 真值表）；
三条来路——应用内「立即以管理员身份重启」、右键 `runas`、`schtasks /run`——共用同一条规则，
不给应用内入口开专用通道（[ADR-0043](../adr/0043-elevated-instance-takeover.md)）。

| 本实例 | 既有实例 | 处置 |
|---|---|---|
| 非提权 | 任意 | 置前退出 |
| 提权 | 提权 | 置前退出（接管无收益，却要付一次进程交接） |
| 提权 | 非提权 | 请求让位；等不到就退出并告知未生效 |

判定**严格单向**：接管的目的只有"提升权限"一种，反向让位等于把用户的提权状态交给一次误双击。

- **既有实例的形态与权限态**由它发布的标记事件读得（`InstanceHandover.ProbeOwner`）：名字编码发布者的
  形态与权限态，存在即"同形态首实例在此"。读不到同形态标记（互斥体被另一形态的实例持有，或对方尚未
  发布）即按置前退出走——不冒险，也不空等。标记由首实例在拿到互斥体后发布并持有到进程结束，
  与互斥体同生共死（`App.OnExit` 释放）。
- **让位握手走命名内核对象**（`Global\` 命名事件，同用户跨完整性级别可开），不用窗口消息——窗口消息只
  保留既有的置前语义。事件对象归首实例所有，因为置位方向总是"更高的完整性级别写更低的对象"。
- **就绪判据只有"单实例互斥体已可取得"**（`InstanceHandover.WaitForSingleInstanceRelease`），
  不取自任何触发命令的退出码——实测 `schtasks /run` 的退出码只表示"任务被受理"，动作为空的任务
  同样返回成功。等不到时新实例在明确时限内退出、不留残进程。
- **让位执行**：接收端 `InstanceHandoverListener` 只装在**非提权**首实例上（提权实例永不让位），收到请求
  即切回 UI 线程走既有退出编排（落盘 → 释托盘 → 关闭）。托盘先于互斥体释放、鼠标钩子在 `ShellHost.Dispose`
  停止，故新实例接手时不会出现两个图标或两条全局钩子。
- **失败告知**：新实例退出前经第二个事件留下"这次提权未生效"，首实例经既有气泡通道说明原因与后续动作；
  连"受理"都失败时由发起方就地告知。成功路径不产生该提示（置位只发生在"请求让位未成"那一格）。

## 扩展点

- 新服务/新页面 VM：M5 新服务/页面在 `ShellContributor`、M1 新服务/
  页面在 `GesturesContributor`（内置贡献者）登记；Host 外观聚合页 VM 在
  `HostPageContributor` 登记，宿主编排/内核件在 `HostCoreContributor` 登记
  （导航项与页面模板一律经所属贡献者 + 模块模板字典，
  见 [navigation.md](navigation.md)/[naming.md](naming.md)）。
- 新托盘入口：在 `ShellHost.BuildTrayMenuEntries` 登记（托盘职责见 [shell.md](shell.md)）。
- 新增“启动/退出/隐藏”副作用：优先以委托注入页面 VM，不新增服务定位器；常驻编排改 `ShellHost`，
  设置台会话内编排改 `SettingsConsole`，解析面的增删改 `Composition`。

## 参见 ADR

[0003](../adr/0003-application-host-restructure.md)（宿主重构）、[0005](../adr/0005-di-container-for-navigation.md)（容器导航）、[0011](../adr/0011-composition-apphost-split.md)（组合根与 AppHost 拆分）、[0015](../adr/0015-module-map-and-ownership.md)（12 模块地图：H1/R2/D4）、[0039](../adr/0039-resident-shell-and-transient-settings-console.md)（常驻壳层与瞬态设置台租户）、[0040](../adr/0040-startup-privilege-policy.md)（启动权限策略）、[0043](../adr/0043-elevated-instance-takeover.md)（接管与让位协议）。
