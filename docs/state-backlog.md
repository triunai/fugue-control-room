# Fugue — backlog (open items)

Append new items at the end. Settled items leave: record the call in `docs/decisions.md`, then remove the item here. All findings go in this ONE file. Do not start parallel finding files.

Heading form (the only one allowed): `### #NNN — <title>`. NNN is three digits. Only the orchestrator mints numbers, after listing all worktrees. Build lanes use a placeholder such as `#NEW-A1`; the orchestrator rewrites it at merge.
First non-blank body line must be `WS: WS-NN` (comma-separate several) or `WS: (pre-WS)`.
Then: what is wrong or missing · where (path) · size (S/M/L) · what would close it.

Code smells that do not block the work in hand go here, not into the current change.

### #001 — Outbox, delivery and subscription-less event retention
WS: WS-06
`outbox_messages` rows (dispatched or dead) and their `webhook_deliveries` are never pruned, and events with no subscriber still accumulate. Size M. Close with a retention job that deletes dispatched messages older than N days (deliveries cascade) and a setting; the trigger already allows DELETE. Found by the Codex S4 review (LOW), deliberately out of S4 (plan non-goals).
