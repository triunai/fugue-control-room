# Fugue — workstreams (the board)

One block per thread. Add the block at task START. Update at wrap. Prune or mark done when finished.

Heading form (exactly three `#`, one space, em dash with spaces, date in parentheses): `### WS-NN — <title> — <STATUS> (<YYYY-MM-DD>)`. WS numbers use two digits minimum (WS-01). A heading at any other level hides the thread from the link checker.
Commit id: each thread also carries an `ID: W-NNN` line (W-00N is the id of WS-0N). `W-NNN` is what a commit's `Refs:` trailer cites; `scripts/check-refs-trailer.mjs` resolves it against this file. Beads are a local queue and are never referenced from a commit.
Body lines: `ID:` commit id · `Status:` where it stands · `Resume:` where you stopped and the literal next action · `Refs:` files, decisions, log dates.
A heading containing the done glyph counts as done. A done thread must be claimed by at least one `docs/project-log.md` entry (`WS: WS-NN`).

Lifecycle: this board holds LIVE threads only. When a thread closes, append a `Closed: W-NNN (WS-NN) <title>` entry to `docs/project-log.md` (date, outcome, commits, D-NNN produced, follow-ups) and DELETE the block here (`hygiene.md`, Doc lifecycle). `node scripts/spine/prune-check.mjs` warns about a done thread left on the board.

Glyph legend:
- `🟢 ACTIVE` — being worked on now
- `⏸ PARKED` — frozen on purpose; `Resume:` says how to restart
- `⛔ BLOCKED` — waiting on something outside the thread; `Status:` names it
- `✅ DONE` — finished and verified

### WS-06 — S4 transactional outbox and signed webhooks — 🟢 ACTIVE (2026-11-26)
ID: W-006
Status: building; plan in `docs/plans/s4-outbox-webhooks.md`, decisions D-037 to D-040 (ADR-011). Schema and migration first, then capture, subscriptions, dispatcher.
Resume: `git log --grep "Refs: W-006"`; the plan's "Commits" list is the order.
Refs: `docs/plans/s4-outbox-webhooks.md`, `docs/plans/roadmap.md`, ADR-005, ADR-006, ADR-010, ADR-011

### WS-15 — Codex W-013 findings: no-op override PUT skips the xmin check, email ceiling charged late — 🟢 ACTIVE (2026-11-26)
ID: W-015
Status: two MED findings from `docs/reviews/codex/2026-11-26-w013-followups.md`; each gets a regression test shown RED before the fix.
Resume: `git log --grep "Refs: W-015"`; fix `OverrideAdmin.cs` (force a version-guarded UPDATE on a no-op) and `AuthRateLimiting.cs` (reserve email capacity before verification, refund on success).
Refs: D-035, D-036, `docs/reviews/codex/2026-11-26-w013-followups.md`

### WS-07 — S5 REST hardening: idempotency, versioning, rate limits, cache, telemetry — ⏸ PARKED (2026-11-25)
ID: W-007
Status: not started; needs S4.
Resume: plan after S4 lands.
Refs: `docs/plans/roadmap.md`

### WS-10 — Verify the Codex gate end to end — ⏸ PARKED (2026-11-25)
ID: W-010
Status: `scripts/codex-gate.mjs` invocation (`codex exec review --base <sha> ... -`) taken from `--help`, never run against a real seam push.
Resume: make a throwaway seam change on a branch, run `node scripts/codex-gate.mjs --dry-run --base=<ref>`, then once for real; confirm `docs/reviews/codex/` receives text and the failure path records "failed". Also confirm lefthook gives git's stdin to both `spine-nudge` and `codex-gate` on a real push.
Refs: D-014, ADR-007

### WS-11 — Project README — ▶ ACTIVE (2026-11-25)
ID: W-011
Status: written by a research agent, stale status lines corrected by the lead; committing now.
Resume: refresh Status/Highlights whenever a batch lands.
Refs: `README.md`

### WS-17 — Fugue Console: Angular admin console for the API (web/) — 🟢 ACTIVE (2026-11-26)
ID: W-017
Status: building in `web/` (Angular, SignalStore for client state, TanStack Query for server state, Tailwind, Vellum tokens). Touches only `web/**`, `docs/runbooks/09-console.md`, web lines in `.gitignore` and this block. Needs W-016 (`docs/api/openapi.v1.json`, `GET /api/v1/me`, CORS).
Resume: `git log --grep "Refs: W-017"`; screens in priority order: sign-in, shell, customers, audit, modules, webhooks (dev sink), then users and profile.
Refs: `web/README.md`, `docs/runbooks/09-console.md`, ADR-008, ADR-009, ADR-010, ADR-011
