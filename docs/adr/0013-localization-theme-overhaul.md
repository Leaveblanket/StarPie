# 本地化与主题切换推翻性重构：resx+强类型+实例服务 / 主题整项替换+实时跟随

2026-09-04 对 `main` 完成 grilling-with-docs 共识（#41，Q1–Q13）后裁决：将本地化
（ADR-0010，#29–#33/#40 建立）与主题切换（ADR-0012，#35–#38 建立）**推翻式重构**
为下述主流形态。本 ADR 记录新目标态，并修订/取代 ADR-0010 决策 1 与 ADR-0012
决策 2 的相应条款。

## Status

Accepted（grilling 共识 Q9-B/Q10-A/Q11 四项全做/Q12 轮盘配色范围外/#41；S1 起分批实施）。

## Considered Options

### 本地化数据层
- **C# 键表（现状）**：`I18n.cs` 1781 行单文件内联 242×4 键；无编译期键校验；与 .NET
  资源体系割裂，工具链/翻译协作不可用。
- **resx + 卫星程序集 + 强类型资源**：`.NET` 正统；`Strings.MyKey` 编译期键校验；资源
  管理工具/翻译生态兼容。代价：WPF 运行时切语仍需声明式桥；键值大迁移。
- **每语言 XAML 资产字典**：与主题换入机制同构、DynamicResource 原生；但失去编译期
  键校验，且引入与 resx 并行的第二套“语言资产”体系。

→ 选 **resx + 强类型**（Q9-B）。

### 本地化语义层
- **静态 `I18n` 唯一源（现状，ADR-0010）**：全局静态 + 强事件，DI/测试接缝弱。
- **纯实例 `ILocalizationService`**：DI 单例、可测、状态与广播收口；代价是全部调用点
  迁移（页面/对话框 VM、壳层、托盘、渲染器、静态工具）。
- **实例 + 静态薄门面**：迁移成本低，但终态仍残留静态入口。

→ 选 **纯实例，终态删除静态 `I18n`**（Q10-A）；S3 过渡期保留薄门面转发仅为分批合并
策略，S4 删除。

### 声明式文案桥
- **标记扩展 / Loc 库**：第三方依赖与 WPF 版本耦合。
- **VM 本地化代理**：为每个绑定挂订阅器，样板大。
- **运行时语言字典 + `{DynamicResource}`（T24 已建立）**：零 code-behind、全树自动刷新、
  无第三方依赖。

→ 保留 **运行时语言字典投影桥**，数据源从 C# 键表换成 resx（经服务枚举投影）。

### 主题换入机制
- **App 直接资源键覆盖（现状，ADR-0012 决策 2）**：切回 Light/多次切换后直接键残留，
  同一资源树并存 Light 合并字典与目标主题直接键两套值；与语言字典共享查找空间时
  次序语义隐晦。
- **MergedDictionaries 整项替换活动主题槽**：同一时刻只有一套完整主题；切 Light 即
  替换回 Light 字典，无残留；键集一致性可测。

→ 选 **整项替换**（Q11）。

### 系统深浅色跟随
- **应用/切换时读注册表（现状）**：Windows 深浅色运行中切换不跟随。
- **`UISettings.ColorValuesChanged` 实时监听**：系统变化事件驱动自动换肤（引用方式待
  S7 spike，备选注册表监听）。

→ 选 **实时监听**（Q11 第 4 项）。

## Decision

1. **本地化数据与取词**：四语言文案迁入 resx + 卫星程序集，配强类型资源类（生成机制
   以 #43 spike 结论回填附录）；`ILocalizationService` 实例注册为 DI 单例，语义与现
   `I18n` 等价：四语言码、`Auto` 按 `CurrentUICulture` 解析、缺语言回退 zh-CN、再缺回退
   键名、`LanguageChanged` 事件、`GetString(key)`/`SetLanguage(code)`。配置键
   `Language` 与 config.json 格式不变（Hard Constraint）。
2. **声明式介质**：运行时语言字典 + `{DynamicResource}` 投影桥保留，数据改由服务从
   resx 枚举投影；**仍不引入每语言 XAML 资产文件**（继承 ADR-0010 该条）；ADR-0010 的
   四类文案（声明式/驻留/即时取词/壳外）分类与生命周期契约维持，仅取词/订阅入口换为
   服务。
3. **静态 `I18n` 删除**：S4 完成全调用点迁移后删除静态类与 C# `Translations` 键表；
   订阅者成对退订纪律沿用（容器 Dispose / 瞬态 IDisposable）。
4. **主题换入**：新增 `ThemePaletteManager`（自包含：加载 `Views/Styles/Themes/*.xaml`、
   缓存、冻结、**整项替换 MergedDictionaries 活动主题槽**）；`App.xaml` 静态合并
   Light 仅作设计时/首帧默认；`AppHost` 只编排调用，不再实现直接键覆盖；切 Light =
   替换回 Light 字典，直接键零残留。
5. **主题门面**：`IThemeService` 增 `SetTheme(name)`（唯一状态入口）与 `ThemeChanged`
   事件；`CurrentEffectiveTheme` 仅由 `SetTheme` 更新；窗口 DWM 标题栏应用保持白名单
   （MainView/对话框构造注入）；页面仍不持 `IThemeService`（ADR-0009 不变）。
6. **系统跟随**：`System` 模式监听系统深浅色实时变化并自动 `SetTheme(解析值)`；系统
   探测保持注入委托，可单测。
7. **键隔离与一致性**：语言键与主题令牌键命名空间约定 + 零交集测试；主题五套令牌键集
   一致测试（缺键即失败）。
8. **文案治理边界**：用户可见硬编码中文清零（品牌/版本名 `StarPie v1.4.1` + Dev 后缀
   锁死不翻译，沿用 ADR-0010 条款）；Models 默认值（数据）与展示文案边界在 #49 处理；
   **轮盘配色（`SelectedTheme`/自定义预设）与主题风格（`UiStyle`）不属本 ADR**，轮盘
   配色重构另立 #51 暂存。

## Consequences

- ADR-0010 决策 1 修订：删除“不引入 `ILocalizationService`”与“`I18n` 静态 +
  `LanguageChanged` 唯一变更源”；保留“不引入每语言 XAML 资产文件”。其余条款
  （四类文案机制、VM 生命周期契约、e2e AutomationId）不变。
- ADR-0012 决策 2（App 直接资源覆盖）被本 ADR 第 4–5 条取代；其余（令牌 XAML 化、
  App 单点合并、ThemeService 瘦身）保留。
- 静态 `I18n` 调用点全量迁移（#45），分批合入、每批 main 绿（S3 薄门面过渡 →
  S4 删除）。
- 语言内容与键名不变，e2e 只按 AutomationId 定位，预期机制迁移期保持绿。
- 架构叶子 `localization.md`/`shell.md` 维持 as-built，随 #44–#48 各批回填，#50 终核。

## Appendix（spike 结论回填位）

- #43（2026-09-04）：resx 强类型机制选型与迁移方案
  - 实验证实：内置 `<Generator>ResXFileCodeGenerator</Generator>` 在纯 `dotnet build`
    （无 VS）不执行自定义工具、不产出 Designer.cs，不可用于 agent/CI 工作流。
  - 选型：NuGet `VocaDb.ResXFileCodeGenerator` v3.2.1（Roslyn source generator，
    `PrivateAssets="all"`；`dotnet build` 直接生成 `internal static class Strings`：
    `string?` 属性 + `ResourceManager` + 可设置 `CultureInfo`）。拼错键产生
    **CS0117** 编译错误（已验证）。
  - 语言资产：`Strings.resx`（中性）+ `Strings.zh-CN/zh-TW/en/ja.resx`（卫星）；
    服务经 `ResourceManager.GetString(key, culture)` 动态取词与
    `GetResourceSet` 枚举键集（XAML 投影桥）；回退链（目标 → zh-CN → 键名）由服务
    显式实现。强类型属性用于静态引用点与键完整性。
  - 文件落位与生成类命名由 #44 定稿；本环境 NuGet restore 需经 127.0.0.1:7897
    代理（GitHub Actions 不需）。
- #48（2026-09-04）：系统深浅色实时跟随
  - spike：TFM 提升为 `net8.0-windows10.0.19041.0` 后 `UISettings.ColorValuesChanged`
    纯 CLI 编译通过（SDK 自带 WinRT 投影，无额外包）；主/测试两个 csproj 同步提升。
  - 实现：`ThemeService.EnableSystemThemeTracking()`（UISettings 实例保活 + 后台线程 →
    UI Dispatcher 封送 → `RefreshSystemTheme()`）；`RequestedTheme` 记录原始请求名，
    仅 System/空模式跟随，固定主题 no-op；AppHost.Run 初始主题应用后启动跟踪。
  - 单测以可变探针模拟系统切换（xUnit 398 绿）；真实系统切换由人工/e2e 冒烟验证。
