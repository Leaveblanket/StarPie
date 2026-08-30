# AGENTS.md

## Agent skills

### Issue tracker

Issues are tracked as GitHub Issues in `Leaveblanket/StarPie`, accessed via the `gh` CLI. See `docs/agents/issue-tracker.md`.

### Triage labels

The five canonical triage roles use their default label strings (`needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, `wontfix`). See `docs/agents/triage-labels.md`.

### Domain docs

Single-context layout: one `CONTEXT.md` plus `docs/adr/` at the repo root. See `docs/agents/domain.md`.



## Core Rules

- 始终使用中文和用户对话。
- 使用渐进式披露：先获取最小必要上下文，明确下一步后停止继续收集。
- 不要扫描、读取、总结或搜索整个仓库，除非用户明确要求。

## MCP Routing

- `context7`
  - 用于查询最新官方库文档、API 用法和示例。
  - 当实现依赖外部库/框架且用法不确定时使用。
  - 不要用它替代本地代码分析。
  

- `CodeGraph`
  -  仓库根存在 `.codegraph/` 时，定位或理解代码前先使用 CodeGraph：
      - MCP：优先调用 `codegraph_explore`；未加载时按名称加载。
      - Shell：`codegraph explore "<符号或问题>"`。
        没有 `.codegraph/` 时跳过。



@RTK.md
