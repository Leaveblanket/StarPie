# 模块：本地化与消息

> 本文是 [docs/architecture.md](../architecture.md) 的拆分文档；新增文案键、消息类型时读本篇。

## 职责

四语言文案唯一键表、运行时语言切换、跨层不可变消息。

## 组成文件

`Services/Localization/I18n.cs`、`Services/Messages/Messages.cs`、`Services/Messages/Notices.cs`、`Composition.cs`（语言字典投影）。

## 关键流程

1. `I18n` 静态键表（zh-CN/zh-TW/en/ja，缺语言回退 zh-CN，再缺回退键名）；`I18n.T(key)` 即时取词；`SetLanguage` 支持 `Auto`（按 `CurrentUICulture`）；语言切换触发 `LanguageChanged` 事件。
2. XAML 声明式文案：组合根维护 `Application.Resources.MergedDictionaries` 中的运行时 `LanguageDictionary`（原地 Clear 重建，`I18n.EnumerateCurrentEntries()` 投影；键为 `DynamicResource` 源）——**静态文案一律声明式，不 code-behind 回填**（[ADR-0010](../adr/0010-localization-copy-principles.md)）。
3. 文案分类（术语见 `CONTEXT.md`）：声明式（DynamicResource）/ 驻留（长期 VM 持有，语言切换时刷新，如 `MainViewModel.WindowTitle`）/ 即时取词（每次展示读当前语言，如通知、托盘菜单）/ 壳外（托盘 tooltip 等，组合根订阅 `LanguageChanged` 刷新）。
4. 消息：`Services/Messages/Messages.cs` 放 IMessenger 消息（不可变空载体/record，如 `DebouncedSaveRequestedMessage`、`ImmediateSaveRequestedMessage`、`ConfigImportedMessage`、`MinimizedToTrayMessage`、`PageConfigReloadedMessage`）；`Notices.cs` 放非 messenger 的跨层载体（`NoticeKind`/`NoticeRequest`）。
5. 新增/修改文案后核对 `docs/i18n-copy-inventory.md`。

## 扩展点

- 新消息：按 [extending.md](extending.md) 各原型清单落位。
- 新语言：键表增加 `LanguageCode` 分支（涉及 CONTEXT/ADR，谨慎）。

## 参见 ADR

[0010](../adr/0010-localization-copy-principles.md)（文案原则）、[0005](../adr/0005-di-container-for-navigation.md)（消息总线）。
