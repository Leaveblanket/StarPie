# 测试规范

> 本文是 [docs/architecture.md](../architecture.md) 的拆分文档；正典范围 = 测试套件的分层边界、检查分类、写作口径与增删口径。
>
> **不属于本文**：何时跑哪道门（提交级 / 合入门 / 免除判定）以 [git-commits.md](../agents/git-commits.md) 为唯一正典，本文只留指针不复制；逐条检查的判定记录属任务型盘点，落 issue、关闭即删，不驻留本叶。

## 1. 分层与守护边界

| 层 | 载体 | 守护面 |
|---|---|---|
| xUnit | `StarPie.Tests` | ViewModel 与纯逻辑层的决策行为；插件运行时与 UI 托管的生命周期；程序集、资源与文档的机械不变量 |
| e2e | `tests/`（pywinauto + pytest） | 钩子、渲染、托盘、对话框的可见行为；导航点击往返；主题选项的目录内容与配置落盘 |
| 人工验收 | 无载体 | 视觉效果；主题的可见效果（换肤后的窗口 chrome）；跨完整性级别行为（提权实例接管、互斥体失败分支）；系统 `MessageBox` 的呈现 |

主题的可见效果归人工验收：键集与槽位不变式由 `ThemePaletteConsistencyTests` 在静态侧守住，运行时侧只剩像素级判据，自动化会随主题微调持续产生噪音。

边界裁定的出处：钩子/渲染/托盘归 e2e（ADR-0001）；系统 `MessageBox` 与真实鼠标命中路径不在 e2e 覆盖内（ADR-0031）；跨完整性级别行为无自动覆盖（ADR-0040 / ADR-0042 / ADR-0043）；逐原型的测试义务见 [extending.md](extending.md)；`ProgramScanner` 一类集成件不单测见 [programs.md](programs.md)。

## 2. 检查分类

### 2.1 主轴：被守护对象

| 类 | 守护对象 | 代表 |
|---|---|---|
| A 行为单元测试 | ViewModel 状态机、纯逻辑引擎与路由纯函数 | `GestureEngineTests`、`ActionRoutingTests`、各 `*ViewModelTests` |
| B 插件运行时与 UI 托管 | 装载/卸载管线、能力与准入、UI 资产登记与释放 | `PluginLoadPipelineTests`、`PluginUiUnloadMatrixTests` |
| C 架构收口 | 四集依赖方向、引用面、导出面白名单、工程与 CI 路径 | `FourSetBoundaryTests`、`RuntimeNoCrossReferenceTests`、`SdkBoundaryTests`、`SdkWpfBoundaryTests`、`HostBoundaryTests` |
| D 资源与键集一致性 | 五套主题键集、四语言 resx 键集、设计期字典、XAML 与 `GetString` 的引用侧存在性 | `ThemePaletteConsistencyTests`、`LocalizationKeyCoverageTests`、`XamlResourceKeyTests`、`DesignTimeStringsConsistencyTests` |
| E 文档不变量 | 路由表与叶子一一对应、ADR 状态与引用可解析、叶子无流水与日期快照、常量归属、注释不载变更史 | `DocInvariantTests` |
| F 供应链与准入 | 清单验签与公钥 pin、清单解析、准入策略 | `ReviewCatalogPinTests`、`PluginSignatureVerifierTests`、`PluginManifestValidatorTests` |
| G 声明面与残留面 | 运行时配置声明（GC 硬顶）、工作集裁剪能力的源码树残留 | `MemoryResidencyTests` |

### 2.2 副轴：确定性等级

分类审查与红了的归因都读这一列：

| 等级 | 判据 | 分布特征 |
|---|---|---|
| 确定 | 结果只取决于构造输入 | 绝大多数行为单测 |
| 概率 | 依赖 GC、弱引用回收或时序 | 卸载矩阵、常驻壳生命周期一类带回收断言的类 |
| 环境依赖 | 依赖进程内唯一 `Application`、仓库文件或临时目录 | StaTestHarness 的使用类；读仓库文件的机械断言 |

## 3. 写作规范

### 3.1 落位与命名

单测文件平铺于 `StarPie.Tests` 根，命名 `{被测类型}Tests.cs`，命名空间镜像被测类型。测试工程**显式** `ProjectReference` 四集，不依赖传递引用（见 [assemblies.md](assemblies.md)）。页面/服务/对话框 VM 单测直接构造并注入依赖，不从容器解析。被测类型保持 `public`，不使用 `InternalsVisibleTo`（见 [layering.md](layering.md)）。

**替身形态**：手写替身与 mocking 框架都可以用，按可读性选择。两条约束：

- **边界**：断言对象是**对象图与生命周期**的检查——程序集卸载与回收、弱引用判定、全局根扫描、真实 `Application` 上的窗口与资源集合——用手写替身。mocking 框架的运行时动态代理会往进程里塞进被测系统从不产生的类型，让这类判据失真，而这批检查恰是确定性最脆弱的一批。
- **护栏**：mock 用来减 arrange 样板，不用来替代行为断言。`Verify()` 式交互断言记录的是「做过什么动作」而不是「产出了什么结果」，与断言不可能失败属同一类病——检查看着在守东西，实际守不住。
- **可见性**：代理只能作用于 `public` 类型。代理 `internal` 类型需要给 `DynamicProxyGenAssembly2` 开 `InternalsVisibleTo`，与本节约定的「不使用 `InternalsVisibleTo`」直接冲突——所以能 mock 的面恰是本仓库的 public 面，与「被测类型保持 `public`」是同一条纪律的两次表述。

### 3.2 自述

| 检查类型 | 要求 |
|---|---|
| 行为单元测试 | 类级 `<summary>` 一句话说明守护什么 |
| 机械断言 / 文件级断言 / 收口测试 | 类级 remarks 必须写出**存在理由**与其他层为何看不见这个缺口——`XamlResourceKeyTests` 的「既有三条守护全在定义侧、看不见引用侧缺口」是标准形态 |

### 3.3 粒度

维持「一个被测类型一个文件」，不按验证面拆文件。软上限 600 行 / 40 条检查；超过则内部 `// --- 分节` 注释必达，并触发一次「这个类是否混了两个被测子面」的审查。

### 3.4 断言强度

能用精确断言就不用布尔断言：`Empty` / `Single` / `Equal` / `NotEqual` / `Contains` 优先于 `True` / `False`。理由是失败诊断——布尔断言只报 `false`，精确断言会打出实际值。只对新写与改动的检查生效，存量布尔断言不为此单独整改。

## 4. 执行

### 4.1 串行边界

套件全量串行执行。约束来自 `StaTestHarness` 持进程内唯一的 WPF `Application` 与 STA 线程，以及多个用例对 `Application` 窗口集合、资源字典的读写——不是在它上面的每个用例都能撑住别的用例插进来。带弱引用回收断言的类同属这一档，并发会扰动回收时机。

释放并行的形态（少数接触 `Application` 的类进显式串行 collection、其余并行）见「目标态与差距」。

### 4.2 配置口径

CI 的构建与测试走 Release，本地约定命令走默认配置，两者都要过。CI 只强制 xUnit，e2e 不在 CI 覆盖内（GitHub runner 上 WPF UI 自动化不可靠）。

## 5. 检查的增删

- **新增**：随被测行为一起加。机械断言类还需在类自述里写明它补的是哪一处缺口（§3.2）。
- **删除**：满足二者之一才成立——守护对象已不存在（某能力连同它的源码一起不存在了），或同一失效场景已被另一处检查覆盖（跨文件、跨层均可）。删除属独立改动，单独开票执行，不夹在审查或重构里。

## 目标态与差距

**分析器硬门**：在根 `Directory.Build.props` 打开 `TreatWarningsAsErrors`，对 `plugins/samples/**` 单独覆盖关闭——示例工程供第三方插件作者照抄，不给他们加构建摩擦。开启前需清掉现存两条警告（`WinTrustSignatureVerifier.cs` 的过时证书 API、`PluginLoadPipeline.cs` 的可空实参）。规划：落地时回填本文 §4.2。

**类粒度软上限**：超限文件的处理随各自改动走，不为达标而单独重构。规划：现状有文件超 600 行。

**并行释放**：触发条件 = 全量 xUnit 耗时超过 30 秒。规划：条件未到，当前全量在 3–5 秒量级。

## 参见 ADR

[0001](../adr/0001-mvvm-with-communitytoolkit.md)（MVVM 重构与单测范围）、[0031](../adr/0031-e2e-silent-background-run.md)（e2e 静默后台运行与对话框策略）、[0040](../adr/0040-startup-privilege-policy.md) / [0042](../adr/0042-privilege-routes-two-only.md) / [0043](../adr/0043-elevated-instance-takeover.md)（提权路径的无自动覆盖边界）。
