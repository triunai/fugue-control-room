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
Status: plan written (`docs/plans/s3-identity.md`), awaiting cross-family review; no code yet.
Resume: get the plan reviewed (`dual-gate-review`), fold findings in, then build slice 1.
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

### WS-12 — Rich append-only audit log — ⏸ PARKED (2026-11-25)
ID: W-012
Status: owner asked for a rich `audit_log` (field-level before/after JSONB, actor, action, outcome, correlation/saga/trace ids, HTTP context, PII masking via AuditOptions, never sampled, same transaction as the change, append-only trigger, query endpoint behind `AUDIT_READ`). Runs after S3 so migrations don't collide.
Resume: plan after W-005 closes; build on S3's IUnitOfWork + grant/access audit seam.
Refs: ADR-006

### WS-11 — S3 review follow-ups (create email oracle, login limiter DoS, audit hardening) — ⏸ PARKED (2026-11-25)
ID: W-011
Status: not started. MED/LOW findings of the S3 build review that were deferred, not fixed: create-path email oracle (per-owner uniqueness or generic 409), per-email limiter charging successes, group/override self-target and 23505 races, access_audit reason_code truncation and HMAC subkey, module PATCH audit, module config parsing and SYSTEM config-disable, tenant is_active, anonymous audit retention.
Resume: read `docs/reviews/s3/rv-s3-build.md`, decide the email-uniqueness rule (owner call), then fix MEDs first with pins.
Refs: `docs/reviews/s3/rv-s3-build.md`, ADR-008

