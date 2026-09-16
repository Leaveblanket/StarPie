# StarPie 架构文档（入口）

> **阅读方式**：先读本文，按任务跳转到 `docs/architecture/` 下的叶子文件；不要把整卷叶子一次性注入上下文。
>
> 现行架构规范（as-built normative）分散在叶子文件中；代码结构、注册、映射或规范变化时，**同步更新对应叶子**，并保持本文路由表登记正确。

## 1. 文档体系与分工

| 文档 | 内容 | 何时读 |
|---|---|---|
| `CONTEXT.md`（仓库根） | 领域术语词汇表 | 术语疑问、新增领域术语时 |
| `docs/adr/` | 难逆转/令人惊讶/真实权衡的决策理由；文件名即主题，**头部 Status 是状态的唯一权威**（编号有历史断档，不再复用） | 想了解“为什么这样设计”、或确认某条决策是否仍现行时 |
| `docs/architecture.md`（本文） | 架构文档入口与任务路由 | 任何架构问题先读这里 |
| `docs/architecture/*.md` | 各主题与模块规范（叶子；每篇自带维护义务） | 按下表任务跳转 |
| `docs/agents/*.md` | Agent 工作流正典（[写作文约](agents/docs-conventions.md)、issue 查询、提交约定、领域文档布局、triage 标签） | 提交、开票、写作与验收流程有疑问时 |

冲突优先级：叶子规范为准（现行规范）；ADR 解释“为什么”，不推翻现行规范；若需要改变规范且满足 ADR 三条件（难逆转 / 无上下文会惊讶 / 真实权衡），先新增 ADR 再回填叶子。

## 2. 按任务路由（渐进式披露）

| 你要做什么 | 读哪个文件 |
|---|---|
| 某个路径放什么 / 新增文件落位 / 命名规则 / 页面映射表 / 对话框配对 | [layout.md](architecture/layout.md) |
| 分层依赖矩阵 / 可见性 / Model/Service/VM/View 边界 | [layering.md](architecture/layering.md) |
| 注释规范（XML 文档注释 / 行注释） | [comments.md](architecture/comments.md) |
| 启动退出 / 单实例与开发实例 / 壳层与设置台编排 / Composition 注册 / 窗口生命周期 | [host.md](architecture/host.md) |
| 配置读写 / 防抖保存 / 导入导出 | [config.md](architecture/config.md) |
| 设置页导航 / 页面 DataTemplate 映射 | [navigation.md](architecture/navigation.md) |
| 对话框实现 / 对话框唯一形态 | [dialogs.md](architecture/dialogs.md) |
| 手势状态机 / 动作路由与执行 | [gestures.md](architecture/gestures.md) |
| 轮盘 VM / RadialWindow / 样式渲染器 | [wheel.md](architecture/wheel.md) |
| 程序扫描与目录 | [programs.md](architecture/programs.md) |
| 界面主题(AppTheme)配置与解析 / XAML 令牌与整项替换 / 主题设置面 | [interface-theme.md](architecture/interface-theme.md) |
| VS XAML 设计器预览 / Properties/DesignTimeResources.xaml / 设计期资源与视口 | [design-time-preview.md](architecture/design-time-preview.md) |
| 托盘 / 开机自启 / 内存整理 / 高级设置面 | [shell.md](architecture/shell.md) |
| 本地化文案键(resx) / 语言切换与回退链 / 运行时语言字典投影 | [localization.md](architecture/localization.md) |
| IMessenger 消息 / 弹窗通知载体 | [messages.md](architecture/messages.md) |
| 概念模块（12 模块）划分 / 归属争议 / 加改功能该动哪（扩展点验收） | [modules.md](architecture/modules.md) |
| 程序集地图 / 程序集依赖方向 / 导航槽位 / 模块间接合缝编目与裁决 | [assemblies.md](architecture/assemblies.md) |
| 插件体系：运行时装载/卸载/能力/UI 托管与插件包形态 | [plugins.md](architecture/plugins.md) |
| 插件可用面（SDK.Wpf 硬约束 / 特性白名单与不支持列表 / HostServices 硬约束 / ABI 与信任） | [plugin-contracts.md](architecture/plugin-contracts.md) |
| 插件开发（开发者视角：示例、准入、上手指引） | [plugin-dev-handbook.md](architecture/plugin-dev-handbook.md) |
| 新增功能（原型 A–F 清单） | [extending.md](architecture/extending.md) |
| 动手改代码前的底线（禁止事项） | [prohibitions.md](architecture/prohibitions.md) |

## 3. 技术栈

- .NET 10 / WPF（`net10.0-windows10.0.19041.0`、`UseWPF`，Ui 集程序集名 `StarPie`）；运行时段与
  windows 投影段的演进政策见 [ADR-0026](adr/0026-runtime-baseline-and-windows-sdk-projection.md)。
  插件化四集骨架：`StarPie.Sdk`/`StarPie.Host` 为 `net10.0` 零 WPF（SDK 另零第三方
  包）、`StarPie.Sdk.Wpf` 为 WPF 类型契约面、`StarPie.Ui` 为唯一含 XAML 与入口的 WinExe；
  依赖方向与机械断言见 [assemblies.md](architecture/assemblies.md) §3。
- `CommunityToolkit.Mvvm`：MVVM 唯一框架（`ObservableObject`、`[ObservableProperty]`、`[RelayCommand]`、`WeakReferenceMessenger`）。
- `Microsoft.Extensions.DependencyInjection`：仅用于 `Composition.cs` 组合根。
- 本地化：`Strings*.resx`（zh-CN 中性 + zh-TW/en/ja 卫星），`VocaDb.ResXFileCodeGenerator` 强类型 + `ILocalizationService` 实例服务。
- 单元测试：`StarPie.Tests`（xUnit v3，运行平台 Microsoft.Testing.Platform，直接 `new` + 手写替身，不用 mocking 框架）。
- e2e 测试：`tests/`（pywinauto，pytest），规范不在此文档体系展开；验证义务见 [git-commits](agents/git-commits.md)。
- 运行配置：`config.json`（宽松读取：大小写不敏感、允许注释与尾逗号；缺文件自动播种默认值；向后兼容为 Hard Constraint）。

> **本节只列技术栈**：类与文件的物理落点不在本节——模块划分与归属见
> [modules.md](architecture/modules.md)，目录树与逐目录落位见 [layout.md](architecture/layout.md)，
> 程序集地图与依赖方向见 [assemblies.md](architecture/assemblies.md) §2/§3，
> 插件运行时与装配管线见 [plugins.md](architecture/plugins.md)。

## 4. 仓库边界

仓库目录树（物理路径正典）见 [layout.md](architecture/layout.md) §1；程序集地图与依赖方向见
[assemblies.md](architecture/assemblies.md) §2/§3——本文不复制。

测试约定：单测文件平铺于 `StarPie.Tests` 根、命名 `{被测类型}Tests.cs`、命名空间镜像被测类型；测试工程**显式** `ProjectReference` 四集（不依赖传递引用，见 [assemblies.md](architecture/assemblies.md)）；页面/服务/对话框 VM 单测直接构造并注入依赖，不从容器解析；被测类型保持 `public`（不使用 `InternalsVisibleTo`，见 [layering.md](architecture/layering.md)）。

## 5. 分层速览

分层图、依赖矩阵、命名空间与可见性、Models/Services/ViewModels/Views 边界见 [layering.md](architecture/layering.md)——本文不复制。

## 6. 维护义务

维护义务（规范变更落点、ADR 三条件与四要素、叶子增删与入口纪律、CONTEXT 边界、盘点关闭即删、
验证义务分层）正典在《[文档写作文约](agents/docs-conventions.md)》——本文与叶子不复制。
