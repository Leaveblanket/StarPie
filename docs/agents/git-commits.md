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

验证义务分两层门：**提交级**与**合入门**。xUnit 全量便宜（569 例，纯执行约 0.5s），任何代码提交都全量跑，不按模块拆分；e2e 用免跑判定，不拆用例子集、不移除每例冷启动。拿不准时跑全量。

> 为什么这样分层：xUnit 全量纯执行约 0.5s，按模块拆子集没有可省空间且归属映射需持续维护；e2e 每用例冷启动保证独立沙箱与默认配置确定性（多数用例写穿 config 不还原），移除重启会引入顺序依赖与单例崩溃级联失败，省时收益有限。全量兜底放在合入门，CI 只强制 xUnit（GitHub runner 上 WPF UI 自动化可靠性不足）。

1. **Build must pass**（每个提交前）:

   ```bash
   dotnet build StarPie/StarPie.slnx
   ```

2. **xUnit 全量**（每个代码提交，含 refactor）:

   ```bash
   dotnet test StarPie.Tests/StarPie.Tests.csproj
   ```

3. **pywinauto e2e（提交级免跑判定）**: feature / bug-fix 提交按下表判定，命中“必跑”时才先 build 再全量跑：

   ```bash
   python -m pytest tests/test_settings.py -v
   ```

   | 改动面（命中任一即全量 e2e） |
   |---|
   | 用户可见文案：`Strings*.resx`、文案键、语言回退/切换 |
   | 页面/窗口 XAML、DataTemplate / 页面模板字典、AutomationId、导航槽位 |
   | 主题字典/令牌、界面主题与轮盘配色可见行为 |
   | 设置交互语义、对话框可见行为、壳层可见行为（托盘/窗口） |
   | `config.json` 模型/默认值/兼容面 |
   | e2e 自身：`tests/*.py`、conftest、e2e 基建 |

   未命中（纯模块内部逻辑 / 纯重构 / 文档）可免跑；边界情况按“必跑”处理。

4. **合入门（merge 到 main 前）**: 主干同步后至少一次全量 xUnit + 一次全量 pywinauto e2e（agent 本地执行）；CI 只强制 xUnit。

## Do not rewrite published history

- Do not `amend`, `rebase`, or `reset` commits that are already pushed to
  `origin/main` or an open task branch used by others.
- Local, unpushed commits on a task branch may be cleaned up freely before the
  merge.
