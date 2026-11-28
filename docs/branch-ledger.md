# Branch ledger

Lane branches are named `nth-<topic>`: "nth" means nice-to-have, work beyond the brief's minimum, built in its own worktree and deleted after it merges.

One row per live branch or worktree, added BEFORE `git worktree add` / `git switch -c`, DELETED when the branch merges or is abandoned. Not history (that is `CHANGELOG.md` and git). If a row is older than a week with no commits, resolve it or park its thread.

| Branch | Worktree path | Base sha | Owner (lane) | W id | Status |
|---|---|---|---|---|---|
| `main` | repo root | - | lead | - | integration branch; the only tree that writes spine files |
| `nth-helpguide` | `.worktrees/nth-helpguide` | 3a35d90 | nth-helpguide (Sonnet), relay-dispatched | W-040 | owner asks 13:48: Help as a guide by role and module; Kafka labelled an optional adapter, off; chip reads dev · .NET 8 · API v1; hard stop 14:30 |
