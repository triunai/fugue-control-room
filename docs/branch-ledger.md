# Branch ledger

Lane branches are named `nth-<topic>`: "nth" means nice-to-have, work beyond the brief's minimum, built in its own worktree and deleted after it merges.

One row per live branch or worktree, added BEFORE `git worktree add` / `git switch -c`, DELETED when the branch merges or is abandoned. Not history (that is `CHANGELOG.md` and git). If a row is older than a week with no commits, resolve it or park its thread.

| Branch | Worktree path | Base sha | Owner (lane) | W id | Status |
|---|---|---|---|---|---|
| `main` | repo root | - | lead | - | integration branch; the only tree that writes spine files |
| `nth-useredit` | `.worktrees/nth-useredit` | 8e0993b | nth-useredit (relay-dispatched) | W-040 | item 25: edit users (PUT /users/{id}, If-Match), show-password toggle, add/edit gap audit; hard stop 12:50 |
| `nth-hybridcache` | `.worktrees/nth-hybridcache` | 8e0993b | nth-hybridcache (relay-dispatched) | W-041 | item 27: HybridCache with stampede protection on hot reads; hard stop 12:50 |
| `nth-readme2` | `.worktrees/nth-readme2` | 7adb4d9 | nth-readme2 (Sonnet), relay-dispatched | W-040 | item 11: README counts and links from final main, keyword full-text wording, ClaimSearchHitDto comment fix |
