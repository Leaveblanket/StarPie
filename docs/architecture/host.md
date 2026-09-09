# 模块：宿主与组合根（App + AppHost + Composition）

> 本文是 [docs/architecture.md](../architecture.md) 的拆分文档；涉及启动顺序、DI 注册、退出/隐藏流程时读本篇。

## 职责

- `App`：进程级生命周期——单实例、全局异常、启动/退出兜底（不做业务）。
- `Composition`：唯一 DI 组合根——`ServiceCollection` 注册、`BuildServiceProvider`、解析宿主依赖并创建 `AppHost`；不持有托盘/主窗口/语言字典等宿主状态。
- `AppHost`：宿主启动/退出编排——鼠标钩子启停、语言字典投影（H1 消费 S3）、托盘创建与菜单、主框架创建、隐藏到托盘与真退出协调（[ADR-0011](../adr/0011-composition-apphost-split.md)）。

## 组成文件

`App.xaml(.cs)`、`Composition.cs`、`AppHost.cs`、`DevInstance.cs`（R2：`DevInstance` 归 H1，物理已随
#70 收编工程根）。

导航运行时（ADR-0021/#92 起归 H1，命名空间不变）：

- `Services/Navigation/`：`NavigationStore`、`NavigationExecutor`（含 `INavigationExecutor`）；
- `ViewModels/Navigation/`：`MainViewModel`、`NavigationItemViewModel`（与 `ShellViewModel` 同目录族）。

> 归属边界（[modules.md](modules.md) §5 D4）：`AppHost` 的语言字典投影与壳外文案刷新是 H1 对 S3 的消费，
> 不是本地化组成文件（见 [localization.md](localization.md)）。

## 生命周期与关键流程

1. `App.OnStartup`：
   - 单实例互斥（`DevInstance.MutexName`；`--dev` 与正式版并存、同类互斥）；命令行含 `--allow-multiple`/`--test-instance` 时跳过互斥（测试运行器用）。
   - 非首实例：查找既有设置窗口并置前（`FindWindow`/`ShowWindow`/`SetForegroundWindow`），然后 `Shutdown(0)`。
   - 注册全局异常处理器（Dispatcher + AppDomain，均不崩溃）。
   - `new Composition()` → `Config.Load()` → `Composition.CreateAppHost()` → `AppHost.Run()` → 内存整理兜底
     `MemoryOptimizer.TrimMemory(true)`（见 [shell.md](shell.md)）；失败弹错误框并退出。
2. `Composition.ConfigureServices`（全部单例）：
   - B2/#75 装配前回填跨程序集环境参数缝：`AppDataPaths.IsDevInstance = DevInstance.IsActive`
     （S2 dev 目录分支）——Core 不反向引用宿主（见 [layering.md](layering.md) 程序集层）；
     S1 .lnk 图标提取的解析契约自 ADR-0019/#87 起经 DI 注入的 `IShortcutTargetResolver`
     （ProgramsModuleRegistrar 注册 M3 实现），不再静态回填。
   - 基础设施：`JsonConfigService`（具体类，配置路径经 Core `AppDataPaths.GetAppDataFolder()` 构造）+
     `IConfigService` 别名、`IMessenger` = `WeakReferenceMessenger.Default`、
     `NavigationStore` + `INavigationExecutor`→`NavigationExecutor`（导航运行时主体随
     ADR-0021/#92 归 Host，目录执行缝为 Host 内部件）。B7/#80 起 M4 的
     `ThemeService`（具体类）+ `IThemeService` 别名注册下放 `ThemeModuleRegistrar.RegisterServices`
     （StarPie.Theme），组合根不再直接登记主题服务；B8/#81 起 M2 的轮盘工厂
     （`IWheelFactory` → `WheelFactory`）与轮盘外观设置子 VM 注册下放
     `WheelModuleRegistrar.RegisterServices`（StarPie.Wheel，D5——工厂随 M2 收编、接口留 M2 侧，
     M1 手势侧只经接口消费），组合根不再直接登记轮盘工厂。
   - ADR-0019/#87 + ADR-0020/#88 + ADR-0023/#96 注：`ProgramsModuleRegistrar.RegisterServices`
     在组合根调用——注册 `IShortcutTargetResolver→ShortcutResolver` 与
     `IProgramScanner→ProgramScanner`（契约随实现方驻 `StarPie.Programs.Contracts`，M3 →
     Programs.Contracts + Icons.Contracts 单向，#96 起不再引用 Core；S21 归零——组合根删除
     `() => ProgramScanner.ScanInstalledPrograms(...)` 委托行）。
   - ADR-0023/#95 注：S1 图标资产实例服务自 #95 起由 `IconsModuleRegistrar.RegisterServices`
     （StarPie.Icons）注册（`IconAssetService` 构造惰性解析 Programs.Contracts 的
     `IShortcutTargetResolver`（#96 起），目录默认 Core `AppDataPaths.GetAppDataFolder`）——
     组合根不再直接登记 `IIconAssetService→IconAssetService`，S1/.lnk 不再经静态回填缝接线。
   - B3/#76（导航自治）+ B6/#79（M5 拆集）+ B9/#82（M1 拆集）：`NavigationCatalog` 由
     `StarPie.Gestures` 的 `GesturesModuleRegistrar.RegisterNavigation`、`StarPie.Shell` 的
     `ShellModuleRegistrar.RegisterNavigation` 与 exe 内 `HostModuleRegistrar` 按固定顺序装配并
     `Validate()` 后单例注册——导航装配/解析清单不再硬编码页面类型（运行时类型与执行缝的
     注册见上段基础设施，ADR-0021/#92 起不再登记跨程序集缝）。
   - 服务：`DialogService`（T3c/#67：构造注入共享图标资产实例服务、.lnk 解析契约与程序扫描
     契约；ADR-0019/#87 + ADR-0020/#88 + ADR-0023/#95/#96 起扫描/.lnk 经 Programs.Contracts、
     图标经 Icons.Contracts 契约注入；程序扫描候选经 `IProgramScanner`（Programs.Contracts
     契约，M3 注册器提供实现）注入、组合根不再登记委托——`DialogService`
     与 `IDialogService` 的注册随 S6 实现下放 `DialogsModuleRegistrar.RegisterServices`
     （StarPie.Dialogs，B11/#88）；对话框服务另注入共享图标资产实例服务与解析契约，
     供图标/程序选择器使用）、`ISaveDebouncer`、`SettingsSaveOrchestrator`。（M1 手势管线 `MouseHook`/`IActionExecutorService`/
     `IWindowContext`/`GestureEngine`/`GestureController` 的注册已随 B9/#82 由
     `GesturesModuleRegistrar.RegisterServices` 下放 `StarPie.Gestures`，组合根不再直接登记；
     `IWheelFactory` 的注册见 WheelModuleRegistrar 注。）
   - 页面 VM 工厂注册（单例）：M4 主题服务与界面主题设置子 VM 由
     `ThemeModuleRegistrar.RegisterServices` 下放 `StarPie.Theme`（B7/#80；模块无导航页，
     只下放 DI 注册）；M5 两页（`GeneralSettingsViewModel`/`AboutViewModel`）由
     `ShellModuleRegistrar.RegisterServices` 下放模块程序集（首个带 DI 的模块注册器样板，
     ADR-0016 决策 8，见 [assemblies.md](assemblies.md) §6）；M1 两页
     （`BehaviorSettingsViewModel`/`ProfileListViewModel`）由 `GesturesModuleRegistrar.RegisterServices`
     下放 `StarPie.Gestures`（B9/#82，最后一个业务模块程序集）；组合根仍注册
     `AppearanceSettingsViewModel`（#54/#56 起为薄聚合页壳，构造注入两个设置子 VM——
     `InterfaceThemeSettingsViewModel`（B7/#80 起由 ThemeModuleRegistrar 注册）与
     `WheelAppearanceSettingsViewModel`（B8/#81 起由 WheelModuleRegistrar 注册，随
     StarPie.Wheel 下放），均另行注册单例）、
     `MainViewModel`（B3/#76：目录驱动语义成立——导航项/选中态全部来自目录注册；
     ADR-0021/#92 起运行时主体已自 Core 迁回 Host `ViewModels/Navigation/`，命名空间不变；
     仍由组合根注册——页面 VM 的 DI 注册已全部下放所属模块注册器（B6/B7/B8/B9），仅 Host
     外观聚合页 VM 与导航 VM 留在组合根，目标态成立；
     `ProfileListViewModel` 另以 M1 只读 `IProfilePreviewSource` 注册别名的动作已随 B9/#82
     下放 GesturesModuleRegistrar（接口 B8/#81 起驻 Core，供轮盘外观设置子 VM 经接口消费））、
     `ShellViewModel`（B1/D3：Host 壳窗口壳层 VM——窗口标题/退出态/保存，主框架分区 DataContext 的壳区，
     见 [shell.md](shell.md)）。
   - B6/#79 注：`AppHostDelegates` 已上提 Core（`Services/AppHostDelegates.cs`）并以单例注册进容器，
     `AppHost` 构造后回填；`ShellModuleRegistrar` 的 VM 工厂经容器惰性解析该委托包，只依赖 Core。
   - B7/#80 注：`ThemeModuleRegistrar.RegisterServices` 在组合根先行调用（M4 → Core 单向），
     主题服务/主题设置子 VM 的工厂只解析 Core 契约；`ThemePaletteManager` 不经容器，
     由 `AppHost` 构造时直接 `new`（StarPie.Theme public，Host 装配面）。
   - B8/#81 注：`WheelModuleRegistrar.RegisterServices` 在组合根调用（M2 → Core + Theme），
     轮盘工厂 `IWheelFactory→WheelFactory` 与轮盘外观设置子 VM 的工厂只解析 Core 契约与
     M4 `IThemeService`（允许边）；RadialWindow 不经 Host 直接 new——由 WheelFactory 在
     StarPie.Wheel 内创建。
   - B9/#82 注：`GesturesModuleRegistrar.RegisterServices` 在组合根调用（M1 → Core + Wheel），
     手势管线/页面 VM/`IProfilePreviewSource` 别名的工厂只解析 Core 契约与 M2 侧接口
     （IWheelFactory/IWheelViewModel，允许边）；MouseHook dev 分支读 Core
     `AppDataPaths.IsDevInstance` 回填缝（组合根装配前已以 DevInstance.IsActive 回填，
     语义与迁移前一致），M1 不反向引用 Host。
   - `GeneralSettingsViewModel` 的托盘气泡/退出回调经 Core `AppHostDelegates` 转发注册，不直接引用宿主类。
   - **Views 不注册**（页面无参构造；`MainView` 由 `AppHost` 显式 `new`；对话框 Window 由
     `DialogService` 在 `StarPie.Dialogs` 内显式 `new`，B11/#88 起不经 Host）。
3. `Composition.CreateAppHost`（解析点仍集中在组合根，[ADR-0005](../adr/0005-di-container-for-navigation.md)/[0011](../adr/0011-composition-apphost-split.md)）：
   - 解析 `IMessenger`、`MouseHook`、`DialogService`、`IThemeService`、`SettingsSaveOrchestrator`、
     `INavigationExecutor`、`NavigationCatalog`、`GestureController`；
   - **页面 VM eager 解析清单目录化（B3/#76）**：遍历 `NavigationCatalog.Entries` 逐个解析注册的
     页面 VM（VM 构造即订阅导入广播/落盘消息与 I18n 事件，时机在 `Config.Load` 之后；eager 语义
     保留——新增页面注册进目录即自动纳入启动构造）；另解析 `MainViewModel`/`ShellViewModel` 与宿主
     直持的 `InterfaceThemeSettingsViewModel`/`GeneralSettingsViewModel`（B7/#80 起前者已由
     `ThemeModuleRegistrar` 注册、B6/#79 起后者已由 `ShellModuleRegistrar` 注册，组合根仅解析
     取回单例；初始主题与托盘/驻留气泡直调语义不变）；
   - 构造 `AppHost` 并回填 `AppHostDelegates`（托盘气泡、退出）。
4. `AppHost.Run`（顺序固定，[ADR-0003](../adr/0003-application-host-restructure.md)）：
   - `_mouseHook.Start()` → 订阅 `ILocalizationService.LanguageChanged`（重建语言字典、刷新托盘 tooltip）并
     首次应用语言字典（投影见 [localization.md](localization.md)）→ 注册托盘驻留气泡订阅 → 初始导航
     `INavigationExecutor.Navigate(NavigationSlot.Trigger)`（触发与场景，B3/#76 目录槽位）→
     `new MainView(...)` + 应用初始界面主题
      （`MainView.ApplyAppTheme`，见 [interface-theme.md](interface-theme.md)）→
      `_dialogService.SetOwner(_mainView)`（StarPie.Dialogs public 装配面，ADR-0020/#88）
     → 创建 `TrayIconManager`（见 [shell.md](shell.md)）→ `_mainView.Show()`。
5. 退出：托盘退出 → `AppHost.ExitApplication`：冲刷挂起保存 → dispose 托盘 → `ShellViewModel.IsExiting = true`
   → `Application.Shutdown()`。`App.OnExit`：`Config.Save()` 兜底 → `AppHost.Dispose()`（退订语言服务、托盘
   dispose、`_mouseHook.Stop()`、`MainViewModel.Dispose()`、`ShellViewModel.Dispose()`）→ `Composition.Dispose()`
   （容器 dispose）→ 释放互斥体。
6. 设置窗口隐藏（关窗/`MinimizedToTray` 语义）：`MainView.IsVisibleChanged`（非退出态）→ 冲刷保存 → 内存整理
   `MemoryOptimizer.TrimMemory()`（见 [shell.md](shell.md)）→ 发 `MinimizedToTrayMessage` → `AppHost` 直调
   `GeneralSettingsViewModel.NotifyMinimizedToTray()`。

## 宿主委托包

`AppHostDelegates`（B6/#79 起为 Core 公开契约，`StarPie.Core/Services/AppHostDelegates.cs`；原定义于
`Composition.cs` 的 internal 类）承载页面 VM 注册所需的宿主回调：`ShowTrayBalloonTip`、`ExitApplication`。
组合根把单例实例注册进容器，`ShellModuleRegistrar` 装配 `GeneralSettingsViewModel` 时持稳定转发委托，
`AppHost` 构造后回填实现（`_hostDelegates.ShowTrayBalloonTip/ExitApplication = …`）；VM 不反向依赖宿主类。

## 扩展点

- 新服务/新页面 VM：B6/#79 起 M5 新服务/页面在 `ShellModuleRegistrar`、B9/#82 起 M1 新服务/
  页面在 `GesturesModuleRegistrar`（模块内注册器）登记；仅 Host 外观聚合页 VM 在
  `Composition.ConfigureServices` 注册（导航项与页面模板一律经所属模块注册器 + 模块模板字典，
  见 [navigation.md](navigation.md)/[naming.md](naming.md)）。
- 新托盘入口：在 `AppHost.BuildTrayMenuEntries` 登记（托盘职责见 [shell.md](shell.md)）。
- 新增“启动/退出/隐藏”副作用：优先以委托注入页面 VM，不新增服务定位器；宿主编排改 `AppHost`，不改 `Composition`。

## 参见 ADR

[0002](../adr/0002-manual-composition-root.md)（手动组合根）、[0003](../adr/0003-application-host-restructure.md)（宿主重构）、[0005](../adr/0005-di-container-for-navigation.md)（容器导航）、[0011](../adr/0011-composition-apphost-split.md)（组合根与 AppHost 拆分）、[0015](../adr/0015-module-map-and-ownership.md)（12 模块地图：H1/R2/D4）。
