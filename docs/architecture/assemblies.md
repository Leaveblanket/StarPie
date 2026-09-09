# 程序集地图与程序集化收尾（目标态 = as-built）

> 本文记录程序集化目标态（[ADR-0016](../adr/0016-assembly-split-target-and-roadmap.md) +
> [ADR-0020](../adr/0020-dialogs-assembly-and-m3-scanner-contract.md)）的地图视图：目标程序集
> 划分、程序集级依赖规则、导航槽位表、注册/可见性契约与批次历史（B0–B10 与 B11/#88 已全部落地）。
>
> **8 程序集目标态（ADR-0016 的 7 程序集 + ADR-0020/#88 新增 StarPie.Dialogs）已全部落地**：
> 程序集化批次（B0–B10 与 B11/#88）无进行中批次；代码现状以 §9 与各叶子
> （`docs/architecture/*.md`）为准，冲突时叶子优先。概念模块地图与归属裁定见
> [modules.md](modules.md)（ADR-0015）。

## 1. 何时读本文

| 想做什么 | 读哪里 |
|---|---|
| 某个类型/文件属于哪个程序集（目标态） | 本文 §2–§4 + modules.md §3 |
| 程序集间依赖是否允许 | 本文 §3 |
| 导航槽位 / 侧边栏顺序正典 | 本文 §5 |
| 新增页面/服务在目标态下要动哪些 | 本文 §5–§6（+ [extending.md](extending.md) 原型 A–F） |
| 程序集化批次历史与现状 | 本文 §8–§9 |
| 为什么这样定 | [ADR-0016](../adr/0016-assembly-split-target-and-roadmap.md) |

## 2. 目标程序集地图（8 程序集，ADR-0020/#88 起）

| 程序集 | 形态 | 承载（目标态） |
|---|---|---|
| `StarPie`（项目 `WinPieGestures`） | WinExe | H1 宿主与组合根（App/AppHost/Composition/DevInstance）；Host 壳窗口（`MainView` 全文件 + `ShellViewModel`）；导航运行时主体（`Services/Navigation/`：NavigationStore/NavigationExecutor（含 INavigationExecutor）；`ViewModels/Navigation/`：MainViewModel/NavigationItemViewModel——ADR-0021/#92 迁入，命名空间不变）；Appearance 聚合页；仅保留 DialogService.SetOwner 回填等宿主装配面（S6 实现已随 B11/#88 迁出） |
| `StarPie.Core` | WPF 类库 | S1–S6 共享内核合并：Models（轮盘配色 WheelPalette* 已随 B8/#81 收编 M2、CustomColorPreset 仍居此——配置 POCO 引用）；S2 Configuration；S3 Localization（含 `Strings*.resx` 与生成器）；S4 Messages；S1 Icons；S6 对话框契约（接口/结果 record）；S5 导航目录/槽位契约（`NavigationCatalog`/`NavigationSlot`/`NavigationSlots`/`NavigationPageRegistration`——纯契约共享模块，ADR-0021/#92 起运行时主体不居 Core）；共享 UI 基建（Views/Converters 通用转换器、Views/Controls/HotkeyRecorderBox、Views/Styles/ModernControls.xaml，B5/#78 已落地；共享页面基类 `Views/Pages/SettingsPageBase`，B6/#79 迁入；宿主回调契约 `Services/AppHostDelegates`，B6/#79 上提；跨 M 预览 Profile 只读契约 `ViewModels/Pages/IProfilePreviewSource`，B8/#81 上提） |
| `StarPie.Dialogs` | 类库 | S6 对话框实现：DialogService、五对对话框 VM/Window、SpectrumCanvasBehavior（**B11/#88 已落地**；契约 IDialogService 与结果 record 留 Core；Dialogs → Core 单向 + Theme 允许边） |
| `StarPie.Gestures` | 类库 | M1 手势与动作：Services/Gestures、Services/Actions、Trigger/Gestures 设置页（**B9/#82 已落地**；Gestures → Core 单向 + Wheel 允许边） |
| `StarPie.Wheel` | 类库 | M2 轮盘与渲染：ViewModels/Wheel、RadialWindow、Renderers、WheelPalette*、WheelGeometry、WheelFactory（**B8/#81 已落地**；含 D5 工厂收编；Wheel → Core 单向 + M4 允许边） |
| `StarPie.Programs` | 类库 | M3 程序扫描与目录：ProgramScanner/ProgramCatalog/ShortcutResolver + ProgramsModuleRegistrar（B4/#77 已落地；ADR-0019/#87 起 M3 → Core 单向，ShortcutResolver 实例实现 Core 契约） |
| `StarPie.Theme` | 类库 | M4 界面主题：ThemeService/Themes XAML/InterfaceThemeSettingsViewModel/ThemePaletteManager（裁决 public——Host AppHost 装配面）（**B7/#80 已落地**；Theme → Core 单向） |
| `StarPie.Shell` | 类库 | M5 壳层服务与设置面：TrayIconManager/AutostartRegistry/MemoryOptimizer/General+About 设置页（**B6/#79 已落地**；`MainView` 壳窗口与 `ShellViewModel` **不**随 M5，留 Host） |

## 3. 程序集级依赖规则

```text
StarPie (Host/exe) ──→ StarPie.Core
     │──→ StarPie.Dialogs ──→ StarPie.Theme ──→ StarPie.Core   （B11/#88 已落地；S6→M4 允许边）
     │──→ StarPie.Gestures ──→ StarPie.Wheel ──→ StarPie.Theme   （B9/#82 已落地；M1→M2 边）
     │──→ StarPie.Programs ──→ StarPie.Core   （ADR-0019/#87 已落地；M3→Core 单向）
     │──→ StarPie.Shell ──→ StarPie.Core
     │──→ StarPie.Theme ──→ StarPie.Core   （B7/#80 已落地）
     └────────────────────────────────────────→ StarPie.Core
```

- `M* → Core` 单向（**Programs 例外已清除，ADR-0019/#87**：M3 自 B4/#77 起为零共享内核依赖、
  扫描图标补全经组合根注入委托；现改为单向 Core——扫描图标补全与 .lnk 解析经 Core 契约
  `IIconAssetService`/`IShortcutTargetResolver` 注入，`ShortcutResolver` 实例实现契约、
  注册器 `ProgramsModuleRegistrar` 下放注册）；**Theme 已落地（B7/#80）**：M4 主题服务与主题设置子 VM 只依赖
  Core 契约（S2/S3/S4），调色板换入经本集 public `ThemePaletteManager` 由 Host AppHost 装配面编排；
  **Shell 已落地（B6/#79）**：M5 托盘深色配色经组合根注入的 `Func<bool>` 探针
  （不引用 IThemeService/ThemeService）、自启 dev 分支改读 Core `AppDataPaths.IsDevInstance`
  回填缝——Shell 只依赖 Core 契约）；**Gestures 已落地（B9/#82）**：M1 手势管线/动作执行/触发+
  手势设置页只消费 Core 契约与 M2 侧接口（`IWheelFactory`/`IWheelViewModel`，允许边），
  MouseHook dev 分支同款改读 Core `AppDataPaths.IsDevInstance` 回填缝——M1 不反向引用
  Host/Shell/Theme/Programs；`Host → 全部`（仅调用各模块注册器与装配宿主对象，不引用模块内部）。
- 允许的模块间单向边仅：**M1→M2**（`IWheelFactory`，接口在 M2 侧）、**M2→M4**（`IThemeService`
  消费）、**Dialogs→M4**（`IThemeService`，窗口主题应用，ADR-0020/#88）。其余跨模块依赖一律经
  Core 契约。
- S6 对话框契约在 Core（`IDialogService` + 结果 record），实现（DialogService/五对对话框
  VM/Window/SpectrumCanvasBehavior）驻 `StarPie.Dialogs`（B11/#88，ADR-0020）；M 页面经 Core
  的 `IDialogService` 调用，实现由 DialogsModuleRegistrar 注册，Host 组合根仅保留
  `SetOwner(MainView)` 装配面。
- 共享放行清单（config 模型字段、i18n 键、消息/通知类型、共享 UI 基建、图标资产）维持 modules.md §2.3，不视为跨模块违规。

## 4. 导航与“壳”的分层语义（防混淆）

程序集化后导航契约与“壳”相关概念分四层，术语别混用：

| 层 | 归属 | 程序集 | 内容 |
|---|---|---|---|
| 导航目录/槽位契约 | S5（纯契约共享模块，ADR-0021/#92） | Core | NavigationCatalog、NavigationSlot/NavigationSlots、NavigationPageRegistration |
| 导航运行时/状态 | H1（宿主壳，与 R4/D3 同判据——单一消费方在 Host） | StarPie（exe） | NavigationStore、NavigationExecutor（含 INavigationExecutor）、MainViewModel、NavigationItemViewModel（ADR-0021/#92 迁入，命名空间不变） |
| 壳层服务与系统集成 | M5 | StarPie.Shell | 托盘、自启、内存、Advanced/About 设置面（B6/#79 已落地） |
| Host 壳窗口 | H1（宿主壳） | StarPie（exe） | MainView 全文件、ShellViewModel、App/AppHost/Composition（导航 VM 与主框架同窗，物理同居 Host） |

`MainView.xaml.cs` 与 `ShellViewModel` **归 Host 壳窗口**（ADR-0016 决策 6/7），不再归 M5；M5 只拥有壳层服务与设置面。
导航运行时（含主框架导航区 VM `MainViewModel`）随壳窗口同判据归 Host（ADR-0021/#92，R9）。

## 5. 导航架构（目标态：模块自治注册）

### 5.1 机制

- Core 提供目录契约 `NavigationCatalog`：`RegisterPage<TViewModel>(槽位, automationId, titleKey, iconData, …)`（纯契约共享模块，ADR-0021/#92 起运行时不居 Core）。
- Host 提供导航执行入口（`NavigationExecutor`/`INavigationExecutor`）：随运行时归 H1 后为宿主内部件
  （ADR-0021/#92；不再是跨程序集"已批准解析缝"，见 [seams.md](seams.md)；第二消费方出现时按
  `IDialogService` 先例把接口上提 Core）。
- 模块注册器自报导航项与页面模板字典（`DataTemplate DataType=VM → View`）；Host 在 App 资源里**每模块一次** pack URI 静态合并。
- 新增页面 = 所属模块内部（注册器声明导航项 + 模板字典加条目），**不碰 Host**。
- 新增模块 = Host 登记：程序集引用 + 注册器调用 + 模板字典合并（各一次，放行共享面）。
- e2e `AutomationId` 沿用 `NavTab0..4`，随槽位稳定。

  as-built：B6/#79 起 M5 已按上述目标态跨程序集落地——`ShellModuleRegistrar`（RegisterNavigation +
  RegisterServices）与 `ShellPageTemplates.xaml` 随 M5 迁入 `StarPie.Shell`；B9/#82 起 M1 同款
  落地——`GesturesModuleRegistrar` 与 `GesturesPageTemplates.xaml` 随 M1 迁入 `StarPie.Gestures`，
  Host App.xaml 对两模块模板字典均经跨程序集 pack URI 合并；exe 内仅剩 Host 外观聚合页的
  HostModuleRegistrar/HostPageTemplates（目标态留 Host）。

### 5.2 导航槽位表（正典）

| 槽位 | AutomationId | TitleKey | 页面 VM | 页面 View | 程序集（目标） |
|---|---|---|---|---|---|
| 0 | `NavTab0` | `TabTrigger` | `BehaviorSettingsViewModel` | `TriggerSettingsPage` | M1 Gestures（B9/#82 已落地） |
| 1 | `NavTab1` | `TabAppearance` | `AppearanceSettingsViewModel`（聚合壳） | `AppearanceSettingsPage` | Host |
| 2 | `NavTab2` | `TabGestures` | `ProfileListViewModel` | `GesturesSettingsPage` | M1 Gestures（B9/#82 已落地） |
| 3 | `NavTab3` | `TabAdvanced` | `GeneralSettingsViewModel` | `AdvancedSettingsPage` | M5 Shell（B6/#79 已落地） |
| 4 | `NavTab4` | `TabAbout` | `AboutViewModel` | `AboutSettingsPage` | M5 Shell（B6/#79 已落地） |

缺失/重复/未知槽位由 Core 收口测试拦截；槽位表是侧边栏顺序唯一正典（B2/#75 契约与收口测试已落地；
B3/#76 目录驱动接线已落地：MainViewModel 按目录注册构造导航项，导航执行走
`INavigationExecutor` 目录执行缝；ADR-0021/#92 起运行时主体（含 MainViewModel）迁 Host，
目录契约仍驻 Core——程序集归属见 §2/§4）。

## 6. DI 与注册契约（目标态）

- **注册自治**：每个业务程序集暴露注册器（建议形态：公开静态类，含 `RegisterServices(IServiceCollection)` 与 `RegisterNavigation(NavigationCatalog)`）；Host Composition 按固定顺序调用（**B6/#79 起以 ShellModuleRegistrar 为首个带 DI 的跨程序集样板落地**：RegisterServices 下放 M5 页面 VM 注册，RegisterNavigation 自报导航项；**B7/#80 起 M4 以 ThemeModuleRegistrar 落地**：RegisterServices 下放主题服务与主题设置子 VM 注册，M4 无导航页故无 RegisterNavigation；**B8/#81 起 M2 以 WheelModuleRegistrar 落地**：RegisterServices 下放轮盘工厂 `IWheelFactory→WheelFactory` 与轮盘外观设置子 VM 注册，M2 无导航页故无 RegisterNavigation——D5 工厂随 M2，M1 手势侧只经 M2 侧接口消费；**B9/#82 起 M1 以 GesturesModuleRegistrar 落地（最后一个业务模块程序集）**：RegisterServices 下放手势管线（MouseHook/IActionExecutorService/IWindowContext/GestureEngine/GestureController）、触发+手势两页 VM 与 `IProfilePreviewSource` 别名（实现方 `ProfileListViewModel`）注册，RegisterNavigation 自报槽位 0/2 导航项）。
- **ADR-0019/#87 起 M3 以 `ProgramsModuleRegistrar` 补齐注册器样板**：RegisterServices 下放
  快捷方式解析契约 `IShortcutTargetResolver→ShortcutResolver` 注册，M3 无导航页故无
  RegisterNavigation——B4/#77「无 DI 注册」例外随 M3 → Core 单向收口。
- **ADR-0020/#88 起 S6 以 `DialogsModuleRegistrar` 补齐共享基础设施模块的注册器样板**：
  RegisterServices 下放 `IDialogService→DialogService` 注册（工厂经容器解析 Core 契约与
  `IProgramScanner`），S6 无导航页故无 RegisterNavigation——对话框实现自 Host 抽出独立
  程序集后，组合根不再直接装配对话框服务，仅保留 `DialogService.SetOwner(MainView)` 回填面。
- **根解析集中**：Host Composition 仍唯一 `BuildServiceProvider` / `CreateAppHost`；模块不解析、不持容器。
- **已批准解析缝**：`WheelFactory`、`DialogService`、模块注册器（仅注册不解析）。
  导航目录执行缝自 ADR-0021/#92 起不再是跨程序集缝（`INavigationExecutor` 随运行时整体归
  Host，为宿主内部件，seams.md 不登记）。
- `AppHostDelegates` 已上提为 Core 公开契约（B6/#79：`StarPie.Core/Services/AppHostDelegates.cs`；
  Host 组合根以单例注册并在 AppHost 构造后回填实现），M5 注册器只依赖 Core（B9/#82 起 M1 注册器
  同款只依赖 Core 契约；其手势管线消费的 M2 侧接口属允许边，不在注册器内直接引用 M2 类型）。
- CreateAppHost 的硬编码解析清单 → 目录/启动激活钩子驱动；eager 页面实例化语义已在 B3/#76 验证保留
  （CreateAppHost 遍历 `NavigationCatalog.Entries` 逐个 eager 解析页面 VM）。
- 不引入子容器、Generic Host、Autofac、Prism（ADR-0016 决策 13）。

## 7. 可见性（目标态）

- 不引入 `InternalsVisibleTo`（layering.md 维持）。
- 模块公开面 = 注册器入口 + 被测 public 类型；内部实现细节保持 internal。
- Host 只引用模块注册器，不引用模块内部。
- 跨集必需的内部件（如 `ThemePaletteManager`）在各批次单独裁决为 public 或改经接口注入。

  B6/#79 先例：Host AppHost 负责装配托盘对象，`TrayIconManager`/`TrayMenuEntry` 随迁后裁决为
  public（Host 装配面）；`AutostartRegistry` 仅由模块注册器接线，维持 internal。

  B7/#80 先例（同判据）：M4 的 `ThemePaletteManager` 随迁后裁决为 **public**（Host `AppHost`
  构造时 `new` 并 `AttachPaletteApplier`，`ThemeService.AttachPaletteApplier` 同步公开）；
  模块内主题文件映射/缓存/冻结等实现细节保持私有。

  B8/#81 先例（同判据）：M2 的 `WheelFactory`/`IWheelFactory` 与外观设置子 VM 只经同集注册器
  接线/容器解析，维持 public（被测类型），**无新增 Host 装配面 public 裁决**——RadialWindow
  由 WheelFactory 在同集内创建，不经 Host 直接 new；`WheelPreviewRenderer` 深浅色探测改由
  调用方（Host 外观页）以 `bool` 传入（M2 不反向引用 Host `MainView`）。

  B11/#88 先例（同判据）：S6 的 `DialogService` 裁决 **public**——Host `AppHost` 建窗后调
  `SetOwner(MainView)` 惰性回填 Owner（ADR-0004），接口 `IDialogService` 不含 SetOwner（Owner
  是实现内部自由，不泄露进契约）；对话框 VM/Window 与 `SpectrumCanvasBehavior` 维持 public
  （被测/装配类型），无新增 InternalsVisibleTo。

## 8. 批次路线 B0–B10（全部清零）

> 每个批次：独立 issue；构建 + 全量 xUnit 绿；涉及可见文案时全量 e2e 绿（分层执行见 ADR-0018）；完成后回填对应叶子并从本表移除。
> B1–B9（#75–#82）已在前批逐批移除（见 §9 逐批登记）；**B10（命名空间统一，#83）已落地并从本表移除**，
> 程序集化路线全部清零。下表仅余 B0 纯文档批的历史登记。

| 批 | 内容 | 主要回填 |
|---|---|---|
| B0 | 纯文档：ADR-0016 + 本文 + modules.md R4/D3/D5/扩展点/§8 修订 + architecture.md 路由/索引 | modules.md、architecture.md |

### 8.1 批次阻塞边（2026-09-06 代码审计）

> 阻塞边 = 该票必须在前置票合入 main 后才能开工的硬门；无阻塞票可按路线顺序或 frontier 先做（多人并行时需先做文件面互斥划分）。

- B2（Core 抽取）← None（已落地，#75）。
- B3（导航自治 + MainViewModel 迁 Core）已落地（#76；前置 B2/#75 已落地）。
- B4（M3 Programs 抽取）← None（已落地，#77：零 Core 依赖、无 DI 注册，注册器样板已随 B6/#79 落地；**ADR-0019/#87 收口**：M3 改单向 Core + ProgramsModuleRegistrar，注册器样板全员补齐）。
- B5（共享 UI 基建迁 Core）已落地（#78；前置 B2/#75 已落地）。
- B6（M5 Shell 抽取）已落地（#79；前置 B3/#76、B5/#78 已落地）。
- B7（M4 抽取）已落地（#80；前置 B2/#75 已落地——主题 XAML 自包含，不依赖 B5）。
- B8（M2 抽取，含 D5）已落地（#81；前置 B7/#80 已落地——`RadialWindow` 注入 M4
  `StarPie.Theme` 的 `IThemeService`；其 XAML 自包含，不依赖 B5）。
- B9（M1 抽取）已落地（#82；前置 B5/#78、B8/#81 已落地——共享页面基类 `SettingsPageBase` 已在
  Core（B6/#79 迁入）；`GesturesSettingsPage` 引用共享控件，且 M1→M2 需 M2 已成集）。
- B10（命名空间统一）已落地（#83；前置 B9/#82 已落地）。**至此全部批次阻塞边清零，无进行中批次。**

### 8.2 执行期集成面串行约束

上述阻塞边描述的是**架构上的硬前置**；它们不等于可以无冲突地并行修改。为避免多个 agent 同时改动组合根和工程入口，执行时还需遵守以下集成面互斥规则：

- `Composition.cs`、`AppHost.cs`、`WinPieGestures.csproj`、`WinPieGestures.slnx`（B2 起含
  `StarPie.Core.csproj`；B4/#77 起含 `StarPie.Programs.csproj`；B7/#80 起含
  `StarPie.Theme.csproj`；B8/#81 起含 `StarPie.Wheel.csproj`；B9/#82 起含
  `StarPie.Gestures.csproj`）同一时间只允许一张票落地。
  B1–B9（#75–#82）与 B10/#83（命名空间统一，触及全部 csproj RootNamespace/GlobalUsings 文件面）
  均已按此规则串行落地。
- `App.xaml`、主题/控件资源字典及其 pack URI 同一时间只允许一张票落地。B5/#78（ModernControls
  迁 Core）、B7/#80（M4 Themes XAML 拆集 + App.xaml Light 改跨集 pack URI）与 B8/#81
  （M2 核图标预览转换器改经 `assembly=StarPie.Wheel` App 级实例）与 B9/#82（M1 模板字典迁
  `StarPie.Gestures`，Host App.xaml 改跨程序集 pack URI 合并）均已落地；B10/#83（XAML xmlns/
  x:Class 改名，触及同一资源面）为最后一个此类批次，已排队集成落地。
- `Services/Shell`、`ThemePaletteManager.cs`、主题与壳层宿主接线存在物理文件重叠，已按串行约束
  先后落地：B6/#79 把 M5 三件（TrayIconManager/AutostartRegistry/MemoryOptimizer）迁入
  `StarPie.Shell/Services/Shell`；B7/#80 把 M4 件（IThemeService/ThemeService/ThemePaletteManager/
  主题字典/主题设置子 VM）迁入 `StarPie.Theme` 并清空 Host 侧目录。此后 M4/M5 文件面不再交叉；
  B8/#81 把 M2 件迁入 `StarPie.Wheel` 并清空 Host 侧 Services/Wheel、ViewModels/Wheel、
  Views/{Wheel,Renderers,Converters} 目录；B9/#82 把 M1 件迁入 `StarPie.Gestures` 并清空
  Host 侧 Services/Actions、Services/Gestures、ViewModels/Gestures 目录（M1 文件面已全部
  迁出，Host 侧仅余 Host 页/对话框/壳窗口与 S6 对话框实现）；B10/#83（命名空间统一）随后
  按本约束只做机械改名、不再迁移文件面，已一票一验落地。
- agent 分支可以并行进行只读分析或不触及上述文件面的代码准备；进入合并队列前必须先完成一次主干同步、全量构建与全量 xUnit，合入 main 前另需一次全量 pywinauto e2e（验证义务分层见 ADR-0018）。

这是一条**执行协调规则**，不是新增业务依赖；它不改变 B0–B10 的拓扑，只约束共享集成面的写入顺序
（路线执行期规则；B10/#83 落地后无进行中批次）。

## 9. 现状对照与差异登记

代码现状 = Host exe（`WinPieGestures/`，程序集 `StarPie`）+ 共享内核（`StarPie.Core/`，程序集
`StarPie.Core`，WPF 类库）+ M3 模块程序集（`StarPie.Programs/`，程序集 `StarPie.Programs`，WPF
类库）+ M5 模块程序集（`StarPie.Shell/`，程序集 `StarPie.Shell`，WPF 类库，B6/#79 起）+
M4 模块程序集（`StarPie.Theme/`，程序集 `StarPie.Theme`，WPF 类库，B7/#80 起）+
M2 模块程序集（`StarPie.Wheel/`，程序集 `StarPie.Wheel`，WPF 类库，B8/#81 起）+
M1 模块程序集（`StarPie.Gestures/`，程序集 `StarPie.Gestures`，WPF 类库，B9/#82 起）+
`WinPieGestures.Tests`（显式引用七工程，不依赖传递引用）。B2/#75 已落地：Models、S2/S3/S4/S1、
S6 契约、S5 导航内核（NavigationStore/INavigationService/NavigationService/
NavigationItemViewModel/NavigationCatalog/槽位表）迁入 Core（导航运行时物理居 Core 的表述已由
ADR-0021/#92 修订——迁 Host，见下段）。**B3/#76 已落地**：`MainViewModel`
目录驱动后迁入 Core（无页面类型硬编码；B3/#76 历史与目录驱动验收不回开，物理落点以
ADR-0021/#92 为准——运行时主体迁回 Host）；导航执行走 `INavigationExecutor` 目录执行缝；exe 内按
M1/Host 临时注册器（`RegisterNavigation`）与模块页面模板字典（App 级每模块一次静态合并）；
CreateAppHost 页面 eager 解析清单目录化（语义保留）；MainView.xaml 纯壳（不再含页面 DataTemplate）。
**B4/#77 已落地**：M3 三件（ProgramScanner/ProgramCatalog(+ProgramEntry)/ShortcutResolver）迁入
`StarPie.Programs`（slnx 登记；Host/Tests 显式 ProjectReference）；M3 零 Core 依赖——扫描图标
补全经组合根注入的 S1 `IconAssets.GetIcon` 委托，`IconAssets.ResolveShortcutTarget` 回填缝指向
`StarPie.Programs` 的 `ShortcutResolver`。**ADR-0019/#87 已落地（M3 边界收口）**：M3 改单向依赖
Core——契约 `IShortcutTargetResolver` 驻 `StarPie.Core/Services/Icons/`，`ShortcutResolver` 改实例
实现契约，`ProgramScanner` 签名改经 `IIconAssetService`/`IShortcutTargetResolver` 注入，新增
`StarPie.Programs/Modules/ProgramsModuleRegistrar.cs`，组合根删除 `IconAssets.ResolveShortcutTarget`
静态回填行（S1 本身已双形拆为 IconCatalog/IIconAssetService，见下 S1 相关回填）。**B5/#78 已落地**：共享 UI 基建迁 Core——四个通用转换器
（`HexToBrushConverter`/`StringToGeometryConverter`/`IntEqualsConverter`/`FilePathToImageConverter`）
、共享自定义控件 `HotkeyRecorderBox` 与全局控件样式字典 `ModernControls.xaml` 物理迁入
`StarPie.Core/Views/{Converters,Controls,Styles}`（命名空间当时维持 `WinPieGestures.*`，
B10/#83 统一为 `StarPie.*`）；
Host `App.xaml` 经跨程序集 pack URI（`/StarPie.Core;component/Views/Styles/ModernControls.xaml`）
单点合并该字典，转换器实例仍为 App 级资源；页面/窗口无自合并样式字典。暂留 Host 的 UI 专用件：
M2 核图标预览转换器（`CoreIconGeometryConverter`/`CoreIconNameConverter`，Appearance 聚合页用；
Geometry 转换器直连 M2 `WheelGeometry`，B8 收编前 Core 不得反向依赖宿主）已随 B8/#81 收编
M2（见下 B8 段）；S6 取色对话框行为 `SpectrumCanvasBehavior`（依赖 Host
`ColorPickerViewModel.SpectrumPoint`；S6 对话框实现留 Host）仍留 Host（B11/#88 起随 S6 实现
迁入 `StarPie.Dialogs`，见下 B11 段）。
**B6/#79 已落地**：M5 壳层服务与系统设置面成独立模块程序集——TrayIconManager/AutostartRegistry/
MemoryOptimizer 迁入 `StarPie.Shell/Services/Shell`，GeneralSettingsViewModel+AdvancedSettingsPage 与
AboutViewModel+AboutSettingsPage 迁入 `StarPie.Shell/ViewModels|Views/Pages`（命名空间当时维持
`WinPieGestures.*`，B10/#83 统一为 `StarPie.*`）；共享页面基类 `SettingsPageBase` 迁入 `StarPie.Core/Views/Pages`
（跨集页面共用，Host/M5/M1 页 XAML 根经 assembly=StarPie.Core 引用）；exe 内临时注册器
`M5ModuleRegistrar`/`M5PageTemplates.xaml` 替换为模块内正式 `ShellModuleRegistrar`
（RegisterServices + RegisterNavigation）与 `ShellPageTemplates.xaml`（Host App.xaml 经跨程序集
pack URI `/StarPie.Shell;component/Modules/ShellPageTemplates.xaml` 单点合并；M1/Host 注册器与
模板字典留 exe，已于 B9/#82 收编 M1（见下 B9 段））；`AppHostDelegates` 上提 `StarPie.Core/Services/AppHostDelegates.cs`
为公开契约（组合根注册单例、AppHost 构造后回填；ShellModuleRegistrar 只依赖 Core）；M5 托盘
深色配色改经组合根注入 `Func<bool>` 探针、AutostartRegistry dev 分支改读 Core `AppDataPaths.IsDevInstance`
（Shell 不反向引用 Host/M4）；slnx 登记 StarPie.Shell，Host/Tests 显式 ProjectReference。
**B7/#80 已落地**：M4 界面主题体系成独立模块程序集——IThemeService/ThemeService 迁入
`StarPie.Theme/Services/Shell`，ThemePaletteManager 迁入 `StarPie.Theme` 模块根并裁决 public
（Host AppHost 装配面：`new` + `AttachPaletteApplier` + `Apply`，ThemeService.AttachPaletteApplier
同步公开；同 B6/#79 TrayIconManager 先例），五套主题字典迁入 `StarPie.Theme/Views/Styles/Themes`，
InterfaceThemeSettingsViewModel+AppThemeOptionItem 迁入 `StarPie.Theme/ViewModels/Pages`
（命名空间当时维持 `WinPieGestures.*`，B10/#83 统一为 `StarPie.*`）；Host App.xaml 对 Light 默认字典改经跨程序集
pack URI `/StarPie.Theme;component/Views/Styles/Themes/Light.xaml` 静态合并，ThemePaletteManager
加载源同步指向 StarPie.Theme（主题令牌 key 集与行为不变）；新增模块注册器 `ThemeModuleRegistrar`
（RegisterServices 下放 M4 的 DI 注册；M4 无导航页，无 RegisterNavigation/模板字典）；slnx 登记
StarPie.Theme，Host/Tests 显式 ProjectReference；Host 侧 Services/Shell 与 Views/Styles/Themes
目录随迁清空。新增 ThemeAssemblyPlacementTests 6 例收口归属/依赖/可见性/BAML/注册器。
**B8/#81 已落地**：M2 轮盘与渲染成独立模块程序集——轮盘 VM（IWheelViewModel/WheelViewModel/
IWheelAppearanceState，ViewModels/Wheel）、RadialWindow（Views/Wheel，BAML 随集）、样式渲染器与
实时预览（Views/Renderers，IRadialStyleRenderer/StyleRendererFactory/BaseStyleRenderer/四风格/
WheelPreviewRenderer）、轮盘配色（WheelPalette/WheelPaletteCatalog/WheelPaletteParser 自 Core
Models 物理收编 StarPie.Wheel/Models；CustomColorPreset 仍 Core——AppConfig 配置 POCO 引用）、
WheelGeometry（Services/Wheel）、轮盘工厂 IWheelFactory/WheelFactory（Services/Wheel，D5 随 M2
收编，命名空间 `WinPieGestures.Services.Gestures` → `WinPieGestures.Services.Wheel` 与物理目录
一致）与核图标预览转换器（CoreIconGeometryConverter/CoreIconNameConverter，Views/Converters，
B5/#78 暂留 Host 的归属裁决：随 M2——Host App.xaml 改经 `assembly=StarPie.Wheel` 实例化）
迁入 `StarPie.Wheel`（命名空间当时维持 `WinPieGestures.*`，B10/#83 统一为 `StarPie.*`）；外观设置子 VM
WheelAppearanceSettingsViewModel 迁入 `StarPie.Wheel/ViewModels/Pages`，其 DI 注册与轮盘工厂
注册下放新增模块注册器 `WheelModuleRegistrar`（RegisterServices；M2 无导航页，无
RegisterNavigation/模板字典）；预览 Profile 只读契约 IProfilePreviewSource 上提
`StarPie.Core/ViewModels/Pages`（D5，实现方 M1 ProfileListViewModel 与消费方 M2 外观子 VM
均只依赖 Core）；WheelPreviewRenderer 深浅色探测改由外观页以 bool 传入（不再引用 Host
MainView，M2 不反向依赖宿主）；slnx 登记 StarPie.Wheel，Host/Tests 显式 ProjectReference
（Wheel → Core 单向 + Wheel → Theme 允许边）；Host 侧 Services/Wheel、ViewModels/Wheel、
Views/{Wheel,Renderers,Converters} 目录随迁清空。新增 WheelAssemblyPlacementTests 6 例
收口归属/依赖/BAML/D5 接口面/注册器。
**B9/#82 已落地**：M1 手势与动作成最后一个独立模块程序集——手势管线（MouseHook/
GestureController/GestureEngine/IWindowContext/WindowContext，Services/Gestures）、动作执行
（IActionExecutorService/ActionExecutorService/ActionRouting，Services/Actions）、触发+手势
设置页（BehaviorSettingsViewModel/TriggerSettingsPage、ProfileListViewModel/
GesturesSettingsPage、SlotViewModel，ViewModels|Views 对应目录）与模块注册器
`GesturesModuleRegistrar`（RegisterServices + RegisterNavigation，原 exe 内 M1ModuleRegistrar
替换）+ `GesturesPageTemplates.xaml`（原 M1PageTemplates.xaml 替换，Host App.xaml 改经跨程序集
pack URI `/StarPie.Gestures;component/Modules/GesturesPageTemplates.xaml` 单点合并）迁入
`StarPie.Gestures`（命名空间当时维持 `WinPieGestures.*`，B10/#83 统一为 `StarPie.*`）；手势管线、触发+手势两页 VM
与 `IProfilePreviewSource` 别名（实现方 ProfileListViewModel）的 DI 注册下放
GesturesModuleRegistrar（页面 VM 组合根集中注册清零）；MouseHook dev 分支改读 Core
`AppDataPaths.IsDevInstance` 回填缝（同 B6 AutostartRegistry 先例，M1 不反向引用 Host）；
slnx 登记 StarPie.Gestures，Host/Tests 显式 ProjectReference（Gestures → Core 单向 +
Gestures → Wheel 允许边，不引用 Host/Shell/Theme/Programs）；Host 侧 Services/Actions、
Services/Gestures、ViewModels/Gestures 目录随迁清空，exe Modules/ 仅余 Host 外观聚合页的
HostModuleRegistrar/HostPageTemplates。新增 GesturesAssemblyPlacementTests 6 例收口
归属/依赖（M1→M2 单向）/dev 回填缝/导航/BAML/注册器；WheelAssemblyPlacementTests 的
D5 断言同步改为 GestureEngine 驻 StarPie.Gestures。
页面 VM/服务的 DI 注册：M4 主题服务与主题设置子 VM 已下放 ThemeModuleRegistrar（B7/#80）、
M5 两页已下放 ShellModuleRegistrar（B6/#79）、M2 轮盘工厂与轮盘外观设置子 VM 已下放
WheelModuleRegistrar（B8/#81）、M1 手势管线与触发+手势两页已下放 GesturesModuleRegistrar
（B9/#82）；仅 Host 外观聚合页 VM 仍由组合根注册（目标态 Host 页）。
**B11/#88（ADR-0020）已落地**：S6 对话框实现成独立模块程序集 `StarPie.Dialogs`——
DialogService（Services/Dialogs）、五对对话框 VM/Window（ViewModels|Views/Dialogs）与
取色行为 SpectrumCanvasBehavior（Views/Controls）自 Host 迁入（命名空间沿用 StarPie.* 树）；
契约 IDialogService 与结果 record 留 Core；依赖方向 Dialogs → Core 单向 + Dialogs → Theme
允许边（IThemeService 窗口主题应用），不引用 Host/Programs/其它业务模块；新增模块注册器
DialogsModuleRegistrar（RegisterServices 下放 IDialogService→DialogService；S6 无导航页，
无 RegisterNavigation/模板字典）。M3 扫描契约收口——ProgramEntry 与纯规则 ProgramCatalog
（第二消费方族判据）上提 `StarPie.Core/Services/Programs/`（命名空间 StarPie.Services.Programs
不变），新增 IProgramScanner 契约驻 Core，ProgramScanner 由 static 改实例实现（构造注入
IIconAssetService/IShortcutTargetResolver），ProgramsModuleRegistrar 增注册 IProgramScanner，
组合根删除 `() => ProgramScanner.ScanInstalledPrograms(...)` 委托行（S21 归零）；
IThemeService.ThemeChanged 死事件移除（主题应用唯一通道 AppThemeChangedMessage，测试改经
AttachPaletteApplier 计数）；slnx 登记 StarPie.Dialogs，Host/Tests 显式 ProjectReference；
Host 侧 Services/Dialogs、ViewModels/Dialogs、Views/Dialogs、Views/Controls 目录随迁清空。
新增 DialogsAssemblyPlacementTests 6 例收口归属/依赖/BAML/契约/构造签名/注册器；
SharedUi/Programs Placement 断言随迁更新；新增 seams.md 活缝编目（见 [seams.md](seams.md)）。

**ADR-0021/#92（导航运行时归 Host）已落地**：导航运行时主体自 `StarPie.Core` 迁入 Host——
`NavigationStore`/`NavigationExecutor`（含 `INavigationExecutor`）落
`WinPieGestures/Services/Navigation/`，`MainViewModel`/`NavigationItemViewModel` 落
`WinPieGestures/ViewModels/Navigation/`（命名空间不变：`StarPie.Services.Navigation` /
`StarPie.ViewModels.Navigation`，与 `ShellViewModel` 同目录族）；Core 仅留目录契约
`Services/Navigation/NavigationCatalog.cs` 四件（`NavigationCatalog`/`NavigationSlot`/
`NavigationSlots`/`NavigationPageRegistration`），不动；C1 死代码删除（`INavigationService`/
`NavigationService` + `Composition.cs` 开放泛型注册行 + `NavigationTests` 的
`NavigationServiceTests` 用例）；`StarPie.Core.csproj` 移除
`Microsoft.Extensions.DependencyInjection` 包引用（Core 内已无使用点；`StarPie.Programs`
因 `ProgramsModuleRegistrar` 直用 `IServiceCollection`，改为自身显式 PackageReference，
与其它模块注册器 csproj 一致）；执行缝自"已批准解析缝"清单移除（Host 内部件，seams.md
不登记跨集缝）；新增 `NavigationAssemblyPlacementTests` 2 例收口运行时四类归属 `StarPie`、
目录契约四件归属 `StarPie.Core`（命名空间均不变）。

**8 程序集目标态（含命名空间）已全部达成**：Host/Core/Dialogs/Programs/Shell/Theme/Wheel/
Gestures 各自成集且依赖方向落地（7 程序集目标态经 ADR-0020/#88 扩展为 8 程序集）；
B10/#83 命名空间统一收尾后，全仓命名空间统一为 `StarPie.*`（全仓前缀替换，保持跨程序集
共享命名空间树，与迁移前 `WinPieGestures.*` 同构），程序集化批次全部清零（B0–B10 + B11/#88）。

## 参见 ADR

[0016](../adr/0016-assembly-split-target-and-roadmap.md)（程序集化目标态与分批执行）、[0015](../adr/0015-module-map-and-ownership.md)（12 模块地图与归属裁定）。
