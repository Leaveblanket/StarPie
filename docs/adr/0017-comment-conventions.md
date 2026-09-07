# 注释规范：代码注释不承载溯源，理由归 git / ADR / 架构叶子

全仓 `///` 与 `//` 注释长期内嵌 issue / 批次 / ADR 溯源（`T##`、`B#/#NN`、`ADR-NNNN`、`S#/M#/H#` 等，约 540 行），把“变更史”混进“API 契约”，随批次持续累积噪音。决定：XML 文档注释与行注释只保留当前事实与信息增量，禁止编号化溯源；决策理由与变更史归 git commit、`docs/adr/` 与架构叶子；配套新叶子 `docs/architecture/comments.md` 执行，存量另立 issue #84 清理。

## Status

Accepted（grilling 共识 Q1–Q10，2026-09-07）。

## Considered Options

1. **维持现状（注释内嵌溯源）**：把版本管理职责塞进源码注释；IntelliSense / 未来 API 文档读者被迫读内部过程史；文件搬移或重构后编号常失效 → 否。
2. **只删 issue / 批次号，保留 ADR 编号**：仍是过程史而非 API 契约；ADR 编号随重构失效且无法从代码语义推回 → 否。
3. **注释零溯源 + 决策理由归位（git / ADR / 叶子）** → **采纳**。
4. **工具强制（StyleCop.Analyzers / CS1591）**：内部应用非发布库；全量开启诱导“凑数废话注释”，与目标相反 → 现阶段不引入（后续可单独评估仅共享内核 `StarPie.Core` 开 CS1591）。

## Decision

1. **范围**：约束 C# 源码 `///` 与 `//`（含文件头；XAML 同底线）。`///` 写 API 契约与用法，`//` 写代码内 why 与约束。
2. **信息增量**：`<summary>` 一句话；`<remarks>` 补约束 / 异常 / 用法；禁止复述签名可见内容。
3. **禁止**：`#NN`、`T##`、`B#/#NN`、`H#`、`S#`、`M#`、`C#`、`ADR-NNNN` 等一切编号化溯源；人名与日期；批次 / 收编 / 迁入 / 上提等变更叙事。唯一例外 `// TODO(#NN)`。
4. **理由落点**：commit message 记本次 why；难逆转 / 令人惊讶的决策记 ADR；现行规则记架构叶子。
5. **语言**：中文，领域术语用 `CONTEXT.md` 规范词。
6. **强制方式**：暂不加文档分析器与 CS1591；靠规范 + 评审；被修改到的旧注释顺手改写。
7. **存量**：不追溯清理（issue #84），新旧并存属预期。

## Consequences

- 新增架构叶子 `docs/architecture/comments.md`，并登记 `docs/architecture.md` 路由表与 `docs/agents/architecture.md` 叶子清单。
- 新注释不再携带任务 / ADR 编号；git log / blame 是唯一权威溯源。
- 存量清理（issue #84）按程序集分批，每批构建 + xUnit 绿。
- 本 ADR 不推翻既有叶子规则，只约束“注释怎么写”。

## 参考事实（2026-09-07 快照）

- 规模：`///` 约 360 行、`//` 约 180 行携带溯源标记。
- 例：`AppDataPaths.cs` 类头（T16 / ADR-0002 / B2/#75 / H1 / Composition）、各 `GlobalUsings.cs` 文件头、模块注册器注释。
- 依据：MS Learn《Create XML documentation》与《How to write /// docs for .NET API ref》；dotnet/runtime 注释指南（公共成员应文档化，复杂内部成员鼓励）；社区共识：issue 号 / 人名 / 日期属版本管理职责，不进源码注释。
