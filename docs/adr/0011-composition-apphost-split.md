# Composition 拆分：组合根与 AppHost 宿主编排分离

> Status: Active（注册源被 [ADR-0016](0016-assembly-split-target-and-roadmap.md) 修订：可下放模块注册器，解析点仍集中 Host 组合根；宿主编排的单对象假设被 [ADR-0039](0039-resident-shell-and-transient-settings-console.md) 修订）
>
> 修订指针：本 ADR 记录时该类名为 `AppHost`，现名 `ShellHost`（标题与文件名沿用当时名）。

`Composition.cs` 原本同时承担 DI 组合根与宿主启动/退出编排：`ConfigureServices`、`Run`、
托盘菜单、语言字典刷新、`ExitApplication`、`Dispose` 全收在一个类里。[ADR-0005](0005-di-container-for-navigation.md)
引入 `ServiceCollection` 后装配面扩大到十余个解析点，但 `Run` 及其托盘/退出/语言副作用仍
留在同一类，组合根持续膨胀。决定：`Composition` 收敛为唯一 DI 组合根（注册 + 解析），新增
`ShellHost` 承接启动/退出编排；解析点仍只出现在组合根。

## Considered Options

- **维持单类组合根**：被否——`Composition` 同时知道“怎么组装依赖”和“托盘/主窗口/退出怎么
  编排”，两类关注点共担一个上帝类，新增托盘入口或启动副作用都要改动组合根。
- **三层拆分**（`Composition` + 启动编排器 + 托盘/壳层协调器）：被否——托盘、退出、隐藏
  到托盘共享 `_trayIcon`、`_mainViewModel`、`_mouseHook` 状态，拆成多个小类反而让状态
  传递变复杂；当前体量下两层足够。
- **新增 `ShellHost` 两层拆分**：采纳——`Composition` 保留 DI；`ShellHost` 承接
  `Run`/`Dispose`/托盘/退出/语言资源等宿主编排。
- **宿主类持有 `IServiceProvider` 自解析**：被否——会让宿主类成为第二个解析点，
  破坏 ADR-0005“解析点只出现在组合根”的既有边界。
- **Generic Host / Prism**：不采纳——ADR-0005 已明确引入 Generic Host 需要独立触发
  （ILogger 生态、多环境/多实例、插件系统），本次只是类级重组。

## Consequences

- `Composition` 不再持有托盘/主窗口/语言字典等宿主状态；`Composition.CreateShellHost()` 是
  唯一新增解析入口（解析常驻依赖后构造宿主类）。
- 页面 VM 不反向依赖宿主类：宿主回调经 `AppHostDelegates` 契约转发，现行落点见
  [assemblies.md](../architecture/assemblies.md) §8 接合缝编目。
- 用户可见行为与 ADR-0003 装配顺序（钩子先启 → 配置 Load → 建窗）不变；现行启动/退出编排与
  扩展落点见 [host.md](../architecture/host.md)。
