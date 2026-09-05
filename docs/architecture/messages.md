# 模块：消息与通知

> 本文是 [docs/architecture.md](../architecture.md) 的拆分文档；新增/修改跨模块消息或通知类型时读本篇
> （B1/#64 自原「本地化与消息」叶拆出：消息与本地化无共享上下文，不并入 [localization.md](localization.md)）。

## 职责

跨模块协调事件契约 hub（IMessenger 消息）与弹窗通知载体（非 messenger 的跨层载体）。中心 hub 保留
（[ADR-0015](../adr/0015-module-map-and-ownership.md) 决策 7/Q16-A）；新消息/通知类型是放行共享面
（[modules.md](modules.md) §2.3），领域语义的消费流程在各所属模块叶子描述，本叶承载 hub 的物理集中。

## 组成文件

`Services/Messages/Messages.cs`（IMessenger 消息，不可变空载体/record）、`Services/Messages/Notices.cs`
（非 messenger 的跨层载体：`NoticeKind`/`NoticeRequest`）。

## 关键流程

1. `Messages.cs` 放跨模块协调消息：`DebouncedSaveRequestedMessage`/`ImmediateSaveRequestedMessage`
   （保存语义见 [config.md](config.md)）、`ConfigImportedMessage`、`MinimizedToTrayMessage`、
   `PageConfigReloadedMessage`、#54 起 `AppThemeChangedMessage`（主题应用语义见
   [interface-theme.md](interface-theme.md)）。
2. `Notices.cs` 放非 messenger 的跨层载体（`NoticeKind`/`NoticeRequest`），供托盘气泡等通知使用。
3. 消息命名遵循 [naming.md](naming.md) 的消息命名表（`XxxRequestedMessage`/`XxxChangedMessage`/…）；
   跨页协调走 IMessenger，同页状态不得用 messenger 替代绑定（见 [layering.md](layering.md)）。

## 扩展点

- 新消息/通知类型：在 `Services/Messages/` hub 文件登记即可（放行共享面，[modules.md](modules.md) §2.3）；
  消费方与领域语义描述放所属模块叶子。
- 新语言/新文案键与消息无关，见 [localization.md](localization.md)。

## 参见 ADR

[0005](../adr/0005-di-container-for-navigation.md)（消息总线与容器导航）、
[0015](../adr/0015-module-map-and-ownership.md)（12 模块地图：S4 与决策 7）。
