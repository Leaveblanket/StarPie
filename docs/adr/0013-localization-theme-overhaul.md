# 本地化与主题切换推翻性重构：resx+强类型+实例服务 / 主题整项替换+实时跟随

> Status: Active
>
> 本 ADR 推翻并取代：本地化静态方案（“静态 `I18n` 唯一源、不引入 `ILocalizationService`”，历史决策已删除）与 [ADR-0012](./0012-resource-dictionary-architecture.md) 原决策 2（App 直接资源键覆盖）。

## 动机

- 本地化：静态 `I18n` 是单文件内联 242×4 键的 C# 键表，无编译期键校验，与 .NET 资源体系割裂，工具链/翻译协作不可用。
- 主题：App 直接资源键覆盖在切回 Light/多次切换后残留直接键，同一资源树并存 Light 合并字典与目标主题两套值；与语言字典共享查找空间时次序语义隐晦。

## Considered Options

### 本地化数据层
- **C# 键表（现状）**：无编译期键校验；与 .NET 资源体系割裂。
- **resx + 卫星程序集 + 强类型资源**：`Strings.MyKey` 编译期键校验；资源工具/翻译生态兼容。代价：WPF 运行时切语仍需声明式桥；键值大迁移。
- **每语言 XAML 资产字典**：与主题换入机制同构、DynamicResource 原生；但失去编译期键校验，且引入与 resx 并行的第二套“语言资产”体系。

→ 选 **resx + 强类型**。

### 本地化语义层
- **静态唯一源（现状）**：全局静态 + 强事件，DI/测试接缝弱。
- **纯实例服务**：DI 单例、可测、状态与广播收口；代价是全部调用点迁移。
- **实例 + 静态薄门面**：迁移成本低，但终态仍残留静态入口。

→ 选 **纯实例，终态删除静态 `I18n`**（薄门面仅作分批迁移过渡）。

### 声明式文案桥
- **标记扩展 / Loc 库**：第三方依赖与 WPF 版本耦合。
- **VM 本地化代理**：为每个绑定挂订阅器，样板大。
- **运行时语言字典 + `{DynamicResource}`**：零 code-behind、全树自动刷新、无第三方依赖。

→ 保留 **运行时语言字典投影桥**，数据源换成 resx（经服务枚举投影）。

### 主题换入机制
- **App 直接资源键覆盖（现状）**：见动机。
- **MergedDictionaries 整项替换活动主题槽**：同一时刻只有一套完整主题；切 Light 即替换回 Light 字典，无残留；键集一致性可测。

→ 选 **整项替换**。

### 系统深浅色跟随
- **应用/切换时读注册表（现状）**：运行中系统切换不跟随。
- **`UISettings.ColorValuesChanged` 实时监听**：系统变化事件驱动自动换肤。

→ 选 **实时监听**。

## Decision

1. **本地化数据与取词**：四语言文案迁入 resx + 卫星程序集，配强类型资源类。生成机制选型：内置 `ResXFileCodeGenerator` 在纯 `dotnet build`（无 VS）不执行自定义工具、不产出 Designer.cs，不可用于 agent/CI 工作流；选 NuGet `VocaDb.ResXFileCodeGenerator`（Roslyn source generator，`dotnet build` 直接生成 `internal static class Strings`，拼错键产生 CS0117）。`ILocalizationService` 实例注册为 DI 单例：四语言码、`Auto` 按 `CurrentUICulture` 解析、缺语言回退 zh-CN、再缺回退键名、`LanguageChanged` 事件、`GetString(key)`/`SetLanguage(code)`。配置键 `Language` 与 `config.json` 格式不变（Hard Constraint）。
2. **声明式介质**：运行时语言字典 + `{DynamicResource}` 投影桥保留，数据由服务从 resx 枚举投影；**不引入每语言 XAML 资产文件**。四类文案（声明式/驻留/即时取词/壳外）分类与生命周期契约维持（分类正典：`docs/architecture/localization.md`），仅取词/订阅入口换为服务。
3. **静态 `I18n` 删除**：全调用点迁移完成后删除静态类与 C# 键表；订阅者成对退订纪律沿用（容器 Dispose / 瞬态 IDisposable）。
4. **主题换入**：`AppThemePaletteManager` 自包含加载 `Views/Styles/Themes/*.xaml`、缓存、冻结、**整项替换 MergedDictionaries 活动主题槽**；`App.xaml` 静态合并 Light 仅作设计时/首帧默认；切 Light = 替换回 Light 字典，直接键零残留。
5. **主题门面**：`IThemeService.SetTheme(name)` 为唯一状态入口，配 `ThemeChanged` 事件；`CurrentEffectiveTheme` 仅由 `SetTheme` 更新；窗口 DWM 标题栏应用保持白名单（主窗口/对话框构造注入）；页面仍不持 `IThemeService`（ADR-0009 不变）。
6. **系统跟随**：`System` 模式监听系统深浅色实时变化并自动 `SetTheme(解析值)`；系统探测保持注入委托，可单测。
7. **键隔离与一致性**：语言键与主题令牌键命名空间隔离 + 零交集测试；主题各套令牌键集一致测试（缺键即失败）。
8. **文案治理边界**：用户可见硬编码中文清零（品牌/版本名锁死不翻译）；Models 默认值（数据）与展示文案分离；**轮盘配色与主题风格不属本 ADR**（边界与后续拆分见 ADR-0014、0024）。

## Consequences

- ADR-0012 原决策 2（App 直接资源键覆盖）被本 ADR 第 4–5 条取代；其令牌 XAML 化、App 单点合并决策保留。
- 语言内容与键名不变；e2e 只按 AutomationId 定位。
- 本地化与主题的现行机制分别见 `docs/architecture/localization.md` 与 `interface-theme.md`。
