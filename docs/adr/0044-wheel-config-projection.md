# 轮盘配置投影：瞬态视图数据收窄宽 AppConfig 耦合

> Status: Active
>
> 本文收口 M2 轮盘与渲染模块（`docs/architecture/wheel.md`）的运行时配置耦合：
> `WheelViewModel` 把整个 `AppConfig` 透传给 `RadialWindow`，视图内联读取十余个配置字段。
> 模块契约边界见 [ADR-0023](0023-module-contracts-hard-boundary-and-core-narrowing.md)；
> 外观只读状态接口先例见 [ADR-0014](0014-wheel-palette-module-boundary-and-appearance-split.md)。

## 动机

1. **投影已建，漏了最后一米**：外观页已有 `IWheelAppearanceState`（20+ 窄属性只读投影）
   作为渲染输入契约的先例；但运行时轮盘的 `WheelViewModel.Config` 仍是整个 `AppConfig`——
   含黑名单（BlacklistedProcesses）、Profiles 等与轮盘渲染无关的字段，视图对全局配置模型
   形成宽耦合。
2. **视图直读配置导致职责下沉混乱**：`RadialWindow.xaml.cs` 内联读 `Config.CoreBgImagePath`、
   `Config.Shape`、`Config.IconLayoutMode` 等十余处，并直接做文件 I/O
   （`File.Exists` / `new BitmapImage`）——读磁盘不该发生在视图 code-behind。
3. **"边界在轮盘外观参数处收窄"本就是既有意图**：ADR-0023 的核心收窄方向即"模块消费的
   配置面只含自身字段"，当前实现没跟上意图。

## Considered Options

- **复用/扩展 `IWheelAppearanceState` 作为运行时投影** → 否。它是设置页单例的只读状态面，
  生命周期（常驻、写穿运行态配置）与轮盘瞬态（每次手势创建、随窗口销毁）完全不同，
  强行共用会把两个场景焊死。
- **保持 `WheelViewModel.Config` 整体透传，靠约定不读无关字段** → 否。宽接口本身就是邀请；
  下一个加配置字段的人无从知晓轮盘视图"不该"读它。
- **新建瞬态投影 `WheelViewData`，由 `WheelFactory` 创建时从 `AppConfig` 快照组装** → 采纳。

## Decision

1. **新建瞬态投影（`WheelViewData`）**：只含轮盘渲染所需字段（Shape、几何参数、配色方案、
   核图标相关、排版参数等），由 `WheelFactory` 在创建轮盘时从运行态 `AppConfig` **快照组装**，
   瞬态生命周期与轮盘窗口一致；`RadialWindow` 不再看得见全局配置。
2. **核图加载等文件 I/O 移出视图**：背景图存在性检查与 `BitmapImage` 构造下沉到资产服务
   （`IIconAssetService` 同层），视图只消费解析结果。
3. **投影是快照不是引用**：轮盘弹出期间配置变更不实时反映到已弹出的轮盘（本就是亚秒级
   瞬态，下次手势自然取新值）；不引入变更传播。
4. **窄调色板输入类型由本 ADR 的投影一并交付**：`IRadialStyleRenderer.Initialize` 与
   `WheelPaletteParser.Resolve` 现接收整个 `AppConfig`，运行态（`RadialWindow`）与预览态
   （`WheelPreviewRenderer`）两处调用点共用同一签名。该签名收窄为只含配色解析所需字段的
   **窄类型**，与投影同批交付，两处调用点一并迁移——不留给预览侧自建"设置页对应物"，
   避免同一 `Initialize` 被两个来源不同的类型各喂一份。

## Consequences

- **`IWheelAppearanceState.CurrentConfig` 的同类漏口由本决策连带收口**：预览渲染器不再直读
  `AppConfig`，改吃 [ADR-0045](0045-wheel-preview-runtime-shared-content-kernel.md) 共享内核
  输出的纯数据或本决策交付的窄调色板输入类型。
- **窄调色板输入类型的收窄是破坏性签名变更**（接口 + 基类 + 各风格渲染器子类 + 两处调用点，
  约 8 个文件），但不构成宽改：改动可单批落绿，无需 expand–contract 展开期。
- **`WheelAppearanceSettingsViewModel` 的多职责拆分不在本文范围**：其多职责
  （外观状态实现 + 预设管理 + 配置直写 + 预览触发）是设置页侧问题，另行处理。
- 轮盘弹出瞬态与设置页写穿的配置一致性由"快照 + 下次手势取新值"承担，无锁无竞态。
