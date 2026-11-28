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

### WS-25 — Admin creates users (overnight item 3) — 🟢 ACTIVE (2026-11-28)
ID: W-025
Status: merged e182dbb (nth-uam); closes at the wrap.
Resume: integrator merges and verifies; close with `scripts/spine/close-workstream.mjs` at the wrap (item 16).
Refs: D-064; `docs/plans/overnight-2026-11-28.md`; contracts in `docs/ai/briefs/fleet-2026-11-28/`

### WS-26 — The Score, one correlation id as a timeline (item 4) — 🟢 ACTIVE (2026-11-28)
ID: W-026
Status: merged dacfb06 (nth-score); closes at the wrap.
Resume: integrator merges and verifies; close with `scripts/spine/close-workstream.mjs` at the wrap (item 16).
Refs: D-064; `docs/plans/overnight-2026-11-28.md`; contracts in `docs/ai/briefs/fleet-2026-11-28/`

### WS-27 — Claim page workbook, Save to Excel, next-step copy, track explainers (items 6, 7) — 🟢 ACTIVE (2026-11-28)
ID: W-027
Status: merged 0439df5 (nth-claimpage); closes at the wrap.
Resume: integrator merges and verifies; close with `scripts/spine/close-workstream.mjs` at the wrap (item 16).
Refs: D-064; `docs/plans/overnight-2026-11-28.md`; contracts in `docs/ai/briefs/fleet-2026-11-28/`

### WS-28 — Stack dialog and the SLA calendar styling (items 8, 9) — 🟢 ACTIVE (2026-11-28)
ID: W-028
Status: merged 00e5c76 (nth-stack); closes at the wrap.
Resume: integrator merges and verifies; close with `scripts/spine/close-workstream.mjs` at the wrap (item 16).
Refs: D-064; `docs/plans/overnight-2026-11-28.md`; contracts in `docs/ai/briefs/fleet-2026-11-28/`

### WS-29 — Agent tooling: read-only role in the stack, pinned MCP, runbook (item 17a) — 🟢 ACTIVE (2026-11-28)
ID: W-029
Status: merged 66604db (nth-tooling); verify on the recreated stack, then close.
Resume: integrator merges and verifies; close with `scripts/spine/close-workstream.mjs` at the wrap (item 16).
Refs: D-064, D-066, D-068; `docs/plans/overnight-2026-11-28.md`; contracts in `docs/ai/briefs/fleet-2026-11-28/`

### WS-30 — Console forms: select, date picker, Fill with an example (item 5) — 🟢 ACTIVE (2026-11-28)
ID: W-030
Status: lane nth-forms live.
Resume: integrator merges and verifies; close with `scripts/spine/close-workstream.mjs` at the wrap (item 16).
Refs: D-064, D-066; `docs/plans/overnight-2026-11-28.md`; contracts in `docs/ai/briefs/fleet-2026-11-28/`

### WS-31 — Intake import: spreadsheet, inbox and WhatsApp file imports to drafts (item 11a) — 🟢 ACTIVE (2026-11-28)
ID: W-031
Status: merged f967355 (nth-import), D-074, migration 13; closes at the wrap.
Resume: integrator merges and verifies; close with `scripts/spine/close-workstream.mjs` at the wrap (item 16).
Refs: D-064, D-066; `docs/plans/overnight-2026-11-28.md`; contracts in `docs/ai/briefs/fleet-2026-11-28/`

### WS-32 — Search hardening on Meilisearch (item 10) — 🟢 ACTIVE (2026-11-28)
ID: W-032
Status: merged aa401bc (nth-searchx), D-072, D-073; closes at the wrap.
Resume: integrator merges and verifies; close with `scripts/spine/close-workstream.mjs` at the wrap (item 16).
Refs: D-063, D-064, D-066; `docs/plans/overnight-2026-11-28.md`; contracts in `docs/ai/briefs/fleet-2026-11-28/`

### WS-33 — Demo fixes: matrix checks, track line, claimant wording, /modules glitches (items 3a, 7a, 7b, 11f) — 🟢 ACTIVE (2026-11-28)
ID: W-033
Status: lane nth-fixes live.
Resume: integrator merges and verifies; close with `scripts/spine/close-workstream.mjs` at the wrap (item 16).
Refs: D-064, D-066; `docs/plans/overnight-2026-11-28.md`; contracts in `docs/ai/briefs/fleet-2026-11-28/`

### WS-34 — Settlement and decision letters as PDF (item 11b) — 🟢 ACTIVE (2026-11-28)
ID: W-034
Status: merged 59e1ad6 (nth-pdf), D-070; closes at the wrap.
Resume: integrator merges and verifies; close with `scripts/spine/close-workstream.mjs` at the wrap (item 16).
Refs: D-064, D-066; `docs/plans/overnight-2026-11-28.md`; contracts in `docs/ai/briefs/fleet-2026-11-28/`

### WS-35 — Under-the-hood coverage on every page (item 11e) — 🟢 ACTIVE (2026-11-28)
ID: W-035
Status: lanes nth-hooda then nth-hoodb live.
Resume: integrator merges and verifies; close with `scripts/spine/close-workstream.mjs` at the wrap (item 16).
Refs: D-064, D-066; `docs/plans/overnight-2026-11-28.md`; contracts in `docs/ai/briefs/fleet-2026-11-28/`

### WS-36 — Decision timeline generated from the docs (item 11d) — 🟢 ACTIVE (2026-11-28)
ID: W-036
Status: merged 2e0ecbf (nth-decisions); closes at the wrap.
Resume: integrator merges and verifies; close with `scripts/spine/close-workstream.mjs` at the wrap (item 16).
Refs: D-066, D-067; `docs/plans/overnight-2026-11-28.md`; contracts in `docs/ai/briefs/fleet-2026-11-28/`

### WS-37 — Paid and settled figure on Team and exposure (item 11c, backlog #009) — 🟢 ACTIVE (2026-11-28)
ID: W-037
Status: merged bceb4e5 (nth-paid); closes at the wrap.
Resume: integrator merges and verifies; close with `scripts/spine/close-workstream.mjs` at the wrap (item 16).
Refs: D-064, D-066, D-069; `docs/plans/overnight-2026-11-28.md`; contracts in `docs/ai/briefs/fleet-2026-11-28/`

### WS-38 — Submission-morning polish lanes before the overnight list (money, prelude, onboard, readme, claimsearch, shellfix) — 🟢 ACTIVE (2026-11-28)
ID: W-038
Status: merged before 03:09; closes at the wrap.
Resume: integrator merges and verifies; close with `scripts/spine/close-workstream.mjs` at the wrap (item 16).
Refs: D-063, D-064; `docs/plans/overnight-2026-11-28.md`; contracts in `docs/ai/briefs/fleet-2026-11-28/`

### WS-39 — Structure refactor after the D-064 lanes: slice folders, IOptions, Worker host (backlog #010, items 12-14) — 🟢 ACTIVE (2026-11-28)
ID: W-039
Status: item 12 lane nth-refactor live from afc6fe9 (exclusive on src/ and tests/); 13 nth-options after it; 14 gated at 10:00.
Resume: integrator merges and verifies (OpenAPI byte-identical, full-suite count equal to base 849 + 37, moves and usings only); close at the wrap.
Refs: D-064, D-066; backlog #010; `docs/plans/overnight-2026-11-28.md` P2; contracts in `docs/ai/briefs/fleet-2026-11-28/`

### WS-40 — Owner asks at 11:26: tour, search bar, import UI, changelog, explained, overrides, The Score grouping, user edit — 🟢 ACTIVE (2026-11-28)
ID: W-040
Status: seven web-only lanes from ddd2ded (nth-tour, nth-searchui, nth-importui, nth-changelog, nth-explained, nth-overrides, nth-scorefix), READY by 12:30; nth-useredit (src/) queued after nth-refactor.
Resume: integrator merges as READY arrives (refactor, then useredit, then the web lanes); feature cut-off 13:00; close at the wrap.
Refs: D-064, D-066; `docs/plans/overnight-2026-11-28.md`; contracts in `docs/ai/briefs/fleet-2026-11-28/` (OWNERS Addendum 10)

### WS-41 — Load testing and caching: k6 scenarios, HybridCache (owner ask 11:30) — 🟢 ACTIVE (2026-11-28)
ID: W-041
Status: lane nth-k6 live (item 26, no src/); nth-hybridcache pending dispatch after the refactor (item 27, hard stop 12:50).
Resume: integrator merges on READY; close or defer at the wrap.
Refs: D-064, D-066; `docs/plans/overnight-2026-11-28.md` items 26-27; contracts in `docs/ai/briefs/fleet-2026-11-28/` (OWNERS Addendum 11)

### WS-23 — API versioning with Sunset, HybridCache, OpenTelemetry — ⏸ PARKED (2026-11-27)
ID: W-023
Status: follow-up of W-007 (closed). Not built: a v2 example with `Deprecation`/`Sunset` on v1, HybridCache on the `/me` permission lookup, OpenTelemetry traces.
Resume: after W-022; one lane each, snapshot refresh after any contract change.
Refs: `docs/plans/roadmap.md`

### WS-10 — Verify the Codex gate end to end — ⏸ PARKED (2026-11-25)
ID: W-010
Status: `scripts/codex-gate.mjs` invocation (`codex exec review --base <sha> ... -`) taken from `--help`, never run against a real seam push.
Resume: make a throwaway seam change on a branch, run `node scripts/codex-gate.mjs --dry-run --base=<ref>`, then once for real; confirm `docs/reviews/codex/` receives text and the failure path records "failed". Also confirm lefthook gives git's stdin to both `spine-nudge` and `codex-gate` on a real push.
Refs: D-014, ADR-007
