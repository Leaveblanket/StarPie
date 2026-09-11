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
   - 后台/静默模式：命令行含 `--background` 时，设置窗口离屏（`-32000,-32000`）+ 挂 `WS_EX_NOACTIVATE` + 不进任务栏，
     且不建托盘、不启全局鼠标钩子；`DialogService` 回填后台模式后提示框不呈现、确认框取"是"，
     自定义对话框（程序/图标/颜色选择器、输入框）同样离屏 + 不可激活。
     仅影响窗口呈现/激活与对话框可见性，导航、配置与渲染语义不变（e2e 静默跑用，见 [ADR-0031](../adr/0031-e2e-silent-background-run.md)）。
   - 非首实例：查找既有设置窗口并置前（`FindWindow`/`ShowWindow`/`SetForegroundWindow`），然后 `Shutdown(0)`。
   - 注册全局异常处理器（Dispatcher + AppDomain，均不崩溃）。
   - `new Composition()` → `Config.Load()` → `Composition.CreateAppHost()` → `AppHost.Run()` → 内存整理兜底
     `MemoryOptimizer.TrimMemory(true)`（见 [shell.md](shell.md)）；失败弹错误框并退出。
2. `Composition.ConfigureServices`（全部单例）：
   - 装配前回填跨程序集环境参数缝：`AppDataPaths.IsDevInstance = DevInstance.IsActive`
     （S2 dev 目录分支）——Core 不反向引用宿主；S1 .lnk 图标提取的解析契约经 DI 注入的
     `IShortcutTargetResolver`（ProgramsModuleRegistrar 注册 M3 实现），无静态回填。
   - 基础设施：`JsonConfigService`（具体类，配置路径经 Core `AppDataPaths.GetAppDataFolder()` 构造）+
     `IConfigService` 别名、`IMessenger` = `WeakReferenceMessenger.Default`、
     `NavigationStore` + `INavigationExecutor`→`NavigationExecutor`（导航运行时主体归 Host，
     目录执行缝为 Host 内部件）。M4 的 `ThemeService`（具体类）+
     `IThemeService` 别名注册由 `ThemeModuleRegistrar.RegisterServices` 下放（StarPie.Theme；
     `IThemeService` 契约驻 Theme.Contracts，ADR-0023），组合根不直接登记主题服务；M2 的
     轮盘工厂（`IWheelFactory` → `WheelFactory`）与轮盘外观设置子 VM 注册由
     `WheelModuleRegistrar.RegisterServices` 下放（StarPie.Wheel，D5；契约驻 `StarPie.Sdk`，ADR-0023，
     P1.3/#112 收口，M1 手势侧只经契约接口消费），组合根不直接登记轮盘工厂。
   - `ProgramsModuleRegistrar.RegisterServices` 在组合根调用——注册
     `IShortcutTargetResolver→ShortcutResolver` 与 `IProgramScanner→ProgramScanner`（契约随
     实现方驻 `StarPie.Programs.Contracts`，ADR-0023；M3 → Programs.Contracts +
     Icons.Contracts 单向，不再引用 Core；组合根无静态扫描委托行）。
   - S1 图标资产实例服务由 `IconsModuleRegistrar.RegisterServices`（StarPie.Icons，
     ADR-0023）注册（`IconAssetService` 构造惰性解析 Programs.Contracts 的
     `IShortcutTargetResolver`（ADR-0023），目录默认 Core `AppDataPaths.GetAppDataFolder`）；组合根
     不直接登记 `IIconAssetService→IconAssetService`。
   - `NavigationCatalog` 由 `StarPie.Gestures` 的 `GesturesModuleRegistrar.RegisterNavigation`、
     `StarPie.Shell` 的 `ShellModuleRegistrar.RegisterNavigation` 与 exe 内 `HostModuleRegistrar`
     按固定顺序装配并 `Validate()` 后单例注册——导航装配/解析清单不硬编码页面类型（运行时
     类型与执行缝的注册见上段基础设施）。
   - 服务：`DialogService`（构造注入共享图标资产实例服务、.lnk 解析契约与程序扫描契约——
     扫描/.lnk 经 Programs.Contracts、图标经 Icons.Contracts 契约注入，程序扫描候选经
     `IProgramScanner`（M3 注册器提供实现）注入（ADR-0023）；
     `DialogService` 与 `IDialogService` 的注册随 S6 实现由 `DialogsModuleRegistrar.RegisterServices`
     下放（StarPie.Dialogs）；对话框服务另注入共享图标资产实例服务与解析契约，
     供图标/程序选择器使用）、`ISaveDebouncer`、`SettingsSaveOrchestrator`。（M1 手势管线
     `MouseHook`/`IActionExecutorService`/`IWindowContext`/`GestureEngine`/`GestureController`
     的注册由 `GesturesModuleRegistrar.RegisterServices` 下放 `StarPie.Gestures`；`IWheelFactory`
     的注册见 WheelModuleRegistrar 注。）
   - 页面 VM 工厂注册（单例）：M4 主题服务与界面主题设置子 VM 由
     `ThemeModuleRegistrar.RegisterServices` 下放 `StarPie.Theme`（模块无导航页，只下放 DI
     注册）；M5 页面（`GeneralSettingsViewModel`）由
     `ShellModuleRegistrar.RegisterServices` 下放模块程序集（ADR-0016 决策 8，见
     [assemblies.md](assemblies.md) §6）；M1 两页（`BehaviorSettingsViewModel`/
     `ProfileListViewModel`）由 `GesturesModuleRegistrar.RegisterServices` 下放
     `StarPie.Gestures`；组合根仍注册 `AppearanceSettingsViewModel`（薄聚合页壳，构造注入两个
     设置子 VM——`InterfaceThemeSettingsViewModel`（由 ThemeModuleRegistrar 注册）与
     `WheelAppearanceSettingsViewModel`（由 WheelModuleRegistrar 注册，随 StarPie.Wheel 下放），
     均另行注册单例）、`MainViewModel`（目录驱动：导航项/选中态全部来自目录注册；运行时主体
     在 Host `ViewModels/Navigation/`，命名空间不变；页面 VM 的 DI 注册已全部
     下放所属模块注册器，仅 Host 外观聚合页 VM 与导航 VM 留在组合根；
     `ProfileListViewModel` 另以 M1 只读 `IProfilePreviewSource` 注册别名的动作由
     GesturesModuleRegistrar 下放（契约随实现方 M1、P1.3/#112 收口入 `StarPie.Sdk`，ADR-0023，
     供轮盘外观设置子 VM 经契约边消费））、`ShellViewModel`（D3：Host 壳窗口壳层 VM——窗口
     标题/退出态/保存，主框架分区 DataContext 的壳区，见 [shell.md](shell.md)）。
   - `AppHostDelegates` 为 SDK 公开契约（`StarPie.Sdk/Services/AppHostDelegates.cs`，P1.3/#112
     自 Core 收口）并以单例注册进容器，
     `AppHost` 构造后回填；`ShellModuleRegistrar` 的 VM 工厂经容器惰性解析该委托包，只依赖 SDK。
    - `ThemeModuleRegistrar.RegisterServices` 在组合根先行调用（M4 → Core + Theme.Contracts
      单向），主题服务/主题设置子 VM 的工厂只解析 Core 契约（`IThemeService` 契约驻
      Theme.Contracts，ADR-0023）；`AppThemePaletteManager` 不经容器，由 `AppHost` 构造时
      直接 `new`（StarPie.Theme public，Host 装配面）。
    - `WheelModuleRegistrar.RegisterServices` 在组合根调用（M2 → Sdk + Core +
      Theme.Contracts/Icons.Contracts 等契约），轮盘工厂
      `IWheelFactory→WheelFactory` 与轮盘外观设置子 VM 的工厂只解析契约程序集（`IThemeService`
      经 Theme.Contracts，M2→M4 runtime 允许边清零，ADR-0023）；RadialWindow 不经 Host
      直接 new——由 WheelFactory 在 StarPie.Wheel 内创建。
    - `GesturesModuleRegistrar.RegisterServices` 在组合根调用（M1 → Sdk + Core + Icons.Contracts
      等契约），手势管线/页面 VM/`IProfilePreviewSource` 别名的工厂只解析 Core 契约与
      SDK 接口（IWheelFactory/IWheelViewModel，M1→M2 runtime 允许边清零，
      ADR-0023；P1.3/#112 收口）；MouseHook dev 分支读 Core `AppDataPaths.IsDevInstance` 回填缝（组合根
      装配前已以 DevInstance.IsActive 回填），M1 不反向引用 Host。
   - `GeneralSettingsViewModel` 的托盘气泡/退出回调经 SDK `AppHostDelegates` 转发注册，不直接引用宿主类。
   - **Views 不注册**（页面无参构造；`MainView` 由 `AppHost` 显式 `new`；对话框 Window 由
     `DialogService` 在 `StarPie.Dialogs` 内显式 `new`）。
3. `Composition.CreateAppHost`（解析点仍集中在组合根，[ADR-0005](../adr/0005-di-container-for-navigation.md)/[0011](../adr/0011-composition-apphost-split.md)）：
   - 解析 `IMessenger`、`MouseHook`、`DialogService`、`IThemeService`、`SettingsSaveOrchestrator`、
     `INavigationExecutor`、`NavigationCatalog`、`GestureController`；
   - **页面 VM eager 解析清单目录化**：遍历 `NavigationCatalog.Entries` 逐个解析注册的页面 VM
     （VM 构造即订阅导入广播/落盘消息与 I18n 事件，时机在 `Config.Load` 之后；eager 语义保留——
     新增页面注册进目录即自动纳入启动构造）；另解析 `MainViewModel`/`ShellViewModel` 与宿主
     直持的 `InterfaceThemeSettingsViewModel`/`GeneralSettingsViewModel`（分别已由
     `ThemeModuleRegistrar`/`ShellModuleRegistrar` 注册，组合根仅解析取回单例；初始主题与托盘/
     驻留气泡直调不变）；
   - 构造 `AppHost` 并回填 `AppHostDelegates`（托盘气泡、退出）。
4. `AppHost.Run`（顺序固定，[ADR-0003](../adr/0003-application-host-restructure.md)）：
   - `_mouseHook.Start()` → 订阅 `ILocalizationService.LanguageChanged`（重建语言字典、刷新托盘 tooltip）并
     首次应用语言字典（投影见 [localization.md](localization.md)）→ 注册托盘驻留气泡订阅 → 初始导航
     `INavigationExecutor.Navigate(NavigationSlot.Trigger)`（触发与场景，目录槽位）→
     `new MainView(...)` + 应用初始界面主题
      （`MainView.ApplyAppTheme`，见 [interface-theme.md](interface-theme.md)）→
      `_dialogService.SetOwner(_mainView)`（StarPie.Dialogs public 装配面）
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
组合根把单例实例注册进容器，`ShellModuleRegistrar` 装配 `GeneralSettingsViewModel` 时持稳定转发委托，
`AppHost` 构造后回填实现（`_hostDelegates.ShowTrayBalloonTip/ExitApplication = …`）；VM 不反向依赖宿主类。

## 扩展点

- 新服务/新页面 VM：M5 新服务/页面在 `ShellModuleRegistrar`、M1 新服务/
  页面在 `GesturesModuleRegistrar`（模块内注册器）登记；仅 Host 外观聚合页 VM 在
  `Composition.ConfigureServices` 注册（导航项与页面模板一律经所属模块注册器 + 模块模板字典，
  见 [navigation.md](navigation.md)/[naming.md](naming.md)）。
- 新托盘入口：在 `AppHost.BuildTrayMenuEntries` 登记（托盘职责见 [shell.md](shell.md)）。
- 新增“启动/退出/隐藏”副作用：优先以委托注入页面 VM，不新增服务定位器；宿主编排改 `AppHost`，不改 `Composition`。

## 参见 ADR

[0003](../adr/0003-application-host-restructure.md)（宿主重构）、[0005](../adr/0005-di-container-for-navigation.md)（容器导航）、[0011](../adr/0011-composition-apphost-split.md)（组合根与 AppHost 拆分）、[0015](../adr/0015-module-map-and-ownership.md)（12 模块地图：H1/R2/D4）。
