# 模块契约硬边界与共享内核收窄：模块出口契约入 `*.Contracts`、S1 成集、Core 仅留全局机制/数据

模块出口契约（接口 + 跨模块 DTO/纯数据）随**实现方模块**下沉到各自 `*.Contracts`
程序集；模块 runtime 之间**互不引用**，只经 Contracts 通信（.NET 模块化单体硬边界派）；
`StarPie.Icons`（S1 图标服务）独立成集以解 SPI 归属死结；`StarPie.Core` 收窄为
「全局机制/数据 + 共享基建」。本决策是 #89（共享内核章程）Q2 的裁决，讨论锚点 #90；
前导：ADR-0021/#92（导航运行时迁 Host）、ADR-0022（共享 UI 基建去共享化）；决策记录
#93，实施票 #95–#97。

## Status

Accepted（2026-09-08/09 grill-with-docs 会话：#89 Q2/#90，用户裁决硬边界派并解除
「8 程序集不变」前提；决策先行——实施票 #95/#96/#97 落地，叶子维持 as-built 随实施
回填，ADR-0016/0020/0021 同款纪律）。

## 背景与动机

1. **#89 Q1 判据与「第二消费方族 → 上提 Core」的张力**：Q1 已确认「内核成员 = 全局
   机制/数据，变更受治理」；但 modules.md §2 判据 3 的旧执行口径把「出现第二消费方族」
   的模块出口契约（`IDialogService`/`IProgramScanner`/`ProgramEntry`/`ProgramCatalog`/
   `IShortcutTargetResolver`/`IProfilePreviewSource`）囤进 Core（ADR-0020/#88 先例），
   Core 成为「公共协议收容所」，模块间真实协议与内核全局机制混居。
2. **Q2 分歧**：并集（契约留 Core、维持 8 程序集拓扑）vs 子集（契约随拥有方下沉）。
   用户裁决采纳**模块化单体硬边界派**：模块只经 `*.Contracts` 通信、runtime 永不互引
   （fullstackhero .NET 10 明文规则；Dometrain/DevLeader/Kamil 样例同构），并解除
   「8 程序集目标态 = 固定上限」前提，允许新增契约程序集。
3. **SPI 归属死结**：`IShortcutTargetResolver` 的实现方是 M3（Programs），但消费方
   `IconAssetService` 住在 Core（S1）。若契约随实现方下沉 Programs.Contracts，Core 将
   产生 `Core → Programs.Contracts` 反向依赖，击穿「共享内核不引用业务模块」基线；
   这正是 ADR-0019/#87 用「契约驻 Core、模块实现」清掉的回填缝的另一种形态。解结 =
   S1 图标服务整体独立成集（消费方离开 Core），SPI 随实现方入 Programs.Contracts。
4. **程序集环约束**：`IProfilePreviewSource` 实现方 M1（Gestures runtime）、消费方 M2
   （Wheel runtime）；而 Gestures runtime 已依赖 Wheel runtime（`IWheelFactory` 允许
   边）。契约若与实现方同驻 Gestures runtime，会产生 Wheel ↔ Gestures runtime 程序集
   环（C# 禁止）→ 契约必须进独立 Contracts 程序集（或留共享处）。
5. **契约程序集可按需引用 WPF**：`ProgramEntry` 携带 `ImageSource?`（`IconSource`），
   `IThemeService.ApplyWindowTheme(FrameworkElement)` 签名含 WPF 类型 →
   Programs.Contracts / Theme.Contracts 为 WPF 类库；「VM 不得引用 WPF」是类型级分层
   规则，与程序集形态无关。

## Considered Options

### Q2. 内核成员范围（#90 本体）
- **A. 并集（契约留 Core）**：治理自洽（破坏性变更本就全仓共担）、社区 shared kernel
  可收公共契约（Milan）；但 Core 持续承载「无主协议收容所」，模块间真实协议与内核
  机制混居，编译期边界仍靠文档与放行清单 → 否（作为历史备选记录）。
- **B. 子集（契约随拥有方 runtime 下沉）**：产生 `Dialogs→Programs`、
  `M1/M2/M5→StarPie.Dialogs` 等 runtime 互引与传递依赖，推翻 ADR-0020/seams 分层基线
  → 否。
- **C. 子集 + 每模块 Contracts（硬边界，采纳）**：契约随实现方入薄 Contracts 程序集；
  runtime 互引清零；共享内核只留全局机制/数据与共享基建。

### Q3. S1 图标服务归属（解 SPI 死结）
- **a. S1 留 Core、SPI 留 Core（DIP）**：维持可运行，但 Q2=C 下 Core 仍持有「模块
  实现的内核 SPI」，且 S1 未来独立成集无门 → 否。
- **b. S1 独立成集（采纳）**：`StarPie.Icons.Contracts`（契约/资产表/DTO）+
  `StarPie.Icons`（实现 + 注册器）；SPI `IShortcutTargetResolver` 随实现方进
  Programs.Contracts；`Icons` runtime 经 Contracts 消费 SPI，无 Core 反向。

### Q4. `IProfilePreviewSource` 归属
- **a. `Gestures.Contracts`（采纳）**：生产方语义（预览源属 M1 配置方案数据），未来
  第三个消费方引用 Gestures.Contracts 即可；同时破除 Wheel ↔ Gestures runtime 环。
- **b. 消费方 Wheel runtime 同集（DIP）**：零新程序集，但「预览源」语义挂在轮盘模块，
  未来非轮盘消费方被迫引用 Wheel runtime → 否。
- **c. 留 Core**：与 Q2=C「契约迁出 Core」自相矛盾 → 否。

### Q5. Theme/Wheel 既有允许边（硬边界推论）
- **a. 维持接口与实现同集（旧允许边不变）**：与「runtime 互不引用」冲突；Wheel/Dialogs
  继续编译期引用 Theme/Wheel runtime 及其实现 → 否。
- **b. 一并契约化（采纳）**：`IThemeService` → Theme.Contracts、
  `IWheelFactory`/`IWheelViewModel` → Wheel.Contracts；现三条允许 runtime 边
  （M1→M2、M2→M4、Dialogs→M4）全部删除，改经契约边。

## Decision

1. **程序集目标态 = 15**（现 8 + `Programs.Contracts`/`Dialogs.Contracts`/
   `Theme.Contracts`/`Wheel.Contracts`/`Gestures.Contracts` 5 + `StarPie.Icons.Contracts`/
   `StarPie.Icons` 2）。**部分推翻 ADR-0016「8 程序集目标态」与 ADR-0020「契约上提
   Core」的物理落点表述**；两 ADR 的批次历史与分层动机不回开，仅目标态与判据修订。
2. **契约归属判据修订**：模块出口契约随**实现方模块**入其 `*.Contracts`，取代
   modules.md §2 判据 3「第二消费方族 → 上提 Core」的旧执行口径；该判据仅剩「全局
   机制/数据」适用（共享件出现第二消费方族 → 若属全局机制入内核，若属某模块出口契约
   下沉该模块 Contracts）。
3. **各 Contracts 承载**：
   - `Programs.Contracts`（WPF 类库）：`IProgramScanner`、`ProgramEntry`、
     `ProgramCatalog`、`IShortcutTargetResolver`（SPI 随实现方）；
   - `Dialogs.Contracts`（纯 C#）：`IDialogService` + 6 结果 record；
   - `Theme.Contracts`（WPF 类库）：`IThemeService`；
   - `Wheel.Contracts`：`IWheelFactory`、`IWheelViewModel`（+ 签名暴露件）；
   - `Gestures.Contracts`：`IProfilePreviewSource`。
4. **S1 成集**：`StarPie.Icons.Contracts` = `IIconAssetService`/`IconCatalog`/
   `CustomIconItem`/`VectorIconItem`（`IconCatalog` 为无状态纯资产表，作资产目录契约
   随集）；`StarPie.Icons` = `IconAssetService` + `IconsModuleRegistrar`。实施中间态
   （#95）`IShortcutTargetResolver` 暂留 Core（Icons runtime → Core），#96 迁
   Programs.Contracts 后 Icons runtime 改经 Programs.Contracts。
5. **允许 runtime 边清零**：M1→M2（`IWheelFactory`）、M2→M4（`IThemeService`）、
   Dialogs→M4（`IThemeService`）三条 runtime 允许边删除，改经 Wheel.Contracts /
   Theme.Contracts 契约边；seams.md 允许边档相应改写。Host → 全部 runtime（组合根
   例外）保留；Host/Tests 直接消费的契约另加显式引用。
6. **Core 收窄边界**：S1 六文件、`Services/Programs/`、`Services/Dialogs/`、
   `ViewModels/Pages/IProfilePreviewSource.cs` 迁出后，Core 仅留 Models（config POCO/
   值类型）、S2 配置、S3 本地化（+resx）、S4 消息 hub、S5 导航目录/槽位契约
   （ADR-0021）、`AppHostDelegates`；S2/S3/S4 作为共享基建例外留 Core（模块引用共享
   基建 runtime ≠ 模块间互引，同 fullstackhero 引用 Infrastructure 项目）。
7. **命名与命名空间**：程序集/项目名 `<模块>.Contracts`；命名空间维持 `StarPie.*`
   树不变（B10/#83 先例：命名空间 ≠ 程序集名），搬迁零 `using` 改动面。
8. **实施批次**：#95（S1 成集）→ #96（Programs.Contracts + Dialogs.Contracts）→
   #97（Theme/Wheel/Gestures.Contracts + 允许边清零）；前置 #92（导航迁 Host）与 #93
   （本批 ADR docs）先行；#94（UI 基建去共享化，ADR-0022）与 #95–#97 依集成面串行
   （csproj/slnx/Composition 同批互斥）。每票验收 = build 0 警告 0 错误 + xUnit 全量绿
   + PlacementTests 更新 + 叶子回填；e2e 按 ADR-0018 免跑判定（无可见文案/DataTemplate/
   AutomationId/槽位/主题/配置变化时免跑，拿不准全量）。

## Consequences

- **编译器边界**：模块 runtime 之间零 ProjectReference；业务模块可达的模块类型仅限
  其显式引用的 Contracts（薄、无实现传递依赖）；实现类 public（被测）但不可达性由
  程序集引用保证。
- **Core 面貌**：不再含任何模块出口契约与 S1 资产；模块加协议 = 新增/扩展 Contracts，
  不再触碰 Core（共享放行清单相应收窄）。
- **测试**：测试工程显式引用全部程序集（含新 Contracts）；`*AssemblyPlacementTests`
  增加「接口驻 Contracts」「runtime 互不引用」断言族；Dialogs/Programs/Theme/Wheel/
  Gestures/SharedUi 既有断言随迁更新。
- **运行期行为**：无用户可见变化（资源键/窗口/主题/槽位不变）；XAML 中
  `assembly=StarPie.Core` 的 xmlns 引用（HotkeyRecorderBox 等）属 #94/#97 处理面。
- **回填清单（随 #95–#97 落地，维持 as-built 纪律）**：
  - `modules.md`：§2 判据 3 修订、§2.3 放行清单、§3 M1/M2/M3/M4/S1/S6 对外契约表述、
    §5 D5；
  - `assemblies.md`：§2/§3/§4/§5 目标态 15 程序集与依赖矩阵、§8 批次历史注记、§9
    现状段；
  - `layering.md`：程序集层、依赖矩阵、Services/ViewModels 契约引用表述；
  - `seams.md`：允许边档（M1→M2/M2→M4/Dialogs→M4 runtime）改写为契约边、S21 历史
    注记、SPI 登记；
  - 叶子：`programs.md`/`dialogs.md`/`theme(interface-theme).md`/`wheel.md`/
    `gestures.md`/`navigation.md`/`layout.md`/`host.md`；
  - `architecture.md`：§3 Core/模块程序集描述与 ADR 索引行（索引随本批）。
- **未来演进**：出现第二消费方或新跨模块协议时按本判据开新 Contracts 或扩展现有
  Contracts（消费方驱动）；S2/S3/S4 若出现独立 runtime 诉求，按 Dialogs/S1 先例成集
  （触发条件登记入章程 B 类）。

## 参考事实（2026-09-09 快照）

- 现 csproj 引用：Dialogs → Core+Theme；Gestures → Core+Wheel；Wheel → Core+Theme；
  Programs/Shell/Theme → Core；Host → 全部；Tests → 显式全部。
- 消费矩阵（非测试引用次数）：`IDialogService` Gestures 10/Shell 6/Wheel 3/Host 2；
  `IProgramScanner` Dialogs 8/Host 1；`IThemeService` Dialogs 8/Wheel 5/Host 3；
  `IWheelFactory` Gestures 4/Host 1；`IProfilePreviewSource` Wheel 8/Host 2；
  `IShortcutTargetResolver` Programs 7（实现）/Dialogs 5/Core 4（IconAssetService）/
  Host 3；`IIconAssetService` Dialogs 8/Wheel 6/Gestures 5/Programs 3/Host 5。
- `StarPie.Core/Services/Icons/` 六文件：`IconCatalog`/`IIconAssetService`/
  `IconAssetService`/`IShortcutTargetResolver`/`CustomIconItem`/`VectorIconItem`；
  `Services/Dialogs/`：`IDialogService` + 6 record；`Services/Programs/`：
  `IProgramScanner`/`ProgramEntry`/`ProgramCatalog`；`ViewModels/Pages/`：
  `IProfilePreviewSource`。
- `ProgramEntry` = `record(string Name, string Path, string FriendlyPath,
  ImageSource? IconSource)` → Programs.Contracts 为 WPF 类库。
- Gestures 对 Wheel 的引用仅 `IWheelFactory`/`IWheelViewModel`（GestureEngine）；
  Wheel/Dialogs 对 Theme 的引用全部为 `IThemeService`。
