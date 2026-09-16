# Domain Docs

How the engineering skills should consume this repo's domain documentation when exploring the codebase.

This is a **single-context repo**: one `CONTEXT.md` plus `docs/adr/` at the repo root. There is no `CONTEXT-MAP.md` and no context-scoped ADR directories — don't look for them.

## Before exploring, read these

- **`CONTEXT.md`** at the repo root — the domain glossary.
- **`docs/adr/`** — the ADRs touching the area you're about to work in. The index with per-ADR status lives in the appendix of `docs/architecture.md`.
- **`docs/architecture.md`** — when the task is architectural, this is the routing entry to the normative leaves.

If any of these files don't exist, **proceed silently**. Don't flag their absence; don't suggest creating them upfront. The `/domain-modeling` skill (reached via `/grill-with-docs` and `/improve-codebase-architecture`) creates them lazily when terms or decisions actually get resolved.

## Use the glossary's vocabulary

When your output names a domain concept (in an issue title, a refactor proposal, a hypothesis, a test name), use the term as defined in `CONTEXT.md`. Don't drift to synonyms the glossary explicitly avoids.

If the concept you need isn't in the glossary yet, that's a signal — either you're inventing language the project doesn't use (reconsider) or there's a real gap (note it for `/domain-modeling`).

## Flag ADR conflicts

If your output contradicts an existing ADR, surface it explicitly rather than silently overriding:

> _与 ADR-00NN（该决策的一句话）冲突 —— 但值得重开，因为……_
