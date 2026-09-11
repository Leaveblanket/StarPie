# Git 提交约定

`Leaveblanket/StarPie` 的提交工作流与提交消息规则，对人类与 agent 的提交同等适用。本文档仅为**纯文档约定**——不强制启用 commitlint、husky 或其它工具。

## 工作流：任务分支 + merge 提交

每个任务都从 `main` 分出独立分支；分支合回 `main` 时使用显式 merge 提交（`--no-ff`）。不要 fast-forward 或 squash。

- 分支命名：`<type>/#<issue>-<slug>`

  ```text
  refactor/#18-settings-console-split
  fix/#21-startup-crash-on-nav
  ```

- 任务分支与 `main` 发生漂移时，合并前先 rebase 到 `main`。
- 使用显式 merge 提交合回：

  ```bash
  git checkout main
  git merge --no-ff refactor/#18-settings-console-split -m "merge: #18 设置控制台拆分"
  ```

- merge 提交的消息格式为 `merge: #<issue> <summary>`。

## 每次代码改动都引用一个 GitHub issue

Issue 以 GitHub Issues 形式记录在 `Leaveblanket/StarPie`；CLI 用法见 `docs/agents/issue-tracker.md`。

- feature、bug-fix、refactor 类工作必须对应一个未关闭的 issue，并在提交主题中引用它。
- 引用格式：issue 编号放在**主题末尾**的括号内——`fix: 修复启动崩溃 (#21)`。（正文行文中裸写 `#21` 是允许的。）
- 没有对应 issue 时，`chore` 与 `docs` 提交可以省略 issue 引用。

## 提交消息格式

采用 Conventional Commits，主题用中文：

```
<type>(<scope>): <subject>
```

- `<type>`：`feat` | `fix` | `refactor` | `docs` | `chore` | `test` | `perf` | `merge`
- `<scope>`（可选）：受影响的子系统，例如 `feat(settings): …`；也可以省略，让 issue 引用承载上下文。
- `<subject>`：简洁中文，结尾不带句号。改动面较宽时在括号内补充关键点：
  `refactor: 设置控制台拆分(主框架+侧边栏+五页面导航+消息协调)`
- 主题保持简短；细节移入正文。

### 正文

改动不平凡时写正文：

- 改动存在的原因；
- 关键决策 / 根因（多因修复用项目符号列表）；
- 验证摘要（执行了什么、结果如何）。

示例形态（取自仓库真实提交）：

```text
fix: 修复启动崩溃与页面导航失效(四处根因) (#21)

- 组合根补 IThemeService 转发注册 …
- 页面/Sidebar 自合并 SettingsStyles 字典 …

验证:沙箱完整启动路径五页真实点击往返全通;370 xUnit 绿;pywinauto e2e 18/18
```

## 原子性

- 一次提交 = 一个逻辑改动。
- 删除 / 移动 / 格式化与功能改动分开提交。
- 一个提交内绝不混类型（尤其不要把 `chore` 与 `feat`/`fix` 混在一起）。
- `main` 上不允许 WIP 提交。

## 提交前必须完成的验证

验证义务分两层门：**提交级**与**合入门**。xUnit 全量便宜（当前 598 例，MTP 报告执行约 2–3 秒），任何代码提交都全量跑，不按模块拆分；e2e 用免跑判定，不拆用例子集、不移除每例冷启动。拿不准时跑全量。

> 为什么这样分层：xUnit 全量执行 2–3 秒，按模块拆子集没有可省空间且归属映射需持续维护；e2e 每用例冷启动保证独立沙箱与默认配置确定性（多数用例写穿 config 不还原），移除重启会引入顺序依赖与单例崩溃级联失败，省时收益有限。全量兜底放在合入门，CI 只强制 xUnit（GitHub runner 上 WPF UI 自动化可靠性不足）。

1. **Build must pass**（每个提交前）:

   ```bash
   dotnet build StarPie.slnx
   ```

   （仓库根执行，构建全解决方案；构建/测试入口与 CI 一致。）

2. **xUnit 全量**（每个代码提交，含 refactor）:

   ```bash
   dotnet test --project StarPie.Tests/StarPie.Tests.csproj
   ```

   运行平台是 Microsoft.Testing.Platform（MTP 模式由 `global.json` 的 `test.runner` 打开），
   工程用 `--project` 指定；位置参数形态只在 VSTest 模式下有效。

3. **pywinauto e2e（提交级免跑判定）**: feature / bug-fix 提交按下表判定，命中“必跑”时才先 build 再全量跑：

   ```bash
   pwsh -File scripts/run-e2e.ps1
   ```

   运行器默认静默形态（被测应用以 `--background` 启动：离屏、不可激活、无托盘/全局钩子，
   Save 提示框不呈现），全程不抢前台、不移动物理光标；日志与 junitxml 落 `artifacts/e2e/`，
   `-Status` 查最近一次结果，`-OnScreen` 为调试用可见形态。等价裸命令见 `tests/conftest.py`。

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

### e2e 用例命名（行为名，不用版本号）

用例名表达**测什么行为**，不表达**哪版加的**：

- ✅ `test_drag_threshold_persists_after_save`
- ❌ `test_v135_program_picker_clean_icons_and_core_customization`

理由是版本号命名会随功能演进迅速失真（一个用例常覆盖多版改动，而一个版本也常被多例拆开），且会让"哪个行为有覆盖"无法从名字读出。存量 `tests/test_settings.py` 中的 `test_v13x_…` / `test_v14x_…` 属历史遗留，**不强制重命名**；但**新增与改动**的用例一律用行为名。

## 不要改写已发布的历史

- 已推送到 `origin/main`、或推送到他人正在使用的开放任务分支的提交，禁止 `amend`、`rebase` 或 `reset`。
- 任务分支上尚未推送的本地提交，可以在合并前随意清理。
