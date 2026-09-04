# AGENTS.md

## Agent skills

### Issue tracker

Issues are tracked as GitHub Issues in `Leaveblanket/StarPie`, accessed via the `gh` CLI. See `docs/agents/issue-tracker.md`.

### Triage labels

The five canonical triage roles use their default label strings (`needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, `wontfix`). See `docs/agents/triage-labels.md`.

### Domain docs

Single-context layout: one `CONTEXT.md` plus `docs/adr/` at the repo root. See `docs/agents/domain.md`.

### Architecture docs

现行架构规范已按主题/模块拆分：先读入口 `docs/architecture.md` 按任务路由到 `docs/architecture/` 的对应叶子，只加载当前任务需要的文件。See `docs/agents/architecture.md`.

### Git commits

Commits follow Conventional Commits with Chinese subjects, reference a GitHub issue (`#NN` at the end of the subject), and land via task branches merged with explicit `merge` commits. Build must pass before any commit; feature/bug-fix commits additionally require the xUnit and pywinauto e2e suites to pass. See `docs/agents/git-commits.md`.
