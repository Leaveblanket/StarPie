# 模块契约硬边界与共享内核收窄：模块出口契约入 `*.Contracts`、S1 成集、共享内核仅留全局机制/数据

> Status: Active（部分被 ADR-0027 修订）
>
> 修订：对外契约判据（模块出口契约分散入各 `*.Contracts`）由 [ADR-0027](0027-plugin-architecture-and-host-sdk-ui-split.md)
> 修订为 SDK 单一引用面；内部模块边界判据继续有效。
>
> 现行程序集形态与依赖方向的正典：`docs/architecture/assemblies.md`（§2/§3）。

## 动机

1. **共享内核成为“公共协议收容所”**：旧判据“出现第二消费方族 → 上提共享内核”把模块出口契约（`IDialogService`/`IProgramScanner`/`ProgramEntry`/`ProgramCatalog`/`IShortcutTargetResolver`/`IProfilePreviewSource`）囤进 Core，模块间真实协议与内核全局机制混居；Core 是否引用业务模块、模块间是否互引，编译期边界仍靠文档与放行清单。
2. **两派分歧**：并集（契约留 Core、维持 8 程序集拓扑）vs 子集（契约随拥有方下沉）。裁决采纳**模块化单体硬边界派**：模块只经 `*.Contracts` 通信、runtime 永不互引，并解除“8 程序集 = 固定上限”前提，允许新增契约程序集。
3. **SPI 归属死结**：`IShortcutTargetResolver` 实现方是 M3（Programs），消费方图标服务原住 Core（S1）。契约若随实现方下沉 Programs.Contracts，Core 将产生反向依赖；解结 = S1 图标服务整体独立成集（消费方离开 Core），SPI 随实现方入 Programs.Contracts。
4. **程序集环约束**：`IProfilePreviewSource` 实现方 M1（Gestures runtime）、消费方 M2（Wheel runtime），而 Gestures runtime 已依赖 Wheel runtime（`IWheelFactory` 允许边）；契约若与实现方同驻 runtime 会产生程序集环 → 契约必须进独立 Contracts 程序集。
5. **契约程序集可按需引用 WPF**：`ProgramEntry` 携带 `ImageSource?`、`IThemeService` 签名含 WPF 类型 → Programs.Contracts / Theme.Contracts 为 WPF 类库；“VM 不得引用 WPF”是类型级分层规则，与程序集形态无关。

## Considered Options

### 内核成员范围
- **并集（契约留 Core）**：治理自洽（破坏性变更本就全仓共担），但 Core 持续承载“无主协议收容所”，模块间真实协议与内核机制混居 → 否（作为历史备选记录）。
- **子集（契约随拥有方 runtime 下沉）**：产生 `Dialogs→Programs`、`M1/M2/M5→StarPie.Dialogs` 等 runtime 互引与传递依赖 → 否。
- **子集 + 每模块 Contracts（硬边界）** → **采纳**：契约随实现方入薄 Contracts 程序集；runtime 互引清零；共享内核只留全局机制/数据与共享基建。

### S1 图标服务归属（解 SPI 死结）
- **S1 留 Core、SPI 留 Core（DIP）**：Core 仍持有“模块实现的内核 SPI”，且 S1 未来独立成集无门 → 否。
- **S1 独立成集** → **采纳**：`StarPie.Icons.Contracts`（契约/资产表/DTO）+ `StarPie.Icons`（实现 + 注册器）；SPI 随实现方进 Programs.Contracts。

### `IProfilePreviewSource` 归属
- **Gestures.Contracts** → **采纳**：生产方语义（预览源属 M1 配置方案数据），未来第三个消费方引用 Gestures.Contracts 即可；同时破除 Wheel ↔ Gestures runtime 环。
- **消费方 Wheel runtime 同集（DIP）**：零新程序集，但“预览源”语义挂在轮盘模块，未来非轮盘消费方被迫引用 Wheel runtime → 否。
- **留共享内核**：与“契约迁出 Core”自相矛盾 → 否。

### Theme/Wheel 既有允许 runtime 边（硬边界推论）
- **维持接口与实现同集（旧允许边不变）**：与“runtime 互不引用”冲突 → 否。
- **一并契约化** → **采纳**：`IThemeService` → Theme.Contracts、`IWheelFactory`/`IWheelViewModel` → Wheel.Contracts；原允许 runtime 边（M1→M2、M2→M4、Dialogs→M4）全部删除，改经契约边。

## Decision

1. **契约化与图标服务成集**（物理落点已被 [ADR-0027](0027-plugin-architecture-and-host-sdk-ui-split.md) 决策 7 演进；现行程序集形态见 [assemblies.md](../architecture/assemblies.md) §2/§3）：模块出口契约随实现方下沉薄 `*.Contracts`，图标服务的契约与实现分集；本 ADR 只承载该判据与分层动机，不记录程序集清单。
2. **契约归属判据**：模块出口契约随**实现方模块**入其 `*.Contracts`；共享件出现第二消费方族时——若属全局机制入内核，若属某模块出口契约下沉该模块 Contracts。
3. **契约归属判据的落地**：模块出口契约随实现方下沉其 `*.Contracts`。各契约具体承载哪些类型、
   现行落在哪个程序集，见 [assemblies.md](../architecture/assemblies.md) §2/§3——本 ADR 不复制类型清单。
4. **S1 图标资产按同一判据成集**：契约与无状态纯资产表随集、实现与注册器另集；现行落点同上。
5. **允许 runtime 边清零**：M1→M2（`IWheelFactory`）、M2→M4（`IThemeService`）、Dialogs→M4（`IThemeService`）三条 runtime 允许边删除，改经 Wheel.Contracts / Theme.Contracts 契约边；接合缝编目允许边档相应改写（[assemblies.md](../architecture/assemblies.md) §8）。Host → 全部 runtime（组合根例外）保留；Host/Tests 直接消费的契约另加显式引用。
6. **共享内核收窄边界**：共享内核只留全局机制/数据与共享基建（配置模型与读写、本地化、消息 hub、导航目录/槽位契约、宿主回调契约），不承载任何模块出口契约；模块引用共享基建 runtime 不算模块间互引（现行落点见 [assemblies.md](../architecture/assemblies.md) §2/§3）。
7. **命名与命名空间**：程序集/项目名 `<模块>.Contracts`；命名空间维持 `StarPie.*` 树不变（命名空间 ≠ 程序集名），搬迁零 `using` 改动面。
8. **编译器边界判据**：模块 runtime 之间零 ProjectReference；业务模块可达的模块类型仅限其显式引用的 Contracts（薄、无实现传递依赖）；实现类 public（被测），不可达性由程序集引用保证（现行断言与清单见 [assemblies.md](../architecture/assemblies.md)）。

## Consequences

- 模块加协议 = 新增/扩展 Contracts，不再触碰 Core；共享放行清单相应收窄（modules.md §2.3）。
- 运行期行为无用户可见变化（资源键/窗口/主题/槽位不变）。
- 出现第二消费方或新跨模块协议时按本判据开新 Contracts 或扩展现有 Contracts（消费方驱动）；S2/S3/S4 若出现独立 runtime 诉求，按 Dialogs/S1 先例成集。
