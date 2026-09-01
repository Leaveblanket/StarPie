# 手动组合根，不使用 DI 容器

> **状态**：核心决定（不使用容器）已被 [ADR-0005](./0005-di-container-for-navigation.md) 推翻——组合根改为 ServiceCollection 解析。本文其余部分（装配集中在组合根、"刻意静态"清单及判据、测试不用容器）仍然有效。

各服务通过构造函数注入（手动组合根），装配集中在一个独立的 `Composition` 类中，不散落在 `App.OnStartup`。服务个位数，手动组装直白、零新增依赖；且手动 `new` 是编译期检查的——构造签名漂移在编译期即报错，而容器注册要到运行时才暴露。将来读者若疑惑"为什么项目没有 DI 容器"——这是刻意选择而非遗漏。

## 服务化边界（哪些类刻意保持静态）

| 静态类 | 处置 | 理由 |
|---|---|---|
| ConfigManager | 已删除（T16）：文件 I/O 早入 `IConfigService`；路径计算收进 `AppDataPaths`、自启注册表收进 `AutostartRegistry`，组合根直接 `new JsonConfigService(path)` | 可测性核心缝 |
| ActionExecutor | 已删除（T15）：决策逻辑提炼为纯函数 `ActionRouting`，系统调用进 `ActionExecutorService : IActionExecutorService` 注入 | 副作用集中地，测试需 mock |
| ActiveWindowHelper + FullScreenHelper | 已删除（T04）：合并为 `IWindowContext` 注入 | GestureEngine 纯逻辑化的必要缝 |
| AppThemeManager | 已删除（T09）：→ `IThemeService` 注入 | 有运行态状态，设置页驱动 |
| I18n | 保持静态 + 语言切换广播事件 | 纯查表无状态 |
| IconHelper | 保持静态 | 无状态 Win32 工具，mock 无意义（T16 起 app-data 目录经 `AppDataPaths` 解析，不再依赖配置门面） |
| MemoryOptimizer | 保持静态 | 无状态系统调用 |
| StyleRendererFactory | 保持静态 | 无状态查找表 |
| DevInstance | 保持静态 | 常量 |
| AppDataPaths（T16 新增） | 保持静态 | 环境派生路径解析（dev 沙箱 + legacy 迁移），分支仅取决于环境变量与启动参数 |
| AutostartRegistry（T16 新增） | 保持静态 | 无状态注册表系统调用，与 MemoryOptimizer 同类；可测缝是 VM 的注入委托 |

后五个保持静态是刻意的：它们无状态、无副作用分支，服务化只加间接层。T16 新增的两个小静态工具沿用同一判据（无状态、无需要 mock 的分支）。

## 装配细节

- `RadialWindow` + 其 ViewModel 是瞬态对象（每次手势在鼠标钩子回调里创建）：组合根注册 `IRadialWindowFactory`，由 `GestureEngine` 经工厂创建，不引入容器来覆盖这个解析点。
- `IDialogService` 需要 Owner（设置窗口），而设置窗口的 ViewModel 链又依赖它，构成循环：用惰性回填 Owner 解决（创建顺序：先服务后窗口，窗口创建完成后回填引用）。
- 测试中直接 `new` 被测对象 + mock 依赖，不使用容器。

## 实现现状（T16 收尾对账）

整场 MVVM 迁移（T04–T16）落定后的最终装配形态，与上文决策的对应关系：

- **唯一配置实例住组合根**：`Composition` 直接 `new JsonConfigService(AppDataPaths.GetAppDataFolder() + "config.json")` 并暴露 `IConfigService`；应用层在 `Run()` 前驱动 `Load`、退出时 `Save`（ADR-0003 装配顺序不变）。ConfigManager 门面及其静态首取实例已整体删除。
- **设置根 ViewModel 的装配点在组合根**（对 T14 过渡形态的收口）：T14 曾在 `SettingsWindow` 构造函数内 `new RootSettingsViewModel` 并由窗口转发宿主委托；T16 把装配上移到 `Composition.Run()`——自启注册表接 `AutostartRegistry`、导入导出接 `IConfigService`、托盘气泡与退出复用应用层委托。窗口构造函数只消费 `(RootSettingsViewModel, IThemeService, IDialogService, IActionExecutorService)`，不再感知宿主副作用。这是对"装配集中在组合根"的回归，非架构变更。
- **视图层对配置的纯读取经根 VM 暴露**：`RootSettingsViewModel.CurrentConfig`（导入后自动取到新实例）与 `SaveConfig()`（落盘经注入服务）。预览绘制、控件初值等 View 读取不再触碰任何静态入口。
- **外观分区残余状态以透传属性收编**：界面主题（AppTheme）与中心核图标四项（ShowCoreIcon/CoreIconType/CoreCustomIconKey/CoreCustomImagePath）住 `AppearanceSettingsViewModel` 透传属性（读直取、写直穿运行态配置，不持副本），中心核图标选取对话框编排进 `PickCoreIcon()`（SlotViewModel.PickIcon 先例）；方案名查重与缺省名收进 `ProfileListViewModel`。
- **Model 层独立成文件**：`AppConfig`/`WheelProfile`/`ActionItem`/`CustomColorPreset` 四个 POCO 自门面文件提取为 `ConfigModels.cs`，字段与默认值一字未动（config.json 向后兼容硬约束不受影响）。
- **刻意静态五件套不动**：I18n / IconHelper / MemoryOptimizer / StyleRendererFactory / DevInstance 维持原判。

## 重新评估触发器（任一出现即重新评估本决策）

- 服务数量超过 ~15，或出现多套环境/多实例生命周期需求
- 引入 ILogger / Generic Host 生态
- 可插拔插件系统进入 roadmap——届时容器作为插件 SDK 的一部分一并决策（插件宿主可自带容器聚合插件贡献的服务，核心手动装配不必先行变更），而不是提前"为了容器而做插件"
