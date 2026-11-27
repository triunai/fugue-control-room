# Branch ledger

One row per live branch or worktree, added BEFORE `git worktree add` / `git switch -c`, DELETED when the branch merges or is abandoned. Not history (that is `CHANGELOG.md` and git). If a row is older than a week with no commits, resolve it or park its thread.

| Branch | Worktree path | Base sha | Owner (lane) | W id | Status |
|---|---|---|---|---|---|
| `main` | repo root | - | lead | - | integration branch; the only tree that writes spine files |
| `nth-grid` | `.worktrees/nth-grid` | b875f49 | nth-grid (Sonnet) | W-022 | nice-to-haves wave, `docs/ai/briefs/nice-to-haves.md` |
| `nth-claimui` | `.worktrees/nth-claimui` | afc0dae | nth-claimui (Sonnet) | W-022 | claim record banner, next step, team and exposure, landing |
| `nth-kafka` | `.worktrees/nth-kafka` | a9e1610 | nth-kafka (Sonnet) | W-022 | review follow-ups wave |
