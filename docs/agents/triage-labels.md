# Triage Labels

The skills speak in terms of five canonical triage roles. **This repo uses the catalog defaults verbatim** — there is no override mapping, so a role's label string is always the role's own name:

| Role (= label string) | Meaning |
| --------------------- | ------- |
| `needs-triage`        | Maintainer needs to evaluate this issue  |
| `needs-info`          | Waiting on reporter for more information |
| `ready-for-agent`     | Fully specified, ready for an AFK agent  |
| `ready-for-human`     | Requires human implementation            |
| `wontfix`             | Will not be actioned                     |

When a skill mentions a role (e.g. "apply the AFK-ready triage label"), apply the label of the same name. If this repo ever renames a label, add a two-column mapping table here instead of the single-column one above.
