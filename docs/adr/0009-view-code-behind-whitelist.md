# View code-behind 白名单与输入适配边界

> Status: Active（[ADR-0014](0014-wheel-palette-module-boundary-and-appearance-split.md) 补充）
>
> **更新（ADR-0014）**：界面主题应用与预览重绘补充消息化驱动（`AppThemeChangedMessage`/`AppearancePreviewInvalidatedMessage`），见 [ADR-0014](./0014-wheel-palette-module-boundary-and-appearance-split.md) 决策 7。

承接 ViewModel/View 严格边界纪律（现行规范见 `docs/architecture/layering.md` Views 节），明确 View 的 `.cs` 里允许保留什么、哪些必须 Binding/ICommand 化。此前各页迁移后仍残留手写 VM 状态读写、空壳事件与 View→Composition 反向依赖，且无文档界定归属，未来 agent 无法判断某段 code-behind 该留该迁。

## 决策

- **View `.cs` 只保留一份封闭白名单**（五项：生命周期接线 / 本地化 / 纯视觉渲染 / 纯 UI 适配 /
  壳层职责），白名单外一律迁到 Binding、`ICommand` 或 VM——逐项口径与现行条文见
  [layering.md](../architecture/layering.md) Views 节，本 ADR 不复制。
- **状态与动作一律走 Binding / `ICommand`，INPC 订阅只驱动纯视觉渲染**：以此取代此前各页手写的
  VM 状态读写与空壳事件；判据并入 layering.md 的 Views/ViewModels 边界。
- **依赖方向**：View 不反向依赖 `Composition`、服务或配置；壳层主题应用是白名单例外（页面无参
  构造、不经容器，VM 不持 `IThemeService`）。

## Consequences

- 新增或遗留 code-behind 按白名单分类：白名单项保留；非白名单项（状态回填、VM 改写、事件当命令
  入口、反向依赖）迁到 Binding、命令或 VM。
- 未来 agent 不得把白名单项“好心”迁进 ViewModel——它们是呈现层适配而非领域状态。
- 新输入控件先找 `Command`/`InputBinding`/`MouseBinding`/附加行为，再考虑事件。
