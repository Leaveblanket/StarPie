# 手动组合根，不使用 DI 容器

各服务通过构造函数注入（手动组合根），装配集中在一个独立的 `Composition` 类中，不散落在 `App.OnStartup`。服务个位数，手动组装直白、零新增依赖；且手动 `new` 是编译期检查的——构造签名漂移在编译期即报错，而容器注册要到运行时才暴露。将来读者若疑惑"为什么项目没有 DI 容器"——这是刻意选择而非遗漏。

## 服务化边界（哪些类刻意保持静态）

| 静态类 | 处置 | 理由 |
|---|---|---|
| ConfigManager | → `IConfigService` 注入 | 可测性核心缝 |
| ActionExecutor | → `IActionExecutorService` 注入 | 副作用集中地，测试需 mock |
| ActiveWindowHelper + FullScreenHelper | → 合并为 `IWindowContext` 注入 | GestureEngine 纯逻辑化的必要缝 |
| AppThemeManager | → `IThemeService` 注入 | 有运行态状态，设置页驱动 |
| I18n | 保持静态 + 语言切换广播事件 | 纯查表无状态 |
| IconHelper | 保持静态 | 无状态 Win32 工具，mock 无意义 |
| MemoryOptimizer | 保持静态 | 无状态系统调用 |
| StyleRendererFactory | 保持静态 | 无状态查找表 |
| DevInstance | 保持静态 | 常量 |

后五个保持静态是刻意的：它们无状态、无副作用分支，服务化只加间接层。

## 装配细节

- `RadialWindow` + 其 ViewModel 是瞬态对象（每次手势在鼠标钩子回调里创建）：组合根注册 `IRadialWindowFactory`，由 `GestureEngine` 经工厂创建，不引入容器来覆盖这个解析点。
- `IDialogService` 需要 Owner（设置窗口），而设置窗口的 ViewModel 链又依赖它，构成循环：用惰性回填 Owner 解决（创建顺序：先服务后窗口，窗口创建完成后回填引用）。
- 测试中直接 `new` 被测对象 + mock 依赖，不使用容器。

## 重新评估触发器（任一出现即重新评估本决策）

- 服务数量超过 ~15，或出现多套环境/多实例生命周期需求
- 引入 ILogger / Generic Host 生态
- 可插拔插件系统进入 roadmap——届时容器作为插件 SDK 的一部分一并决策（插件宿主可自带容器聚合插件贡献的服务，核心手动装配不必先行变更），而不是提前"为了容器而做插件"
