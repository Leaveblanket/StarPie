# Git Commits

Commit workflow and message rules for `Leaveblanket/StarPie`. Applies to human
and agent commits alike. This is a **documentation-only convention** — no
commitlint, husky, or other tooling is enforced.

## Workflow: task branch + merge commit

Every task gets its own branch off `main`; the branch is merged back with an
explicit merge commit (`--no-ff`). Do not fast-forward or squash.

- Branch naming: `<type>/#<issue>-<slug>`

  ```text
  refactor/#18-settings-console-split
  fix/#21-startup-crash-on-nav
  ```

- Rebase the task branch onto `main` before merging when it has drifted.
- Merge with an explicit merge commit:

  ```bash
  git checkout main
  git merge --no-ff refactor/#18-settings-console-split -m "merge: #18 设置控制台拆分"
  ```

- A merge commit message is `merge: #<issue> <summary>`.

## Every code change references a GitHub issue

Issues are tracked as GitHub Issues in `Leaveblanket/StarPie`; see
`docs/agents/issue-tracker.md` for CLI usage.

- Feature, bug-fix, and refactor work must map to one open issue and reference
  it in the commit subject.
- Reference style: issue number at the **end of the subject** in parentheses —
  `fix: 修复启动崩溃 (#21)`. (A bare `#21` in prose is fine in the body.)
- `chore` and `docs` commits may omit the issue reference when there is no
  corresponding issue.

## Commit message format

Conventional Commits with a Chinese subject:

```
<type>(<scope>): <subject>
```

- `<type>`: `feat` | `fix` | `refactor` | `docs` | `chore` | `test` | `perf` | `merge`
- `<scope>` (optional): the affected subsystem, e.g. `feat(settings): …`,
  or omit it and let the issue reference carry context.
- `<subject>`: concise Chinese, no trailing period. Add parenthesized key
  points when the change is broad:
  `refactor: 设置控制台拆分(主框架+侧边栏+五页面导航+消息协调)`
- Keep the subject short; move details to the body.

### Body

Write a body when the change is non-trivial:

- why the change exists;
- key decisions / root causes (bullet list for multi-cause fixes);
- verification summary (what ran and the result).

Example shape (from an actual repo commit):

```text
fix: 修复启动崩溃与页面导航失效(四处根因) (#21)

- 组合根补 IThemeService 转发注册 …
- 页面/Sidebar 自合并 SettingsStyles 字典 …

验证:沙箱完整启动路径五页真实点击往返全通;370 xUnit 绿;pywinauto e2e 18/18
```

## Atomicity

- One commit = one logical change.
- Keep deletions / moves / formatting separate from feature work.
- Never mix types in one commit (especially `chore` with `feat`/`fix`).
- No WIP commits on `main`.

## Required verification before committing

These are project rules, so agents must run them before committing:

1. **Build must pass**:

   ```bash
   dotnet build WinPieGestures/WinPieGestures.slnx
   ```

2. **Feature / bug-fix commits** must also pass the relevant test suites:

   ```bash
   # xUnit unit tests
   dotnet test WinPieGestures.Tests/WinPieGestures.Tests.csproj

   # pywinauto end-to-end tests (build first, then:)
   python -m pytest tests/test_settings.py -v
   ```

   Refactor-only commits still require the xUnit suite to stay green.

## Do not rewrite published history

- Do not `amend`, `rebase`, or `reset` commits that are already pushed to
  `origin/main` or an open task branch used by others.
- Local, unpushed commits on a task branch may be cleaned up freely before the
  merge.
