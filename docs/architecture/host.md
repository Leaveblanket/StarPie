# 模块：宿主与组合根（App + Composition）

> 本文是 [docs/architecture.md](../architecture.md) 的拆分文档；涉及启动顺序、DI 注册、退出/隐藏流程时读本篇。

## 职责

进程级生命周期编排与唯一依赖装配点。

## 组成文件

`App.xaml(.cs)`、`Composition.cs`、`Services/Shell/DevInstance.cs`、`Services/Shell/MemoryOptimizer.cs`（后者见 [shell.md](shell.md)）。

## 生命周期与关键流程

1. `App.OnStartup`：
   - 单实例互斥（`DevInstance.MutexName`；`--dev` 与正式版并存、同类互斥）；命令行含 `--allow-multiple`/`--test-instance` 时跳过互斥（测试运行器用）。
   - 非首实例：查找既有设置窗口并置前（`FindWindow`/`ShowWindow`/`SetForegroundWindow`），然后 `Shutdown(0)`。
   - 注册全局异常处理器（Dispatcher + AppDomain，均不崩溃）。
   - `new Composition()` → `Config.Load()` → `Composition.Run()` → `MemoryOptimizer.TrimMemory(true)`；失败弹错误框并退出。
2. `Composition.ConfigureServices`（全部单例）：
   - 基础设施：`JsonConfigService`（具体类）+ `IConfigService` 别名、`ThemeService`（具体类）+ `IThemeService` 别名、`IMessenger` = `WeakReferenceMessenger.Default`、`NavigationStore`、开放泛型 `INavigationService<>` → `NavigationService<>`。
   - 服务：`MouseHook`、`IActionExecutorService`、`IWindowContext`、`IWheelFactory`、`GestureEngine`、`DialogService`(+`IDialogService`)、`GestureController`、`ISaveDebouncer`、`SettingsSaveOrchestrator`。
   - 页面 VM 工厂注册（单例）：`BehaviorSettingsViewModel`、`ProfileListViewModel`、`AppearanceSettingsViewModel`、`GeneralSettingsViewModel`（闭包委托：托盘气泡/退出/自启/导入导出/提权检测）、`AboutViewModel`、`MainViewModel`。
   - **Views 不注册**（页面无参构造；`MainView`/对话框 Window 由组合根或 `DialogService` 显式 `new`）。
3. `Composition.Run`（顺序固定，[ADR-0003](../adr/0003-application-host-restructure.md)）：
   - `_mouseHook.Start()` → 订阅 `I18n.LanguageChanged`（语言字典重建、托盘 tooltip 刷新）并首次 `ApplyLanguageDictionary()` → 解析全部页面 VM（导入广播/保存消息订阅建立）→ 注册托盘驻留气泡订阅 → 解析 `MainViewModel` → 初始导航 `BehaviorSettingsViewModel`（触发与场景）→ `new MainView(...)` + 应用外观主题 → `_dialogService.SetOwner(_mainView)` → 创建 `TrayIconManager` → `_mainView.Show()`。
4. 退出：托盘退出 → `ExitApplication`：冲刷挂起保存 → dispose 托盘 → `MainViewModel.IsExiting = true` → `Application.Shutdown()`。`App.OnExit`：`Config.Save()` 兜底 → `Composition.Dispose()`（退订 I18n、托盘 dispose、`_mouseHook.Stop()`、`MainViewModel.Dispose()`、容器 dispose）→ 释放互斥体。
5. 设置窗口隐藏（关窗/`MinimizedToTray` 语义）：`MainView.IsVisibleChanged`（非退出态）→ 冲刷保存 → `MemoryOptimizer.TrimMemory()` → 发 `MinimizedToTrayMessage` → 组合根直调 `GeneralSettingsViewModel.NotifyMinimizedToTray()`。

## 扩展点

- 新服务/新页面 VM：在 `Composition.ConfigureServices` 注册（服务清单见 [extending.md](extending.md)）。
- 新托盘入口：在 `Composition.BuildTrayMenuEntries` 登记（托盘职责见 [shell.md](shell.md)）。
- 新增“启动/退出/隐藏”副作用：优先以委托注入页面 VM，不新增服务定位器。

## 参见 ADR

[0002](../adr/0002-manual-composition-root.md)（手动组合根）、[0003](../adr/0003-application-host-restructure.md)（宿主重构）、[0005](../adr/0005-di-container-for-navigation.md)（容器导航）。
