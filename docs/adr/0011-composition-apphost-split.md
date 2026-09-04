# Composition 拆分：组合根与 AppHost 宿主编排分离

`Composition.cs` 原本同时承担 DI 组合根与宿主启动/退出编排：`ConfigureServices`、`Run`、
托盘菜单、语言字典刷新、`ExitApplication`、`Dispose` 全收在一个类里。ADR-0005 引入
`ServiceCollection` 后装配面扩大到十余个解析点，但 `Run` 及其托盘/退出/语言副作用仍留在
同一类，组合根持续膨胀。决定：`Composition` 收敛为唯一 DI 组合根（注册 + 解析），新增
`AppHost` 承接启动/退出编排；解析点仍只出现在组合根。

## Status

Accepted。

## Considered Options

- **维持单类组合根**：被否——`Composition` 同时知道“怎么组装依赖”和“托盘/主窗口/退出怎么
  编排”，两类关注点共担一个上帝类，新增托盘入口或启动副作用都要改动组合根。
- **三层拆分**（`Composition` + 启动编排器 + 托盘/壳层协调器）：被否——托盘、退出、隐藏
  到托盘共享 `_trayIcon`、`_mainViewModel`、`_mouseHook` 状态，拆成多个小类反而让状态
  传递变复杂；当前体量下两层足够。
- **新增 `AppHost` 两层拆分**：采纳——`Composition` 保留 DI；`AppHost` 承接
  `Run`/`Dispose`/托盘/退出/语言资源等宿主编排。
- **AppHost 持有 `IServiceProvider` 自解析**：被否——会让 `AppHost` 成为第二个解析点，
  破坏 ADR-0005“解析点只出现在组合根”的既有边界。
- **Generic Host / Prism**：不采纳——ADR-0005 已明确引入 Generic Host 需要独立触发
  （ILogger 生态、多环境/多实例、插件系统），本次只是类级重组。

## Consequences

- `Composition.CreateAppHost()` 是唯一新增解析入口：解析宿主依赖与页面 VM 后构造
  `AppHost`；`Composition` 不再持有托盘/主窗口/语言字典等宿主状态。
- `App.OnStartup` 启动路径改为 `new Composition()` → `Config.Load()` →
  `Composition.CreateAppHost()` → `AppHost.Run()`；`App.OnExit` 先 `Config.Save()` →
  `AppHost.Dispose()` → `Composition.Dispose()`。
- `GeneralSettingsViewModel` 的托盘气泡/退出回调经内部 `AppHostDelegates` 转发：VM 注册时
  持稳定转发委托，`AppHost` 构造后回填宿主方法，避免 VM 反向依赖宿主类，也不改动 VM
  构造签名。
- 用户可见行为与 ADR-0003 装配顺序（钩子先启 → 配置 Load → 建窗）不变；页面 VM 解析时机
  从 `Run` 上移到 `CreateAppHost`（仍在 `Config.Load` 之后）。
- 架构叶子 `host.md`、`layout.md` 同步登记 `AppHost.cs`；后续新增托盘入口/启动副作用改
  `AppHost`，新增服务/页面 VM 仍改 `Composition.ConfigureServices`。
- ADR-0005 的重新评估触发器继续有效：出现插件系统、多环境/多实例生命周期或 ILogger 生态
  时再评估 Host 化。
