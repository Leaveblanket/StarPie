# 程序集化目标态与分批执行：Host + Core + M1–M5（7 程序集）

> Status: Active（部分被 0020/0021/0022/0023 修订）

> 目标态演进：[ADR-0020](./0020-dialogs-assembly-and-m3-scanner-contract.md)（8 程序集）→ [ADR-0021](./0021-navigation-runtime-to-host.md)（导航运行时归 Host）→ [ADR-0022](./0022-shared-ui-infrastructure-decentralization.md)（共享 UI 去共享化）→ [ADR-0023](./0023-module-contracts-hard-boundary-and-core-narrowing.md)（Contracts 程序集与 Core 收窄）。

为后期多人维护，把结构形态从“概念模块 + 端口”升级为程序集化目标态：共享内核聚合成 `StarPie.Core`，五个业务纵向模块各成程序集，宿主 exe 保留为壳与装配；导航改为模块自治注册，DI 保持演进式单一组合根。实施沿 [assemblies.md](../architecture/assemblies.md) §8 路线分批执行，其中命名空间统一是独立收尾批次。

## Status

Accepted（grilling 共识 Q1–Q19，2026-09-06）。

- 部分推翻 [ADR-0015](./0015-module-map-and-ownership.md) **决策 3（结构形态：概念模块 + 端口）**；ADR-0015 其余内容（12 模块概念地图、R1–R8、D1–D6 除 R4/D3 修订外）继续有效。
- 部分修订 [ADR-0005](./0005-di-container-for-navigation.md) / [0011](./0011-composition-apphost-split.md)：**注册源可分散（模块注册器），解析点仍集中（Host 组合根）**；其余（容器只做解析、单例生命周期、测试不经容器等）维持。
- 本 ADR 即 ADR-0015 §5 D5 预留的“另行 ADR”（装配职责迁入 M2 的裁决，见决策 11）。

## Considered Options

1. **维持概念模块 + 端口（ADR-0015 决策 3）**：作为基线评估。后期多人维护需要编译器级模块边界与模块独立构建/测试（收益 1/4），文档级归属不够 → 本次会话重开延后分支。
2. **垂直模块目录（单程序集内）**：无编译器级边界，却推翻 ADR-0006/0007 目录正典 → 否。
3. **12 程序集全拆（每个 S/M/H 一集）**：共享件程序集化过碎，打包与引用噪音超过收益 → 否。
4. **7 程序集（Host + Core + M1–M5）**：业务纵向模块成集取得收益 1/4；S1–S6 共享件按“共享内核”聚合为 Core → **采纳**。
5. **共享内核留 exe、其余拆成库（字面方案）**：Host 需编译期引用模块做注册，模块需引用 exe 内的共享内核 → 项目引用循环，不可编译；唯一字面出路是插件运行时发现 → 否（M1–M5 是产品本体而非可选插件，运行时缺件风险且需改写组合根正典）。
6. **插件运行时发现（模块 dll 由 exe 扫描装配）**：否（同上；ADR-0005“插件系统再评估 Host 化”的触发器不因编译期模块化而命中）。
7. **导航架构**：壳集中映射（A）/ 模块自治注册（B）/ 模块级模板（E）/ ViewLocator / 运行时字典合并：
   - B 采纳（Q8）：模块自治注册 + Core 导航目录契约 + 模块页面模板字典，新增页面不碰 Host；
   - E 的“模块自带 DataTemplate + Host 每模块一次静态合并”作为 B 的模板交付机制一并采纳（Q16）；
   - ViewLocator 代码装配、运行时动态字典合并 → 否（推翻 ADR-0008/0009 的 DataTemplate 呈现正典 / 模板来源不可静态追踪）；
   - 排序用全局槽位表（Q17），相对锚点 → 否（5 项场景过度设计）。
8. **DI/组合根**：演进式单一组合根（A）/ 模块子容器 / 去容器纯手动 / Generic Host / Autofac / Prism：
   - A 采纳（Q19）：框架维持 MS.DI；注册源下放模块注册器，根对象解析仍集中 Host Composition；不引入子容器；`AppHostDelegates` 上提 Core 契约。
   - 其余 → 否（子容器割裂全应用单例；纯手动在页面导航十余互连点后噪声超过收益且开放泛型无法手写；Generic Host/Autofac/Prism 触发条件未命中）。
9. **D3（MainViewModel 类型级双职责）**：维持类型级例外（ADR-0015 D3/#70 登记） vs 拆分为导航 VM + 壳 VM → **采纳拆分**（Q18）：`MainViewModel`（纯导航，随 S5 导航内核进 Core）+ `ShellViewModel`（壳成员，留 Host，与 R4 同判据：壳窗口归 Host）。
10. **可见性**：无 InternalsVisibleTo + 模块公开面收敛为“注册器 + 被测 public 类型” → 采纳（Q14）；引入 IVT / 每模块独立测试工程 → 否。

## Decision

1. **结构形态**：目标态 = Host（exe，程序集 `StarPie`）+ `StarPie.Core`（S1–S6 共享内核合并 + Models + 共享 UI 基建 + 导航内核；WPF 类库）+ M1 `StarPie.Gestures` / M2 `StarPie.Wheel` / M3 `StarPie.Programs` / M4 `StarPie.Theme` / M5 `StarPie.Shell` 五个业务程序集。概念模块地图与共享内核放行清单（modules.md §2.3）不变。
2. **依赖方向**：`M* → Core` 单向；`Host → 全部`；允许的 M 间单向边仅 M1→M2（`IWheelFactory`）与 M2→M4（`IThemeService` 消费）；其余跨 M 一律经 Core 契约。S6 对话框 Window 与 DialogService 实现留 Host，契约进 Core。
3. **导航架构（模块自治注册）**：Core 提供 `NavigationCatalog` / `RegisterPage<T>(槽位/automationId/titleKey/iconData/模板字典)` 契约与导航执行缝（同 `NavigationService<T>` 的已批准惰性解析）；模块注册器自报导航项与页面模板；**新增页面不碰 Host**；新增模块才在 Host 登记（程序集引用 + 注册器调用 + 模板字典合并各一次）。
4. **页面模板交付（模块模板字典 + 静态合并）**：页面模块自带 `DataTemplate DataType=VM → View` 模块字典；Host 在 App 资源里每模块一次 pack URI 静态合并；页面 View 保持无参构造、DataType 模板语义与 e2e `AutomationId`（`NavTab0..4`）不变。
5. **排序与唯一性（全局槽位表）**：Core 定义槽位表（0 触发 /1 外观 /2 手势 /3 高级 /4 关于，键值 = `NavTab0..4`）；缺失/重复/未知槽位由 Core 收口测试拦截；槽位表是侧边栏顺序唯一正典。
6. **R4 重新归属（纯文档起点，代码随批次）**：MainView 全文件 = Host 壳窗口（含 code-behind）；页面 DataTemplate 分批迁出后 MainView.xaml 不含任何页面映射——纯壳；M5 只拥有壳层服务（托盘/自启/内存）与 Advanced/About 设置面，不再拥有 MainView 及其 code-behind。
7. **D3 拆分**：拆 `MainViewModel`（纯导航 → 随 S5 导航内核进 Core）与 `ShellViewModel`（`WindowTitle`/`IsExiting`/`Save()` → 留 Host）；MainView 分区 DataContext（导航区绑导航 VM、壳区绑壳 VM）；AppHost 退出链（IsExiting 置位）、Composition 接线与相关测试同步改。
8. **DI 架构（演进式单一组合根）**：框架维持 MS.DI（ADR-0005 不推翻）；注册源下放模块注册器（`RegisterServices(IServiceCollection)` + `RegisterNavigation(catalog)`，Q15），Host Composition 仍唯一 `BuildServiceProvider`/`CreateAppHost`、仍是唯一根对象解析点；不引入子容器/模块 provider；已批准解析缝清单升级为：`NavigationService<T>`、导航目录执行缝、`WheelFactory`、`DialogService`、模块注册器（仅注册不解析）；`AppHostDelegates` 上提为 Core 公开契约（Host 回填实现）；CreateAppHost 硬编码解析清单改为目录/启动激活钩子驱动，eager 页面实例化语义在批次中验证保留。
9. **可见性（Q14）**：不引入 `InternalsVisibleTo`；维持 layering.md“被测类型 public、直接 new + mock 不用容器”约定；模块公开面 = 注册器入口 + 被测 public 类型，内部实现细节保持 internal；Host 只引用模块注册器，不引用模块内部；跨集必需的内部件（如 `ThemePaletteManager`）在各自批次单独裁决为 public 或改经接口注入。
10. **分批执行**（每批独立 issue + 构建 + xUnit 绿 + 涉及可见文案时 e2e 绿 + 叶子回填后从路线移除；批次内不做无关重构）：路线与批次见 [assemblies.md](../architecture/assemblies.md) §8。
11. **D5 解结**：`WheelFactory` 随 M2 收编（进 `StarPie.Wheel`），`IWheelFactory` 留 M2 侧接口 → M1→M2 单向；`IProfilePreviewSource` 接口上提 Core（实现方 M1 `ProfileListViewModel`、消费方 M2 `WheelAppearanceSettingsViewModel` 均只依赖 Core）。modules.md §5 D5 的“当前不推动”随之失效。
12. **命名空间策略（Q13）**：拆分期间保持 `WinPieGestures.*` 命名空间不动；**收尾以独立批次执行命名空间统一**——目标命名空间映射于该批次开工时裁定（候选 `StarPie.*` 系与程序集对齐）；验收 = 纯机械改名（.cs namespace、XAML xmlns、resx 生成类、GlobalUsings、文档、测试命名空间同步），无行为变化，构建 + xUnit + e2e 全绿 + 叶子回填。
13. **明确否决并记录**（防止未来被当“修复”重提）：插件运行时发现；共享内核留 exe 字面方案；12 程序集全拆；垂直目录单程序集；模块子容器；Generic Host / Autofac / Prism；去容器纯手动；ViewLocator 代码装配；运行时动态字典合并；相对锚点排序。

## Consequences

- 新增 [assemblies.md](../architecture/assemblies.md) 作为程序集地图与路线文档（目标态 + 方向性），architecture.md 路由表与 ADR 索引登记。
- 回填纪律：架构叶子维持 as-built，不先行描述未实现结构；代码批次落地后按批次回填对应叶子（modules.md R4/D3、layout.md、layering.md、navigation.md、shell.md、host.md、programs.md、wheel.md、gestures.md、interface-theme.md、dialogs.md），收尾批处理命名空间相关全文。
- 测试：WinPieGestures.Tests 随批次显式 ProjectReference 各新程序集（不依赖传递引用）；单测平铺/命名约定不变；`NavigationTests` 的微型容器用例保留。
- e2e：exe 程序集名仍 `StarPie`、`AutomationId` 不变 → pywinauto 零改动（每批验证）。
- `config.json` 格式与 Hard Constraint 不受影响（Models 物理进 Core，语义归属 R8 不变）。
- CONTEXT 不修订（模块/程序集为架构术语，ADR-0015 先例）。
- 本 ADR 为决策批，不做代码改动。

## Appendix：参考事实（2026-09-06 快照）

- `Composition.cs` 约 220 行，`CreateAppHost` 硬编码解析约 15 个根对象（5 个类型化 `INavigationService<T>`、全部页面 VM、`GestureController`、`MainViewModel` 等）；`NavigationService<T>` 是既有已批准惰性解析缝。
- `AppHostDelegates` 是 `Composition.cs` 内的 internal 类；M5 拆集后其注册器不能反向引用 Host → 必须上提为 Core 契约。
- `MainView.xaml` 集中 5 条页面 DataTemplate；`MainViewModel` 硬编码 5 导航项且混装壳成员（D3）；页面现状归属（D6）：Trigger/Gestures → M1、Appearance 聚合 → Host、Advanced/About → M5。
- 共享件 WPF 耦合快照：`IconAssets`/`ProgramCatalog`/`DialogService` 实现依赖 WPF；Models、Configuration（除 `DispatcherSaveDebouncer` 用 Dispatcher）、Localization、Messages、导航内核（NavigationStore/INavigationService/NavigationItemViewModel）WPF-free。
- 全部页面固定 5 个；e2e 依赖 `NavTab0..4` AutomationId。
