# 程序集地图与程序集化收尾（目标态 = as-built）

> 本文记录程序集化目标态（[ADR-0016](../adr/0016-assembly-split-target-and-roadmap.md) +
> [ADR-0023](../adr/0023-module-contracts-hard-boundary-and-core-narrowing.md)）的地图视图：
> 目标程序集划分、程序集级依赖规则、导航槽位表与注册/可见性契约。

> **目标态变更（P1 起）**：15 集并入三集（StarPie.Sdk/StarPie.Host/StarPie.Ui）+ `StarPie.Sdk.Wpf` + 能力插件，见 [ADR-0027](../adr/0027-plugin-architecture-and-host-sdk-ui-split.md) 与 [plugins.md](plugins.md)。P1.2/#111 已建四集骨架，并把 exe 工程目录/文件改名为 `StarPie.Ui`（程序集名与发布产物保持 `StarPie`）；P1.3/#112 已把 headless 契约/模型收口入 `StarPie.Sdk`（见 §2 与 §3）。P1.3–P1.10 分批归并期间，旧 15 集仍是未搬迁代码的 as-built 主体，P1.11 按目标态回填全文。
>
> **程序集现状**：四集骨架（`StarPie.Sdk`/`StarPie.Sdk.Wpf`/`StarPie.Host` + exe `StarPie.Ui`）
> + 旧 15 集（Host/Core + 5 业务 runtime + `StarPie.Dialogs` + `StarPie.Icons` + 各 `*.Contracts`，
> 划分见 §2）。代码现状以 §2–§7/§9 与各叶子（`docs/architecture/*.md`）
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

## 2. 程序集地图（as-built：旧 15 集 + 四集骨架）

| 程序集 | 形态 | 承载 |
|---|---|---|
| `StarPie`（项目 `StarPie.Ui`） | WinExe | Ui 集与组合根（App/AppHost/Composition/DevInstance）；Host 壳窗口（`MainView` 全文件 + `ShellViewModel`）；导航运行时主体（`Services/Navigation/`：NavigationStore/NavigationExecutor（含 INavigationExecutor）；`ViewModels/Navigation/`：MainViewModel/NavigationItemViewModel，命名空间不变；Appearance 聚合页；共享 UI 基建（通用转换器 `Views/Converters/` + `ModernControls.xaml` `Views/Styles/`（App.xaml 本地单点实例化/合并、资源 key 不变）；仅保留 DialogService.SetOwner 回填等宿主装配面 |
| `StarPie.Sdk` | 类库（net10.0） | SDK 集（P1.2/#111 骨架，P1.3/#112 headless 收口）：零 WPF、零第三方包、零 ProjectReference；承载跨集共享的纯托管契约/模型/DTO——`Models/`（AppConfig/WheelProfile/ActionItem/CustomColorPreset/ColorMath/GesturePoint）、`Services/`（AppHostDelegates、Messages、Navigation 目录与槽位契约、Dialogs 契约与 6 结果 record、IWheelFactory）、`ViewModels/`（Pages 预览源接口、Wheel 轮盘只读接口）；迁移期镜像旧相对路径、命名空间保持 `StarPie.*` 不变，导出面与类型唯一性由 `SdkBoundaryTests` 收口 |
| `StarPie.Sdk.Wpf` | WPF 类库 | SDK 的 WPF 类型契约面骨架（P1.2/#111）：唯一允许的 ProjectReference 是 `StarPie.Sdk`；不产出 XAML；P1.4 起迁入 WPF 契约件 |
| `StarPie.Host` | 类库（net10.0） | 宿主内核骨架（P1.2/#111）：零 WPF（不引用 `StarPie.Sdk.Wpf`），ProjectReference 只许 `StarPie.Sdk`；P1.5 起迁入内核运行时 |
| `StarPie.Core` | WPF 类库 | 共享内核运行时件（P1.3/#112 收窄）：S2 Configuration（配置读写/防抖保存/AppDataPaths）与 S3 Localization（含 `Strings*.resx` 与生成器），P1.5 起迁 `StarPie.Host` 内核；契约与模型（Models/Messages/Navigation 目录与槽位契约/宿主回调 `Services/AppHostDelegates`）已随 P1.3/#112 迁 `StarPie.Sdk`，本集经 SDK 单向引用取用。**不含共享 UI 基建**（已去共享化）、**S1 图标资产**（ADR-0023 独立成集）与**模块出口契约**（扫描/.lnk SPI 与主题契约分别驻 Programs.Contracts/Theme.Contracts，WPF 面留 P1.4） |
| `StarPie.Icons.Contracts` | WPF 类库 | S1 图标契约：`IIconAssetService`/`IconCatalog`/`CustomIconItem`/`VectorIconItem`（命名空间 `StarPie.Services.Icons` 不变；零 ProjectReference——薄契约，按需 WPF） |
| `StarPie.Icons` | WPF 类库 | S1 图标实现：`IconAssetService` + `IconsModuleRegistrar`；Icons → `StarPie.Sdk`（IconCatalog/条目类型，P1.3/#112）+ Icons.Contracts + Programs.Contracts（SPI 契约边，ADR-0023）+ Core（S2 AppDataPaths 共享基建）单向；实现 runtime 只被 Host/注册器/测试引用 |
| `StarPie.Programs.Contracts` | WPF 类库 | M3 扫描/SPI 契约：`IProgramScanner`/`ProgramEntry`/`ProgramCatalog`（`Services/Programs/`，命名空间 `StarPie.Services.Programs` 不变）+ `IShortcutTargetResolver`（`Services/Icons/`，命名空间 `StarPie.Services.Icons` 不变；零 ProjectReference——薄契约，按需 WPF：ProgramEntry.IconSource，ADR-0023） |
| `StarPie.Dialogs.Contracts` | 类库（纯 C#） | S6 对话框契约工程（ADR-0023）：`IDialogService` + 6 结果 record 已随 P1.3/#112 迁 `StarPie.Sdk/Services/Dialogs/`（命名空间 `StarPie.Services.Dialogs` 不变）；本工程暂留空壳（无源码、无 ProjectReference），待 P1.10 撤销 |
| `StarPie.Dialogs` | 类库 | S6 对话框实现：DialogService、五对对话框 VM/Window、SpectrumCanvasBehavior（契约随实现方独立成集、P1.3/#112 收口入 `StarPie.Sdk`，ADR-0023；Dialogs → Sdk + Programs.Contracts + Icons.Contracts + Core（S2/S3/S4）+ Theme.Contracts（ADR-0023）单向，不引用任何 runtime/Host/其它模块 runtime） |
| `StarPie.Gestures.Contracts` | 类库 | M1 预览 Profile 契约工程（ADR-0023）：`IProfilePreviewSource` 已随 P1.3/#112 迁 `StarPie.Sdk/ViewModels/Pages/`（命名空间 `StarPie.ViewModels.Pages` 不变）；本工程暂留空壳（无源码、无 ProjectReference），待 P1.6 撤销 |
| `StarPie.Gestures` | 类库 | M1 手势与动作：Services/Gestures、Services/Actions、Trigger/Gestures 设置页、热键录制控件 HotkeyRecorderBox（控件 + 样式字典 `Views/Styles/HotkeyRecorderBox.xaml`）；Gestures → Core 单向 + `StarPie.Sdk`（预览源/轮盘/对话框契约与模型，P1.3/#112）+ Icons.Contracts（M1→M2 runtime 允许边经 SDK 清零，ADR-0023） |
| `StarPie.Wheel.Contracts` | 类库 | M2 轮盘契约工程（ADR-0023）：`IWheelFactory`/`IWheelViewModel`/`IWheelAppearanceState` 已随 P1.3/#112 迁 `StarPie.Sdk/Services|ViewModels/Wheel/`（命名空间不变）；本工程暂留空壳（无源码、无 ProjectReference），待 P1.7 撤销 |
| `StarPie.Wheel` | 类库 | M2 轮盘与渲染：WheelViewModel/RadialWindow/Renderers/WheelPalette*/WheelGeometry/WheelFactory（含 D5 工厂收编；Wheel → Core + `StarPie.Sdk`（轮盘/预览/对话框契约与模型，P1.3/#112）+ Theme.Contracts + Icons.Contracts 单向，M2→M4 runtime 允许边经 Theme.Contracts 清零，ADR-0023） |
| `StarPie.Programs` | 类库 | M3 程序扫描与目录实现：ProgramScanner/ShortcutResolver + ProgramsModuleRegistrar（契约本体驻 Programs.Contracts，ADR-0023 起 M3 → Programs.Contracts + Icons.Contracts 单向，不再引用 Core/其它模块 runtime） |
| `StarPie.Theme.Contracts` | WPF 类库 | M4 界面主题契约：`IThemeService`（`Services/Shell/`，命名空间 `StarPie.Services.Shell` 不变；零 ProjectReference——薄契约，按需 WPF：ApplyWindowTheme(FrameworkElement)，ADR-0023） |
| `StarPie.Theme` | 类库 | M4 界面主题：ThemeService/Themes XAML/InterfaceThemeSettingsViewModel/AppThemePaletteManager（裁决 public——Host AppHost 装配面；Theme → Core + `StarPie.Sdk`（配置/消息模型，P1.3/#112）+ Theme.Contracts 单向，实现自有契约） |
| `StarPie.Shell` | 类库 | M5 壳层服务与设置面：TrayIconManager/AutostartRegistry/MemoryOptimizer/General 设置页（`MainView` 壳窗口与 `ShellViewModel` **不**随 M5，留 Host）；Shell → Core + `StarPie.Sdk`（对话框契约/导航目录/模型，P1.3/#112）单向 |

## 3. 程序集级依赖规则

### 四集基线（P1.2/#111 起；机械断言在 `StarPie.Tests/FourSetBoundaryTests.cs` 与 `RuntimeNoCrossReferenceTests.cs`）

```text
StarPie.Ui（WinExe，程序集名 StarPie；唯一含 XAML 与入口）
     ├──→ StarPie.Host ──→ StarPie.Sdk
     ├──→ StarPie.Sdk
     └──→ StarPie.Sdk.Wpf ──→ StarPie.Sdk
```

- Ui 是唯一组合根：必须显式引用 `StarPie.Sdk`/`StarPie.Sdk.Wpf`/`StarPie.Host`，且是归并期
  唯一可直接引用旧 15 集 runtime 的工程；其余工程不得引用 Ui。
- `StarPie.Sdk` 零 WPF、零第三方包（csproj 无 `PackageReference`，程序集引用面只含平台程序集，
  TFM 为 `net10.0` 无 windows 平台投影）、零 ProjectReference。
- `StarPie.Host` 零 WPF：TFM `net10.0`，不引用 `StarPie.Sdk.Wpf`（plugins.md §5.1 约束 7），
  ProjectReference 只许 `StarPie.Sdk`。
- `StarPie.Sdk.Wpf` 是 WPF 类型契约面（`UseWPF`、windows TFM、带 Windows 平台投影），不产出
  XAML；ProjectReference 只许 `StarPie.Sdk`。
- 归并期（P1.3–P1.10）三集不得引用旧 15 集 runtime（跨集只经 SDK）；旧集只被 Ui 组合根引用，
  并可经 SDK 取已收口的 headless 契约/模型（旧集 → SDK 单向，P1.3/#112 起；无反向引用）。

### 旧 15 集 as-built（P1.3–P1.10 归并期口径）

```text
StarPie (Host/exe) ──→ StarPie.Sdk + StarPie.Core
     │──→ StarPie.Dialogs ──→ StarPie.Sdk + StarPie.Programs.Contracts + StarPie.Icons.Contracts + StarPie.Core + StarPie.Theme.Contracts   （Dialogs→M4 runtime 允许边经 Theme.Contracts 清零；S6 契约 P1.3/#112 收口于 SDK，ADR-0023）
     │──→ StarPie.Gestures ──→ StarPie.Sdk + StarPie.Core + StarPie.Icons.Contracts   （M1→M2 runtime 允许边经 SDK 的 IWheelFactory/IWheelViewModel 清零；P1.3/#112）
     │──→ StarPie.Wheel ──→ StarPie.Sdk + StarPie.Core + StarPie.Theme.Contracts + StarPie.Icons.Contracts   （M2→M4 runtime 允许边经 Theme.Contracts 清零；P1.3/#112）
     │──→ StarPie.Programs ──→ StarPie.Programs.Contracts（零依赖） + StarPie.Icons.Contracts   （M3 不再引用 Core，ADR-0023）
     │──→ StarPie.Shell ──→ StarPie.Sdk + StarPie.Core
     │──→ StarPie.Theme ──→ StarPie.Sdk + StarPie.Core + StarPie.Theme.Contracts   （实现自有契约，ADR-0023）
     │──→ StarPie.Icons ──→ StarPie.Sdk + StarPie.Icons.Contracts（零依赖） + StarPie.Programs.Contracts + StarPie.Core   （SPI 经 Programs.Contracts 契约边；Core 仅 S2 AppDataPaths，ADR-0023）
     └────────────────────────────────────────→ StarPie.Sdk + StarPie.Core

     StarPie.Dialogs/Gestures/Wheel.Contracts：P1.3/#112 契约迁 SDK 后暂留空壳（无源码、无消费方
     引用；测试显式加载以断言空壳与全仓类型唯一），待 P1.6/P1.7/P1.10 撤销。
```

- 模块 runtime 对 Core/契约**单向**：M*/S* runtime 只引用自身契约与经 `*.Contracts` 消费的能力
  （扫描/.lnk 经 Programs.Contracts、主题经 Theme.Contracts、图标资产经 Icons.Contracts；
  P1.3/#112 起对话框/轮盘工厂与 VM 接口/预览源契约统一经 `StarPie.Sdk`，原
  Dialogs/Gestures/Wheel.Contracts 工程暂留空壳），不反向引用
  Host/其它模块 runtime；托盘深色探针、dev 分支等宿主能力经组合根注入委托或 Core
  `AppDataPaths` 回填缝提供；`Host → 全部`（仅调用各模块注册器与装配宿主对象，不引用模块内部）。
- **S1 成集（ADR-0023）**：契约四件驻 `StarPie.Icons.Contracts`（零依赖薄契约），实现
  `IconAssetService` + 注册器 `IconsModuleRegistrar` 驻 `StarPie.Icons` runtime；消费方
  Dialogs/Wheel/Gestures/Programs/Host 的 csproj 显式引用 `StarPie.Icons.Contracts`；Icons
  runtime 只被 Host（组合根调注册器）/Tests 引用；`.lnk` SPI `IShortcutTargetResolver` 随 M3
  驻 Programs.Contracts（ADR-0023），Icons runtime 经契约边消费（P1.3/#112 起 Icons → Sdk 取
  IconCatalog/条目类型；Core 仅余 S2 AppDataPaths 共享基建）。
- 模块 runtime 之间**零 ProjectReference**（ADR-0023）：跨模块依赖一律经 `StarPie.Sdk`/`*.Contracts`
  契约边（Dialogs→Programs 经 Programs.Contracts、M1/M2/M5→S6 与 M2→M1 预览源、M1→M2 经
  StarPie.Sdk、S1→M3 SPI 经 Programs.Contracts、M2→M4/Dialogs→M4 经 Theme.Contracts）；
  仅 Host（组合根）引用全部 runtime。
- S6 对话框契约（`IDialogService` + 结果 record，纯 C#，ADR-0023）原驻 `StarPie.Dialogs.Contracts`、
  P1.3/#112 随 SDK 收口迁 `StarPie.Sdk`，实现（DialogService/五对对话框 VM/Window/
  SpectrumCanvasBehavior）驻 `StarPie.Dialogs`；M 页面经 SDK 的 `IDialogService`
  调用（Gestures/Shell/Wheel/Host csproj 显式引用 SDK），实现由 DialogsModuleRegistrar 注册，
  Host 组合根仅保留 `SetOwner(MainView)` 装配面。
- 共享放行清单（config 模型字段、i18n 键、消息/通知类型、图标资产；共享视图基础设施已去共享化，
  落点与资源缝放行面见 modules.md §2.3）维持 modules.md §2.3，不视为跨模块违规。

## 4. 导航与“壳”的分层语义（防混淆）

程序集化后导航契约与“壳”相关概念分四层，术语别混用：

| 层 | 归属 | 程序集 | 内容 |
|---|---|---|---|
| 导航目录/槽位契约 | S5（纯契约共享模块） | Core | NavigationCatalog、NavigationSlot/NavigationSlots、NavigationPageRegistration |
| 导航运行时/状态 | H1（宿主壳，与 R4/D3 同判据——单一消费方在 Host） | StarPie（exe） | NavigationStore、NavigationExecutor（含 INavigationExecutor）、MainViewModel、NavigationItemViewModel（命名空间不变） |
| 壳层服务与系统集成 | M5 | StarPie.Shell | 托盘、自启、内存、Advanced 设置面 |
| Host 壳窗口 | H1（宿主壳） | StarPie（exe） | MainView 全文件、ShellViewModel、App/AppHost/Composition（导航 VM 与主框架同窗，物理同居 Host） |

`MainView.xaml.cs` 与 `ShellViewModel` **归 Host 壳窗口**（ADR-0016 决策 6/7），不再归 M5；M5 只拥有壳层服务与设置面。
导航运行时（含主框架导航区 VM `MainViewModel`）随壳窗口同判据归 Host（R9）。

## 5. 导航架构（as-built：模块自治注册）

### 5.1 机制

- `StarPie.Sdk` 提供目录契约 `NavigationCatalog`：`RegisterPage<TViewModel>(槽位, automationId, titleKey, iconData, …)`（纯契约，P1.3/#112 自 Core 收口；运行时不居 SDK）。
- Host 提供导航执行入口（`NavigationExecutor`/`INavigationExecutor`）：随运行时归 H1 后为宿主内部件
  （不再是跨程序集"已批准解析缝"，见 [seams.md](seams.md)；第二消费方出现时按
  ADR-0023 契约归属判据裁决落点——属全局机制/数据入共享内核，属某模块出口契约下沉该模块
  Contracts（如 S6 先例：`IDialogService` 随实现方独立成集、P1.3/#112 收口入 `StarPie.Sdk`））。
- 模块注册器自报导航项与页面模板字典（`DataTemplate DataType=VM → View`）；Host 在 App 资源里**每模块一次** pack URI 静态合并。
- 新增页面 = 所属模块内部（注册器声明导航项 + 模板字典加条目），**不碰 Host**。
- 新增模块 = Host 登记：程序集引用 + 注册器调用 + 模板字典合并（各一次，放行共享面）。
- e2e `AutomationId` 沿用 `NavPage0..3`，随槽位稳定。

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

缺失/重复/未知槽位由收口测试拦截；槽位表是侧边栏顺序唯一正典。MainViewModel 按目录注册
构造导航项，导航执行走 `INavigationExecutor` 目录执行缝；运行时主体（含 MainViewModel）在
Host、目录契约驻 `StarPie.Sdk`（P1.3/#112 收口），程序集归属见 §2/§4。

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
- `AppHostDelegates` 是 SDK 公开契约（`StarPie.Sdk/Services/AppHostDelegates.cs`，P1.3/#112 自 Core 收口）；
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

15 程序集现状以 §2 程序集地图、§3 依赖规则、§5 导航槽位（NavPage0..3）与 §7 可见性为正典；
概念模块归属见 [modules.md](modules.md)。程序集化落地历史见 ADR-0016、ADR-0023 与 git 历史，
本文不逐批登记。


## 参见 ADR

[0016](../adr/0016-assembly-split-target-and-roadmap.md)（程序集化目标态与分批执行）、[0015](../adr/0015-module-map-and-ownership.md)（12 模块地图与归属裁定）。
