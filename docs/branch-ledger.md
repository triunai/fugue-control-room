# Branch ledger

Lane branches are named `nth-<topic>`: "nth" means nice-to-have, work beyond the brief's minimum, built in its own worktree and deleted after it merges.

One row per live branch or worktree, added BEFORE `git worktree add` / `git switch -c`, DELETED when the branch merges or is abandoned. Not history (that is `CHANGELOG.md` and git). If a row is older than a week with no commits, resolve it or park its thread.

| Branch | Worktree path | Base sha | Owner (lane) | W id | Status |
|---|---|---|---|---|---|
| `main` | repo root | - | lead | - | integration branch; the only tree that writes spine files |
| `nth-uam` | `.worktrees/nth-uam` | dd2a8d9 | nth-uam (Opus) | D-064 | admin creates users with roles, USERS_MANAGE |
| `nth-score` | `.worktrees/nth-score` | f5389da | nth-score (Opus) | D-064 | The Score: per-correlation transaction inspector from real rows |
| `nth-claimpage` | `.worktrees/nth-claimpage` | f566bb0 | nth-claimpage (Sonnet) | D-064 | overnight items 6+7: workbook feel, Save to Excel, Next step copy, track explainers |
| `nth-stack` | `.worktrees/nth-stack` | f566bb0 | nth-stack (Sonnet) | D-064 | overnight items 9+8: Stack modal from the env pill, SLA calendar styling |
