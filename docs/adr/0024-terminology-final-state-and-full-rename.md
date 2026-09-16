# 术语终态与全仓正名：不留历史别名，只留持久化迁移

> Status: Active（终态词表见 `CONTEXT.md` 与 `docs/architecture/layout.md` §3）

## 动机

1. **同一概念多套英文并存造成实际滞后**：轮盘配色在领域词/类型名中叫 Wheel Palette，在配置键、UI 文案、VM/XAML 与渲染器参数中叫 Wheel Theme / `Theme`；「界面主题」与「轮盘配色」在 `Theme` 一词上互相污染。文档层面的“兼容性注记”只会复制同一病灶（程序集地图曾在多处互抄并出现滞后实锤）。
2. **品牌名残留**：产物名/命名空间/AppData 目录早已是 `StarPie`，但工程目录、工程文件名、解决方案、测试工程、注册表自启值、注释与文档仍大面积使用旧品牌 `WinPieGestures`。
3. **页面（Page）与 Tab 冲突**：领域词正名为「页面」并明令 Avoid Tab，但 UIA 自动化 ID 与 resx 键仍是 `NavTab*` / `Tab*`。
4. **`config.json` 向后兼容是 Hard Constraint**：已发布版本存在存量用户配置，键名不能直接删除；但“长期双读双写”会让新代码继续携带旧名，违背单一终态。

## Considered Options

### 命名策略
- **单一名义 + 加载期迁移**：代码/UI/文档只存在终态名；`config.json` 加载时若新键缺失则回退读旧键一次，保存写新键。旧键不出现在新代码、新文档。
- **保留旧名并加「历史遗留」注记**：改动最小，但术语仍双轨、文档持续解释旧名，互抄滞后风险继续 → 否。
- **不兼容改名**：违反 `config.json` Hard Constraint，存量用户丢失设置 → 否。

→ 选 **单一名义 + 加载期迁移**。

### 终态词表（采纳）

终态名本身见 [CONTEXT.md](../../CONTEXT.md) 与 [layout.md](../architecture/layout.md) §3——本 ADR 不复制词表，
只记**旧名去向**：统一为**加载期迁移 + 单一名义**，不做双名并存；旧名只存在于加载期迁移读取路径与
迁移测试中，作为行为而非术语保留（详见下方 Decision 2）。

### 壳层术语（伞形终态）

「壳层 (Shell)」是应用外壳职责的伞形术语，下分**壳窗口**与**系统集成**两个子词条——定义见
[CONTEXT.md](../../CONTEXT.md)，本 ADR 不复制。本 ADR 只记该伞形拆分的决策：此前「Shell」一词同时
指模块、程序集与窗口职责，改名与拆分同步收口，伞形语义保留在 `StarPie.Services.Shell` /
`StarPie.ShellIntegration` 命名空间中。

## Decision

1. **全仓按终态名书写**：`.cs`/`.xaml`/`.resx`/`.py`/`.csproj`/`.slnx`/CI/`.vscode`/`docs` 不保留任何“旧名为历史遗留”注记（终态词表见 [CONTEXT.md](../../CONTEXT.md) 与 [layout.md](../architecture/layout.md) §3）。
2. **唯一例外是持久化迁移**：`JsonConfigService` 加载/导入时新键缺失则按旧键读取一次（轮盘配色键与轮盘风格键各一），保存一律写新键；xUnit 覆盖「旧文件读入 → 新键落盘」与「新文件直读」。
3. **自启注册表值名随工程正名**：写 `StarPie`，启动时清理旧品牌值，避免旧装机双自启。
4. **不重命名已发布 config 值**（`System`/`Dark`/`ClassicRing`/`MatchaForest` 等），不重排 `StarPie.*` 程序集/命名空间拓扑。

## Consequences

- 术语单一来源：CONTEXT.md（领域词）+ modules.md/assemblies.md（架构词）不再需要“旧名”说明段；新代码/新文案按终态名书写。
- `config.json` 旧键仅存在于迁移读取路径与迁移测试中，作为行为而非术语保留。
- e2e 随 AutomationId（`NavPage*`）与 XAML 名称同步更新。
- git 历史保留旧名，未来读者经本 ADR 反查。
