# 模块：导航

> 本文是 [docs/architecture.md](../architecture.md) 的拆分文档；新增/修改设置页导航时读本篇。

## 职责

设置控制台页面切换；页面状态常驻、View 按导航重建。

## 组成文件

SDK（`StarPie.Sdk/`；P1.3/#112 收口，仅目录/槽位契约）：

- `Services/Navigation/NavigationCatalog.cs`：`NavigationCatalog`/`NavigationSlot`/`NavigationSlots`/
  `NavigationPageRegistration`（全局槽位表 0–3、`NavPage0..3` 正典与缺失/重复/未知槽位收口测试，
  见 [assemblies.md](assemblies.md) §5.2）——跨模块注册契约（贡献者写、控制台读），
  属共享契约面的"全局机制"，不受运行时归属影响。

宿主 Ui 集（`StarPie.Ui/`，导航运行时主体）：

- `Services/Navigation/`：`NavigationStore`（当前页状态单一根源）、`NavigationExecutor`（含
  `INavigationExecutor`，目录驱动执行入口——按槽位取目录注册项并惰性解析页面 VM；
  接口随实现整体归 Host，为宿主内部件而非跨程序集解析缝，见 [seams.md](seams.md)；
  第二消费方出现时按 ADR-0023 契约归属判据裁决落点——属全局机制入内核、属某模块出口契约
  下沉该模块 Contracts（如 S6 先例：`IDialogService` 随实现方独立成集、P1.3/#112 收口入 `StarPie.Sdk`））。
- `ViewModels/Navigation/`：`NavigationItemViewModel`；`MainViewModel`（目录驱动：导航项顺序/
  标识/标题键/图标/目标类型全部来自 `NavigationCatalog`，无页面 VM 硬编码；壳层职责已拆至
  同目录族的 `ShellViewModel`，见 [shell.md](shell.md)）。
- `Modules/`：统一注册管线（`ICompositionContributor` + `BuiltInContributors` 有序清单）与
  Host 外观聚合页贡献者 `HostPageContributor`（含
  `RegisterNavigation(NavigationCatalog)`）及页面模板字典 `HostPageTemplates.xaml`（App 级每
  模块一次静态合并；M5/M1 的贡献者与模板字典在 Ui 集，
  见 [assemblies.md](assemblies.md) §5.1/§6）。
- `Views/Navigation/MainView.xaml`（R4/ADR-0016：Host 壳窗口（H1）文件；**不含页面
  DataTemplate 映射**——纯壳；分区 DataContext 与 `MainView.xaml.cs` 壳层 code-behind 见
  [shell.md](shell.md)）。

M5（P1.10/#119 起驻 `StarPie.Ui/`）：

- `Modules/ShellContributor.cs`（M5 贡献者：`RegisterNavigation(NavigationCatalog)` +
  `RegisterServices(IServiceCollection)`，页面 VM 的 DI 注册随 M5 下放）与 `Modules/ShellPageTemplates.xaml`
  （页面模板字典；Ui 集内本地单点合并，见 [assemblies.md](assemblies.md) §5.1/§6）。

M1（P1.6/#115 起驻 `StarPie.Ui/`）：

- `Modules/GesturesContributor.cs`（M1 贡献者：`RegisterNavigation(NavigationCatalog)` +
  `RegisterServices(IServiceCollection)`，手势管线与页面 VM 的 DI 注册随 M1 下放，含
  `IProfilePreviewSource` 别名）与 `Modules/GesturesPageTemplates.xaml`（页面模板字典；Host
  App.xaml 本地单点合并，见 [assemblies.md](assemblies.md) §5.1/§6）。

## 关键流程

1. `Composition` 装配目录：构造期遍历 `BuiltInContributors` 有序清单调各贡献者的
   `RegisterNavigation(catalog)`（M1 槽位 0/2、Host 页槽位 1、M5 槽位 3）并 `catalog.Validate()`（四槽收口），
   目录单例注册；随后同一清单的 `RegisterServices` 登记 M5/M1 页面 VM 与手势管线
   （`ShellContributor`/`GesturesContributor`，含宿主回调经 `StarPie.Sdk` 的 `AppHostDelegates`
   接线、`IProfilePreviewSource` 别名）；导航运行时（`NavigationStore`/`NavigationExecutor`/
   `MainViewModel`）与 Host 外观聚合页 VM 由 `HostCoreContributor`/`HostPageContributor` 登记——
   前者为 Host 内部件，后者为 Host 页。
2. `MainViewModel`（Host，目录驱动；运行时归 Host）按 `catalog.Entries`
   构造 `NavigationItemViewModel` 列表：`AutomationId`/`TitleKey`/`IconData`/`TargetViewModelType`
   均来自目录注册，导航 `Action` = `INavigationExecutor.Navigate(槽位)`。
3. 导航项"选中态置真"即导航——点击（RadioButton `Command`）与 UIA `SelectionItem.Select` 是等价入口
   （后者是 e2e 静默导航与无障碍客户端可用路径，见 [ADR-0031](../adr/0031-e2e-silent-background-run.md)）→
   `INavigationExecutor.Navigate(slot)` → `NavigationCatalog.GetEntry(slot)` → 容器解析
   页面 VM（单例 → 状态常驻）→ 更新 `NavigationStore.CurrentViewModel`。`MainViewModel` 订阅 store
   变更同步各导航项选中态（回灌的选中态指向已停驻页面，短路不自我导航），并随 I18n 广播刷新标题。
4. `MainView` 分区 DataContext（D3）：导航区（侧栏 + 页面 ContentControl）绑 `MainViewModel`，壳区
   （窗口标题/底部操作区）绑 `ShellViewModel`（见 [shell.md](shell.md)）；页面 `ContentControl`
   `Content="{Binding CurrentViewModel}"`，页面 View 由 App 级模块模板字典中
   `DataTemplate DataType="{x:Type vm:Xxx}"` 映射（View 无参、按导航重建、不经容器）。
5. 初始导航/托盘直达 = `INavigationExecutor.Navigate(槽位)` +（托盘场景另加）
   `MainView.ShowAndActivate()`（AppHost，见 [host.md](host.md)）。

## 扩展点

新增页面 = 页面 VM + 所属贡献者 `RegisterNavigation` 一行（槽位/AutomationId/TitleKey/IconData）
+ 所属模块页面模板字典 `DataTemplate` 一行 + [naming.md](naming.md) 映射表登记；任何一步缺失都算
未完成。完整清单见 [extending.md](extending.md)（原型 B）。

as-built：
- M5/M1（`StarPie.Ui`）：新增页面只动模块内部——所属 Shell/GesturesContributor 的
  RegisterNavigation/RegisterServices + Shell/GesturesPageTemplates.xaml + 页面 VM/View 文件，
  **不碰 Host**；
- Host（外观聚合页，留 Host）：由 `HostPageContributor` + HostPageTemplates.xaml
  登记，页面 VM 的 DI 注册在同一贡献者内。

**槽位容量**：槽位表 = SDK `NavigationSlot` 固定 0–3（`NavigationSlots.All` + e2e
`NavPage0..3`），是产品侧边栏顺序的唯一正典；**产品页面数封顶 4**（槽位 4「关于与更新」
已随 #107 于 2026-09-10 下线移除，其余四页 AutomationId 零漂移）。新增第 5 页起需改 SDK
枚举与收口测试（可能波及 e2e AutomationId），属放行共享面而非纯模块内部——此约束被有意
接受；若未来出现新模块页面需求，再议槽位表可扩展化（字符串槽位/目录驱动
注册，会破坏 `NavPage0..3` 稳定性，需先写 ADR）。

## 参见 ADR

[0005](../adr/0005-di-container-for-navigation.md)（DI 导航）、[0016](../adr/0016-assembly-split-target-and-roadmap.md)（导航自治注册）。
