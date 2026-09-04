# 模块：配置与保存

> 本文是 [docs/architecture.md](../architecture.md) 的拆分文档；涉及 `config.json`、自动保存、导入/导出时读本篇。

## 职责

运行态配置单源、防抖自动落盘、导入/导出。

## 组成文件

`Services/Configuration/`：`IConfigService`/`JsonConfigService`、`ISaveDebouncer`/`DispatcherSaveDebouncer`、`SettingsSaveOrchestrator`、`AppDataPaths`、`AutostartRegistry`；`Services/Messages/Messages.cs` 中的保存消息。

## 生命周期与关键流程

1. 启动：`Composition` 以 `AppDataPaths.GetAppDataFolder()/config.json` 构造 `JsonConfigService`；`Config.Load()` 读取（缺文件播种默认并落盘；损坏回退默认；宽容 JSON）并 `I18n.SetLanguage`。
2. 运行：VM 修改运行态 `AppConfig` → 发 `DebouncedSaveRequestedMessage.Instance`（连续变更）或 `ImmediateSaveRequestedMessage.Instance`（需立即落盘）。
3. `SettingsSaveOrchestrator`（单例，组合根解析保活）订阅两类消息：防抖经 `ISaveDebouncer`（`DispatcherSaveDebouncer`，UI 线程 `DispatcherTimer`，`AutoSaveDelay = 400ms`）折叠；立即请求 `CancelPending + Save`。
4. 兜底冲刷点（`FlushPendingSave()` = `SaveNow()`）：显式保存、设置窗口隐藏、退出、**导入前**。
5. 导入/导出保留在具体类 `JsonConfigService.Import/Export`（不在 `IConfigService`），经组合根委托注入 `GeneralSettingsViewModel`；导入前先 `FlushPendingSave()`，再替换运行态配置并广播 `ConfigImportedMessage`/`PageConfigReloadedMessage`。

## 扩展点

- 新设置变更：变更处发保存消息即可，不直接写文件。
- 调整落盘节奏：改 `AutoSaveDelay`。
- 改配置路径：只动 `AppDataPaths`/`DevInstance`。

## 参见 ADR

[0002](../adr/0002-manual-composition-root.md)（配置接缝）。
