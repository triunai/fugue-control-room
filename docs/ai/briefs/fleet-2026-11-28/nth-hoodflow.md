# Lane nth-hoodflow (item 11e, final part) — Sonnet — worktree .worktrees/nth-hoodflow, branch nth-hoodflow, base c191a05
Ports: web 4411. Read HEADER.md (incl. Addenda 1 and 2) and OWNERS.md (Addendum 3) first.

Objective: every claims-area page's "Under the hood" note gets a "What happens" flow to the same standard as the Audit log
note, so all pages of the console explain end to end what really happens.

Current state (verify): `HoodNote.flow?: readonly HoodStep[]` exists in `web/src/app/features/hood/hood-note.ts`
(`HoodStep { layer: 'web'|'api'|'handler'|'db'|'audit'|'outbox'|'webhook'|'search'|'job'; text (<=160 chars); path? }`),
rendered as "What happens". The standard to copy: `web/src/app/features/audit/audit-page.hood.ts` (9 steps) and the
modules/webhooks/customers/profile notes. `web/src/app/features/hood/hood-notes.spec.ts` checks flows only for routes in its
`withFlow` list (layer valid, text <= 160, path exists in the repo). Claims-area notes have NO flow yet: home, claims queue,
claim detail, my claims, report, team, reserve sheet, SLA calendar, assessment.

Desired state: each of those 9 notes gains a `flow` of 4-9 steps tracing the page's MAIN action through the real code:
- report: submit → idempotency key (ADR-013) → validator → handler → claim insert + audit + outbox in one transaction
  (ADR-011) → event → webhook / search indexer.
- claim detail: a command (e.g. set reserve) → If-Match/ETag check (ADR-006 xmin, D-056) → state machine → audit + outbox.
- queue / my claims: list → scope (claimant sees own; D-063) → EF keyset read → search (Meilisearch) where used.
- team: workload/exposure report queries; reserve sheet and calendar: what reads and what (if anything) writes; assessment:
  browser-only worksheet → "Use as reserve" → the set-reserve command.
Every step's `path` is a real file that does that step (open it to confirm); text is one plain line. Add each route to the
`withFlow` list in hood-notes.spec.ts (that list only — nothing else in features/hood/**).

Acceptance: hood-notes spec passes with all 9 routes in `withFlow`; prove the spec bites (inject one non-existent path → red,
restore); headless as staff (and self for my claims/report) on :8080 with the sidebar open on each page: "What happens" renders,
screenshot each, 0 console errors (pace for the rate limiter); web gates green.
Non-goals: no other note fields rewritten (only add flow; fix an existing line only if it is false — report it), no sidebar
code, no backend, no other pages.
Commits: 1 report + claim detail + assessment flows, 2 queue/my claims/team/sheet/calendar/home flows; status file + own ledger
row each commit (your base has the ledger rows; add yours if missing).

Recite: nine claims-area flows, every path real, spec bites, nothing invented, same standard as the Audit log.
