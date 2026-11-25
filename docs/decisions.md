# Fugue — decisions (why it is this way)

Latest first. Add an entry when a call settles. To change a decision, add a new entry that supersedes it. Never delete.

Heading form: `## D-NNN — <title> — <YYYY-MM-DD>`. NNN is three digits. Only the orchestrator mints numbers.
First non-blank body line must be `WS: WS-NN` (comma-separate several) or `WS: (pre-WS)` for decisions older than the rule. Never guess.
Then: Context (one or two lines) · Decision · Why · Consequences · Supersedes (if any).

## D-001 — Adopt the doc spine — 2026-11-11
WS: WS-01
Context: AI sessions lose context between runs; hand-written notes drift.
Decision: keep project memory in split files by lifecycle (now, board, why, open, journal, handoff), joined by a thread key and checked by a script.
Why: each file has one job and every link can be machine-checked.
Consequences: every wrap updates the spine in the order in `hygiene.md`; pushes run `scripts/spine/ledger-check.sh`.
