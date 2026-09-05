# 模块划分共识：12 模块地图、归属裁定与修整单元判据

2026-09-04 对模块划分会话（grilling-with-docs，Q1–Q17）达成共识后裁决：为模块化路线定稿目标模块划分、归属裁定与“独立修整单元”判据。本 ADR 记录该共识；只涉及设计/文档，不推翻既有叶子（as-built）条款——叶子按 ADR-0014 同款“随实施批次回填”惯例维持现状，差异清单与批次见 [modules.md](../architecture/modules.md) §7/§8。

## Status

Accepted（grilling 共识 Q1–Q17/模块划分会话，2026-09-04；实施批次 B1–B6 待排期，架构叶子维持 as-built，随批次回填）。

## Considered Options

### 1. 判据：什么算“独立/可模块化”
- **二值“有叶子即独立”**：文档盘点价值低。
- **成熟度矩阵 + 修整单元判据**：模块 = 领域能力；加/改功能只动相关模块内部；跨模块只经稳定契约或放行共享面。

→ 选 **修整单元判据 + 共享内核放行清单**（Q12-A/Q13-A）。

### 2. 结构形态
- **概念模块 + 端口（保持现有水平目录）**：`Services/ViewModels/Views/Models` 正典不动，模块为文档级归属 + 契约边界。
- **垂直模块目录 / 独立程序集**：编译器级强制，但推翻 ADR-0006/0007 目录正典，重构量级完全不同。

→ 选 **概念模块 + 端口**（Q14-A）；垂直目录/独立程序集留待未来单独 ADR。

### 3. 顶层模块清单与粒度
- **配置方案是否独立成模块**：否决——新增动作类型（原型 D）会同时穿“动作执行（手势）”与“配置方案编辑（槽位 UI）”两个模块；为满足修整单元判据，配置方案设置面必须与动作执行同在 M1。
- **界面主题是否升层**：采纳升为顶层 M4——#54/#56 已实现独立设置子 VM、消息化与 5+ 窗口消费方，壳层叶已滞后；与 ADR-0014 “界面主题模块”正名衔接。
- **本地化与消息**：否决维持合并——两者无共享上下文、无耦合（`Messages.cs` 不依赖本地化），属“文档分组惯性”反例；拆为 S3 本地化 + S4 消息与通知。
- **IconHelper 归属**：否决整体留“程序扫描与图标”——新增动作类型需新图标键时会穿程序模块；几何成员唯一消费方是轮盘（消费者原则），不并入共享图标模块；最终 **IconHelper 三分**（R6）：图标资产 → S1、几何 → M2、程序侧 → M3。
- **图标模块命名**：S1 定名「图标资产」（而非「图标与几何」），几何不属共享。

→ 选 **12 个目标模块**（M1–M5 业务纵向、S1–S6 共享/基础设施、H1 宿主），见 modules.md §3。

### 4. 消息类型归属
- **中心 hub**：`Services/Messages` 保留为跨模块事件契约 hub，新消息类型 = 放行共享面；发现性/一致性延续 ADR-0005 现状。
- **按域归属**：消息随所属模块（主题消息 → M4、保存消息 → S2…），代价是 `layout.md`/`naming.md` 正典与目录迁移。
- **维持“本地化与消息”合并**：文档分组惯性，否决。

→ 选 **中心 hub + 放行共享面**（Q16-A）。

### 5. 归属裁定与登记
- R1–R8 归属裁定（`AutostartRegistry`→M5、`DevInstance`→H1、`MemoryOptimizer`→M5、MainView 文件级拆分、`GesturePoint`→共享内核、IconHelper 三分、选择器对话框→S6、Models 语义归属）。
- 登记表 D1–D6：M1 内部子面、M5 子职责目录与护栏、MainViewModel 类型级双职责、AppHost 语言字典投影、WheelFactory 装配点例外、页面壳原则。

→ 全数采纳（Q17-A）。

## Decision

1. **判据**：模块 = 领域能力；验收基准 = “修改/新增功能只动相关模块内部”，以 [extending.md](../architecture/extending.md) 原型 A–F 为验收样例。
2. **共享内核放行清单**：config 模型字段、i18n 键、Composition/导航登记一次、消息与通知 hub 新类型、共享视图基础设施、共享图标资产——不算“其它业务模块内部”。
3. **结构形态**：概念模块 + 端口；保持 `Services/ViewModels/Views/Models` 水平目录正典（ADR-0006/0007 不回退）。
4. **目标模块地图（12）**：M1 手势与动作、M2 轮盘与渲染、M3 程序扫描与目录、M4 界面主题、M5 壳层与系统集成；S1 图标资产、S2 配置与保存、S3 本地化、S4 消息与通知、S5 导航、S6 对话框；H1 宿主与组合根。职责与关键内部见 modules.md §3。
5. **配置方案设置面并入手势与动作（M1）**：不单独成模；M1 内部登记三个子面（D1）。
6. **界面主题升为顶层模块（M4）**：文档先行，物理目录暂不动。
7. **消息 hub 保留（S4）**：新增消息类型为放行共享面；本地化与消息在文档层拆为 S3/S4。
8. **图标与几何裁决（R6）**：IconHelper 三分——图标资产 → S1、几何 → M2、程序侧 → M3；实施见 B3。
9. **归属裁定 R1–R8 采纳**（modules.md §4）。
10. **登记表 D1–D6 采纳**（modules.md §5），其中 MainViewModel 主归属 S5、壳层成员登记类型级双职责例外（D3）。
11. **页面壳原则**：页面 = 聚合壳；配置面按卡片/子 VM 归模块（#56 Appearance 先例推广，D6）。
12. **候选路线 B1–B6**：记录于 modules.md §8，非规范、方向性，随批次回填叶子并从路线移除。

## Consequences

- 新建 [modules.md](../architecture/modules.md) 作为模块地图与模块化路线；本 ADR 为决策依据。
- 架构叶子维持 as-built，不先行描述未实现结构；差异对照见 modules.md §7，回填按 B1–B6 批次（与 #42–#56 的 S1–S9/分批回填惯例一致）。
- CONTEXT 无需修订：模块名（界面主题模块等）与“修整单元”是架构术语而非领域术语（ADR-0014 先例）。
- 本 ADR 不推翻 ADR-0005（消息总线）、0006/0007（目录正典）、0011（Composition/AppHost 拆分）、0013（本地化/主题）、0014（轮盘配色归属与外观拆分）；0014 的“界面主题模块”正名由本 ADR 收口为 12 模块地图中的 M4。
- 本 ADR 不做代码改动；后续批次每批需 issue 排期 + 构建 + xUnit 绿（涉及可见文案时 e2e 绿），再回填叶子。

## Appendix：参考事实（2026-09-04 快照）

- 现状叶子 9 个：config/dialogs/gestures/host/localization/navigation/programs/shell/wheel。
- `GlobalUsings.cs` 全局引入全部 `WinPieGestures.*` 命名空间 → 命名空间级扫描失效，取证按类型级引用。
- `Messages.cs`/`Notices.cs` 不引用本地化；本地化不引用消息；仅 `AppHost` 同时消费两者。
- `IconHelper` 消费方：几何成员（`CreateAdvancedSectorGeometry`/`GetCoreIconGeometry`）唯一消费族 = 轮盘（RadialWindow/WheelPreviewRenderer/CoreIconGeometryConverter）；图标资产成员（矢量清单/SVG 键/自定义图标存储/`GetIcon`）被 M1 槽位编辑、S6 图标选择器、M2 渲染共用；`ResolveShortcutTarget` 被 ProgramScanner/ProgramPicker 使用。
- `MainViewModel` 同时承载导航（NavigationItems/CurrentViewModel/SyncSelection/5 个 `INavigationService<>`）与壳层职责（`WindowTitle`+`DevInstance.Suffix`/`IsExiting`/`Save()`），成员级交织于同一类型。
- `extending.md` 原型 A–F 为验收样例来源。
