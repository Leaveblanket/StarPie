# dev 实例标记改由环境变量注入（STARPIE_INSTANCE）

> Status: Superseded by [0037](0037-dev-instance-flag-by-build-config.md)（判定机制改按构建配置编译期定死）
>
> 关联：#145

## 动机

dev 实例原以 `--dev` 命令行参数判定（`DevInstance.IsActive` 对 `Environment.CommandLine` 做 `Contains` 子串匹配），再由组合根在装配前回填静态可写属性 `AppDataPaths.IsDevInstance`（Host 不能反向引用 Ui，形成跨程序集回填缝）。三处负担：子串匹配可被恰好包含 `--dev` 的参数（如路径）误触发；判定真相有两个（Ui 的 `IsActive` 与 Host 的静态可写属性）；回填缝是纯时序约束——组合根必须赶在一切消费前赋值，注释与文档需持续解释回填职责。

## Considered Options

- **保留 `--dev`，解析收敛为一次性 LaunchOptions** → 否。回填缝与静态可写属性仍在，属改良而非消除病灶。
- **Debug 构建即 dev（`#if DEBUG` 编译期分支）** → 否。Debug/Release 语义被重载：失去"Release 构建 + dev 沙箱"组合（Release 性能验证无法与正式版并行），且单测永远跑 Debug，正式形态分支从此不可单测。
- **环境变量 `STARPIE_INSTANCE=dev`** → 采纳。Host 自足判定，回填缝整体删除；launchSettings 原生支持 `environmentVariables`；与 e2e 以环境变量隔离配置（重定向 `LOCALAPPDATA`）是同一惯用法；测试可控变量即控分支。

## Decision

1. **判定唯一真相在内核**：`AppDataPaths.IsDevInstance` 改只读属性，类型初始化时读 `AppDataPaths.DevEnvVariable`（`STARPIE_INSTANCE`）一次性求值缓存（值 `"dev"`，大小写不敏感）；宿主组合根不再回填，Composition 的「早期回填」阶段删除（四阶段收敛为三阶段）。
2. **子进程不继承标记**：`App.OnStartup` 求值后即 `Environment.SetEnvironmentVariable(name, null)` 把变量移出进程环境——动作执行器会启动任意子进程，继承标记会让用户从 dev 实例绑定的动作再启动 StarPie.exe 时意外落入 dev 沙箱。
3. **Ui 侧只留投影**：`DevInstance` 缩为 `Suffix`/`MutexName` 两个读 `AppDataPaths.IsDevInstance` 的表达式，删除参数解析。

## Consequences

- 直接运行构建产物（不经 `dotnet run`/VS F5）即正式形态——launchSettings 只对 `dotnet run`/IDE 生效，与原 `--dev` 的生效面一致，行为无回归。
- 临时以命令行切 dev 的旧习惯失效：改用 IDE 的 StarPie Dev profile，或手动设 `STARPIE_INSTANCE=dev` 后启动。
- 求值一次性语义要求「先读后清」顺序：若在 `AppDataPaths` 类型初始化前清变量，标记将永远为 false；该顺序由 `App.OnStartup` 首行求值固定。
- 配置目录、互斥名、触发键、自启保护、`(Dev)` 标记五个行为分支逐项不变，仅判定机制变化。
