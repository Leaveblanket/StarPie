# 模块：本地化

> 本文是 [docs/architecture.md](../architecture.md) 的拆分文档；新增/修改文案键、语言切换与回退链时读本篇；
> 跨模块消息与通知见 [messages.md](messages.md)。

## 职责

四语言文案唯一键表（resx）与取词、语言状态/切换/回退链、运行时语言字典投影桥（宿主消费）、文案分类语义。

## 组成文件

**宿主内核（`StarPie.Host/Kernel/Localization/`，命名空间 `StarPie.Kernel.Localization`）**：
`ILocalizationService.cs`、`LocalizationService.cs`、`Strings.resx`（中性 = zh-CN）与
`Strings.zh-TW/en/ja.resx`（卫星，`VocaDb.ResXFileCodeGenerator` 强类型资源——生成器包与
`EmbeddedResource` 条目配置于 `StarPie.Host.csproj`；`RootNamespace=StarPie`
使强类型类落在 `StarPie.Kernel.Localization`）。
设计期投影字典 `DesignTimeStrings.xaml` 与生成脚本在 `StarPie.Ui/Services/Localization/`
（源 resx 在上面的内核目录；见 [design-time-preview.md](design-time-preview.md)）。

> 宿主消费边界（[modules.md](modules.md) §5 D4）：运行时语言字典投影与壳外文案刷新由宿主侧（H1，
> 见 [host.md](host.md)）维护，属 H1 对 S3 的消费，不是本模块组成文件。

## 关键流程

1. **resx 数据源 + 实例服务**（ADR-0013）：`LocalizationService` 经 `Strings.ResourceManager`
   取词；回退链为“目标语言 → zh-CN 中性 → 键名”。`SetLanguage(code)` 支持 `Auto`
   （按 `CurrentUICulture` 前缀规则解析 zh-TW/zh/ja/en）与已知码/别名；任意别名/区域码经
   `AliasToCanonical` 表折叠为规范 BCP-47 码（"zh-CN"/"zh-TW"/"en"/"ja"），未知码兜底 zh-CN，
   语言状态不再保留自定义枚举中间表示；语言实际变化才触发 `LanguageChanged`。
   静态 `I18n` 已删除，消费点一律注入 `ILocalizationService`。
2. **XAML 声明式文案**：宿主 `AppHost.Run`（H1）订阅 `ILocalizationService.LanguageChanged` 并维护
   Application 级静态 `LanguageDictionary`（MergedDictionaries 中仅一份，切语原地 `Clear` 重建，数据源为
   `EnumerateCurrentEntries()`；键是 `{DynamicResource}` 的源）——**静态文案一律声明式，
   不 code-behind 回填**。
3. **文案分类**（术语见 `CONTEXT.md`）：声明式（`{DynamicResource}`）/ 驻留（长期 VM 持有、
   语言切换时刷新：壳层 `ShellViewModel.WindowTitle`（H1 壳窗口，见 [shell.md](shell.md)）/
    导航标题（`MainViewModel`——导航运行时归 Host，见 [navigation.md](navigation.md)）、
   `WheelAppearanceSettingsViewModel.PaletteOptions`（轮盘配色，M2，见 [wheel.md](wheel.md)）、
   `InterfaceThemeSettingsViewModel.AppPaletteOptions`（界面主题，M4，见 [interface-theme.md](interface-theme.md)）等）/
   即时取词（每次展示读当前语言：通知、对话框标题与系统文件对话框文案、托盘菜单）/
   壳外（托盘 tooltip：宿主 `AppHost` 订阅 `LanguageChanged` 按暂停态刷新，见 [host.md](host.md)）。
4. **新增/修改文案后**补齐四语言 resx 键值（新增/修改与盘点登记流程见下方扩展点）。

## 扩展点

- 新语言：新增卫星 resx（`Strings.xx.resx`）+ `AliasToCanonical` 别名表条目（规范码随
  `AutoCultureRules` 前缀规则按需同步；涉及 CONTEXT/ADR，谨慎）。
- 新文案键：`Strings*.resx` 四语言同步 + 盘点清单登记（声明式键无需其它接线；
  即时取词/驻留按文案分类落位，见 [ADR-0013](../adr/0013-localization-theme-overhaul.md)）。
- 新消息/通知类型：见 [messages.md](messages.md)（S4 hub，放行共享面）。

## 参见 ADR

[0013](../adr/0013-localization-theme-overhaul.md)（resx+实例服务+整项替换）、
[0015](../adr/0015-module-map-and-ownership.md)（12 模块地图：S3 与 D4 宿主消费）。
