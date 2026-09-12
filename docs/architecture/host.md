# 模块：宿主与组合根（App + AppHost + Composition）

> 本文是 [docs/architecture.md](../architecture.md) 的拆分文档；涉及启动顺序、DI 注册、退出/隐藏流程时读本篇。

## 职责

- `App`：进程级生命周期——单实例、全局异常、启动/退出兜底（不做业务）。
- `Composition`：唯一 DI 组合根——`ServiceCollection` 注册、`BuildServiceProvider`、解析宿主依赖并创建 `AppHost`；不持有托盘/主窗口/语言字典等宿主状态。
- `AppHost`：宿主启动/退出编排——鼠标钩子启停、语言字典投影（H1 消费 S3）、托盘创建与菜单、主框架创建、隐藏到托盘与真退出协调（[ADR-0011](../adr/0011-composition-apphost-split.md)）。

## 组成文件

`App.xaml(.cs)`、`Composition.cs`、`AppHost.cs`、`DevInstance.cs`（R2：归 H1，驻工程根）。

导航运行时（归 H1，命名空间不变）：

- `Services/Navigation/`：`NavigationStore`、`NavigationExecutor`（含 `INavigationExecutor`）；
- `ViewModels/Navigation/`：`MainViewModel`、`NavigationItemViewModel`（与 `ShellViewModel` 同目录族）。

> 归属边界（[modules.md](modules.md) §5 D4）：`AppHost` 的语言字典投影与壳外文案刷新是 H1 对 S3 的消费，
> 不是本地化组成文件（见 [localization.md](localization.md)）。

## 生命周期与关键流程

1. `App.OnStartup`：
   - 单实例互斥（`DevInstance.MutexName`；`--dev` 与正式版并存、同类互斥）；命令行含 `--allow-multiple`/`--test-instance` 时跳过互斥（测试运行器用）。
   - 后台/静默模式：命令行含 `--background` 时，设置窗口固定在屏幕左上角（`0,0`）、界面 `0.9` 缩放（954×648）+ 挂
     `WS_EX_NOACTIVATE`/`WS_EX_TRANSPARENT` 并对 `WM_NCHITTEST` 返回 `HTTRANSPARENT`（不抢焦点、点击穿透）
     + 不进任务栏，且不启全局鼠标钩子；托盘照常创建（人工观察/退出入口）。
     `DialogService` 回填后台模式后提示框不呈现、确认框取"是"，自定义对话框（程序/图标/颜色选择器、
     输入框）仍离屏 + 不可激活。仅影响窗口呈现/激活/命中测试与对话框可见性，导航、配置与渲染语义不变
     （e2e 静默跑用，见 [ADR-0031](../adr/0031-e2e-silent-background-run.md) / [ADR-0032](../adr/0032-e2e-silent-visible-window.md)）。
   - 非首实例：查找既有设置窗口并置前（`FindWindow`/`ShowWindow`/`SetForegroundWindow`），然后 `Shutdown(0)`。
   - 注册全局异常处理器（Dispatcher + AppDomain，均不崩溃）。
   - `new Composition()` → `Config.Load()` → `Composition.CreateAppHost()` → `AppHost.Run()` → 内存整理兜底
     `MemoryOptimizer.TrimMemory(true)`（见 [shell.md](shell.md)）；失败弹错误框并退出。
2. `Composition` 注册期（**内置贡献者有序清单驱动**，全部单例；注册顺序 ≠ 解析时机）：
   - 阶段 1｜早期回填（注册前）：`AppDataPaths.IsDevInstance = DevInstance.IsActive`
     （S2 dev 目录分支，`AppDataPaths` 在宿主内核 `StarPie.Host/Kernel/Configuration/`）——
     内核不反向引用宿主；.lnk 图标提取的解析契约经 DI 注入的
     `IShortcutTargetResolver`（组合根登记宿主内核 `ShortcutResolver`），无静态回填。
   - 阶段 2｜注册期：`BuiltInContributors.CreateAll(_hostDelegates)` 得到按 `Order` 升序的清单
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
`AppHost` 构造后回填；`ShellContributor` 的 VM 工厂经容器惰性解析该委托包，只依赖 SDK。
    - `ThemeContributor.RegisterServices` 在注册期调用（M4 → Host 内核 + Sdk.Wpf
      单向），主题服务/主题设置子 VM 的工厂只解析内核/SDK 契约（`IThemeService` 契约驻
      StarPie.Sdk.Wpf，ADR-0023）；`AppThemePaletteManager` 不经容器，由 `AppHost` 构造时
      直接 `new` 并经内核端口 `IThemeApplier` 接到主题服务（同集适配器，装配面在 Ui 内）。
    - `WheelContributor.RegisterServices` 在注册期调用（M2 → Sdk + Host 内核 +
      Sdk.Wpf 契约面），轮盘工厂
      `IWheelFactory→WheelFactory` 与轮盘外观设置子 VM 的工厂只解析契约程序集（`IThemeService`
      经 Sdk.Wpf，ADR-0023）；RadialWindow 不经 Host
      直接 new——由 WheelFactory 在 StarPie.Ui 内创建。
    - `GesturesContributor.RegisterServices` 在注册期调用（M1 → Sdk + Host 内核 + Sdk.Wpf
      契约面），手势管线/页面 VM/`IProfilePreviewSource` 别名的工厂只解析内核/SDK 契约与
      SDK 接口（IWheelFactory/IWheelViewModel，M1→M2 runtime 允许边清零，
      ADR-0023；P1.3/#112 收口）；MouseHook dev 分支读内核 `AppDataPaths.IsDevInstance` 回填缝（组合根
      装配前已以 DevInstance.IsActive 回填），M1 不反向引用宿主。
   - `GeneralSettingsViewModel` 的托盘气泡/退出回调经 SDK `AppHostDelegates` 转发注册，不直接引用宿主类。
   - **Views 不注册**（页面无参构造；`MainView` 由 `AppHost` 显式 `new`；对话框 Window 由
     `DialogService` 在 Ui 集内显式 `new`）。
3. 阶段 4｜`Composition.CreateAppHost`（解析点仍集中在组合根，[ADR-0005](../adr/0005-di-container-for-navigation.md)/[0011](../adr/0011-composition-apphost-split.md)）：
   - 解析 `IMessenger`、`MouseHook`、`DialogService`、`IThemeService`、`SettingsSaveOrchestrator`、
     `INavigationExecutor`、`NavigationCatalog`、`GestureController`；
   - **页面 VM eager 解析清单目录化**：遍历 `NavigationCatalog.Entries` 逐个解析注册的页面 VM
     （VM 构造即订阅导入广播/落盘消息与 I18n 事件，时机在 `Config.Load` 之后；eager 语义保留——
     新增页面注册进目录即自动纳入启动构造）；另解析 `MainViewModel`/`ShellViewModel` 与宿主
     直持的 `InterfaceThemeSettingsViewModel`/`GeneralSettingsViewModel`（分别已由
     `ThemeContributor`/`ShellContributor` 登记，组合根仅解析取回单例；初始主题与托盘/
     驻留气泡直调不变）；
   - 构造 `AppHost` 并回填 `AppHostDelegates`（托盘气泡、退出）。
4. `AppHost.Run`（顺序固定，[ADR-0003](../adr/0003-application-host-restructure.md)）：
   - 插件启动扫描（发现/清单校验/准入 + 宿主状态与启动报告落盘，见 [plugins.md](plugins.md) §3；
     不装载插件代码，失败不阻断启动）→ `_mouseHook.Start()` → 订阅
     `ILocalizationService.LanguageChanged`（重建语言字典、刷新托盘 tooltip）并
    首次应用语言字典（投影见 [localization.md](localization.md)）→ 注册托盘驻留气泡订阅 → 初始导航
     `INavigationExecutor.Navigate(NavigationSlot.Trigger)`（触发与场景，目录槽位）→
     `new MainView(...)` + 应用初始界面主题
      （`MainView.ApplyAppTheme`，见 [interface-theme.md](interface-theme.md)）→
      `_dialogService.SetOwner(_mainView)`（Ui 集 public 装配面）
     → 创建 `TrayIconManager`（见 [shell.md](shell.md)）→ `_mainView.Show()`。
5. 退出：托盘退出 → `AppHost.ExitApplication`：冲刷挂起保存 → dispose 托盘 → `ShellViewModel.IsExiting = true`
   → `Application.Shutdown()`。`App.OnExit`：`Config.Save()` 兜底 → `AppHost.Dispose()`（退订语言服务、托盘
   dispose、`_mouseHook.Stop()`、`MainViewModel.Dispose()`、`ShellViewModel.Dispose()`）→ `Composition.Dispose()`
   （容器 dispose）→ 释放互斥体。
6. 设置窗口隐藏（关窗/`MinimizedToTray` 语义）：`MainView.IsVisibleChanged`（非退出态）→ 冲刷保存 → 内存整理
   `MemoryOptimizer.TrimMemory()`（见 [shell.md](shell.md)）→ 发 `MinimizedToTrayMessage` → `AppHost` 直调
   `GeneralSettingsViewModel.NotifyMinimizedToTray()`。

## 宿主委托包

`AppHostDelegates`（SDK 公开契约，`StarPie.Sdk/Services/AppHostDelegates.cs`，P1.3/#112 收口）承载页面 VM
注册所需的宿主回调：`ShowTrayBalloonTip`、`ExitApplication`。
组合根持有该实例并由 `HostCoreContributor` 登记单例，`ShellContributor` 装配 `GeneralSettingsViewModel` 时持稳定转发委托，
`AppHost` 构造后回填实现（`_hostDelegates.ShowTrayBalloonTip/ExitApplication = …`）；VM 不反向依赖宿主类。

## 扩展点

- 新服务/新页面 VM：M5 新服务/页面在 `ShellContributor`、M1 新服务/
  页面在 `GesturesContributor`（内置贡献者）登记；Host 外观聚合页 VM 在
  `HostPageContributor` 登记，宿主编排/内核件在 `HostCoreContributor` 登记
  （导航项与页面模板一律经所属贡献者 + 模块模板字典，
  见 [navigation.md](navigation.md)/[naming.md](naming.md)）。
- 新托盘入口：在 `AppHost.BuildTrayMenuEntries` 登记（托盘职责见 [shell.md](shell.md)）。
- 新增“启动/退出/隐藏”副作用：优先以委托注入页面 VM，不新增服务定位器；宿主编排改 `AppHost`，不改 `Composition`。

## 参见 ADR

[0003](../adr/0003-application-host-restructure.md)（宿主重构）、[0005](../adr/0005-di-container-for-navigation.md)（容器导航）、[0011](../adr/0011-composition-apphost-split.md)（组合根与 AppHost 拆分）、[0015](../adr/0015-module-map-and-ownership.md)（12 模块地图：H1/R2/D4）。
