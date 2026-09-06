# 程序集地图与程序集化路线（目标态 + 方向性）

> 本文记录程序集化目标态（[ADR-0016](../adr/0016-assembly-split-target-and-roadmap.md)）的地图视图：目标程序集划分、程序集级依赖规则、导航槽位表、注册/可见性契约与 B0–B10 批次路线。
>
> 本文含**目标态与方向性**内容，不是纯 as-built。代码现状与各叶子（`docs/architecture/*.md`）为准，冲突时叶子优先；差异按 §8 批次随代码回填叶子。概念模块地图与归属裁定见 [modules.md](modules.md)（ADR-0015）。

## 1. 何时读本文

| 想做什么 | 读哪里 |
|---|---|
| 某个类型/文件属于哪个程序集（目标态） | 本文 §2–§4 + modules.md §3 |
| 程序集间依赖是否允许 | 本文 §3 |
| 导航槽位 / 侧边栏顺序正典 | 本文 §5 |
| 新增页面/服务在目标态下要动哪些 | 本文 §5–§6（+ [extending.md](extending.md) 原型 A–F） |
| 当前排期中的程序集化批次 | 本文 §8 |
| 为什么这样定 | [ADR-0016](../adr/0016-assembly-split-target-and-roadmap.md) |

## 2. 目标程序集地图（7 程序集）

| 程序集 | 形态 | 承载（目标态） |
|---|---|---|
| `StarPie`（项目 `WinPieGestures`） | WinExe | H1 宿主与组合根（App/AppHost/Composition/DevInstance）；Host 壳窗口（`MainView` 全文件 + `ShellViewModel`）；S6 对话框 Window 与 DialogService 实现；Appearance 聚合页；B3 前仍含全部页面与全部业务代码（逐批迁出） |
| `StarPie.Core` | WPF 类库 | S1–S6 共享内核合并：Models；S2 Configuration；S3 Localization（含 `Strings*.resx` 与生成器）；S4 Messages；S1 Icons；S6 对话框契约（接口/结果 record）；S5 导航内核（NavigationStore/INavigationService/NavigationService/NavigationItemViewModel/NavigationCatalog/槽位表/MainViewModel 纯导航）；共享 UI 基建（Views/Converters 通用转换器、Views/Controls/HotkeyRecorderBox、Views/Styles/ModernControls.xaml，B5/#78 已落地；M2 核图标预览转换器与 S6 取色对话框行为暂留 Host，见 §9） |
| `StarPie.Gestures` | 类库 | M1 手势与动作：Services/Gestures、Services/Actions、Trigger/Gestures 设置页（B9 迁入） |
| `StarPie.Wheel` | 类库 | M2 轮盘与渲染：ViewModels/Wheel、RadialWindow、Renderers、WheelPalette*、WheelGeometry、WheelFactory（B8 迁入，D5） |
| `StarPie.Programs` | 类库 | M3 程序扫描与目录：ProgramScanner/ProgramCatalog/ShortcutResolver（B4/#77 已落地；零 Core 依赖） |
| `StarPie.Theme` | 类库 | M4 界面主题：ThemeService/Themes XAML/InterfaceThemeSettingsViewModel/ThemePaletteManager 裁决（B7 迁入） |
| `StarPie.Shell` | 类库 | M5 壳层服务与设置面：TrayIconManager/AutostartRegistry/MemoryOptimizer/General+About 设置页（B6 迁入；`MainView` 壳窗口与 `ShellViewModel` **不**随 M5，留 Host） |

## 3. 程序集级依赖规则

```text
StarPie (Host/exe) ──→ StarPie.Core
     │──→ StarPie.Gestures ──→ StarPie.Wheel ──→ StarPie.Theme
     │──→ StarPie.Programs        （B4/#77 起零 Core 依赖，Host 显式引用）
     │──→ StarPie.Shell ──→ StarPie.Core
     └────────────────────────────────────────→ StarPie.Core
```

- `M* → Core` 单向（**Programs 例外**：M3 零共享内核依赖，B4/#77——扫描图标补全经组合根注入的
  S1 `IconAssets.GetIcon` 委托）；`Host → 全部`（仅调用各模块注册器与装配宿主对象，不引用模块内部）。
- 允许的 M 间单向边仅：**M1→M2**（`IWheelFactory`，接口在 M2 侧）、**M2→M4**（`IThemeService` 消费）。其余跨 M 依赖一律经 Core 契约。
- S6 对话框 Window/DialogService 实现留 Host，契约在 Core；M 页面经 Core 的 `IDialogService` 调用，实现由 Host 组合根接线。
- 共享放行清单（config 模型字段、i18n 键、消息/通知类型、共享 UI 基建、图标资产）维持 modules.md §2.3，不视为跨模块违规。

## 4. “壳”的三层语义（防混淆）

程序集化后“壳”分三层，术语别混用：

| 层 | 归属 | 程序集 | 内容 |
|---|---|---|---|
| 导航内核/状态 | S5 | Core | NavigationStore、INavigationService<>、NavigationCatalog、MainViewModel（纯导航） |
| 壳层服务与系统集成 | M5 | StarPie.Shell | 托盘、自启、内存、Advanced/About 设置面 |
| Host 壳窗口 | H1（宿主壳） | StarPie（exe） | MainView 全文件、ShellViewModel、App/AppHost/Composition |

`MainView.xaml.cs` 与 `ShellViewModel` **归 Host 壳窗口**（ADR-0016 决策 6/7），不再归 M5；M5 只拥有壳层服务与设置面。

## 5. 导航架构（目标态：模块自治注册）

### 5.1 机制

- Core 提供 `NavigationCatalog`：`RegisterPage<TViewModel>(槽位, automationId, titleKey, iconData, …)` + 导航执行缝（同 `NavigationService<T>` 的已批准惰性解析）。
- 模块注册器自报导航项与页面模板字典（`DataTemplate DataType=VM → View`）；Host 在 App 资源里**每模块一次** pack URI 静态合并。
- 新增页面 = 所属模块内部（注册器声明导航项 + 模板字典加条目），**不碰 Host**。
- 新增模块 = Host 登记：程序集引用 + 注册器调用 + 模板字典合并（各一次，放行共享面）。
- e2e `AutomationId` 沿用 `NavTab0..4`，随槽位稳定。

### 5.2 导航槽位表（正典）

| 槽位 | AutomationId | TitleKey | 页面 VM | 页面 View | 程序集（目标） |
|---|---|---|---|---|---|
| 0 | `NavTab0` | `TabTrigger` | `BehaviorSettingsViewModel` | `TriggerSettingsPage` | M1 Gestures |
| 1 | `NavTab1` | `TabAppearance` | `AppearanceSettingsViewModel`（聚合壳） | `AppearanceSettingsPage` | Host |
| 2 | `NavTab2` | `TabGestures` | `ProfileListViewModel` | `GesturesSettingsPage` | M1 Gestures |
| 3 | `NavTab3` | `TabAdvanced` | `GeneralSettingsViewModel` | `AdvancedSettingsPage` | M5 Shell |
| 4 | `NavTab4` | `TabAbout` | `AboutViewModel` | `AboutSettingsPage` | M5 Shell |

缺失/重复/未知槽位由 Core 收口测试拦截；槽位表是侧边栏顺序唯一正典（B2/#75 契约与收口测试已落地；
B3/#76 目录驱动接线已落地：MainViewModel 迁 Core 并按目录注册构造导航项，导航执行走
`INavigationExecutor` 目录执行缝）。

## 6. DI 与注册契约（目标态）

- **注册自治**：每个业务程序集暴露注册器（建议形态：公开静态类，含 `RegisterServices(IServiceCollection)` 与 `RegisterNavigation(NavigationCatalog)`）；Host Composition 按固定顺序调用。
- **根解析集中**：Host Composition 仍唯一 `BuildServiceProvider` / `CreateAppHost`；模块不解析、不持容器。
- **已批准解析缝**：`NavigationService<T>`、导航目录执行缝（`INavigationExecutor`，B3/#76 已落地）、
  `WheelFactory`、`DialogService`、模块注册器（仅注册不解析）。
- `AppHostDelegates` 上提为 Core 公开契约（Host 回填实现），M5/M1 注册器只依赖 Core。
- CreateAppHost 的硬编码解析清单 → 目录/启动激活钩子驱动；eager 页面实例化语义已在 B3/#76 验证保留
  （CreateAppHost 遍历 `NavigationCatalog.Entries` 逐个 eager 解析页面 VM）。
- 不引入子容器、Generic Host、Autofac、Prism（ADR-0016 决策 13）。

## 7. 可见性（目标态）

- 不引入 `InternalsVisibleTo`（layering.md 维持）。
- 模块公开面 = 注册器入口 + 被测 public 类型；内部实现细节保持 internal。
- Host 只引用模块注册器，不引用模块内部。
- 跨集必需的内部件（如 `ThemePaletteManager`）在各批次单独裁决为 public 或改经接口注入。

## 8. 批次路线 B0–B10（排期）

> 每个批次：独立 issue；构建 + xUnit 绿；涉及可见文案时 e2e 绿；完成后回填对应叶子并从本表移除。

| 批 | 内容 | 主要回填 |
|---|---|---|
| B0 | 纯文档：ADR-0016 + 本文 + modules.md R4/D3/D5/扩展点/§8 修订 + architecture.md 路由/索引（本批） | modules.md、architecture.md |
| B6 | M5 Shell 抽取（Advanced/About 页随集；宿主回调走 Core 契约；ShellViewModel 留 Host 核对） | shell.md、navigation.md、host.md、layout.md |
| B7 | M4 Theme 抽取（含 ThemePaletteManager 可见性裁决） | interface-theme.md、host.md、layout.md |
| B8 | M2 Wheel 抽取（D5：WheelFactory 随 M2、IProfilePreviewSource 上提 Core） | wheel.md、gestures.md、layering.md、modules.md（D5 清零） |
| B9 | M1 Gestures 抽取（Trigger/Gestures 页收口） | gestures.md、navigation.md、layout.md |
| B10 | 命名空间统一收尾（原 B8 内容，编号顺延；ADR-0016 决策 12） | 全部叶子 + 测试 + XAML xmlns + resx 生成类 |

### 8.1 批次阻塞边（2026-09-06 代码审计）

> 阻塞边 = 该票必须在前置票合入 main 后才能开工的硬门；无阻塞票可按路线顺序或 frontier 先做（多人并行时需先做文件面互斥划分）。

- B2（Core 抽取）← None（已落地，#75）。
- B3（导航自治 + MainViewModel 迁 Core）已落地（#76；前置 B2/#75 已落地）。
- B4（M3 Programs 抽取）← None（已落地，#77：零 Core 依赖、无 DI 注册，注册器样板随 B6）。
- B5（共享 UI 基建迁 Core）已落地（#78；前置 B2/#75 已落地）。
- B6（M5 抽取）← B5（B3 已落地，#76）。
- B7（M4 抽取）← B2（主题 XAML 自包含，不依赖 B5）。
- B8（M2 抽取，含 D5）← B7（`RadialWindow` 注入 M4 的 `IThemeService`；其 XAML 自包含，不依赖 B5）。
- B9（M1 抽取）← B5、B8（B3 已落地，#76；页面共享 `SettingsPageBase`、`GesturesSettingsPage` 引用共享控件，且 M1→M2 需 M2 已成集）。
- B10（命名空间统一）← B9。

### 8.2 执行期集成面串行约束

上述阻塞边描述的是**架构上的硬前置**；它们不等于可以无冲突地并行修改。为避免多个 agent 同时改动组合根和工程入口，执行时还需遵守以下集成面互斥规则：

- `Composition.cs`、`AppHost.cs`、`WinPieGestures.csproj`、`WinPieGestures.slnx`（B2 起含
  `StarPie.Core.csproj`；B4/#77 起含 `StarPie.Programs.csproj`）同一时间只允许一张票落地。
  B1/B2/B4/B5 已落地；其余触及这些文件面的批次（B6 起）必须串行合并。
- `App.xaml`、主题/控件资源字典及其 pack URI 同一时间只允许一张票落地。B5/#78 已落地；后续
  B7（M4 Themes XAML 拆集）触及同一资源面，必须与其它批次排队集成（架构上无需新增阻塞边）。
- `Services/Shell`、`ThemePaletteManager.cs`、主题与壳层宿主接线存在物理文件重叠。B6 与 B7 不得同时进行文件搬迁；先完成一票并通过构建，再开始另一票的搬迁。
- agent 分支可以并行进行只读分析或不触及上述文件面的代码准备；进入合并队列前必须先完成一次主干同步、构建与 xUnit。

这是一条**执行协调规则**，不是新增业务依赖；它不改变 B0–B10 的拓扑，只约束共享集成面的写入顺序。

## 9. 现状对照与差异登记

代码现状 = Host exe（`WinPieGestures/`，程序集 `StarPie`）+ 共享内核（`StarPie.Core/`，程序集
`StarPie.Core`，WPF 类库）+ M3 模块程序集（`StarPie.Programs/`，程序集 `StarPie.Programs`，WPF
类库）+ `WinPieGestures.Tests`（显式引用三工程，不依赖传递引用）。B2/#75 已落地：Models、
S2/S3/S4/S1、S6 契约、S5 导航内核（NavigationStore/INavigationService/NavigationService/
NavigationItemViewModel/NavigationCatalog/槽位表）迁入 Core。**B3/#76 已落地**：`MainViewModel`
目录驱动后迁入 Core（无页面类型硬编码）；导航执行走 `INavigationExecutor` 目录执行缝；exe 内按
M1/M5/Host 临时注册器（`RegisterNavigation`）与模块页面模板字典（App 级每模块一次静态合并）；
CreateAppHost 页面 eager 解析清单目录化（语义保留）；MainView.xaml 纯壳（不再含页面 DataTemplate）。
**B4/#77 已落地**：M3 三件（ProgramScanner/ProgramCatalog(+ProgramEntry)/ShortcutResolver）迁入
`StarPie.Programs`（slnx 登记；Host/Tests 显式 ProjectReference）；M3 零 Core 依赖——扫描图标
补全经组合根注入的 S1 `IconAssets.GetIcon` 委托，`IconAssets.ResolveShortcutTarget` 回填缝指向
`StarPie.Programs` 的 `ShortcutResolver`。**B5/#78 已落地**：共享 UI 基建迁 Core——四个通用转换器
（`HexToBrushConverter`/`StringToGeometryConverter`/`IntEqualsConverter`/`FilePathToImageConverter`）
、共享自定义控件 `HotkeyRecorderBox` 与全局控件样式字典 `ModernControls.xaml` 物理迁入
`StarPie.Core/Views/{Converters,Controls,Styles}`（命名空间维持 `WinPieGestures.*`，B10 统一）；
Host `App.xaml` 经跨程序集 pack URI（`/StarPie.Core;component/Views/Styles/ModernControls.xaml`）
单点合并该字典，转换器实例仍为 App 级资源；页面/窗口无自合并样式字典。暂留 Host 的 UI 专用件：
M2 核图标预览转换器（`CoreIconGeometryConverter`/`CoreIconNameConverter`，Appearance 聚合页用；
Geometry 转换器直连 M2 `WheelGeometry`，B8 收编前 Core 不得反向依赖宿主）与 S6 取色对话框行为
`SpectrumCanvasBehavior`（依赖 Host `ColorPickerViewModel.SpectrumPoint`；S6 对话框实现留 Host）。
`AppHostDelegates` 上提延至 B6；页面 VM 的 DI 注册仍集中组合根（模块服务注册器样板随 B6 起落地）。
其余四个业务模块程序集（M1/M2/M4/M5）尚未拆分，差异随 B6–B10 逐批回填叶子并清零。

## 参见 ADR

[0016](../adr/0016-assembly-split-target-and-roadmap.md)（程序集化目标态与分批执行）、[0015](../adr/0015-module-map-and-ownership.md)（12 模块地图与归属裁定）。
