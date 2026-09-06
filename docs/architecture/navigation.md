# 模块：导航

> 本文是 [docs/architecture.md](../architecture.md) 的拆分文档；新增/修改设置页导航时读本篇。

## 职责

设置控制台页面切换；页面状态常驻、View 按导航重建。

## 组成文件

共享内核（`StarPie.Core/`，B2/#75 起；导航 VM B3/#76 迁入）：

- `Services/Navigation/`：`NavigationStore`、`INavigationService<T>`/`NavigationService<T>`（类型化解析缝，
  保留注册与收口测试）、`NavigationCatalog`/`NavigationSlots`（全局槽位表 0–4、`NavTab0..4` 正典与
  缺失/重复/未知槽位收口测试，见 [assemblies.md](assemblies.md) §5.2）、`INavigationExecutor`/
  `NavigationExecutor`（B3/#76 目录执行缝——按槽位取目录注册项并惰性解析页面 VM）。
- `ViewModels/Navigation/`：`NavigationItemViewModel`；`MainViewModel`（B3/#76 迁入 Core 且目录驱动：
  导航项顺序/标识/标题键/图标/目标类型全部来自 `NavigationCatalog`，无页面 VM 硬编码；壳层职责已拆至
  Host `ShellViewModel`，见 [shell.md](shell.md)）。

M5 模块程序集（`StarPie.Shell/`，B6/#79 起）：

- `Modules/ShellModuleRegistrar.cs`（正式模块注册器：`RegisterNavigation(NavigationCatalog)` +
  `RegisterServices(IServiceCollection)`，页面 VM 的 DI 注册随 M5 下放）与 `Modules/ShellPageTemplates.xaml`
  （页面模板字典；Host App.xaml 经跨程序集 pack URI 单点合并，见 [assemblies.md](assemblies.md) §5.1/§6）。

宿主（`WinPieGestures/`）：

- `Modules/`（B3/#76 单程序集内先行；M5 部分已随 B6/#79 迁出，M1/Host 部分留 exe 至 B9）：exe 内
  M1/Host 临时注册器（`M1ModuleRegistrar`/`HostModuleRegistrar`，各含 `RegisterNavigation(NavigationCatalog)`）
  与页面模板字典（`M1PageTemplates.xaml`/`HostPageTemplates.xaml`，App 级每模块一次静态合并，
  见 [assemblies.md](assemblies.md) §5.1/§6）。
- `ViewModels/Navigation/`：`ShellViewModel`（Host 壳窗口壳层 VM，见 [shell.md](shell.md)）。
- `Views/Navigation/MainView.xaml`（R4/ADR-0016：Host 壳窗口（H1）文件；B3/#76 起**不再含页面
  DataTemplate 映射**——纯壳；分区 DataContext 与 `MainView.xaml.cs` 壳层 code-behind 见
  [shell.md](shell.md)）。

## 关键流程

1. `Composition` 装配目录：构造时依次调 `M1ModuleRegistrar`/`ShellModuleRegistrar`/
   `HostModuleRegistrar.RegisterNavigation(catalog)` 并 `catalog.Validate()`（五槽收口），目录单例注册；
   M5 页面 VM 的 DI 注册由 `ShellModuleRegistrar.RegisterServices` 下放模块程序集（B6/#79，含宿主
   回调经 Core `AppHostDelegates` 的接线）；M1/Host 页面 VM 在 B9 前仍集中 `Composition.ConfigureServices`。
2. `MainViewModel`（Core，B3/#76 目录驱动）按 `catalog.Entries` 构造 `NavigationItemViewModel` 列表：
   `AutomationId`/`TitleKey`/`IconData`/`TargetViewModelType` 均来自目录注册，导航 `Action` =
   `INavigationExecutor.Navigate(槽位)`。
3. 点击导航项 → `INavigationExecutor.Navigate(slot)` → `NavigationCatalog.GetEntry(slot)` → 容器解析
   页面 VM（单例 → 状态常驻）→ 更新 `NavigationStore.CurrentViewModel`。`MainViewModel` 订阅 store
   变更同步各导航项选中态，并随 I18n 广播刷新标题。
4. `MainView` 分区 DataContext（B1/D3）：导航区（侧栏 + 页面 ContentControl）绑 `MainViewModel`，壳区
   （窗口标题/底部操作区）绑 `ShellViewModel`（见 [shell.md](shell.md)）；页面 `ContentControl`
   `Content="{Binding CurrentViewModel}"`，页面 View 由 App 级模块模板字典中
   `DataTemplate DataType="{x:Type vm:Xxx}"` 映射（View 无参、按导航重建、不经容器）。
5. 初始导航/托盘直达 = `INavigationExecutor.Navigate(槽位)` +（托盘场景另加）
   `MainView.ShowAndActivate()`（AppHost，见 [host.md](host.md)）。

## 扩展点

新增页面 = 页面 VM + 所属模块注册器 `RegisterNavigation` 一行（槽位/AutomationId/TitleKey/IconData）
+ 所属模块页面模板字典 `DataTemplate` 一行 + [naming.md](naming.md) 映射表登记；任何一步缺失都算
未完成。完整清单见 [extending.md](extending.md)（原型 B）。

as-built（B6/#79 起）：
- M5（`StarPie.Shell`）：新增页面只动模块内部——`ShellModuleRegistrar` 的 RegisterNavigation/
  RegisterServices + `ShellPageTemplates.xaml` + 页面 VM/View 文件，**不碰 Host**（跨程序集形态成立）；
- M1/Host：B9 前仍在 exe 内以临时注册器登记；页面 VM 的 DI 注册仍在 `Composition.ConfigureServices`
  加一行（M1 随 B9 下放）。

## 参见 ADR

[0005](../adr/0005-di-container-for-navigation.md)（DI 导航）、[0016](../adr/0016-assembly-split-target-and-roadmap.md)（导航自治注册）。
