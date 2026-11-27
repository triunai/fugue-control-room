# Branch ledger

One row per live branch or worktree, added BEFORE `git worktree add` / `git switch -c`, DELETED when the branch merges or is abandoned. Not history (that is `CHANGELOG.md` and git). If a row is older than a week with no commits, resolve it or park its thread.

| Branch | Worktree path | Base sha | Owner (lane) | W id | Status |
|---|---|---|---|---|---|
| `main` | repo root | - | lead | - | integration branch; the only tree that writes spine files |
| `nth-kafka` | `.worktrees/nth-kafka` | a9e1610 | nth-kafka (Sonnet) | W-022 | review follow-ups wave |
| `nth-hood` | `.worktrees/nth-hood` | ac68687 | nth-hood (Sonnet) | W-022 | under-the-hood sidebar (#008) |
