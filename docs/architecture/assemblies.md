# 程序集地图与程序集化收尾（目标态 = as-built）

> 本文记录程序集化目标态（[ADR-0016](../adr/0016-assembly-split-target-and-roadmap.md) +
> [ADR-0023](../adr/0023-module-contracts-hard-boundary-and-core-narrowing.md)）的地图视图：
> 目标程序集划分、程序集级依赖规则、导航槽位表与注册/可见性契约。

> **目标态变更（P1 起）**：15 集并入三集（StarPie.Sdk/StarPie.Host/StarPie.Ui）+ `StarPie.Sdk.Wpf` + 能力插件，见 [ADR-0027](../adr/0027-plugin-architecture-and-host-sdk-ui-split.md) 与 [plugins.md](plugins.md)。P1.2/#111 已建四集骨架，并把 exe 工程目录/文件改名为 `StarPie.Ui`（程序集名与发布产物保持 `StarPie`）；P1.3/#112 已把 headless 契约/模型收口入 `StarPie.Sdk`，P1.4/#113 已把 WPF 契约件收口入 `StarPie.Sdk.Wpf`（见 §2 与 §3）。P1.3–P1.10 分批归并期间，旧 15 集仍是未搬迁代码的 as-built 主体，P1.11 按目标态回填全文。
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
| `StarPie`（项目 `StarPie.Ui`） | WinExe | Ui 集与组合根（App/AppHost/Composition/DevInstance）；Host 壳窗口（`MainView` 全文件 + `ShellViewModel`）；导航运行时主体（`Services/Navigation/`：NavigationStore/NavigationExecutor（含 INavigationExecutor）；`ViewModels/Navigation/`：MainViewModel/NavigationItemViewModel，命名空间不变）；Appearance 聚合页；共享 UI 基建（通用转换器 `Views/Converters/` + `ModernControls.xaml` `Views/Styles/`（App.xaml 本地单点实例化/合并、资源 key 不变）；S1/M3 的 WPF 图像构造与扫描消费；M4 的 `IThemeService` 实现 `Services/Shell/`、调色板适配器 `Adapters/`、五套主题字典 `Themes/`、主题设置子 VM 与 `ThemeModuleRegistrar`；仅保留 DialogService.SetOwner 回填等宿主装配面 |
| `StarPie.Sdk` | 类库（net10.0） | SDK 集（P1.2/#111 骨架，P1.3/#112 headless 收口）：零 WPF、零第三方包、零 ProjectReference；承载跨集共享的纯托管契约/模型/DTO——`Models/`（AppConfig/WheelProfile/ActionItem/CustomColorPreset/ColorMath/GesturePoint）、`Services/`（AppHostDelegates、Messages、Navigation 目录与槽位契约、Dialogs 契约与 6 结果 record、IWheelFactory、Icons 条目类型与 .lnk SPI、Programs 扫描契约与纯规则）、`ViewModels/`（Pages 预览源接口、Wheel 轮盘只读接口）；迁移期镜像旧相对路径、命名空间保持 `StarPie.*` 不变，导出面与类型唯一性由 `SdkBoundaryTests` 收口 |
| `StarPie.Sdk.Wpf` | WPF 类库 | SDK 的 WPF 类型契约面（P1.2/#111 骨架，P1.4/#113 收口）：承载跨集共享的 WPF 契约件——`Services/Icons/`（IIconAssetService）、`Services/Shell/`（IThemeService）、`Compatibility/`（UiSdkAbi 主次版本/兼容判定 + DefaultAlcPolicy 默认 ALC 统一加载政策）；唯一允许的 ProjectReference 是 `StarPie.Sdk`；不产出 XAML；导出面与 ABI 政策由 `SdkWpfBoundaryTests` 收口 |
| `StarPie.Host` | 类库（net10.0） | 宿主内核：零 WPF、零 XAML（不引用 `StarPie.Sdk.Wpf`），ProjectReference 只许 `StarPie.Sdk`；承载 `Kernel/Configuration`（S2 配置读写/防抖落盘接缝/AppDataPaths）与 `Kernel/Localization`（S3 本地化实现 + `Strings*.resx` 四语言，命名空间 `StarPie.Kernel.*`），S1/M3 的 WPF-free 逻辑——`Icons/`（`IconCatalog` 静态纯目录 + `CustomIconStore` 自定义图标目录，命名空间 `StarPie.Icons`）与 `Programs/`（`ProgramScanner` 八源扫描 + `ShortcutResolver` .lnk 解析，命名空间 `StarPie.Programs`），以及 M4 的主题引擎 `Themes/`（`ThemeEngine`，命名空间 `StarPie.Themes`）与宿主→Ui 端口 `Ports/`（`IThemeApplier`，命名空间 `StarPie.Ports`）；件件可 headless 直接构造；导出面与零 WPF 泄漏由 `HostBoundaryTests` 收口 |
| `StarPie.Core` | WPF 类库 | 设计期投影壳（P1.5/#114）：零导出类型、零运行时件，只余设计期字符串字典 `Services/Localization/DesignTimeStrings.xaml`（Page 编译惰性 BAML，仅设计期合并，ADR-0025 的唯一 XAML 例外）与生成脚本；归并期含 UI 工程保留本集引用只为解析该字典的 pack URI（非运行时依赖）。契约/模型已迁 `StarPie.Sdk`（P1.3/#112）、WPF 契约件迁 `StarPie.Sdk.Wpf`（P1.4/#113），运行时迁 `StarPie.Host` |
| `StarPie.Dialogs.Contracts` | 类库（纯 C#） | S6 对话框契约工程（ADR-0023）：`IDialogService` + 6 结果 record 已随 P1.3/#112 迁 `StarPie.Sdk/Services/Dialogs/`（命名空间 `StarPie.Services.Dialogs` 不变）；本工程暂留空壳（无源码、无 ProjectReference），待 P1.10 撤销 |
| `StarPie.Dialogs` | 类库 | S6 对话框实现：DialogService、五对对话框 VM/Window、SpectrumCanvasBehavior（契约随实现方独立成集、P1.3/#112 收口入 `StarPie.Sdk`，ADR-0023；Dialogs → Sdk（对话契约与程序扫描/.lnk 契约）+ Sdk.Wpf（图标资产与主题契约面）+ `StarPie.Host`（S2/S3 与图标目录/程序扫描实现）单向，不引用任何其它模块 runtime；`StarPie.Core` 引用仅设计期资源锚 pack URI，非运行时依赖） |
| `StarPie.Gestures.Contracts` | 类库 | M1 预览 Profile 契约工程（ADR-0023）：`IProfilePreviewSource` 已随 P1.3/#112 迁 `StarPie.Sdk/ViewModels/Pages/`（命名空间 `StarPie.ViewModels.Pages` 不变）；本工程暂留空壳（无源码、无 ProjectReference），待 P1.6 撤销 |
| `StarPie.Gestures` | 类库 | M1 手势与动作：Services/Gestures、Services/Actions、Trigger/Gestures 设置页、热键录制控件 HotkeyRecorderBox（控件 + 样式字典 `Views/Styles/HotkeyRecorderBox.xaml`）；Gestures → `StarPie.Host`（S2/S3）单向 + `StarPie.Sdk`（预览源/轮盘/对话框契约与模型，P1.3/#112）+ `StarPie.Sdk.Wpf`（S1 图标契约面，P1.4/#113；M1→M2 runtime 允许边经 SDK 清零，ADR-0023）+ Core（仅设计期资源锚） |
| `StarPie.Wheel.Contracts` | 类库 | M2 轮盘契约工程（ADR-0023）：`IWheelFactory`/`IWheelViewModel`/`IWheelAppearanceState` 已随 P1.3/#112 迁 `StarPie.Sdk/Services|ViewModels/Wheel/`（命名空间不变）；本工程暂留空壳（无源码、无 ProjectReference），待 P1.7 撤销 |
| `StarPie.Wheel` | 类库 | M2 轮盘与渲染：WheelViewModel/RadialWindow/Renderers/WheelPalette*/WheelGeometry/WheelFactory（含 D5 工厂收编；Wheel → `StarPie.Host`（S2/S3）+ `StarPie.Sdk`（轮盘/预览/对话框契约与模型，P1.3/#112）+ `StarPie.Sdk.Wpf`（IThemeService 与 S1 图标契约面）单向，ADR-0023；Core 引用仅设计期资源锚） |
| `StarPie.Shell` | 类库 | M5 壳层服务与设置面：TrayIconManager/AutostartRegistry/MemoryOptimizer/General 设置页（`MainView` 壳窗口与 `ShellViewModel` **不**随 M5，留 Host）；Shell → `StarPie.Host`（S2/S3）+ `StarPie.Sdk`（对话框契约/导航目录/模型，P1.3/#112）单向（`StarPie.Core` 引用仅设计期资源锚 pack URI） |

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
  XAML；ProjectReference 只许 `StarPie.Sdk`；承载主题/图标/扫描 WPF 契约件与 ABI/装载政策
  （`Compatibility/`：UiSdkAbi 主次版本兼容判定、DefaultAlcPolicy 默认 ALC 统一加载），引用面
  只含平台/WPF 程序集（`SdkWpfBoundaryTests`/`RuntimeNoCrossReferenceTests` 机械断言）。
- 归并期（P1.3–P1.10）三集不得引用旧 15 集 runtime（跨集只经 SDK）；旧集只被 Ui 组合根与测试
  引用，并可经 SDK/Sdk.Wpf 契约面与 Host 内核取已收口/已归并件（旧集 → SDK/Sdk.Wpf/Host 单向；
  无反向引用）。

### 旧 15 集 as-built（P1.3–P1.10 归并期口径）

```text
StarPie (Ui/exe) ──→ StarPie.Sdk + StarPie.Sdk.Wpf + StarPie.Host + StarPie.Core（仅设计期资源锚）
     │──→ StarPie.Dialogs ──→ StarPie.Sdk + StarPie.Sdk.Wpf + StarPie.Host（+ Core 仅设计期资源锚）   （窗口主题应用经 Sdk.Wpf 的 IThemeService 契约面；S6 契约收口于 SDK、WPF 契约件收口于 Sdk.Wpf，ADR-0023）
     │──→ StarPie.Gestures ──→ StarPie.Sdk + StarPie.Sdk.Wpf + StarPie.Host（+ Core 仅设计期资源锚）   （M1→M2 runtime 允许边经 SDK 的 IWheelFactory/IWheelViewModel 清零；S1 契约面 P1.4/#113）
     │──→ StarPie.Wheel ──→ StarPie.Sdk + StarPie.Sdk.Wpf + StarPie.Host（+ Core 仅设计期资源锚）   （窗口主题应用经 Sdk.Wpf 的 IThemeService 契约面）
     │──→ StarPie.Shell ──→ StarPie.Sdk + StarPie.Host（+ Core 仅设计期资源锚）
     └────────────────────────────────────────→ StarPie.Sdk + StarPie.Sdk.Wpf + StarPie.Host + StarPie.Core（仅设计期资源锚）

     StarPie.Dialogs/Gestures/Wheel.Contracts：契约迁 SDK 后暂留空壳（无源码、无消费方引用；
     测试显式加载以断言空壳与全仓类型唯一），分别待 P1.6/P1.7/P1.10 撤销；
     StarPie.Icons/Programs 与 StarPie.Theme/Theme.Contracts 已撤销（M4 引擎归 Host、
     字典/VM 归 Ui；S1/M3 的 WPF-free 逻辑归 Host、图像构造归 Ui；契约归 SDK/Sdk.Wpf）。
```

- 模块 runtime 对内核/契约**单向**：M*/S* runtime 只引用自身契约与经 `StarPie.Sdk`/
  `StarPie.Sdk.Wpf` 消费的契约面能力（扫描/.lnk 与图标条目经 SDK，主题与图标资产服务经
  Sdk.Wpf；对话框/轮盘工厂与 VM 接口/预览源契约统一经 `StarPie.Sdk`，原
  Dialogs/Gestures/Wheel.Contracts 工程暂留空壳），不反向引用
  其它模块 runtime；P1.5/#114 起内核运行时（S2/S3）在 `StarPie.Host`，旧集 → Host 是归并期
  过渡边（模块 runtime 尚未拆入 Ui）；托底深色探针、dev 分支等宿主能力经组合根注入委托或
  `AppDataPaths` 回填缝提供；`Ui → 全部`（仅调用各模块注册器与装配宿主对象，不引用模块内部）。
- **S1/M3 归并（不再独立成集）**：`IIconAssetService` 驻 `StarPie.Sdk.Wpf`；图标条目类型与
  `.lnk` SPI 驻 `StarPie.Sdk/Services/Icons/`；静态纯目录 `IconCatalog` 与自定义图标目录
  `CustomIconStore` 驻 `StarPie.Host/Icons/`；WPF 图像构造 `IconAssetService` 驻
  `StarPie.Ui/Services/Icons/`（组合内核图标目录与 .lnk 契约）。M3 契约（`IProgramScanner`/
  `ProgramEntry`/`ProgramCatalog`）驻 `StarPie.Sdk/Services/Programs/`，实现（`ProgramScanner`/
  `ShortcutResolver`）驻 `StarPie.Host/Programs/`。两模块的 DI 注册回组合根直登记；
  消费方（Dialogs/Wheel/Gestures）经契约面与内核实现消费，零 runtime 互引。
- **M4 归并（不再独立成集）**：主题引擎 `ThemeEngine` 与宿主→Ui 端口 `IThemeApplier` 驻
  `StarPie.Host/Themes|Ports/`（零 WPF）；`IThemeService` 实现 `ThemeService`、调色板适配器
  `AppThemePaletteManager`、五套主题字典与主题设置子 VM 驻 `StarPie.Ui`；DI 注册由
  `ThemeModuleRegistrar` 下放（无导航页）。
- 模块 runtime 之间**零 ProjectReference**（ADR-0023）：跨模块依赖一律经 `StarPie.Sdk`/
  `StarPie.Sdk.Wpf` 契约边（对话框链的程序扫描/.lnk 与图标条目经 SDK、主题与图标资产
  服务经 Sdk.Wpf；M1/M2/M5→S6 与 M2→M1 预览源、M1→M2 经 StarPie.Sdk）；仅 Host（组合根）
  引用全部 runtime。
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
| 导航目录/槽位契约 | S5（纯契约共享模块） | `StarPie.Sdk`（P1.3/#112 收口） | NavigationCatalog、NavigationSlot/NavigationSlots、NavigationPageRegistration |
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

- **注册自治**：每个业务程序集暴露注册器（公开静态类，含 `RegisterServices(IServiceCollection)` 与 `RegisterNavigation(NavigationCatalog)`）；Host Composition 按固定顺序调用——`ShellModuleRegistrar`（M5：页面 VM 注册 + RegisterNavigation）、`ThemeModuleRegistrar`（M4：主题服务与主题设置子 VM 注册；无导航页）、`WheelModuleRegistrar`（M2：轮盘工厂 `IWheelFactory→WheelFactory` 与轮盘外观设置子 VM 注册；无导航页）、`GesturesModuleRegistrar`（M1：手势管线、触发+手势两页 VM 与 `IProfilePreviewSource` 别名注册，RegisterNavigation 自报槽位 0/2）、`DialogsModuleRegistrar`（S6）；S1/M3 已归并撤销，其服务由组合根直登记（两模块无注册器、无导航页）。
- **M3（程序扫描，已归并）**：组合根直登记
  `IShortcutTargetResolver→ShortcutResolver` 与 `IProgramScanner→ProgramScanner`
  （契约驻 `StarPie.Sdk/Services/Programs|Icons/`，实现在 `StarPie.Host/Programs/`）。
- **S6（`DialogsModuleRegistrar`）**：RegisterServices 下放
  `IDialogService→DialogService` 注册（工厂经容器解析 SDK 的程序扫描/.lnk 契约与 Sdk.Wpf 的
  `IIconAssetService`/`IThemeService` 契约面），S6 无导航页
  故无 RegisterNavigation；组合根仅保留 `DialogService.SetOwner(MainView)` 回填面。
- **S1（图标资产，已归并）**：组合根直登记内核 `CustomIconStore`（`StarPie.Host/Icons/`）与
  Ui 侧 `IIconAssetService→IconAssetService`（`StarPie.Ui/Services/Icons/`，构造解析内核图标
  目录与 `IShortcutTargetResolver`）。
- **根解析集中**：Host Composition 仍唯一 `BuildServiceProvider` / `CreateAppHost`；模块不解析、不持容器。
- **已批准解析缝**：`WheelFactory`、`DialogService`、模块注册器（仅注册不解析）。导航目录执行缝
  不是跨程序集缝（`INavigationExecutor` 随运行时整体归 Host，为宿主内部件，见 [seams.md](seams.md)）。
- `AppHostDelegates` 是 SDK 公开契约（`StarPie.Sdk/Services/AppHostDelegates.cs`，P1.3/#112 自 Core 收口）；
  Host 组合根以单例注册并在 AppHost 构造后回填实现。模块注册器只依赖 SDK/Sdk.Wpf 契约面与
  Host 内核面，不在注册器内引用其它模块 runtime 类型。
- CreateAppHost 的解析清单目录化：遍历 `NavigationCatalog.Entries` 逐个 eager 解析页面 VM。
- 不引入子容器、Generic Host、Autofac、Prism（ADR-0016 决策 13）。

## 7. 可见性（as-built）

- 不引入 `InternalsVisibleTo`（layering.md 维持）。
- 模块公开面 = 注册器入口 + 被测 public 类型；内部实现细节保持 internal。
- Host 只引用模块注册器，不引用模块内部。
- 跨集必需的内部件（如 `TrayIconManager`）单独裁决为 public 或改经接口注入。

  既有先例：Host AppHost 负责装配托盘对象，`TrayIconManager`/`TrayMenuEntry` 随迁后裁决为
  public（Host 装配面）；`AutostartRegistry` 仅由模块注册器接线，维持 internal。

  同判据先例：M4 的 `AppThemePaletteManager` 随实现并入 Ui 集后回落 **internal**——
  装配方 `AppHost` 与实现在同一程序集，跨集公开面按需撤销；模块内主题文件映射/缓存/冻结等
  实现细节保持私有。

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
