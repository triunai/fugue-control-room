# Branch ledger

Lane branches are named `nth-<topic>`: "nth" means nice-to-have, work beyond the brief's minimum, built in its own worktree and deleted after it merges.

One row per live branch or worktree, added BEFORE `git worktree add` / `git switch -c`, DELETED when the branch merges or is abandoned. Not history (that is `CHANGELOG.md` and git). If a row is older than a week with no commits, resolve it or park its thread.

| Branch | Worktree path | Base sha | Owner (lane) | W id | Status |
|---|---|---|---|---|---|
| `main` | repo root | - | lead | - | integration branch; the only tree that writes spine files |
| `nth-refactor` | `.worktrees/nth-refactor` | afc6fe9 | nth-refactor (Opus), relay-dispatched | W-039 | item 12: per-slice folders + DI extensions, EXCLUSIVE on src/ and tests/ |
| `nth-tour` | `.worktrees/nth-tour` | ddd2ded | nth-tour (relay-dispatched) | W-040 | admin tour covers the core claim flow |
| `nth-searchui` | `.worktrees/nth-searchui` | ddd2ded | nth-searchui (relay-dispatched) | W-040 | obvious search bar + first-use explainer (keyword full-text, not hybrid) |
| `nth-importui` | `.worktrees/nth-importui` | ddd2ded | nth-importui (relay-dispatched) | W-040 | Import page redesign: icons, drop zone, sample files |
| `nth-changelog` | `.worktrees/nth-changelog` | ddd2ded | nth-changelog (relay-dispatched) | W-040 | Decisions -> Changelog (/changelog, /decisions redirects), versioning explained |
| `nth-explained` | `.worktrees/nth-explained` | ddd2ded | nth-explained (relay-dispatched) | W-040 | "Fugue, Explained" in docs/explained + web/public/explained + a Help link |
| `nth-overrides` | `.worktrees/nth-overrides` | ddd2ded | nth-overrides (relay-dispatched) | W-040 | explain the override formula on Users & access |
| `nth-scorefix` | `.worktrees/nth-scorefix` | ddd2ded | nth-scorefix (relay-dispatched) | W-040 | The Score groups one request's audit rows by command |
