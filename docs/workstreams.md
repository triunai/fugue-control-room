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

### WS-20 — Claims platform: the timed build window — 🟢 ACTIVE (2026-11-27)
ID: W-020
Status: window opened at tag `claims-window-start` (2026-11-27 ~17:00 +08, hard stop 22:00 +08). Plan: `docs/plans/claims-window.md`.
Resume: backend core lane, then the frontend lane on the regenerated client, Codex review on the state machine seam, README + demo script.
Refs: `docs/plans/claims-window.md`, D-052, D-053

### WS-07 — S5 REST hardening: idempotency, versioning, rate limits, cache, telemetry — 🟢 ACTIVE (2026-11-27)
ID: W-007
Status: 2026-11-27 headless run: Docker image + one-command compose lane dispatched; idempotency planned (D-050, ADR-013, `docs/plans/w007-idempotency.md`), Codex plan review running. Versioning/HybridCache/OpenTelemetry only if time.
Resume: land the compose lane (re-verify `docker compose up`), then build idempotency slice by slice per the plan.
Refs: `docs/plans/roadmap.md`, `docs/plans/w007-idempotency.md`, D-049, D-050

### WS-10 — Verify the Codex gate end to end — ⏸ PARKED (2026-11-25)
ID: W-010
Status: `scripts/codex-gate.mjs` invocation (`codex exec review --base <sha> ... -`) taken from `--help`, never run against a real seam push.
Resume: make a throwaway seam change on a branch, run `node scripts/codex-gate.mjs --dry-run --base=<ref>`, then once for real; confirm `docs/reviews/codex/` receives text and the failure path records "failed". Also confirm lefthook gives git's stdin to both `spine-nudge` and `codex-gate` on a real push.
Refs: D-014, ADR-007

### WS-18 — Import the owner's earlier audit-logging design and harden audit — 🟢 ACTIVE (2026-11-27)
ID: W-018
Status: source cloned read-only at `~/Repos/career-work/<source-repo>/ (exact path: in the owner's seed prompt)` (outside this repo; never name it in Fugue). Copying allowed (D-048), names scrubbed on import. Compare its audit implementation with W-012's `audit_log` and take what is stronger (knobs, PII config, payload processing, retention, query shapes); keep same-transaction, append-only and never-sampled guarantees.
Resume: Haiku sweep of the source's audit code first (paths + what it does), then a Sonnet port with tests, Codex review on the diff.
Refs: D-048, ADR-010

### WS-19 — Users list endpoint for the Access screen — ⏸ PARKED (2026-11-27)
ID: W-019
Status: the console's Users & access screen has no list; the operator pastes a user id or picks from audit actors.
Resume: `GET /api/v1/users` (keyset, search, `USERS_READ`), refresh the OpenAPI snapshot, regenerate the console client, add the list to the Access screen.
Refs: ADR-008, ADR-012
