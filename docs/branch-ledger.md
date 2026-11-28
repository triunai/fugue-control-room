# Branch ledger

Lane branches are named `nth-<topic>`: "nth" means nice-to-have, work beyond the brief's minimum, built in its own worktree and deleted after it merges.

One row per live branch or worktree, added BEFORE `git worktree add` / `git switch -c`, DELETED when the branch merges or is abandoned. Not history (that is `CHANGELOG.md` and git). If a row is older than a week with no commits, resolve it or park its thread.

| Branch | Worktree path | Base sha | Owner (lane) | W id | Status |
|---|---|---|---|---|---|
| `main` | repo root | - | lead | - | integration branch; the only tree that writes spine files |
| `nth-refactor` | `.worktrees/nth-refactor` | afc6fe9 | nth-refactor (Opus), relay-dispatched | W-039 | item 12: per-slice folders + DI extensions, EXCLUSIVE on src/ and tests/ |
| `nth-tour` | `.worktrees/nth-tour` | ddd2ded | nth-tour (relay-dispatched) | W-040 | admin tour covers the core claim flow |
| `nth-k6` | `.worktrees/nth-k6` | ddd2ded | nth-k6 (Sonnet), relay-dispatched | W-041 | item 26: k6 load scenarios, percentile report, compare overlay, runbook; no src/ |
| `nth-explained` | `.worktrees/nth-explained` | b55270a | nth-explained (relay-dispatched) | W-040 | item 22 follow-up: "Fugue, explained" nav entry |
