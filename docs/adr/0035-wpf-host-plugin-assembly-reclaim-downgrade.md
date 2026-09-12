# WPF 宿主降级回收判定：插件程序集留在进程内是宿主框架行为，不判隔离

> Status: Active
>
> 本文修订 [ADR-0034](0034-headless-unload-handover-and-hard-reclaim.md) 决策 3 的适用范围，
> 以及 [ADR-0030](0030-ui-plugin-unload-semantics-downgrade.md) 决策 1 中 headless 插件
> "承诺 ALC 真卸载"在 WPF 宿主的适用边界：回收判据按**宿主环境**分级，而不是按插件形态分级。
> 契约正典：`docs/architecture/plugins.md` §8。

## 动机

1. **宿主框架先于插件决定程序集可回收性**：System.Xaml 的 `XamlSchemaContext` 在初始化时经
   `AppDomain.AssemblyLoad` 收拢全部已加载程序集（`_unexaminedAssemblies`），并在任何 XAML schema
   查询时把其中的非 dynamic 程序集写进强引用字典（`_xmlnsInfo`）；BCL 侧另有一处静态程序集名字典
   同样强引用。两者都随进程存活，与插件是否自觉清理无关。
2. **插件是否触碰 WPF 不改变结论**：本票的 dogfood 插件是纯 headless 插件（只经 SDK 契约返回 DTO），
   仍被上述缓存收拢。ADR-0030 动机 2 的"不触 WPF 的路径可回收"只在**没有 XAML schema 上下文的进程**
   里成立：xUnit 夹具与"只跑 `Dispatcher.Run()`、不解析 BAML"的探针都能回收，真实 WPF 进程不能。
3. **不修订就会得到不可实现的验收**：真实宿主里停用插件会因 ALC 未回收被硬判隔离——用户显式停用
   （正常操作）与管理面的故障终态（隔离）被混为一谈，"停用"入口永远不可用。
4. **反射清缓存路线已被证否**：ADR-0030 的穷举取证（清 5 处可定位全局缓存后 ALC 与程序集仍存活，
   残余是运行时内部句柄）同样适用于本场景，且缓存布局随框架版本变化，不适合作为产品依赖。

## 取证（.NET 10.0.11，真实 WPF 宿主，dotnet-dump 转储）

- 隔离现场 `gcroot` 插件 ALC 与入口程序集，唯一托管 root 链为：
  `WpfSharedBamlSchemaContext._xmlnsInfo`（`ConcurrentDictionary<Assembly, XmlNsInfo>`，强引用键）
  → 插件 `RuntimeAssembly` → `LoaderAllocator` → `LoaderAllocatorScout`；另有静态程序集名字典与
  线程栈侧的同等链。
- 插件侧类型实例（入口实例、扫描器、闭包类）在隔离现场**已全部被回收**：残留的只有 ALC、程序集与
  框架缓存条目——即插件代码本身没有泄漏，阻碍完全来自宿主框架。
- 同上流程在 xUnit 与不带 BAML 解析的 WPF Dispatcher 探针中 `Reclaimed=True`，
  在真实 WPF 进程中恒为 `Reclaimed=False`，差异可归因到 XAML schema 上下文初始化。

## Considered Options

- **私有反射清理 WPF/BCL 缓存** → 否。ADR-0030 已证否；依赖未公开内部结构，随框架版本失效，
  且清理后仍可能被后续 XAML 查询重新写入。
- **维持硬判，接受"停用即隔离"** → 否。把正常操作渲染成故障终态，管理面与管理语义失效；
  用户无法表达"我现在不用这个插件"。
- **回收判定按宿主环境分档** → 采纳。

## Decision

1. **判定分两档**（`PluginReclaimPolicy`）：
   - `Hard`（缺省）：ALC 与入口程序集必须回收，任一存活 → 隔离 + 重启提示；适用纯 headless 宿主
     （xUnit 夹具、未来 headless 后端）。
   - `Diagnostic`：只硬判插件自有对象（入口实例、作用域句柄、在途调用）可回收；ALC、程序集与类型的
     存活只记诊断，不判隔离、不置 `Quarantined`。
2. **Ui 组合根一律装配 `Diagnostic`**：StarPie 的进程内插件（headless 与 UI）都运行在 WPF 宿主里，
   程序集必然留在进程内。停用语义因此是「停止调用 + 摘除能力 + 释放服务作用域 + 回收插件对象；
   ALC 与程序集留到重启释放」。
3. **降级不静默**：残留清单随卸载结果带出并进入 `PluginDiagnosticsReport`，管理面显示
   "宿主框架缓存程序集，重启后释放"，不谎报完全回收；硬判档的失败语义不变。
4. **调用方无从借降级放宽插件自有对象**：入口实例、作用域账本与在途调用在任何档位下存活都判失败，
   保证"插件自身泄漏"仍被如实记账。

## Consequences

- `PluginUnloadResult.Reclaimed` 的语义明确为"**可回收部分已回收、请求耗尽**"：降级档下 ALC 与程序集
  残留不再代表"还有资源可续做回收"，请求不再保留在交接账本里。
- 隔离持久化与重试路径不受影响：降级档下隔离只由真问题（落盘失败、摘除失败、在途未归零、停用异常、
  作用域残留、插件对象残留、调用熔断）触发。
- 内存占用语义：每次"停用再启用"会留下上一代程序集，直到重启；管理面须能表达这一点（由后续管理面完善承接）。
- 若将来出现 headless 宿主（无 WPF/XAML），组装根传 `PluginReclaimPolicy.Hard` 即可恢复
  ADR-0034 的硬判承诺；本 ADR 不放宽 headless 的验收能力，只修正"WPF 宿主里也适用硬判"的隐含假设。
