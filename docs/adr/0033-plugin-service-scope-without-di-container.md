# 插件服务作用域：自持服务实例与句柄账本，不引入 MS.DI 子容器

> Status: Active
>
> 关联：#123。契约正典：[plugins.md §6/§6.1](../architecture/plugins.md)（§6.1 第 4 条按本 ADR 修订）。

## 动机

1. 目标态写法（ADR-0027 决策 3、plugins.md §6.1 约束 4）是"每插件子 `ServiceProvider`"，其要义是**作用域隔离**：宿主根容器不含插件类型，插件实例只活在按插件划分的边界内。
2. 宿主内核没有组合根：`Microsoft.Extensions.DependencyInjection` 只被 Ui 的组合根使用，`StarPie.Host` 是只引 SDK 的手写装配。
3. 运行期注册与容器语义冲突：能力实例由插件在 `StartAsync` 里逐个注册（`IPluginContext.RegisterCapability<T>`），而 `ServiceProvider` 构建后不可追加注册——要支持就得再挂一个可变注册表，容器只剩壳。
4. 现有形状已给出等价隔离：每插件一个 `PluginServiceScope`，自持宿主服务实例、能力实例字典与句柄账本；能力表只引用作用域，消费者取用的是"每次调用现取实例"的守卫适配器。

## Considered Options

- **Host 引入 MS.DI，每插件 `CreateScope()` 子容器** → 否。容器里没有可注册的东西：实例终究要落在运行期注册表，净增一个依赖与一层间接，隔离语义不增加。
- **把 `IServiceProvider` 作为插件可见面**（插件自定义服务供宿主解析） → 否。plugins.md §6.1 约束 3 明确插件只经 `IPluginContext` 取用；首期也不开放插件间依赖。
- **每插件一个自持作用域（服务实例 + 能力实例 + 句柄账本）** → 采纳。

## Decision

1. `PluginServiceScope` 即"每插件一个"的服务边界：持有该插件的宿主服务实例（日志、事件）、能力实例字典与句柄账本；释放幂等，释放后拒绝登记，是 ALC 卸载的前置。
2. 隔离判据从"容器归属"落到"实例归属"：能力实例只进作用域字典；`CapabilityRegistry` 只存（作用域、契约）与顺序元数据，消费者每次取用都构造新的守卫适配器，适配器不缓存实例。
3. 撤销"子 `ServiceProvider`"中的容器承诺，保留其隔离承诺：宿主根容器不含任何插件类型这一条不变（Ui 组合根也不注册插件类型）。plugins.md §6.1 第 4 条按此修订。

## Consequences

- 插件暂不获得"自行注册服务"的能力：首期插件可见面只有能力注册与宿主服务；将来若开放，需先设计运行期注册语义，再评估是否引入容器（另立 ADR）。
- 作用域的账本、幂等释放与异常聚合成为可测单元（`PluginServiceScopeTests`），比容器行为更容易断言。
- 宿主内部消费能力经 `CapabilityRegistry.GetAll<T>()` 而非 `GetService(Type)`：类型安全留在编译期，装配错误更早暴露。
