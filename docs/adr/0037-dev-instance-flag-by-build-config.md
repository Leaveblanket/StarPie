# dev 实例标记按构建配置定死（Debug=dev，Release=正式）

> Status: Active（互斥名与触发键两个行为分支被 [0038](0038-dev-instance-no-parallel.md) 移除）
>
> 关联：#146。取代 [ADR-0036](0036-dev-instance-flag-via-environment-variable.md) 的环境变量判定；五个行为分支（配置沙箱、互斥名、触发键、自启保护、`(Dev)` 标记）不变。

## 动机

ADR-0036 以环境变量 `STARPIE_INSTANCE=dev` 替代 `--dev` 参数后，实际使用中确认两点：不存在 dev 实例与正式版并行运行之外的实例形态需求；「Release 构建 + dev 沙箱」的人工验证场景（发布前性能验证、试用）没有真实发生，且即便出现也可用重定向 `LOCALAPPDATA` 启动覆盖大部分隔离需求（e2e 现成惯用法）。环境变量机制因此成为无消费场景的间接层：求值缓存、「先读后清」顺序约束、App 启动的环境清理代码都是为它支付的复杂度。

## Considered Options

- **维持环境变量判定** → 否。间接层无对应场景；环境变量在 launchSettings 之外不可见，排查实例形态时多一个来源。
- **编译期按构建配置定死** → 采纳。`dotnet run`（默认 Debug）即 dev，`-c Release`/`dotnet publish` 即正式，无参数、无 profile、无运行时求值；开发与发布的心智模型与 .NET 生态惯例一致。

## Decision

1. `AppDataPaths.IsDevInstance` 以 `#if DEBUG` 在编译期定死：Debug 构建为 true（dev 沙箱），Release 构建为 false（正式形态）。
2. 删除环境变量机制：`DevEnvVariable` 常量、`App.OnStartup` 的求值后清理块、`launchSettings.json`（Dev profile 不再需要）。
3. `DevInstance` 维持 Ui 侧投影（`Suffix`/`MutexName`），判定唯一真相仍在内核 `AppDataPaths`。

## Consequences

- Release 构建本地运行即为正式形态：使用真实 `%LOCALAPPDATA%\StarPie` 配置与正式互斥名，与已安装正式版互斥——这是「Release=正式」语义的直接体现，需要隔离时改用重定向 `LOCALAPPDATA` 启动。
- dev 形态绑定 Debug：无法产出「Release 优化 + dev 沙箱」的组合构建；该需求重新出现时再立 ADR（如恢复显式开关）。
- 仓库四集引入首处 `#if DEBUG` 分支（内核 `AppDataPaths`）；编译期常量使两个分支各自可被 JIT 折叠，运行时零判定成本。
