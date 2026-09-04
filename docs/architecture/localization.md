# 模块：本地化与消息

> 本文是 [docs/architecture.md](../architecture.md) 的拆分文档；新增文案键、消息类型时读本篇。

## 职责

四语言文案唯一键表（resx）、运行时语言切换（实例服务 + Application 级语言字典）、跨层不可变消息。

## 组成文件

`Services/Localization/ILocalizationService.cs`、`Services/Localization/LocalizationService.cs`、
`Services/Localization/Strings.resx`（中性 = zh-CN）与 `Strings.zh-TW/en/ja.resx`（卫星，
`VocaDb.ResXFileCodeGenerator` 强类型资源）、`AppHost.cs`（运行时语言字典投影 + 壳外文案刷新）、
`Services/Messages/Messages.cs`、`Services/Messages/Notices.cs`。

## 关键流程

1. **resx 数据源 + 实例服务**（ADR-0013/#44-#45）：`LocalizationService` 经 `Strings.ResourceManager`
   取词；回退链为“目标语言 → zh-CN 中性 → 键名”。`SetLanguage(code)` 支持 `Auto`
   （按 `CurrentUICulture` 解析 zh-TW/zh/ja/en）与已知码/别名；语言实际变化才触发
   `LanguageChanged`。静态 `I18n` 已删除（S4/#45），消费点一律注入 `ILocalizationService`。
2. **XAML 声明式文案**：`AppHost.Run` 订阅 `LanguageChanged` 并维护 Application 级静态
   `LanguageDictionary`（MergedDictionaries 中仅一份，切语原地 `Clear` 重建，数据源为
   `EnumerateCurrentEntries()`；键是 `{DynamicResource}` 的源）——**静态文案一律声明式，
   不 code-behind 回填**（[ADR-0010](../adr/0010-localization-copy-principles.md)）。
3. **文案分类**（术语见 `CONTEXT.md`）：声明式（`{DynamicResource}`）/ 驻留（长期 VM 持有、
   语言切换时刷新：`MainViewModel.WindowTitle`/导航标题、
   `WheelAppearanceSettingsViewModel.ThemeOptions`（轮盘配色，#56 起随轮盘外观子 VM 迁址）、
   `InterfaceThemeSettingsViewModel.AppThemeOptions`（界面主题，#54）等）/
   即时取词（每次展示读当前语言：通知、对话框标题与系统文件对话框文案、托盘菜单）/
   壳外（托盘 tooltip：`AppHost` 订阅 `LanguageChanged` 按暂停态刷新）。
4. **消息**：`Services/Messages/Messages.cs` 放 IMessenger 消息（不可变空载体/record，如
   `DebouncedSaveRequestedMessage`、`ImmediateSaveRequestedMessage`、`ConfigImportedMessage`、
   `MinimizedToTrayMessage`、`PageConfigReloadedMessage`、#54 起 `AppThemeChangedMessage`）；
   `Notices.cs` 放非 messenger 的跨层载体（`NoticeKind`/`NoticeRequest`）。
5. **新增/修改文案后**补齐四语言 resx 键值并核对 `docs/i18n-copy-inventory.md`。

## 扩展点

- 新消息：按 [extending.md](extending.md) 各原型清单落位。
- 新语言：新增卫星 resx（`Strings.xx.resx`）+ `LanguageCode` 枚举与解析分支
  （涉及 CONTEXT/ADR，谨慎）。
- 新文案键：`Strings*.resx` 四语言同步 + 盘点清单登记（声明式键无需其它接线；
  即时取词/驻留按 ADR-0010 分类落位）。

## 参见 ADR

[0010](../adr/0010-localization-copy-principles.md)（文案原则，决策 1 已被 0013 修订）、
[0013](../adr/0013-localization-theme-overhaul.md)（resx+实例服务+整项替换）、
[0005](../adr/0005-di-container-for-navigation.md)（消息总线）。
