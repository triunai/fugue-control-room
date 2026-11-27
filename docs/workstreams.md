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
Status: window open since tag `claims-window-start` (16:56 +08, hard stop 21:56 +08). Backend lane in `.worktrees/claims` (branch `claims-core`): domain, schema, contract and an ETag fix committed; rebase onto main and slices 4-7 (handling tests, workload/exposure, seed + runbook 11, Bruno) remain; status file `docs/ai/lanes/claims-core-status.md` in that worktree. Angular lane not started (brief `docs/ai/briefs/claims-web.md`).
Resume: follow the SEED PROMPT in `docs/ai/handoffs/2026-11-27-lead.md` (evening section).
Refs: `docs/plans/claims-window.md`, D-052, D-053

### WS-07 — S5 REST hardening: idempotency, versioning, rate limits, cache, telemetry — 🟢 ACTIVE (2026-11-27)
ID: W-007
Status: idempotency store done (ADR-013, two Codex reviews), Docker one-command stack + Bruno compose env + CI workflow + reviewer runbook done, console modules/shell done. Versioning + Sunset, HybridCache, OpenTelemetry not built. Readability pass (D-054 SQL files + read-only Postgres MCP, D-055 one file per concept, backlog #005) runs after the claims merge.
Resume: after W-020 merges: the readability pass, then close W-007 with versioning/cache/OTel as "more time" follow-ups.
Refs: `docs/plans/roadmap.md`, `docs/plans/w007-idempotency.md`, D-049, D-050

### WS-22 — Post-core quality wave and nice-to-haves (lands together after the claims build) — ⏸ PARKED (2026-11-27)
ID: W-022
Status: owner asked 2026-11-27; starts only after W-020's core is merged. Items: (1) explain the AI harness: the 60% context cap (wrap state ready before compaction), the lefthook-based gates that run tests automatically, the orchestration layer used for agents; (2) mutation testing with Stryker.NET on a few worth-it targets (Claim state machine, AuditRedactor, the Idempotency-Key header parser); (3) PICT pairwise tests where the input space is combinatorial (role x module state x claim status x action); (4) k6 load scripts (claims queue read, submit with Idempotency-Key, pickup race) with thresholds; (5) "Nice to haves" docs section: Univer spreadsheet (bulk reserve review / claims import) and a calendar (SLA due dates, assessor appointments), with the owner's own note on trimming them for memory-capped deploys (owner to supply the facts), driver.js tour (#002), shell (#003), roles matrix (#004).
Resume: after the claims merge and the readability pass; one lane per item, docs last.
Refs: backlog #002-#005, D-054, D-055

### WS-10 — Verify the Codex gate end to end — ⏸ PARKED (2026-11-25)
ID: W-010
Status: `scripts/codex-gate.mjs` invocation (`codex exec review --base <sha> ... -`) taken from `--help`, never run against a real seam push.
Resume: make a throwaway seam change on a branch, run `node scripts/codex-gate.mjs --dry-run --base=<ref>`, then once for real; confirm `docs/reviews/codex/` receives text and the failure path records "failed". Also confirm lefthook gives git's stdin to both `spine-nudge` and `codex-gate` on a real push.
Refs: D-014, ADR-007

### WS-21 — Audit full-text search — ⏸ PARKED (2026-11-27)
ID: W-021
Status: follow-up of W-018. Postgres `tsvector` over action, entity, actor display and error code with a GIN index; `q=` on `GET /api/v1/audit`; a search box in the console. Meilisearch rejected (D-051).
Resume: after the claims window.
Refs: D-051

### WS-19 — Users list endpoint for the Access screen — ⏸ PARKED (2026-11-27)
ID: W-019
Status: the console's Users & access screen has no list; the operator pastes a user id or picks from audit actors.
Resume: `GET /api/v1/users` (keyset, search, `USERS_READ`), refresh the OpenAPI snapshot, regenerate the console client, add the list to the Access screen.
Refs: ADR-008, ADR-012
