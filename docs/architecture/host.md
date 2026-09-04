# 模块：宿主与组合根（App + AppHost + Composition）

> 本文是 [docs/architecture.md](../architecture.md) 的拆分文档；涉及启动顺序、DI 注册、退出/隐藏流程时读本篇。

## 职责

- `App`：进程级生命周期——单实例、全局异常、启动/退出兜底（不做业务）。
- `Composition`：唯一 DI 组合根——`ServiceCollection` 注册、`BuildServiceProvider`、解析宿主依赖并创建 `AppHost`；不持有托盘/主窗口/语言字典等宿主状态。
- `AppHost`：宿主启动/退出编排——鼠标钩子启停、语言资源字典、托盘创建与菜单、主框架创建、隐藏到托盘与真退出协调（[ADR-0011](../adr/0011-composition-apphost-split.md)）。

## 组成文件

`App.xaml(.cs)`、`Composition.cs`、`AppHost.cs`、`Services/Shell/DevInstance.cs`、`Services/Shell/MemoryOptimizer.cs`（后者见 [shell.md](shell.md)）。

## 生命周期与关键流程

1. `App.OnStartup`：
   - 单实例互斥（`DevInstance.MutexName`；`--dev` 与正式版并存、同类互斥）；命令行含 `--allow-multiple`/`--test-instance` 时跳过互斥（测试运行器用）。
   - 非首实例：查找既有设置窗口并置前（`FindWindow`/`ShowWindow`/`SetForegroundWindow`），然后 `Shutdown(0)`。
   - 注册全局异常处理器（Dispatcher + AppDomain，均不崩溃）。
   - `new Composition()` → `Config.Load()` → `Composition.CreateAppHost()` → `AppHost.Run()` → `MemoryOptimizer.TrimMemory(true)`；失败弹错误框并退出。
2. `Composition.ConfigureServices`（全部单例）：
   - 基础设施：`JsonConfigService`（具体类）+ `IConfigService` 别名、`ThemeService`（具体类）+ `IThemeService` 别名、`IMessenger` = `WeakReferenceMessenger.Default`、`NavigationStore`、开放泛型 `INavigationService<>` → `NavigationService<>`。
   - 服务：`MouseHook`、`IActionExecutorService`、`IWindowContext`、`IWheelFactory`、`GestureEngine`、`DialogService`(+`IDialogService`)、`GestureController`、`ISaveDebouncer`、`SettingsSaveOrchestrator`。
   - 页面 VM 工厂注册（单例）：`BehaviorSettingsViewModel`、`ProfileListViewModel`、
     `AppearanceSettingsViewModel`（#54 起构造注入界面主题子 VM `InterfaceThemeSettingsViewModel`）、
     `GeneralSettingsViewModel`、`AboutViewModel`、`MainViewModel`。
   - `GeneralSettingsViewModel` 的托盘气泡/退出回调经 `AppHostDelegates` 转发注册，不直接引用宿主类。
   - **Views 不注册**（页面无参构造；`MainView`/对话框 Window 由 `AppHost` 或 `DialogService` 显式 `new`）。
3. `Composition.CreateAppHost`（解析点仍集中在组合根，[ADR-0005](../adr/0005-di-container-for-navigation.md)/[0011](../adr/0011-composition-apphost-split.md)）：
   - 解析 `IMessenger`、`MouseHook`、`DialogService`、`IThemeService`、`SettingsSaveOrchestrator`、`GestureController`、五个类型化导航服务；
   - 解析全部页面 VM 与 `MainViewModel`（VM 构造即订阅导入广播/落盘消息，时机在 `Config.Load` 之后）；
   - 构造 `AppHost` 并回填 `AppHostDelegates`（托盘气泡、退出）。
4. `AppHost.Run`（顺序固定，[ADR-0003](../adr/0003-application-host-restructure.md)）：
   - `_mouseHook.Start()` → 订阅 `I18n.LanguageChanged`（语言字典重建、托盘 tooltip 刷新）并首次 `ApplyLanguageDictionary()` → 注册托盘驻留气泡订阅 → 初始导航 `BehaviorSettingsViewModel`（触发与场景）→ `new MainView(...)` + 应用外观主题 → `_dialogService.SetOwner(_mainView)` → 创建 `TrayIconManager` → `_mainView.Show()`。
5. 退出：托盘退出 → `AppHost.ExitApplication`：冲刷挂起保存 → dispose 托盘 → `MainViewModel.IsExiting = true` → `Application.Shutdown()`。`App.OnExit`：`Config.Save()` 兜底 → `AppHost.Dispose()`（退订 I18n、托盘 dispose、`_mouseHook.Stop()`、`MainViewModel.Dispose()`）→ `Composition.Dispose()`（容器 dispose）→ 释放互斥体。
6. 设置窗口隐藏（关窗/`MinimizedToTray` 语义）：`MainView.IsVisibleChanged`（非退出态）→ 冲刷保存 → `MemoryOptimizer.TrimMemory()` → 发 `MinimizedToTrayMessage` → `AppHost` 直调 `GeneralSettingsViewModel.NotifyMinimizedToTray()`。

## 宿主委托包

`AppHostDelegates`（定义于 `Composition.cs`）承载页面 VM 注册所需的宿主回调：`ShowTrayBalloonTip`、`ExitApplication`。`GeneralSettingsViewModel` 注册时持稳定转发委托，`AppHost` 构造后回填实现；VM 不反向依赖宿主类。

## 扩展点

- 新服务/新页面 VM：在 `Composition.ConfigureServices` 注册（服务清单见 [extending.md](extending.md)）。
- 新托盘入口：在 `AppHost.BuildTrayMenuEntries` 登记（托盘职责见 [shell.md](shell.md)）。
- 新增“启动/退出/隐藏”副作用：优先以委托注入页面 VM，不新增服务定位器；宿主编排改 `AppHost`，不改 `Composition`。

## 参见 ADR

[0002](../adr/0002-manual-composition-root.md)（手动组合根）、[0003](../adr/0003-application-host-restructure.md)（宿主重构）、[0005](../adr/0005-di-container-for-navigation.md)（容器导航）、[0011](../adr/0011-composition-apphost-split.md)（组合根与 AppHost 拆分）。
