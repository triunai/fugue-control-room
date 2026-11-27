# Branch ledger

One row per live branch or worktree, added BEFORE `git worktree add` / `git switch -c`, DELETED when the branch merges or is abandoned. Not history (that is `CHANGELOG.md` and git). If a row is older than a week with no commits, resolve it or park its thread.

| Branch | Worktree path | Base sha | Owner (lane) | W id | Status |
|---|---|---|---|---|---|
| `main` | repo root | - | lead | - | integration branch; the only tree that writes spine files |
| `nth-money` | `.worktrees/nth-money` | dd2a8d9 | nth-money (Sonnet) | D-064 | indicative USD view, bar and pie charts on exposure |
| `nth-uam` | `.worktrees/nth-uam` | dd2a8d9 | nth-uam (Opus) | D-064 | admin creates users with roles, USERS_MANAGE |
| `nth-onboard` | `.worktrees/nth-onboard` | dd2a8d9 | nth-onboard (Sonnet) | D-064 | first-visit welcome, tour autoplay, My profile |
| `nth-prelude` | `.worktrees/nth-prelude` | f5389da | nth-prelude (Sonnet) | D-064 | sign-in Prelude animation and copy |
| `nth-score` | `.worktrees/nth-score` | f5389da | nth-score (Opus) | D-064 | The Score: per-correlation transaction inspector from real rows |
| `nth-readme` | `.worktrees/nth-readme` | f5389da | nth-readme (Sonnet) | D-064 | README rewrite after the BFF layout, from the Fugue, Explained page |
