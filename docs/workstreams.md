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

### WS-05 — S3 identity: JWT bearer, role policies, ownership handler — ⏸ PARKED (2026-11-25)
ID: W-005
Status: not started. Plan first (critical seam: auth), see `docs/plans/roadmap.md` S3.
Resume: write `docs/plans/s3-identity.md`, get the plan reviewed, then build. Open it with "new task: …".
Refs: `docs/plans/roadmap.md`, ADR-003

### WS-06 — S4 transactional outbox and signed webhooks — ⏸ PARKED (2026-11-25)
ID: W-006
Status: not started; needs S3. The outbox writes through `IUnitOfWork` (ADR-006).
Resume: plan after S3 lands.
Refs: `docs/plans/roadmap.md`, ADR-005, ADR-006

### WS-07 — S5 REST hardening: idempotency, versioning, rate limits, cache, telemetry — ⏸ PARKED (2026-11-25)
ID: W-007
Status: not started; needs S4.
Resume: plan after S4 lands.
Refs: `docs/plans/roadmap.md`

### WS-08 — H1 port the AI engineering harness — 🟢 ACTIVE (2026-11-25)
ID: W-008
Status: gates, guard, skills, fitness functions, doc lifecycle scripts and narrative written; verifying and committing.
Resume: run the verify list, commit in slices, then close with `scripts/spine/close-workstream.mjs W-008`.
Refs: D-012, D-013, D-014, D-015, ADR-007, `docs/ai/how-we-use-ai.md`

### WS-09 — MapStatus: ErrorType to HTTP status (owner's hand-written decision) — ⛔ BLOCKED (2026-11-25)
ID: W-009
Status: blocked on the owner. Agents never implement it (D-009). Known-red tests are listed in `docs/hot-state.md`.
Resume: owner implements `ApiControllerBase.MapStatus`, then re-run `dotnet test` and `npm run api:test`.
Refs: D-009, ADR-004
