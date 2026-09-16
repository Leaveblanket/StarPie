# AGENTS.md

## Agent skills

### Issue tracker

Issues are tracked as GitHub Issues in `Leaveblanket/StarPie`, accessed via the `gh` CLI. See `docs/agents/issue-tracker.md`.

### Triage labels

The five canonical triage roles use their default label strings. See `docs/agents/triage-labels.md`.

### Domain docs

Single-context layout: one `CONTEXT.md` plus `docs/adr/` at the repo root. See `docs/agents/domain.md`.

### Architecture docs

现行架构规范按主题/模块拆分：先读入口 `docs/architecture.md`，按任务路由到 `docs/architecture/` 的对应叶子，只加载当前任务需要的文件。

### Git commits

提交前必读 `docs/agents/git-commits.md`（约定式提交与中文主题、issue 引用、任务分支与显式 merge、两层验证门）。
