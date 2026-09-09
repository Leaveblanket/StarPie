# Architecture Docs

现行架构规范按主题/模块拆分，避免整卷加载：

- 入口：`docs/architecture.md`（文档体系 + 按任务路由表 + ADR 索引）。
- 叶子：`docs/architecture/*.md`（layout / layering / naming / comments / host / config / navigation / dialogs / gestures / wheel / programs / shell / localization / messages / modules / assemblies / seams / interface-theme / extending / prohibitions）。

## 何时读

- 动手实现、重构或审查前：先读入口，再按路由表打开与本次任务**直接相关**的叶子（例如加新功能 → `extending.md`；改某模块 → 对应模块文件）。
- 术语用 `CONTEXT.md`；想知道“为什么”读 `docs/adr/`；现行规则以叶子为准，ADR 解释理由不推翻叶子。
- 目录、分层、模块之外与本任务无关的内容不读；不要把整卷架构文档一次性注入。

## 变更义务

- 规范内容变化只改对应叶子；叶子增删或路径变化时同步更新入口的路由表，并保持本文件描述一致。
- ADR 头部必带状态（Active / Superseded by NNN / Active（部分被 NNN 修订））与修订指针。
- 叶子与 ADR 不记录“已落地/已清零/批次流水/日期快照”：完成即删，历史归 git 与 issue。
- 程序集地图与依赖方向只许 `assemblies.md` §2/§3 一份正典，其它叶子引用不抄写。
- CONTEXT 只收领域术语；架构词（宿主/模块/程序集/M1–M5 等）正典在 `modules.md`/`assemblies.md`。
- 任务型盘点文档必须带“关闭即删”义务，不保留为常驻文档。
