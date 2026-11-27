# Branch ledger

One row per live branch or worktree, added BEFORE `git worktree add` / `git switch -c`, DELETED when the branch merges or is abandoned. Not history (that is `CHANGELOG.md` and git). If a row is older than a week with no commits, resolve it or park its thread.

| Branch | Worktree path | Base sha | Owner (lane) | W id | Status |
|---|---|---|---|---|---|
| `main` | repo root | - | lead | - | integration branch; the only tree that writes spine files |
| `rd-sql` | `.worktrees/rd-sql` | ce403cb | readability-sql (Sonnet) | W-007 | D-054 runtime SQL into .sql files; Infrastructure only |
