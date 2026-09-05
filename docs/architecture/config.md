# 模块：配置与保存

> 本文是 [docs/architecture.md](../architecture.md) 的拆分文档；涉及 `config.json`、自动保存、导入/导出时读本篇。

## 职责

运行态配置单源、防抖自动落盘、导入/导出。

## 组成文件

`Services/Configuration/`：`IConfigService`/`JsonConfigService`、`ISaveDebouncer`/`DispatcherSaveDebouncer`、
`SettingsSaveOrchestrator`、`AppDataPaths`（自启注册表 `AutostartRegistry` 归 M5，已随 #70 收编
`Services/Shell/`，见 [shell.md](shell.md)）；保存请求经消息上报，`DebouncedSaveRequestedMessage`/
`ImmediateSaveRequestedMessage` 定义于 S4 hub `Services/Messages/Messages.cs`（放行共享面，见
[messages.md](messages.md)）。

## 生命周期与关键流程

1. 启动：`Composition` 以 `AppDataPaths.GetAppDataFolder()/config.json` 构造 `JsonConfigService`；`Config.Load()` 读取（缺文件播种默认并落盘；损坏回退默认；宽容 JSON）并 `I18n.SetLanguage`。
2. 运行：VM 修改运行态 `AppConfig` → 发 `DebouncedSaveRequestedMessage.Instance`（连续变更）或 `ImmediateSaveRequestedMessage.Instance`（需立即落盘）。
3. `SettingsSaveOrchestrator`（单例，组合根解析保活）订阅两类消息：防抖经 `ISaveDebouncer`（`DispatcherSaveDebouncer`，UI 线程 `DispatcherTimer`，`AutoSaveDelay = 400ms`）折叠；立即请求 `CancelPending + Save`。
4. 兜底冲刷点（`FlushPendingSave()` = `SaveNow()`）：显式保存、设置窗口隐藏、退出、**导入前**。
5. 导入/导出保留在具体类 `JsonConfigService.Import/Export`（不在 `IConfigService`），经组合根委托注入 `GeneralSettingsViewModel`；导入前先 `FlushPendingSave()`，再替换运行态配置并广播 `ConfigImportedMessage`/`PageConfigReloadedMessage`。

## 扩展点

- 新设置变更：变更处发保存消息即可，不直接写文件。
- 调整落盘节奏：改 `AutoSaveDelay`。
- 改配置路径：S2 内只动 `AppDataPaths`（开发配置夹分支依赖 `DevInstance`，见 [host.md](host.md)）。

## 参见 ADR

[0002](../adr/0002-manual-composition-root.md)（配置接缝）。
