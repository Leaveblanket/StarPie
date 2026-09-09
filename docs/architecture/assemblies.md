# 程序集地图与程序集化收尾（目标态 = as-built）

> 本文记录程序集化目标态（[ADR-0016](../adr/0016-assembly-split-target-and-roadmap.md) +
> [ADR-0023](../adr/0023-module-contracts-hard-boundary-and-core-narrowing.md)）的地图视图：
> 目标程序集划分、程序集级依赖规则、导航槽位表与注册/可见性契约。
>
> **15 程序集现状**：Host/Core + 5 业务 runtime + `StarPie.Dialogs` + `StarPie.Icons` +
> 各 `*.Contracts`（划分见 §2）。代码现状以 §2–§7/§9 与各叶子（`docs/architecture/*.md`）
> 为准，冲突时叶子优先。概念模块地图与归属裁定见 [modules.md](modules.md)（ADR-0015）。

## 1. 何时读本文

| 想做什么 | 读哪里 |
|---|---|
| 某个类型/文件属于哪个程序集 | 本文 §2–§4 + modules.md §3 |
| 程序集间依赖是否允许 | 本文 §3 |
| 导航槽位 / 侧边栏顺序正典 | 本文 §5 |
| 新增页面/服务要动哪些 | 本文 §5–§6（+ [extending.md](extending.md) 原型 A–F） |
| 程序集化落地历史与现状 | ADR-0016、ADR-0023 与 git 历史；现状以本文 §2–§7 为准 |
| 为什么这样定 | [ADR-0016](../adr/0016-assembly-split-target-and-roadmap.md) |

## 2. 程序集地图（15 程序集，as-built）

| 程序集 | 形态 | 承载 |
|---|---|---|
| `StarPie`（项目 `StarPie`） | WinExe | H1 宿主与组合根（App/AppHost/Composition/DevInstance）；Host 壳窗口（`MainView` 全文件 + `ShellViewModel`）；导航运行时主体（`Services/Navigation/`：NavigationStore/NavigationExecutor（含 INavigationExecutor）；`ViewModels/Navigation/`：MainViewModel/NavigationItemViewModel，命名空间不变；Appearance 聚合页；共享 UI 基建（通用转换器 `Views/Converters/` + `ModernControls.xaml` `Views/Styles/`（App.xaml 本地单点实例化/合并、资源 key 不变）；仅保留 DialogService.SetOwner 回填等宿主装配面 |
| `StarPie.Core` | WPF 类库 | 共享内核（S2/S3/S4/S5 共享件 + Models）：Models（CustomColorPreset 居此——配置 POCO 引用；轮盘配色 WheelPalette* 属 M2，驻 `StarPie.Wheel/Models/`）；S2 Configuration；S3 Localization（含 `Strings*.resx` 与生成器）；S4 Messages；S5 导航目录/槽位契约（`NavigationCatalog`/`NavigationSlot`/`NavigationSlots`/`NavigationPageRegistration`——纯契约共享模块，运行时主体不居 Core）；宿主回调契约 `Services/AppHostDelegates`。**不含共享 UI 基建**（已去共享化）、**S1 图标资产**（ADR-0023 独立成集）与**任何模块出口契约**（扫描/对话框/.lnk SPI 与主题/轮盘/预览 Profile 契约分别驻对应 Contracts 程序集；`Services/{Programs,Dialogs,Icons}` 与 `ViewModels/Pages/` 目录清空，ADR-0023） |
| `StarPie.Icons.Contracts` | WPF 类库 | S1 图标契约：`IIconAssetService`/`IconCatalog`/`CustomIconItem`/`VectorIconItem`（命名空间 `StarPie.Services.Icons` 不变；零 ProjectReference——薄契约，按需 WPF） |
| `StarPie.Icons` | WPF 类库 | S1 图标实现：`IconAssetService` + `IconsModuleRegistrar`；Icons → Icons.Contracts + Programs.Contracts（SPI 契约边，ADR-0023）+ Core（S2 AppDataPaths 共享基建）单向；实现 runtime 只被 Host/注册器/测试引用 |
| `StarPie.Programs.Contracts` | WPF 类库 | M3 扫描/SPI 契约：`IProgramScanner`/`ProgramEntry`/`ProgramCatalog`（`Services/Programs/`，命名空间 `StarPie.Services.Programs` 不变）+ `IShortcutTargetResolver`（`Services/Icons/`，命名空间 `StarPie.Services.Icons` 不变；零 ProjectReference——薄契约，按需 WPF：ProgramEntry.IconSource，ADR-0023） |
| `StarPie.Dialogs.Contracts` | 类库（纯 C#） | S6 对话框契约：`IDialogService` + 6 结果 record（`Services/Dialogs/`，命名空间 `StarPie.Services.Dialogs` 不变；零 ProjectReference——薄契约，ADR-0023） |
| `StarPie.Dialogs` | 类库 | S6 对话框实现：DialogService、五对对话框 VM/Window、SpectrumCanvasBehavior（契约随实现方驻 Dialogs.Contracts，ADR-0023；Dialogs → Dialogs.Contracts + Programs.Contracts + Icons.Contracts + Core（S2/S3/S4）+ Theme.Contracts（ADR-0023）单向，不引用任何 runtime/Host/其它模块 runtime） |
| `StarPie.Gestures.Contracts` | 类库 | M1 预览 Profile 契约：`IProfilePreviewSource`（`ViewModels/Pages/`，命名空间 `StarPie.ViewModels.Pages` 不变；仅引用 Core（WheelProfile 签名）——薄契约，ADR-0023） |
| `StarPie.Gestures` | 类库 | M1 手势与动作：Services/Gestures、Services/Actions、Trigger/Gestures 设置页、热键录制控件 HotkeyRecorderBox（控件 + 样式字典 `Views/Styles/HotkeyRecorderBox.xaml`）；Gestures → Core 单向 + Gestures.Contracts + Wheel.Contracts + Dialogs.Contracts + Icons.Contracts（M1→M2 runtime 允许边经 Wheel.Contracts 清零，ADR-0023） |
| `StarPie.Wheel.Contracts` | 类库 | M2 轮盘契约：`IWheelFactory`（`Services/Wheel/`）、`IWheelViewModel`/`IWheelAppearanceState`（`ViewModels/Wheel/`，命名空间不变；仅引用 Core（GesturePoint/WheelProfile/AppConfig 签名）——薄契约，ADR-0023） |
| `StarPie.Wheel` | 类库 | M2 轮盘与渲染：WheelViewModel/RadialWindow/Renderers/WheelPalette*/WheelGeometry/WheelFactory（含 D5 工厂收编；Wheel → Core + Wheel.Contracts（自身契约）+ Theme.Contracts + Gestures.Contracts + Dialogs.Contracts + Icons.Contracts 单向，M2→M4 runtime 允许边经 Theme.Contracts 清零，ADR-0023） |
| `StarPie.Programs` | 类库 | M3 程序扫描与目录实现：ProgramScanner/ShortcutResolver + ProgramsModuleRegistrar（契约本体驻 Programs.Contracts，ADR-0023 起 M3 → Programs.Contracts + Icons.Contracts 单向，不再引用 Core/其它模块 runtime） |
| `StarPie.Theme.Contracts` | WPF 类库 | M4 界面主题契约：`IThemeService`（`Services/Shell/`，命名空间 `StarPie.Services.Shell` 不变；零 ProjectReference——薄契约，按需 WPF：ApplyWindowTheme(FrameworkElement)，ADR-0023） |
| `StarPie.Theme` | 类库 | M4 界面主题：ThemeService/Themes XAML/InterfaceThemeSettingsViewModel/AppThemePaletteManager（裁决 public——Host AppHost 装配面；Theme → Core + Theme.Contracts 单向，实现自有契约） |
| `StarPie.Shell` | 类库 | M5 壳层服务与设置面：TrayIconManager/AutostartRegistry/MemoryOptimizer/General+About 设置页（`MainView` 壳窗口与 `ShellViewModel` **不**随 M5，留 Host） |

## 3. 程序集级依赖规则

```text
StarPie (Host/exe) ──→ StarPie.Core
     │──→ StarPie.Dialogs ──→ StarPie.Dialogs.Contracts + StarPie.Programs.Contracts + StarPie.Icons.Contracts + StarPie.Core + StarPie.Theme.Contracts   （Dialogs→M4 runtime 允许边经 Theme.Contracts 清零，ADR-0023）
     │──→ StarPie.Gestures ──→ StarPie.Core + StarPie.Gestures.Contracts + StarPie.Wheel.Contracts + StarPie.Dialogs.Contracts + StarPie.Icons.Contracts   （M1→M2 runtime 允许边经 Wheel.Contracts 清零，ADR-0023）
     │──→ StarPie.Wheel ──→ StarPie.Core + StarPie.Wheel.Contracts + StarPie.Theme.Contracts + StarPie.Gestures.Contracts + StarPie.Dialogs.Contracts + StarPie.Icons.Contracts   （M2→M4 runtime 允许边经 Theme.Contracts 清零，ADR-0023）
     │──→ StarPie.Gestures/Shell/Wheel ──→ StarPie.Dialogs.Contracts   （ADR-0023 契约边）
     │──→ StarPie.Programs ──→ StarPie.Programs.Contracts（零依赖） + StarPie.Icons.Contracts   （M3 不再引用 Core，ADR-0023）
     │──→ StarPie.Shell ──→ StarPie.Core + StarPie.Dialogs.Contracts
     │──→ StarPie.Theme ──→ StarPie.Core + StarPie.Theme.Contracts   （实现自有契约，ADR-0023）
     │──→ StarPie.Icons ──→ StarPie.Icons.Contracts（零依赖） + StarPie.Programs.Contracts + StarPie.Core   （SPI 经 Programs.Contracts 契约边；Core 仅 S2 AppDataPaths，ADR-0023）
     └────────────────────────────────────────→ StarPie.Core
```

- 模块 runtime 对 Core/契约**单向**：M*/S* runtime 只引用自身契约与经 `*.Contracts` 消费的能力
  （扫描/.lnk 经 Programs.Contracts、主题经 Theme.Contracts、轮盘经 Wheel.Contracts、预览源经
  Gestures.Contracts、对话框经 Dialogs.Contracts、图标资产经 Icons.Contracts），不反向引用
  Host/其它模块 runtime；托盘深色探针、dev 分支等宿主能力经组合根注入委托或 Core
  `AppDataPaths` 回填缝提供；`Host → 全部`（仅调用各模块注册器与装配宿主对象，不引用模块内部）。
- **S1 成集（ADR-0023）**：契约四件驻 `StarPie.Icons.Contracts`（零依赖薄契约），实现
  `IconAssetService` + 注册器 `IconsModuleRegistrar` 驻 `StarPie.Icons` runtime；消费方
  Dialogs/Wheel/Gestures/Programs/Host 的 csproj 显式引用 `StarPie.Icons.Contracts`；Icons
  runtime 只被 Host（组合根调注册器）/Tests 引用；`.lnk` SPI `IShortcutTargetResolver` 随 M3
  驻 Programs.Contracts（ADR-0023），Icons runtime 经契约边消费（Icons → Core 仅余 S2
  AppDataPaths 共享基建）。
- 模块 runtime 之间**零 ProjectReference**（ADR-0023）：跨模块依赖一律经 `*.Contracts`
  契约边（Dialogs→Programs 经 Programs.Contracts、M1/M2/M5→S6 经 Dialogs.Contracts、
  S1→M3 SPI 经 Programs.Contracts、M2→M4/Dialogs→M4 经 Theme.Contracts、M1→M2 经
  Wheel.Contracts、M2→M1 预览源经 Gestures.Contracts）；仅 Host（组合根）引用全部 runtime。
- S6 对话框契约在 `StarPie.Dialogs.Contracts`（`IDialogService` + 结果 record，纯 C#，
  ADR-0023），实现（DialogService/五对对话框 VM/Window/SpectrumCanvasBehavior）
  驻 `StarPie.Dialogs`；M 页面经 Dialogs.Contracts 的 `IDialogService`
  调用（Gestures/Shell/Wheel/Host csproj 显式引用），实现由 DialogsModuleRegistrar 注册，
  Host 组合根仅保留 `SetOwner(MainView)` 装配面。
- 共享放行清单（config 模型字段、i18n 键、消息/通知类型、图标资产；共享视图基础设施已去共享化，
  落点与资源缝放行面见 modules.md §2.3）维持 modules.md §2.3，不视为跨模块违规。

## 4. 导航与“壳”的分层语义（防混淆）

程序集化后导航契约与“壳”相关概念分四层，术语别混用：

| 层 | 归属 | 程序集 | 内容 |
|---|---|---|---|
| 导航目录/槽位契约 | S5（纯契约共享模块） | Core | NavigationCatalog、NavigationSlot/NavigationSlots、NavigationPageRegistration |
| 导航运行时/状态 | H1（宿主壳，与 R4/D3 同判据——单一消费方在 Host） | StarPie（exe） | NavigationStore、NavigationExecutor（含 INavigationExecutor）、MainViewModel、NavigationItemViewModel（命名空间不变） |
| 壳层服务与系统集成 | M5 | StarPie.Shell | 托盘、自启、内存、Advanced/About 设置面 |
| Host 壳窗口 | H1（宿主壳） | StarPie（exe） | MainView 全文件、ShellViewModel、App/AppHost/Composition（导航 VM 与主框架同窗，物理同居 Host） |

`MainView.xaml.cs` 与 `ShellViewModel` **归 Host 壳窗口**（ADR-0016 决策 6/7），不再归 M5；M5 只拥有壳层服务与设置面。
导航运行时（含主框架导航区 VM `MainViewModel`）随壳窗口同判据归 Host（R9）。

## 5. 导航架构（as-built：模块自治注册）

### 5.1 机制

- Core 提供目录契约 `NavigationCatalog`：`RegisterPage<TViewModel>(槽位, automationId, titleKey, iconData, …)`（纯契约共享模块，运行时不居 Core）。
- Host 提供导航执行入口（`NavigationExecutor`/`INavigationExecutor`）：随运行时归 H1 后为宿主内部件
  （不再是跨程序集"已批准解析缝"，见 [seams.md](seams.md)；第二消费方出现时按
  ADR-0023 契约归属判据裁决落点——属全局机制/数据入共享内核，属某模块出口契约下沉该模块
  Contracts（如 S6 先例：`IDialogService` 随实现方入 Dialogs.Contracts））。
- 模块注册器自报导航项与页面模板字典（`DataTemplate DataType=VM → View`）；Host 在 App 资源里**每模块一次** pack URI 静态合并。
- 新增页面 = 所属模块内部（注册器声明导航项 + 模板字典加条目），**不碰 Host**。
- 新增模块 = Host 登记：程序集引用 + 注册器调用 + 模板字典合并（各一次，放行共享面）。
- e2e `AutomationId` 沿用 `NavPage0..4`，随槽位稳定。

  as-built：`ShellModuleRegistrar`（RegisterNavigation + RegisterServices）与 `ShellPageTemplates.xaml`
  在 `StarPie.Shell`；`GesturesModuleRegistrar` 与 `GesturesPageTemplates.xaml` 在
  `StarPie.Gestures`；Host App.xaml 对两模块模板字典均经跨程序集 pack URI 合并；exe 内仅剩 Host
  外观聚合页的 HostModuleRegistrar/HostPageTemplates（留 Host）。

### 5.2 导航槽位表（正典）

| 槽位 | AutomationId | TitleKey | 页面 VM | 页面 View | 程序集 |
|---|---|---|---|---|---|
| 0 | `NavPage0` | `PageTrigger` | `BehaviorSettingsViewModel` | `TriggerSettingsPage` | M1 Gestures |
| 1 | `NavPage1` | `PageAppearance` | `AppearanceSettingsViewModel`（聚合壳） | `AppearanceSettingsPage` | Host |
| 2 | `NavPage2` | `PageGestures` | `ProfileListViewModel` | `GesturesSettingsPage` | M1 Gestures |
| 3 | `NavPage3` | `PageAdvanced` | `GeneralSettingsViewModel` | `AdvancedSettingsPage` | M5 Shell |
| 4 | `NavPage4` | `PageAbout` | `AboutViewModel` | `AboutSettingsPage` | M5 Shell |

缺失/重复/未知槽位由 Core 收口测试拦截；槽位表是侧边栏顺序唯一正典。MainViewModel 按目录注册
构造导航项，导航执行走 `INavigationExecutor` 目录执行缝；运行时主体（含 MainViewModel）在
Host、目录契约仍驻 Core，程序集归属见 §2/§4。

## 6. DI 与注册契约（as-built）

- **注册自治**：每个业务程序集暴露注册器（公开静态类，含 `RegisterServices(IServiceCollection)` 与 `RegisterNavigation(NavigationCatalog)`）；Host Composition 按固定顺序调用——`ShellModuleRegistrar`（M5：页面 VM 注册 + RegisterNavigation）、`ThemeModuleRegistrar`（M4：主题服务与主题设置子 VM 注册；无导航页）、`WheelModuleRegistrar`（M2：轮盘工厂 `IWheelFactory→WheelFactory` 与轮盘外观设置子 VM 注册；无导航页）、`GesturesModuleRegistrar`（M1：手势管线、触发+手势两页 VM 与 `IProfilePreviewSource` 别名注册，RegisterNavigation 自报槽位 0/2）、`ProgramsModuleRegistrar`（M3）、`DialogsModuleRegistrar`（S6）、`IconsModuleRegistrar`（S1）。
- **M3（`ProgramsModuleRegistrar`）**：RegisterServices 下放快捷方式解析契约
  `IShortcutTargetResolver→ShortcutResolver` 与 `IProgramScanner→ProgramScanner` 注册（契约随
  实现方驻 Programs.Contracts，ADR-0023），M3 无导航页故无 RegisterNavigation。
- **S6（`DialogsModuleRegistrar`）**：RegisterServices 下放
  `IDialogService→DialogService` 注册（工厂经容器解析 Dialogs.Contracts 契约、
  Programs.Contracts 的 `IProgramScanner`/`IShortcutTargetResolver` 与 Theme.Contracts 的
  `IThemeService`，ADR-0023），S6 无导航页故无 RegisterNavigation；组合根仅保留
  `DialogService.SetOwner(MainView)` 回填面。
- **S1（`IconsModuleRegistrar`，ADR-0023）**：RegisterServices 下放
  `IIconAssetService→IconAssetService` 注册（工厂经容器惰性解析 Programs.Contracts 的
  `IShortcutTargetResolver`，ADR-0023），S1 无导航页故无 RegisterNavigation；Host
  Composition 仅调注册器。
- **根解析集中**：Host Composition 仍唯一 `BuildServiceProvider` / `CreateAppHost`；模块不解析、不持容器。
- **已批准解析缝**：`WheelFactory`、`DialogService`、模块注册器（仅注册不解析）。导航目录执行缝
  不是跨程序集缝（`INavigationExecutor` 随运行时整体归 Host，为宿主内部件，见 [seams.md](seams.md)）。
- `AppHostDelegates` 是 Core 公开契约（`StarPie.Core/Services/AppHostDelegates.cs`）；
  Host 组合根以单例注册并在 AppHost 构造后回填实现。模块注册器只依赖 Core 契约与各自 Contracts，
  不在注册器内引用其它模块 runtime 类型。
- CreateAppHost 的解析清单目录化：遍历 `NavigationCatalog.Entries` 逐个 eager 解析页面 VM。
- 不引入子容器、Generic Host、Autofac、Prism（ADR-0016 决策 13）。

## 7. 可见性（as-built）

- 不引入 `InternalsVisibleTo`（layering.md 维持）。
- 模块公开面 = 注册器入口 + 被测 public 类型；内部实现细节保持 internal。
- Host 只引用模块注册器，不引用模块内部。
- 跨集必需的内部件（如 `AppThemePaletteManager`）单独裁决为 public 或改经接口注入。

  既有先例：Host AppHost 负责装配托盘对象，`TrayIconManager`/`TrayMenuEntry` 随迁后裁决为
  public（Host 装配面）；`AutostartRegistry` 仅由模块注册器接线，维持 internal。

  同判据先例：M4 的 `AppThemePaletteManager` 随迁后裁决为 **public**（Host `AppHost`
  构造时 `new` 并 `AttachPaletteApplier`，`ThemeService.AttachPaletteApplier` 同步公开）；
  模块内主题文件映射/缓存/冻结等实现细节保持私有。

  同判据先例：M2 的 `WheelFactory`/`IWheelFactory` 与外观设置子 VM 只经同集注册器
  接线/容器解析，维持 public（被测类型），**无新增 Host 装配面 public 裁决**——RadialWindow
  由 WheelFactory 在同集内创建，不经 Host 直接 new；`WheelPreviewRenderer` 深浅色探测改由
  调用方（Host 外观页）以 `bool` 传入（M2 不反向引用 Host `MainView`）。

  同判据先例：S6 的 `DialogService` 裁决 **public**——Host `AppHost` 建窗后调
  `SetOwner(MainView)` 惰性回填 Owner（ADR-0004），接口 `IDialogService` 不含 SetOwner（Owner
  是实现内部自由，不泄露进契约）；对话框 VM/Window 与 `SpectrumCanvasBehavior` 维持 public
  （被测/装配类型），无新增 InternalsVisibleTo。

## 9. 现状对照与差异登记

15 程序集现状以 §2 程序集地图、§3 依赖规则、§5 导航槽位（NavPage0..4）与 §7 可见性为正典；
概念模块归属见 [modules.md](modules.md)。程序集化落地历史见 ADR-0016、ADR-0023 与 git 历史，
本文不逐批登记。


## 参见 ADR

[0016](../adr/0016-assembly-split-target-and-roadmap.md)（程序集化目标态与分批执行）、[0015](../adr/0015-module-map-and-ownership.md)（12 模块地图与归属裁定）。
