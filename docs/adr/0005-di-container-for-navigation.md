# 视图导航重构引入 DI 容器（ServiceCollection）

T19 设置控制台重构（MainView 主框架 + 左侧导航 + 五页面经 DataTemplate 映射切换）把装配面从"一个根 VM + 一个窗口"扩大到五个页面 ViewModel、NavigationStore、导航服务等十余个解析点；参照的导航模式（SingletonSean WpfNavigationDemo）亦以容器解析为前提。决定：引入 Microsoft.Extensions.DependencyInjection 作为组合根的装配手段，推翻 ADR-0002"手动组合根、不使用容器"的核心决定。

## Status

Supersedes ADR-0002（核心决定部分）。ADR-0002 中仍然有效的部分：装配集中在独立的组合根（`Composition`）、"刻意静态"清单及其判据、测试中直接 `new` + mock 不用容器。ADR-0001 Considered Options 中"Microsoft.Extensions.DependencyInjection 被否（见 ADR-0002）"随之失效——MEDI 属 DI 基建，不影响"CommunityToolkit.Mvvm 是唯一 MVVM 基建依赖"。

## Considered Options

- **维持手动组合根**：被否——页面导航引入十余个互连点，手动装配的噪声超过"编译期检查"的收益；导航服务需按泛型目标解析 VM，手写分支重复且易漂移。
- **Generic Host**（HostApplicationBuilder + IHostedService）：被否——宿主形态是独立决策，导航重构不构成"引入 Generic Host 生态"的正当理由；只取容器的解析能力，不换应用宿主。
- **手写服务定位器**：被否——静态服务定位器是反模式，依赖隐藏且更难测试。

## Consequences

- 容器只做解析：`Composition` 内 `new ServiceCollection()` 注册 → `BuildServiceProvider()`，解析点仍集中在组合根；ADR-0003 装配顺序（钩子先启 → 配置 Load → 建窗）不变。
- 生命周期：服务与页面 ViewModel 单例（状态跨导航常驻，见 CONTEXT.md"导航"条目）；页面 View 瞬态，由 DataTemplate 无参构造实例化，不经容器。
- 跨页面广播协调走 CommunityToolkit.Mvvm 的 WeakReferenceMessenger（`IMessenger` 以单例注册进容器，便于测试替换）；静态已知依赖（如外观页预览读选中方案）仍构造注入。
- 重新评估触发器（继承自 ADR-0002）：出现插件系统、多环境/多实例生命周期需求，或引入 ILogger 生态时，重新评估 Host 化。
