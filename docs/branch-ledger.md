# Branch ledger

One row per live branch or worktree, added BEFORE `git worktree add` / `git switch -c`, DELETED when the branch merges or is abandoned. Not history (that is `CHANGELOG.md` and git). If a row is older than a week with no commits, resolve it or park its thread.

| Branch | Worktree path | Base sha | Owner (lane) | W id | Status |
|---|---|---|---|---|---|
| `main` | repo root | - | lead | - | integration branch; the only tree that writes spine files |
| `nth-shellfix` | `.worktrees/nth-shellfix` | 3f526b0 | nth-shellfix (Sonnet) | D-062 | doubled page padding, short-viewport nav, market flags |
| `nth-claimsearch` | `.worktrees/nth-claimsearch` | 3f526b0 | nth-claimsearch (Sonnet) | D-063 | Meilisearch claims search, scope enforced server-side |
