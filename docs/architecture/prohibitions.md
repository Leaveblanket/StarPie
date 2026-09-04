# 禁止事项

> 本文是 [docs/architecture.md](../architecture.md) 的拆分文档；审查代码或动手改代码前核对底线。

- 不得在 ViewModel 使用 WPF 表现类型（`Window`/`MessageBox`/`Color`/`Brush`/对话框）或暴露临时 `event Action`。
- 不得在 View 中写业务、配置、服务调用或副作用（`IThemeService` 白名单除外）；不得反向依赖 `Composition`。
- 不得在使用处创建 `ServiceCollection`、服务实例或静态服务定位器；解析点只在组合根（`NavigationService<T>` 例外）。
- 不得改变现有 `config.json` 字段语义；新字段必须带默认值。
- 不得修改 `releases/` 旧版本代码。
- 不得新增“对话框第二种形态”或绕过 `IDialogService` new 对话框；新对话框必须同名配对（`InputDialog` 例外不复制）。
- 不得在源码根新建未登记的目录/杂项（如根级 `Controls/`、HTML 原型）。
- 不得引入 `InternalsVisibleTo` 或 mocking 框架作为绕过测试边界的手段。
- 不得用 messenger 替代同页绑定；不得把 ADR-0009 白名单 code-behind “好心”迁进 ViewModel（反之亦然）。
- 不得把 ADR 推理复制进规范文档；规范文档只保留规则与引用。

分层边界细则见 [layering.md](layering.md)；新功能流程见 [extending.md](extending.md)。
