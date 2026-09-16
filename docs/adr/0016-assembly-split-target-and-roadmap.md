# 程序集化目标态：宿主 + 共享内核 + 业务模块程序集

> Status: Active（目标态被 [ADR-0023](./0023-module-contracts-hard-boundary-and-core-narrowing.md)、[ADR-0027](./0027-plugin-architecture-and-host-sdk-ui-split.md) 演进）
>
> 本 ADR 记录程序集化的起因、基线形态与仍有效原则；**现行程序集地图与依赖方向以 `docs/architecture/assemblies.md` §2/§3 为准**。

## 动机

后期多人维护需要编译器级模块边界与模块独立构建/测试；[ADR-0015](./0015-module-map-and-ownership.md) 的“概念模块 + 端口”是文档级归属，编译器不强制，跨模块漂移只能靠评审。

## Considered Options

1. **维持概念模块 + 端口（基线）**：文档级归属不够，模块独立构建/测试无从谈起。
2. **垂直模块目录（单程序集内）**：无编译器级边界，却推翻水平目录正典 → 否。
3. **12 程序集全拆（每个 S/M/H 一集）**：共享件程序集化过碎，打包与引用噪音超过收益 → 否。
4. **业务模块成集、共享件聚为共享内核** → **采纳为基线**：业务纵向模块各成一集取得编译器边界，S1–S6 共享件按“共享内核”聚合。
5. **共享内核留 exe、其余拆成库**：Host 需编译期引用模块做注册、模块需引用 exe 内共享内核 → 项目引用循环，不可编译；唯一字面出路是插件运行时发现 → 否（业务模块是产品本体而非可选插件，运行时缺件风险且需改写组合根正典）。
6. **插件运行时发现（模块 dll 由 exe 扫描装配）**：否（同上）。
7. **导航架构**：壳集中映射 / 模块自治注册 / ViewLocator / 运行时字典合并——采纳**模块自治注册**（模块注册器自报导航项与页面模板，新增页面不碰 Host）+ **模块模板字典静态合并**；ViewLocator 代码装配与运行时动态字典合并被否（推翻 DataTemplate 呈现正典、模板来源不可静态追踪）。现状见 [navigation.md](../architecture/navigation.md)。
8. **DI/组合根**：演进式单一组合根（框架维持 MS.DI、注册源下放模块注册器、根对象解析仍集中 Host 组合根）被采纳；模块子容器、去容器纯手动、Generic Host、Autofac、Prism 均否（子容器割裂全应用单例；纯手动在页面导航十余互连点后噪声超过收益且开放泛型无法手写；Generic Host 等触发条件未命中）。**该原则被 ADR-0023 继承；现行注册源形态 = 内置贡献者有序清单（`ICompositionContributor` + `BuiltInContributors`，见 [assemblies.md](../architecture/assemblies.md) §6）。**
9. **可见性**：无 `InternalsVisibleTo`；模块公开面 = 注册器入口 + 被测 public 类型，内部实现保持 internal；宿主只引用模块注册器，不引用模块内部。**现行入口 = 贡献者清单/接口，注册器实现类为 internal。**
10. **命名空间策略**：拆分期间命名空间不动，收尾统一为 `StarPie.*`（与程序集对齐但跨集共享命名空间树，不要求命名空间 = 程序集名）。

## Decision

1. **结构形态基线**：宿主（exe）+ 共享内核 + 业务纵向模块各成程序集；后续演进为「模块出口契约随实现方下沉薄 `*.Contracts`、模块 runtime 互不引用」（[ADR-0023](./0023-module-contracts-hard-boundary-and-core-narrowing.md)），现行形态见 [assemblies.md](../architecture/assemblies.md) §2/§3。
2. **明确否决并记录**（防止未来被当“修复”重提）：插件运行时发现；共享内核留 exe 字面方案；12 程序集全拆；垂直目录单程序集；模块子容器；Generic Host / Autofac / Prism；去容器纯手动；ViewLocator 代码装配；运行时动态字典合并；相对锚点排序。

## Consequences

- [assemblies.md](../architecture/assemblies.md) 为程序集地图与依赖方向正典；本 ADR 不描述现状。
- 测试工程显式引用各程序集（不依赖传递引用）；被测类型保持 public。
- e2e 以 `AutomationId` 定位，程序集化不改变用户可见面。
- `config.json` 格式与 Hard Constraint 不受程序集化影响。
- CONTEXT 不收录模块/程序集术语（架构术语，ADR-0015 先例）。
