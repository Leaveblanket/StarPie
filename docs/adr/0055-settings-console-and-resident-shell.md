# 设置台与常驻壳层归位:环状包含转并列,Shell 家族符号随术语改名

**状态**:accepted

词表原有三处定义首尾相接成环:「设置控制台」是「承载页面导航与**壳层**的设置主窗口」、「壳层」含子域「**壳窗口**」、「壳窗口」是「**设置控制台主窗口**的窗口生命周期」——顺着词条读一圈会回到起点,读者判不出设置台的窗口 chrome 归哪一层。而代码事实是第三种结构:常驻编排面(`ResidentShell`)先于设置台存在并负责按需创建它,设置台只是它手里的瞬态租户;词表把常驻面写成瞬态面的"残影"(原「常驻壳层」定义为"设置控制台关窗后仍存活的应用形态"),方向正好反了。

决定分三件事。

**一、包含关系改成两条并列的生命周期轴。** 删「设置控制台」「壳层」「壳窗口」三条(含子域式包含表述),立「设置台」与「常驻壳层」两条并列词条:设置台是按需创建、关窗即销毁的界面会话(一次开启到关闭之间持有窗口与其全部视图模型);常驻壳层是进程存活期内一直存在的编排层(托盘、输入栈、插件运行时、单实例接收、退出编排,以及设置台的按需创建与销毁)。设置台不再"承载壳层",壳层也不再"是设置台关窗后的形态"——前者由后者创建与销毁。「系统集成」收窄为常驻壳层面向操作系统的服务面(托盘与气泡、开机自启、提权、单实例、退出编排、内存整理、高级设置面),补上原定义漏掉的提权、单实例与退出编排三项。「窗口外框」替代原来的「壳窗口」,指设置台窗口上不属于页面导航的外围部件(标题与底部操作区),与代码里"壳区"这个 UI 区域名对齐。

**二、「设置控制台」退出全部命名面。** 该词只在词表与窗口标题里存在(代码 .cs 里 3 处、词表 10 处,而"设置台"在 .cs 里约 150 处),用户可见面只有窗口标题一处。决定统一用「设置台」:窗口标题四语改写,托盘项「偏好设置」一并改名为「设置台」——它先前与窗口标题各用一词指同一目的地。词表「设置台」条目的 `_Avoid_` 列出旧称与「主窗口」:代码里 `Application.MainWindow` 长期由永不显示的锚窗口占住(`AnchorWindow`,专为此存在),同一个词在词表与代码里指两个不同窗口,而「主窗口」正是此前被用来指设置台的那一个。

**三、Shell 家族符号照术语改名。** 与 ADR-0054 同一纪律:名字与词条一对一,grep 词条就能落到符号。这次覆盖面更大,因为 `Shell` 一名六用:常驻壳层、锚窗口、窗口外框 VM、系统集成贡献者、退出编排、外加 Windows 外壳(COM 的 `ShellLink`/`IShellLinkW`)。改名只动前五者,系统 API 名一个不改。

## 考虑过的方案

- **只改词表、不动符号**(即把 `ShellHost` 等原样保留):否决。词表与代码的漂移正是本次要消的病;`ShellHost` 对应词条「常驻壳层」,不改名则 grep「常驻壳层」落不到任何符号,而 grep `Shell` 会同时落进托盘、锚窗口、退出编排与 Windows 外壳。
- **保留「设置控制台」作为词表正名、把"设置台"收进 `_Avoid_`**:否决。约定是词表正名即代码与文案的目标名,而"设置台"才是代码里唯一在用的词(约 150 : 3);让绝大多数代码与测试方法名改用少数派词,是把成本花在错的一侧。
- **把 `Shell` 保留为"应用外壳"的整称,只补词条不拆名**:否决。六个含义里有两组本可自解释的独立概念(常驻编排、系统集成服务面),合成一个词后每处出现都得靠上下文猜;而 `ShellLink` 是 Windows 外壳链接,与它们同名不同物,留着整称会让读者以为它在同一命名空间里。
- **`ShellContributor` 只改页面注册、不改贡献者名与 Id**:否决。它注册的是「高级与系统」页并接线自启与提权,是系统集成域的贡献者;名字留 `shell` 会让"导航页归属"与"贡献者身份"对不上,Id 又是本仓唯一的裸 `"shell"` 字面量。
- **`ShellLink`(COM coclass 包装)一并改名**:否决。它是 OS coclass `ShellLink`(CLSID `00021401-…`)的包装,同文件的 `IShellLinkW` 是系统接口名不可改;给系统对象另起别名会制造"类型说 A、接口说 B"的漂移。
- **保留「关闭并隐藏」文案,只改代码**:否决。四语按钮文案承诺"隐藏",实现是销毁(`TransientWindowTeardown.Complete` → `Close()`,重开重建新窗,索引下已由 e2e 断言),词表也写"关窗即销毁"——界面在骗人。文案必须与实现同侧。

## 后果

**符号映射**(`Shell*` 家族;系统 API 名不在其列):

| 现名 | 新名 |
|---|---|
| `StarPie.Host/ShellIntegration/`(`StarPie.Host.ShellIntegration`) | `StarPie.Host/SystemIntegration/`(`StarPie.Host.SystemIntegration`) |
| `ShellExitSequence` / `ShellExitStep` | `ExitSequence` / `ExitStep` |
| `StarPie.Ui/Services/Shell/`(`StarPie.Ui.Services.Shell`) | 拆三处:`Services/Themes/`(界面主题)、`Services/SystemIntegration/`(托盘)、`Services/WindowLifecycle/`(瞬态窗口收尾) |
| `StarPie.Sdk.Wpf/Services/Shell/`(`StarPie.Sdk.Wpf.Services.Shell`) | `StarPie.Sdk.Wpf/Services/Themes/`(`StarPie.Sdk.Wpf.Services.Themes`) |
| `ShellHost` / `Composition.CreateShellHost` | `ResidentShell` / `CreateResidentShell` |
| `ShellAnchorWindow` | `AnchorWindow` |
| `ShellViewModel`(壳区 DataContext) | `WindowChromeViewModel` |
| `ShellContributor` / `ShellPageTemplates.xaml`(Id `"shell"`) | `SystemIntegrationContributor` / `SystemIntegrationPageTemplates.xaml`(Id `"system.integration"`) |
| `MainView`(设置台窗口) | `SettingsConsoleWindow` |
| `MainViewModel`(导航区 DataContext) | `NavigationViewModel` |
| `ShellLink` / `IShellLinkW` / `Shell_NotifyIcon` / `SHGetFileInfo` / `GetShellWindow` / `Shell_TrayWnd` 等 | **不改**(OS 类型与 API 名) |

- **`StarPie.Ui/Services/Shell/` 一分为三**的理由:该目录四个文件分属三个域——`ThemeService`(界面主题)、`TrayIconManager` + `TrayMenuComposer`(托盘,属系统集成)、`TransientWindowTeardown`(瞬态窗口收尾)。整目录只换个名字会把"命名空间说不清内容"的问题原地保留;拆开后每个命名空间自解释,且 `Services/Themes/` 与既有的 `StarPie.Ui/Themes/`(主题字典)、`StarPie.Sdk/Services/Themes/` 同族。
- **SDK 面**:`IThemeService` 的命名空间变更(`StarPie.Sdk.Wpf.Services.Shell` → `.Services.Themes`)是 public 面的源级破坏性变更;核查确认 `plugins/` 全树零引用 `IThemeService`(插件侧深浅色走无状态探针,`docs/architecture/assemblies.md` 已记为"不构成插件可达面"),故不升 `UiSdkAbi`。
- **文案面**:`WindowTitle` 四语改「设置台」(ja 保留「環境設定コンソール」——「コンソール」是词条英文名 Settings Console 的对译);`BtnClose` 去「隐藏」(四语改为「关闭设置台」/「關閉設定台」/`Close`/「設定を閉じる」);`BottomStatusNote` 改自动保存口径(「所有修改即时生效并自动保存;【保存更改】用于立即落盘」),`BtnSave` 取值不变(e2e 断言按钮文案含「保存」/`Save`);`ConsoleThemeTitle` / `ConsoleThemeDesc` / `LanguageDesc` 与 `DesignTimeStrings.xaml` 的设计时镜像同步;`TrayPreferences` 改「设置台」。
- **文档面**:`docs/architecture/assemblies.md`、`layering.md`、`plugins.md` 中 `Shell*` 符号名、`Services/Shell/`、`SystemIntegration/`、"壳层/壳窗口/壳区/设置台"字样同步改写;ADR-0054 正文里作为反例提及的「设置控制台」不追改(那是当时的决策语境)。
- **词表面**:新增「设置台」「窗口外框」「锚窗口」「插件窗口」;改写「常驻壳层」「系统集成」「界面主题」;「对话框」收窄为"设置台之上的自绘模态窗口"并写明系统消息框与文件选择器不属此列、全屏取色工具是刻意不设归属的例外;插件域新增 33 条词条(插件、宿主内核、插件包、清单、发现、准入、审核清单、开发者模式、装载、停用、彻底移除、隔离、熔断、重试、重载、更新、挂起版本、待重启、生命周期状态、安全点、危险区、泄漏验证、残留、能力、扩展点、界面资产、资源根、宿主门面、宿主服务作用域、宿主状态、诊断报告、启动报告、内置)。
- **门禁**:以 xUnit 全绿 + e2e `-Full` 为门禁(`AGENTS.md` 收尾口径)。改名触及 `InteropBaselineTests` 的 PInvoke 白名单路径(`Services/Shell/TrayIconManager.cs` → `Services/SystemIntegration/…`),该表按路径比对,漏改不会编译失败而会静默失配——已列入本次必改清单。
