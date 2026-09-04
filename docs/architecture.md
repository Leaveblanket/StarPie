# WinPieGestures 架构文档（入口）

> **阅读方式**：先读本文，按任务跳转到 `docs/architecture/` 下的叶子文件；不要把整卷叶子一次性注入上下文。
>
> 现行架构规范（as-built normative）分散在叶子文件中；代码结构、注册、映射或规范变化时，**同步更新对应叶子**，并保持本文路由表登记正确。

## 1. 文档体系与分工

| 文档 | 内容 | 何时读 |
|---|---|---|
| `CONTEXT.md`（仓库根） | 领域术语词汇表 | 术语疑问、新增领域术语时 |
| `docs/adr/` | 难逆转/令人惊讶/真实权衡的决策理由 | 想了解“为什么这样设计”时 |
| `docs/architecture.md`（本文） | 架构文档入口与任务路由 | 任何架构问题先读这里 |
| `docs/architecture/*.md` | 各主题与模块规范（叶子） | 按下表任务跳转 |
| `docs/i18n-copy-inventory.md` | 界面文案键位盘点 | 新增/修改用户可见文案时 |

冲突优先级：叶子规范为准（现行规范）；ADR 解释“为什么”，不推翻现行规范；若需要改变规范且满足 ADR 三条件（难逆转 / 无上下文会惊讶 / 真实权衡），先新增 ADR 再回填叶子。

## 2. 按任务路由（渐进式披露）

| 你要做什么 | 读哪个文件 |
|---|---|
| 某个路径放什么 / 新增文件落位 | [layout.md](architecture/layout.md) |
| 分层依赖矩阵 / 可见性 / Model/Service/VM/View 边界 | [layering.md](architecture/layering.md) |
| 命名规则 / 页面映射表 / 对话框配对 | [naming.md](architecture/naming.md) |
| 启动退出 / Composition 注册 / 窗口隐藏流程 | [host.md](architecture/host.md) |
| 配置读写 / 防抖保存 / 导入导出 | [config.md](architecture/config.md) |
| 设置页导航 / 页面 DataTemplate 映射 | [navigation.md](architecture/navigation.md) |
| 对话框实现 / 对话框唯一形态 | [dialogs.md](architecture/dialogs.md) |
| 手势状态机 / 动作路由与执行 | [gestures.md](architecture/gestures.md) |
| 轮盘 VM / RadialWindow / 样式渲染器 | [wheel.md](architecture/wheel.md) |
| 程序扫描与图标 | [programs.md](architecture/programs.md) |
| 主题 / 托盘 / 单实例 / 内存 / 自启 | [shell.md](architecture/shell.md) |
| I18n 文案键 / IMessenger 消息 | [localization.md](architecture/localization.md) |
| 新增功能（原型 A–F 清单） | [extending.md](architecture/extending.md) |
| 动手改代码前的底线（禁止事项） | [prohibitions.md](architecture/prohibitions.md) |

## 3. 技术栈

- .NET 8 / WPF（`net8.0-windows`、`UseWPF`，程序集名 `StarPie`）。
- `CommunityToolkit.Mvvm`：MVVM 唯一框架（`ObservableObject`、`[ObservableProperty]`、`[RelayCommand]`、`WeakReferenceMessenger`）。
- `Microsoft.Extensions.DependencyInjection`：仅用于 `Composition.cs` 组合根。
- 单元测试：`WinPieGestures.Tests`（xUnit，直接 `new` + 手写替身，不用 mocking 框架）。
- e2e 测试：`tests/`（pywinauto，pytest），规范不在此文档体系展开。
- 运行配置：`config.json`（宽松读取：大小写不敏感、允许注释与尾逗号；缺文件自动播种默认值；向后兼容为 Hard Constraint）。

## 4. 仓库边界

```text
StarPie/
├── CONTEXT.md
├── AGENTS.md
├── docs/
│   ├── architecture.md          # 本文（入口）
│   ├── architecture/            # 架构叶子文档
│   ├── adr/                     # 决策记录（ADR-0001 ~ 0010）
│   ├── agents/                  # Agent 工作流文档
│   └── i18n-copy-inventory.md   # 文案盘点
├── WinPieGestures/              # 主程序（规范对象，见 layout.md）
├── WinPieGestures.Tests/        # xUnit 单元测试
├── tests/                       # pywinauto e2e（不在本文档体系展开）
└── releases/                    # 旧版本，冻结：不修改、不作为当前实现依据
```

测试约定：单测文件平铺于 `WinPieGestures.Tests` 根、命名 `{被测类型}Tests.cs`、命名空间镜像被测类型；页面/服务/对话框 VM 单测直接构造并注入依赖，不从容器解析；被测类型保持 `public`（不使用 `InternalsVisibleTo`，见 [layering.md](architecture/layering.md)）。

## 5. 分层速览

```text
App / Composition            # 装配与生命周期（唯一解析点）
      |
      v
ViewModels ---> Views        # 经 DataContext/DataTemplate；View 不反向引用 VM 之外
      |
      v
Services ---> Models
```

完整依赖矩阵、命名空间与可见性、Models/Services/ViewModels/Views 边界见 [layering.md](architecture/layering.md)。

## 6. 维护义务

1. 规范内容变更只改**对应叶子文件**；新增主题时先建叶子并在本文路由表登记。
2. 新增决策若满足 ADR 三条件，先新增 ADR，再把结论回填对应叶子；反之只改叶子。
3. 新增用户可见文案时补齐四语言键值（zh-CN / zh-TW / en / ja），并核对 `docs/i18n-copy-inventory.md`（见 [localization.md](architecture/localization.md)）。
4. 叶子增删、文件路径变化时同步更新本文（文档体系表 + 路由表 + 仓库边界树）。

## 附录：ADR 索引

| 编号 | 文件 | 主题 |
|---|---|---|
| 0001 | `docs/adr/0001-mvvm-with-communitytoolkit.md` | MVVM 采用 CommunityToolkit |
| 0002 | `docs/adr/0002-manual-composition-root.md` | 手动组合根与接缝 |
| 0003 | `docs/adr/0003-application-host-restructure.md` | 应用宿主重构 |
| 0004 | `docs/adr/0004-dialog-service-design.md` | 对话框服务设计 |
| 0005 | `docs/adr/0005-di-container-for-navigation.md` | 容器化导航 |
| 0006 | `docs/adr/0006-viewmodel-service-folder-architecture.md` | VM/服务目录架构 |
| 0007 | `docs/adr/0007-views-folder-and-namespace-architecture.md` | Views 目录与命名空间 |
| 0008 | `docs/adr/0008-strict-viewmodel-view-boundary.md` | VM/View 严格边界 |
| 0009 | `docs/adr/0009-view-code-behind-whitelist.md` | View code-behind 白名单 |
| 0010 | `docs/adr/0010-localization-copy-principles.md` | 本地化文案原则 |
