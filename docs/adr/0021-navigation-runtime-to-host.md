# 导航运行时归 Host：共享内核仅留目录/槽位契约

把设置控制台的导航运行时主体（`NavigationStore`/`NavigationExecutor`/`MainViewModel`/
`NavigationItemViewModel`，含 `INavigationExecutor`）自共享内核 `StarPie.Core` 迁回宿主
`WinPieGestures`（Host/exe）；`StarPie.Core` 仅保留导航注册契约（`NavigationCatalog`/
`NavigationSlot`/`NavigationSlots`/`NavigationPageRegistration`）。本决策是 #89
（StarPie.Core 共享内核章程）Q3 的裁决，讨论锚点 #91。

## Status

Accepted（2026-09-08 grill-with-docs 会话：#89 Q3/#91，Q1–Q6 全部按推荐认可；
决策先行——本 ADR 落地后另开实施票，含 C1 死代码删除与叶子回填；架构叶子维持
as-built，随实施批次回填，ADR-0016 同款纪律）。

## 背景与动机

1. **#89 Q1 判据口径不含运行时主体**：Q1 已确认"内核成员 = 服务全局的能力/全局数据"，其
   "全局机制"示例列的是 `NavigationCatalog`/`NavigationSlots`（目录/槽位），未含运行时主体。
   #91 选项 a 把"整组导航模块 = 全局机制"当作已确认判据，属口径放大。
2. **运行时主体是单消费方件**：`Composition`/`AppHost`/`MainView`（全部在 Host）是四类的
   全部生产消费者；模块程序集（Shell/Gestures/Wheel/Theme/Programs/Dialogs）对四类**零引用**，
   只消费 `NavigationCatalog` 注册面。按 ADR-0015 §2.3 消费方判据（推广 ADR-0014）：
   单一消费方的能力应留在消费方内部，不属于共享内核。
3. **共享内核携带 Host 窗口 VM 与 DI 解析缝**：`MainViewModel`/`NavigationItemViewModel` 是
   Host 壳窗口 `MainView` 的 DataContext VM；`NavigationExecutor` 是 Core 内仅有的两个使用
   `Microsoft.Extensions.DependencyInjection` 的文件之一（另一个是死代码
   `NavigationService`）。运行时迁出（+ C1 删除）后 Core 可整体移除 MS.DI 包引用，向"共享
   内核零外部依赖、只放多模块真正需要的类型"收敛（社区 shared-kernel 通行规则）。
4. **同窗同判据先例已存在**：ADR-0016 决策 6/7（R4/D3）裁定同一 `MainView` 的壳层 VM
   `ShellViewModel` 留 Host，判据 = "壳窗口归 Host"；`MainViewModel` 只是同一窗口导航区的
   DataContext。B3/#76 当时把 `MainViewModel` 进 Core 是"S5 归属裁定"，不是编译器强制。
5. **迁 Host 不破坏 B3/#76 验收目标**："新增页面不碰 Host"由"目录驱动 + 模块注册器 +
   模块页面模板字典"实现，与运行时所在程序集无关；`NavigationCatalog` 契约留 Core 不动，
   模块加页仍只动模块内部。
6. **社区参照**：模块化单体 shared kernel 只放 ≥2 模块需要的契约（不放 ViewModel/基础设施）；
   App Shell 模式由壳拥有导航与布局；Prism 的 Shell 主窗属应用工程、模块只向区域贡献视图。
   本仓是单应用（无跨应用框架复用前提），导航运行时更应随宿主壳，而非留在共享内核。

## Considered Options

### Q1. 运行时主体归属（#91 根决策）
- **A1. 维持留 Core 并判为内核成员（全局机制口径）**：代码零动，但需把 #89 Q1 已确认口径从
  "目录/槽位"放大到"整组导航模块"；Core 继续携带 Host 窗口 VM 与 MS.DI 解析缝，模块编译期
  仍可达 Host 私有类型（边界只有文档）→ 否。
- **A2. 维持物理留 Core，按 B 档降格为非内核成员**：严格符合 Q1 判据且改动最小，但承认
  "Core 囤单消费方件"为负债；编译器边界问题仍在 → 否（作为 B 的降级备选）。
- **B. 运行时主体迁 Host（采纳）**：Core 只留目录/槽位契约；运行时与壳窗口/壳层 VM 同判据
  归 Host，模块失去对运行时类型的编译期可达性。

### Q4. S5 在模块地图中的形态
- **4a. S5 收敛为纯契约共享模块（采纳）**：S5 = 导航目录/槽位注册契约（同 S4 纯契约先例）；
  运行时主体归 H1 宿主壳件（R4/D3 同判据）。S 模块数量不变，概念层与物理层一致。
- **4b. 撤销 S5 模块概念**：目录契约并入章程"收口契约组"，共享模块 6 → 5，地图改动大 → 否。
- **4c. S5 概念不动、仅记物理在 Host**：实质否定 Q1 精神（共享模块成员物理住 H1）→ 否。

### Q5. `INavigationExecutor` 接口归属
- **5a. 接口随实现整体迁 Host（采纳）**：执行缝从跨程序集"已批准解析缝"降为 Host 内部件，
  seams.md 不登记跨集缝；将来 M* 需要请求导航时再按 `IDialogService` 先例上提接口
  （消费方驱动，YAGNI）。
- **5b. 接口留 Core、实现迁 Host**：为不存在的消费方提前占位，Core 多一个无消费方契约，
  且保留一条跨集解析缝 → 否。

### Q6. 迁移边界与实施切分
- **6a. 单张实施票整体处理（采纳）**：四类迁 Host + C1 死代码（`INavigationService`/
  `NavigationService`）删除 + 对应测试用例清理 + Core csproj 去 MS.DI + placement/收口
  断言核对；前置为 ADR-0021 文档批。
- **6b. C1 先独立成票、搬迁再成票**：票更小但两次全量验证，且死代码与搬迁互锁（
  `NavigationService` 引用 `NavigationStore`），中间态反而绕 → 否。
- **6c. `NavigationStore` 留 Core 作"控制台全局状态"**：等于把单消费方件再留一份在 Core，
  运行时四类同因同变，拆开无收益 → 否。

## Decision

1. **运行时主体迁 Host**：`NavigationStore`、`NavigationExecutor`（含 `INavigationExecutor`）、
   `MainViewModel`、`NavigationItemViewModel` 自 `StarPie.Core` 迁入 `WinPieGestures`，落点
   `WinPieGestures/Services/Navigation/` 与 `WinPieGestures/ViewModels/Navigation/`；
   命名空间不变（`StarPie.Services.Navigation`/`StarPie.ViewModels.Navigation`，B10/#83 共享
   命名空间树，跨程序集搬迁无需改名）。
2. **Core 仅留目录/槽位契约**：`StarPie.Core/Services/Navigation/NavigationCatalog.cs`
   （`NavigationSlot`/`NavigationSlots`/`NavigationPageRegistration`/`NavigationCatalog`）
   不动——它是跨模块注册契约（模块写、控制台读），属 #89 Q1"全局机制"，继续为内核成员。
3. **S5 概念修订（Q4=4a）**：modules.md §3 S5 收敛为"导航目录/槽位注册契约（纯契约共享
   模块）"；运行时主体归 H1 宿主壳件（R4/D3 同判据）。**部分推翻 ADR-0016 决策 7 中
   "MainViewModel 随 S5 导航内核进 Core"的物理落点表述**；决策 7 的职责拆分语义（纯导航
   vs 壳层职责）与 B3/#76 目录驱动设计全部保留。
4. **执行缝（Q5=5a）**：`INavigationExecutor` 与实现整体迁 Host；从 ADR-0016 决策 8 /
   assemblies.md §6"已批准解析缝"清单中移除"导航目录执行缝"（降为 Host 内部件，不再登记
   为跨程序集缝）。第二消费方出现时，按 `IDialogService` 先例把接口上提 Core。
5. **C1 死代码（Q6=6a）**：`INavigationService`/`NavigationService` 删除，含
   `Composition.cs` 开放泛型注册行与 `NavigationTests` 相关用例；`StarPie.Core.csproj`
   移除 `Microsoft.Extensions.DependencyInjection` 包引用（实施时先核对无其它使用点）。
6. **实施批次单票**：决策 1/2/4/5 作为一张实施票落地（文件搬迁 + 注册行调整 + 测试清理 +
   csproj 去包 + 叶子回填），验收 = build 0 警告 0 错误 + xUnit 全量绿；无用户可见行为/
   AutomationId/槽位变化 → e2e 按 ADR-0018 免跑判定（必要时全量跑保险）。
7. **B3/#76 历史不回开**：目录驱动验收达成不变；#76 记录的是历史事实，物理落点修订以
   本 ADR + 现行叶子为准。

## Consequences

- **编译器边界**：模块程序集（M*→Core 单向）不再可达 Host 壳窗口 VM/导航运行时类型；
  Host 侧 `Composition`/`AppHost`/`MainView` 因共享命名空间树无需改代码（仅删 C1 注册行
  与注释）。
- **Core 依赖面**：移除 MS.DI 后 Core 仅余 CommunityToolkit.Mvvm 与 resx 生成器，向
  "共享内核零外部依赖"收敛；`NavigationCatalog` 收口测试与 `NavTab0..4` e2e 契约不变。
- **概念地图**：modules.md S5 变纯契约模块；运行时主体归 H1 宿主壳件；seams.md 不登记
  导航执行缝（Host 内部件）。
- **回填清单（随实施批落地，维持 as-built 纪律）**：
  - `navigation.md`：组成文件（Core 段删运行时、增 Host `Services/Navigation` 与
    `ViewModels/Navigation` 段）、关键流程 2/3 引用改 Host；
  - `modules.md`：§3 S5 收敛为契约模块、§5 D3 修订物理落点、§4 归属表新增 R9
    （导航运行时主体 → H1，迁移 = 本 ADR）；
  - `assemblies.md`：§2/§4 程序集地图（运行时移 Host 行）、§5.1/§5.2 表述、
    §9 现状段与 B3 历史行加注；
  - `seams.md`：§2 导航缝补注（执行缝为 Host 内部件）；
  - `layering.md`：主框架 VM 拆分节（两 VM 均 Host）、Services 解析点例外删除
    `NavigationService<T>`；
  - `layout.md`：Core/Host 目录表两行迁移；
  - `host.md`：组成文件与 ConfigureServices 段（删除开放泛型注册、改"迁 Core"措辞）；
  - `architecture.md`：§3 Core 描述（"Navigation 内核与槽位表"→目录契约 + 运行时归 Host
    注记）；ADR 索引新增本行（随本文档批）。
- **未来演进路径**：若出现第二消费方（如 M* 页面请求导航、消息驱动跳转），按契约上提
  流程把 `INavigationExecutor`（或导航请求消息）提回 Core，另写收口测试。

## 参考事实（2026-09-08 快照）

- `StarPie.Core/Services/Navigation/`：`NavigationCatalog.cs`（契约四件）、`NavigationStore.cs`、
  `NavigationExecutor.cs`（接口+实现）、`INavigationService.cs`、`NavigationService.cs`；
  `StarPie.Core/ViewModels/Navigation/`：`MainViewModel.cs`、`NavigationItemViewModel.cs`。
- 运行时消费者全部在 Host：`Composition.cs`（L149–154 注册 `NavigationStore`/开放泛型/
  `INavigationExecutor`，L173 注册 `MainViewModel`；L74/L94 解析）、`AppHost.cs`（L31–71）、
  `MainView.xaml.cs`（L30 DataContext）、`ShellViewModel`（XML 注释）。
- 模块程序集对四类运行时类型零引用（grep 验证）；Core 中 `Microsoft.Extensions.
  DependencyInjection` 仅 `NavigationExecutor.cs` 与 `NavigationService.cs` 使用。
- `NavigationTests.cs` 覆盖 NavigationStore/NavigationService（C1 用例将删除）/
  NavigationExecutor/MainViewModel。
