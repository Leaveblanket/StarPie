# StarPie 插件开发手册

> 路由页：只给最短上手路径与指针，不复制正典条文。机制与生命周期 → [plugins.md](plugins.md)；可用面、硬约束（含不支持列表）与准入判据 → [plugin-contracts.md](plugin-contracts.md)；决策理由 → [ADR-0029](../adr/0029-plugin-trust-model.md)。

## 示例在哪

[plugins/samples/README.md](../../plugins/samples/README.md)：`MinimalHeadless` 与 `MinimalUi` 两条最小路径，可独立构建、部署、运行。

## 上手三步

1. 读两个契约对象：`IPluginContext`（headless 面：日志/事件/能力注册）与 `IPluginUiContext`（UI 面，仅清单声明 `ui` 段可拿）——接口与用法见 [plugins.md](plugins.md) §5–§7。
2. 写清单 `plugin.json`（字段与发现期校验，[plugins.md](plugins.md) §3）。
3. 部署到 `%LOCALAPPDATA%\StarPie\plugins\<清单 id>\`——包目录名必须等于清单 id；dev 实例数据目录隔离。

## 验证看哪

- 插件管理页：状态 / 准入四态 / 隔离原因 / 诊断面板 / 重试与停用出口（[plugins.md](plugins.md) §10）；UI 插件页 AutomationId `NavPlugin_<插件 id>`。
- 启动报告 `%LOCALAPPDATA%\StarPie\plugin-startup-report.json`；插件日志经 `IPluginContext.Log`（自动带 plugin id）。

## 准入与维护者私有流程

- 准入四态与签名判据： [plugin-contracts.md](plugin-contracts.md) §5。
- 审核清单重签与密钥轮换：`scripts/sign-review-catalog.ps1`（用法在脚本头部）；流程与公钥 pin 见 [plugin-contracts.md](plugin-contracts.md) §5。
