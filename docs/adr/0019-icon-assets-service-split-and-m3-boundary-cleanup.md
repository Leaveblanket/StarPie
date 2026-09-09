# 图标资产实例服务化、M3 边界例外收口与模块化边界维持

> Status: Active（部分被 0023 修订）

> [ADR-0023](./0023-module-contracts-hard-boundary-and-core-narrowing.md) 修订本 ADR 决策 2“S1 不拆独立模块/程序集”：S1 现已独立成集 `StarPie.Icons.Contracts` + `StarPie.Icons`。

为对齐上游（SoftBlack42/StarPie，本地基线 V1.7.0 / HEAD v1.7.1-beta1）并收纳合理改动，把 S1
「图标资产」修整为“无状态静态目录 + 有状态/IO/Win32 实例服务”双形，收口 B4/#77 遗留的 M3
“零 Core 依赖”例外（契约进 Core、`StarPie.Programs` 单向依赖 Core、补模块注册器、取消静态
回填缝），并明确**不新建独立图标程序集**——S1 保留在 Core，Win32 污染以 S1 内部隔离收敛。
本决策同时固化 DI/组合根维持评估结论与“何时值得重开 Icon 拆集”的触发条件。

## Status

Accepted（2026-09-08 grill-with-docs 会话；Q1 命名/位置、Q2 预览桥、Q3 M3 形态、Q4 执行顺序
均按推荐认可）。实施批次待排期；架构叶子维持 as-built，随批次回填（ADR-0016 同款纪律）。

## 背景与动机

1. **上游对齐目标是“功能导出移植”而非结构合并**：上游单 csproj、无程序集拆分、IconHelper 为
   单一 static；本地已完成 7 程序集（B0–B10/#75–#83，路线清零）。两侧结构不可 merge，功能基线
   取上游 V1.7.0 稳定 tag、HEAD v1.7.1-beta1 仅复核差异；本地模块化边界是收纳合理改动的结构
   基线，不在移植中回退。
2. **`IconAssets` 违反仓库自身静态判据**：layering.md/ADR-0002 判据 = 静态仅限无状态纯表、
   mock 无意义的调用；有状态/IO/Win32 → 实例服务（先例：ConfigManager→IConfigService、
   ActionExecutor→IActionExecutorService、AppThemeManager→IThemeService、I18n→ILocalizationService）。
   现状 `IconAssets` 混合两类：内置矢量清单/SVG 键目录/`ExtractSvgPathData` 无状态纯表；
   `_cachedCustomIcons` 缓存、自定义图标存储（导入/删除/图片源）、`GetIcon`（Win32 Shell
   P/Invoke + WPF 位图）与可变的 static `ResolveShortcutTarget` 回填缝属有状态/IO/Win32 面。
   ADR-0002 静态清单中“IconHelper 保持静态 | 无状态 Win32 工具”已成为死信。
3. **M3 零 Core 例外是程序集化路线中唯一与 ADR-0016 决策 2（M*→Core 单向）不一致的项**：
   B4/#77 时为“首个独立模块程序集”用委托注入 + 静态回填绕开共享契约；B6–B9 模块注册器样板
   （RegisterServices/RegisterNavigation 下放）落地后，M3 应回归与 M1/M2/M4/M5 相同的单向
   Core 形态，消除 Core↔M3 事实耦合与“组合根装配前回填”时序敏感面。
4. **Core 是有意的 WPF 共享内核，不是需要防污染的纯领域层**：Core 已含 Views/、转换器、窗口
   主题字典（B5/#78）等 WPF 面；S1 体量小（2 文件 / 约 490 行），Win32/WPF 细节应收敛进
   S1 服务实现内部而非把 S1 拆成独立程序集。

## Considered Options

### 1. S1 图标资产形态
- **A. 维持全 static `IconAssets` + 委托回填**：改动最小，但继续违反静态判据；可变 static
  缓存/回填缝使测试与并发语义依赖装配顺序，无法经容器注入替身。
- **B. 双形拆分（采纳）**：无状态纯目录保持 static（矢量清单、`GetSvgPathByKey`、
  `ExtractSvgPathData`）；有状态/IO/Win32 面（自定义图标存储、`GetIcon` 与图片源、.lnk 解析
  消费）→ 构造注入的 `IIconAssetService`/`IconAssetService` 实例服务。命名/文件位置按 Q1 推荐：
  纯目录定名 `IconCatalog`（保留 static vectors 与键查询），`CustomIconItem` 独立成文件，
  退役 `IconAssets` 名；`IIconAssetService`/`IconAssetService` 新增于
  `StarPie.Core/Services/Icons/`（成员最终划分在执行批次按引用面裁决，此处定边界）。
- **C. 全部实例化（含矢量清单）**：纯表服务化只加间接层，无 mock 收益，且与
  `ProgramCatalog`/纯函数提炼惯例（layering.md Services 节）不一致 → 否。

### 2. Icon 是否拆为独立模块/程序集
- **A. 新建 `StarPie.Icons` 独立程序集**：程序集数量 +1、打包/引用噪音超过收益；Core 体量小
  （35 文件 / 约 2328 行）且是有意的 WPF 共享库（含 Views/ 约 477 行）；ADR-0016 决策 4 已拒绝
  12 程序集全拆，7 程序集目标态已达成并清零 → 否。
- **B. S1 留 Core + 服务化修整（采纳）**：Win32 P/Invoke 与 WPF 图像提取收敛进
  `IconAssetService` 实现内部（必要时 `Services/Icons/Native/` 子目录），对调用方只暴露
  `ImageSource`/条目模型面；写“重量级拆分触发条件”（见 Decision 2）。

### 3. M3 边界（B4/#77 例外）收口方式
- **A. 维持零 Core 例外**：委托 + 静态回填继续作为正典 → 否（与 ADR-0016 决策 2 及
  B6–B9 模块注册器样板不一致；共享契约被绕过）。
- **B. 打开边界（采纳，Q3 推荐 a）**：Core 定义契约（如 `IShortcutTargetResolver`），
  `StarPie.Programs` 增加 ProjectReference Core（M3 → Core 单向）；M3 内 `ShortcutResolver`
  改实例实现契约；新增 `StarPie.Programs/Modules/ProgramsModuleRegistrar.cs`
  （`RegisterServices`，M3 无导航页故无 `RegisterNavigation`），Host Composition 调用；
  `ProgramScanner`/Host `ProgramPickerViewModel` 经实例注入使用；取消
  `IconAssets.ResolveShortcutTarget` static 回填缝及 Composition 回填行。
- **C. M3 整体并回 Core**：程序扫描与目录是产品业务能力（M3）而非共享件，程序集化裁决
  （ADR-0016）不回退 → 否。

### 4. DI/模块化模式维持（2026-09-08 社区对照评估）
- 现模式 = MS.DI + 单一组合根（Host Composition 唯一 BuildServiceProvider/解析点）+
  static 注册器下放模块程序集 + approved-seam 清单 + 编译期模块引用。对照社区：Seemann
  Composition Root（每应用单组合根）、Microsoft `Add{GROUP}` 注册约定、modular monolith
  编译期引用权衡、Prism legacy WPF Bootstrapper/ModuleCatalog 作为 WPF 侧替代路线。
- 评估结论：**维持现状，不引入子容器/每模块容器/Generic Host/Autofac/Prism/assembly
  scanning/`AddStarPieModules()` 统一入口**；把触发条件记入 ADR（见 Decision 5）。
- 结构性张力仅两处：(a) 有状态静态类（本 ADR S1 修整对象）；(b) WPF DataTemplate 无参 View
  构造拿不到 DI（经已批准缝/工厂/桥处理，不引入 ViewModelLocator）——WheelPreviewRenderer
  预览桥属 (b) 的显式登记。

### 5. 上游移植路线
- **A. 文件同步 / 结构合并**：不可 merge（单 csproj vs 7 程序集、命名空间/模块化差异大）→ 否。
- **B. 功能导出移植 + 上游移植登记（采纳）**：登记文档按 feature/差异记录“来源版本、
  本地落点、确认/否决与理由”；共享初始提交 v1.3.8 `214caec` 之后本地已收纳的改动在移植时
  逐一对照。

## Decision

1. **S1 双形修整**：
   - 无状态纯目录（static）：`IconCatalog`（内置 `VectorIconList`、`GetSvgPathByKey`、
     `ExtractSvgPathData`；`VectorIconItem` 维持独立文件）；
   - 实例服务（构造注入）：`IIconAssetService`/`IconAssetService` 承载自定义图标存储
     （目录/列表缓存/导入/删除/图片源）、`GetIcon`（文件/程序图标提取）及 .lnk 解析消费
     （经注入的 `IShortcutTargetResolver`）；
   - `CustomIconItem` 独立成文件；`IconAssets` 名称退役（引用面迁移见 Consequences 影响清单）。
   - 判据重申：**static = 无状态纯表；有状态/IO/Win32 = 实例服务**（layering.md Services 节）。
2. **S1 不拆独立模块/程序集**：S1 保留在 Core；Win32/WPF 面收进 `IconAssetService` 实现内部
   （可划 `Services/Icons/Native/` 子目录）。**重开 Icon 拆集触发条件**（任一命中才重新评估）：
   图标资产源/格式族显著增长（如支持主题图标集、插件图标、矢量库运行时加载）；Core 共享面
   新增需独立版本化的稳定契约；S1 被非 WPF 消费方（如纯服务端/CLI）引用；7 程序集目标态被
   主动扩大。触发评估仍须先走 ADR。
3. **M3 边界收口（B4/#77 例外清除）**：
   - Core 新增契约 `IShortcutTargetResolver`（.lnk → 目标/图标路径/索引，语义随 M3 现出口）；
   - `StarPie.Programs.csproj` 增加 ProjectReference Core（**M3 → Core 单向**；不引用
     Host/其它业务模块）；
   - M3 `ShortcutResolver` 改实例并实现契约（无状态 COM 解析保持可直 new 单测）；
   - `ProgramScanner` 经实例注入消费契约与 S1 图标服务（替代组合根传 `IconAssets.GetIcon`
     委托与内部直呼 static `ShortcutResolver`）；执行批次以最小签名改动落地；
   - 新增 `StarPie.Programs/Modules/ProgramsModuleRegistrar.cs`（`RegisterServices`，无
     `RegisterNavigation`——M3 无导航页），Host Composition 按固定顺序调用；
   - 删除 `IconAssets.ResolveShortcutTarget` static 回填属性与 Composition 回填行；
     Host `DialogService`/`ProgramPickerViewModel` 经 Core 契约/注册服务获得 resolver 与
     图标服务（保持对话框 VM 不经容器、new + 注入形态，S6 惯例不回退）；
   - `AppDataPaths.IsDevInstance = DevInstance.IsActive` 属环境参数回填，非本决策处理对象，
     维持现状。
4. **预览桥（Q2 推荐 a）**：`WheelPreviewRenderer` 保持 View 层纯渲染、无公共 DI 构造；
   S1 实例服务经**已批准 preview bridge** 到达——外观聚合 VM
   （`AppearanceSettingsViewModel`，容器单例）暴露图标服务入口，`AppearanceSettingsPage`
   在 `OnPageLoaded` 阶段从聚合 DataContext 读取并装配渲染器（与现有预览状态桥同构）；
   在 layering.md Views 例外清单显式登记；不引入 ViewModelLocator、不把页面 View 改造成
   容器注入。
5. **DI/组合根维持**：维持 MS.DI + 演进式单一组合根 + 模块注册器下放 + approved-seam 清单；
   不引入子容器/Generic Host/Autofac/Prism/assembly scanning/统一 `AddStarPieModules()`。
   **重新评估触发条件**：业务模块注册器超过 5 个且出现跨注册器顺序依赖；模块间共享契约
   数量显著增长使 Core 成为“隐式总线”；出现运行时插件/动态加载需求；届时另行 ADR。
6. **上游对齐方式**：功能导出移植 + 上游移植登记（feature 级差异、确认/否决与理由入库）；
   本地程序集化与服务化边界作为收纳合理改动的结构基线，不在移植中回退。
7. **执行顺序（Q4 推荐 a）**：本 ADR → 重构批次（单 issue；build + 全量 xUnit 绿；refactor
   不涉及可见文案按 ADR-0018 免跑判定 e2e）→ 叶子回填 → 上游移植登记 + V1.7.0 差异盘点 →
   功能导出移植（逐 feature 登记确认/否决）。代码改动不随本 ADR 提交。

## Consequences

- **代码影响面（实施批次细化）**：
  - Core：`Services/Icons/IconAssets.cs` 拆分为 `IconCatalog`（static 纯目录）+
    `IconAssetService`/`IIconAssetService` + `CustomIconItem`（独立文件）+ 契约
    `IShortcutTargetResolver`；Win32/P-Invoke 收敛进服务实现内部；
  - M1（StarPie.Gestures）：`SlotViewModel` 图标取值改经注入的 `IIconAssetService` +
    static `IconCatalog`（构造注入由 GesturesModuleRegistrar 接线）；
  - M2（StarPie.Wheel）：`WheelGeometry`/`RadialWindow`（WheelFactory 注入链）、
    `WheelPreviewRenderer`（经预览桥，见 Decision 4）消费实例服务/static 目录；
  - M3（StarPie.Programs）：`ShortcutResolver` 实例化实现 Core 契约、`ProgramScanner` 签名
    注入、新增 `ProgramsModuleRegistrar`、csproj 增加 Core 引用；
  - Host：Composition（删除回填行、调用 ProgramsModuleRegistrar、DialogService/图标服务
    注册改实例）、`DialogService`/`IconPickerViewModel`/`IconPickerWindow`/
    `ProgramPickerViewModel`（改用注入的图标服务与 resolver，VM 保持 new + 注入）、
    `AppearanceSettingsViewModel`/`AppearanceSettingsPage`（预览桥）；
  - 测试：`IconAssetsTests` 拆分（IconCatalog 纯表断言 + IconAssetService 注入测试）、
    `IconPickerViewModelTests`/`SlotViewModelTests`/`WheelGeometryTests`/
    `WheelPreviewRendererTests`/`ProgramPickerViewModelTests` 构造随签名同步；
    `ProgramsAssemblyPlacementTests` 的“M3程序集_不引用共享内核Core与Host()”改为
    “M3 → Core 单向、不引用 Host/其它业务模块”断言；新增/更新
    `ProgramsModuleRegistrar` 相关 AssemblyPlacement 收口。
- **叶子回填清单（批次落地后逐项回填，as-built 纪律）**：`assemblies.md`（§2 表 Programs 行、
  §3 依赖图与 Programs 例外表述、§6 注册器清单、§9 B4 段）、`modules.md`（§2.3 放行面注、
  §3 M3/S1 关键内部与对外契约、R6 登记行）、`layering.md`（程序集层图 M3 行、跨程序集回填缝
  清单删 `IconAssets.ResolveShortcutTarget`、Services 静态判据示例、Views 例外登记预览桥）、
  `layout.md`（`Services/Icons/` 与 `StarPie.Programs/Services/Programs/` 行、HostModules
  描述、回填缝注）、`host.md`（ConfigureServices 回填缝与 DialogService 注）、`programs.md`
  （组成文件/关键流程 1–2）、`dialogs.md`（程序选择器委托与 S1 出口表述）、`wheel.md`/
  `gestures.md`（S1 消费表述）、`architecture.md`（技术栈模块程序集段、ADR 索引登记 0019）。
- 死信处理：ADR-0002 静态清单“IconHelper 保持静态”等历史表述不直接改写（ADR 为历史记录），
  以本 ADR 为现行判据依据；layering.md 同步为现行规范。
- 上游登记：移植开始前建立登记文档（建议 `docs/architecture/upstream.md` 或独立 leaf）。
