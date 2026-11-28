# Branch ledger

Lane branches are named `nth-<topic>`: "nth" means nice-to-have, work beyond the brief's minimum, built in its own worktree and deleted after it merges.

One row per live branch or worktree, added BEFORE `git worktree add` / `git switch -c`, DELETED when the branch merges or is abandoned. Not history (that is `CHANGELOG.md` and git). If a row is older than a week with no commits, resolve it or park its thread.

| Branch | Worktree path | Base sha | Owner (lane) | W id | Status |
|---|---|---|---|---|---|
| `main` | repo root | - | lead | - | integration branch; the only tree that writes spine files |
| `nth-notifydoc` | `.worktrees/nth-notifydoc` | 0fd072f | nth-notifydoc (Sonnet), relay-dispatched | W-040 | owner ask: docs/design/notifications.md (n8n MVP on signed webhooks, then a notifications worker: WhatsApp Cloud API, Resend) + a link from the explained row |
