# Fugue — backlog (open items)

Append new items at the end. Settled items leave: record the call in `docs/decisions.md`, then remove the item here. All findings go in this ONE file. Do not start parallel finding files.

Heading form (the only one allowed): `### #NNN — <title>`. NNN is three digits. Only the orchestrator mints numbers, after listing all worktrees. Build lanes use a placeholder such as `#NEW-A1`; the orchestrator rewrites it at merge.
First non-blank body line must be `WS: WS-NN` (comma-separate several) or `WS: (pre-WS)`.
Then: what is wrong or missing · where (path) · size (S/M/L) · what would close it.

Code smells that do not block the work in hand go here, not into the current change.

### #001 — Outbox, delivery and subscription-less event retention
WS: WS-06
`outbox_messages` rows (dispatched or dead) and their `webhook_deliveries` are never pruned, and events with no subscriber still accumulate. Size M. Close with a retention job that deletes dispatched messages older than N days (deliveries cascade) and a setting; the trigger already allows DELETE. Found by the Codex S4 review (LOW), deliberately out of S4 (plan non-goals).

### #010 — Structure refactor after the D-064 lanes (owner, 2026-11-28 02:45; scheduled "after lanes merge, today")
WS: WS-22
Three owner suggestions, planned together and NOT started:
1. One layout inside every vertical slice (`Features/<Feature>/`): `Commands/`, `Queries/`, `Models/` (DTOs, outcomes, errors), `Repos/` (the slice's ports, whatever their current name: `I*Repository`, `I*Store`, `I*Reader`, `I*Admin`, `I*Writer`; today auth ports sit in `Abstractions/IAuthPorts.cs`), `Helpers/` (pure rules such as `ClaimFlow`, `ClaimScope`), and a per-slice DI extension where there is something to register (most wiring is Infrastructure's; Mediator handlers are source-generated). Namespaces follow folders; FitnessTests derive audit names from `Features.<Feature>`, which survives.
2. IOptions for what is still hardcoded. Already options-bound (12 classes, 22 files): CORS, audit, auth, rate limits, dispatcher, publisher, idempotency, Kafka, Meilisearch, password, persistence, webhooks. Still constants: list page sizes (`DefaultLimit`/`MaxLimit` in 8 queries; part of the API contract, so options must keep the validator in step), the webhook replay window (5 min), the dev sink body cap, SLA day counts (pickup 2, decision 14, answer 7, settle 7; mirrored in the web `sla.ts`).
3. Runners and cross-cutting code as their own projects: the four `BackgroundService`s (`OutboxDispatcherService`, `ClaimIndexer`, `AuditIndexer`, `IdempotencyPurgeService`) into a `Fugue.Worker` host (the API stops hosting them; compose gets a `worker` service), and the Mediator pipeline behaviours and shared primitives (`Application/Common`) into a building-blocks project. ArchTests gain the new layer rules.
One mechanical commit per item, each behind the full suite; Size M each.

### #011 — `outbox_messages.correlation_id` has no index (The Score query scans)
WS: WS-22
`GET /audit/trace/{correlationId}` (nth-score, merge dacfb06) reads `Sql/audit/trace.sql`; its outbox leg filters `outbox_messages.correlation_id`, which has no index, so that part scans the table (audit_log's leg is fine). Harmless at demo volume. Size S. Close with a migration adding a btree index on `outbox_messages(correlation_id)` through the migration QA gate and catalog row; not on submission day (migrations are a critical seam).

## Done or retired (kept for history; 2026-11-28 03:30 prune, overnight lead)

- #002 guided product tour: DONE, delivered by nth-onboard (e4e750f, merge 4345366): first-visit welcome and autoplayed landing tour per user.
- #003 console shell closer to the reference design: RETIRED, superseded by the shell work (nth-shell merge ec7bf0c, nth-shellfix, the 11-28 welcome/nav fixes). The reference-only extras (tree line, count badges, theme switch, inbox bell) are not owed for submission.
- #004 role x permission matrix on Access: DONE, delivered by nth-shell (714ebdd, merge ec7bf0c); the check-icon polish is overnight item 3a.
- #005 controllers stack `[FromQuery]`: DONE, merge of rd-files (60ea816).
- #008 Under the hood panel: DONE, delivered by nth-hood (merge 80a1fbe) and nth-hood2 (6bec535, f7c6dcc); per-page coverage gaps are overnight item 11e.
- #006 queue counts in one call: DONE in W-024 (e3885f2, `GET /claims/counts`); the backlog row was stale.
- #007 past info requests list: DONE in W-024 (e3885f2, `GET /claims/{id}/info-requests` and the Q&A list on the claim page); stale row.
- #009 panel leftovers: split by D-069. Paid/settled figure -> W-037 (nth-paid); outside notification CUT; claimant SLA calendar by design; Send question during an API restart -> known limit in hot-state.
