# Branch ledger

Lane branches are named `nth-<topic>`: "nth" means nice-to-have, work beyond the brief's minimum, built in its own worktree and deleted after it merges.

One row per live branch or worktree, added BEFORE `git worktree add` / `git switch -c`, DELETED when the branch merges or is abandoned. Not history (that is `CHANGELOG.md` and git). If a row is older than a week with no commits, resolve it or park its thread.

| Branch | Worktree path | Base sha | Owner (lane) | W id | Status |
|---|---|---|---|---|---|
| `main` | repo root | - | lead | - | integration branch; the only tree that writes spine files |
| `nth-forms` | `.worktrees/nth-forms` | dacfb06 | nth-forms (Opus), relay-dispatched | D-064 | 5: console select + date picker, Fill with an example |
| `nth-import` | `.worktrees/nth-import` | dacfb06 | nth-import (Opus), relay-dispatched | D-064 | 11a: intake import module (spreadsheet, inbox, WhatsApp file imports) |
| `nth-searchx` | `.worktrees/nth-searchx` | dacfb06 | nth-searchx (Opus), relay-dispatched | D-064 | 10: Meilisearch hardening |
| `nth-pdf` | `.worktrees/nth-pdf` | dacfb06 | nth-pdf (Opus), relay-dispatched | D-064 | 11b: settlement and decision letters as PDF |
| `nth-paid` | `.worktrees/nth-paid` | f3d7637 | nth-paid (Opus), relay-dispatched | W-037 | 11c: paid/settled figure on Team and exposure |
