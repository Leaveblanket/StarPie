# 模块：导航

> 本文是 [docs/architecture.md](../architecture.md) 的拆分文档；新增/修改设置页导航时读本篇。

## 职责

设置控制台页面切换；页面状态常驻、View 按导航重建。

## 组成文件

共享内核（`StarPie.Core/`，B2/#75 起；ADR-0021/#92 起仅留目录/槽位契约）：

- `Services/Navigation/NavigationCatalog.cs`：`NavigationCatalog`/`NavigationSlot`/`NavigationSlots`/
  `NavigationPageRegistration`（全局槽位表 0–4、`NavTab0..4` 正典与缺失/重复/未知槽位收口测试，
  见 [assemblies.md](assemblies.md) §5.2）——跨模块注册契约（模块注册器写、控制台读），
  属共享内核"全局机制"，不受运行时归属影响。

宿主（`WinPieGestures/`，导航运行时主体 ADR-0021/#92 自 Core 迁入，命名空间不变）：

- `Services/Navigation/`：`NavigationStore`（当前页状态单一根源）、`NavigationExecutor`（含
  `INavigationExecutor`，目录驱动执行入口——按槽位取目录注册项并惰性解析页面 VM；
  接口随实现整体归 Host，为宿主内部件而非跨程序集解析缝，见 [seams.md](seams.md)；
  第二消费方出现时按 ADR-0023 契约归属判据裁决落点——属全局机制入内核、属某模块出口契约
  下沉该模块 Contracts（如 S6 先例：`IDialogService` 随实现方入 Dialogs.Contracts，#96））。
- `ViewModels/Navigation/`：`NavigationItemViewModel`；`MainViewModel`（B3/#76 目录驱动：
  导航项顺序/标识/标题键/图标/目标类型全部来自 `NavigationCatalog`，无页面 VM 硬编码；
  壳层职责已拆至同目录族的 `ShellViewModel`，见 [shell.md](shell.md)）。
- `Modules/`（B3/#76 单程序集内先行；M5 已随 B6/#79 迁出、M1 已随 B9/#82 迁出）：exe 内
  Host 临时注册器 `HostModuleRegistrar`（含 `RegisterNavigation(NavigationCatalog)`）与页面模板
  字典 `HostPageTemplates.xaml`（App 级每模块一次静态合并，目标态 Host 外观聚合页，
  见 [assemblies.md](assemblies.md) §5.1/§6）。
- `Views/Navigation/MainView.xaml`（R4/ADR-0016：Host 壳窗口（H1）文件；B3/#76 起**不再含页面
  DataTemplate 映射**——纯壳；分区 DataContext 与 `MainView.xaml.cs` 壳层 code-behind 见
  [shell.md](shell.md)）。

M5 模块程序集（`StarPie.Shell/`，B6/#79 起）：

- `Modules/ShellModuleRegistrar.cs`（正式模块注册器：`RegisterNavigation(NavigationCatalog)` +
  `RegisterServices(IServiceCollection)`，页面 VM 的 DI 注册随 M5 下放）与 `Modules/ShellPageTemplates.xaml`
  （页面模板字典；Host App.xaml 经跨程序集 pack URI 单点合并，见 [assemblies.md](assemblies.md) §5.1/§6）。

M1 模块程序集（`StarPie.Gestures/`，B9/#82 起）：

- `Modules/GesturesModuleRegistrar.cs`（正式模块注册器：`RegisterNavigation(NavigationCatalog)` +
  `RegisterServices(IServiceCollection)`，手势管线与页面 VM 的 DI 注册随 M1 下放，含
  `IProfilePreviewSource` 别名）与 `Modules/GesturesPageTemplates.xaml`（页面模板字典；Host
  App.xaml 经跨程序集 pack URI 单点合并，见 [assemblies.md](assemblies.md) §5.1/§6）。

## 关键流程

1. `Composition` 装配目录：构造时依次调 `GesturesModuleRegistrar`/`ShellModuleRegistrar`/
   `HostModuleRegistrar.RegisterNavigation(catalog)` 并 `catalog.Validate()`（五槽收口），目录单例注册；
   M5/M1 页面 VM 与手势管线的 DI 注册已分别由 `ShellModuleRegistrar.RegisterServices`（B6/#79，
   含宿主回调经 Core `AppHostDelegates` 的接线）与 `GesturesModuleRegistrar.RegisterServices`
   （B9/#82，含 `IProfilePreviewSource` 别名下放）下放模块程序集；导航运行时（`NavigationStore`/
   `NavigationExecutor`/`MainViewModel`）与 Host 外观聚合页 VM 由 `Composition.ConfigureServices`
   注册——前者为 Host 内部件（ADR-0021/#92），后者目标态 Host 页。
2. `MainViewModel`（Host，B3/#76 目录驱动；ADR-0021/#92 起运行时归 Host）按 `catalog.Entries`
   构造 `NavigationItemViewModel` 列表：`AutomationId`/`TitleKey`/`IconData`/`TargetViewModelType`
   均来自目录注册，导航 `Action` = `INavigationExecutor.Navigate(槽位)`。
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

as-built（B6/#79 起；B9/#82 起 M1 亦跨程序集自治）：
- M5（`StarPie.Shell`）/M1（`StarPie.Gestures`）：新增页面只动模块内部——所属
  Shell/GesturesModuleRegistrar 的 RegisterNavigation/RegisterServices + Shell/GesturesPageTemplates.xaml
  + 页面 VM/View 文件，**不碰 Host**（跨程序集形态成立）；
- Host（外观聚合页，目标态留 Host）：仍由 exe 内 HostModuleRegistrar + HostPageTemplates.xaml
  登记，页面 VM 的 DI 注册在 `Composition.ConfigureServices`。

**槽位容量（Q4=a，ADR-0020/#88 登记）**：槽位表 = Core `NavigationSlot` 固定 0–4
（`NavigationSlots.All` + e2e `NavTab0..4`），是产品侧边栏顺序的唯一正典；**产品页面数封顶
5**。新增第 6 页起需改 Core 枚举与收口测试（可能波及 e2e AutomationId），属放行共享面而非
纯模块内部——此约束被有意接受；若未来出现新模块页面需求，再议槽位表可扩展化（字符串槽位/
目录驱动注册，会破坏 NavTab0..4 稳定性，需先写 ADR）。

## 参见 ADR

[0005](../adr/0005-di-container-for-navigation.md)（DI 导航）、[0016](../adr/0016-assembly-split-target-and-roadmap.md)（导航自治注册）、[0021](../adr/0021-navigation-runtime-to-host.md)（导航运行时归 Host）。
