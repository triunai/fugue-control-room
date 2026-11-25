# Fugue — hot state (the resume point)

OVERWRITTEN at every wrap or pause, never appended; at most 40 lines. After a wrap or days away: read this first, then do "Next action" exactly. History lives in `docs/project-log.md` and `CHANGELOG.md`.

## ▶ RESUME HERE — 2026-11-25
Branch: `main` (no other worktrees).
Last green sha: ad58e29
(build 0 warnings; arch tests 17 green; Fugue.Tests 48 green 16 red-by-design; only docs follow it.)

## Last action done
H1 harness (W-008) finished and committed in slices; S0 to S2 closed into the project log and CHANGELOG.

## Next action (exact)
1. `git status --short --branch`; `git config core.hooksPath` must print nothing.
2. Open S3 (W-005): write `docs/plans/s3-identity.md`, get the plan reviewed (`dual-gate-review`), then build.
3. Seed prompt below.

## Uncommitted work in progress
None expected after the final H1 commit. Local-only and ignored: `.beads/` (local queue), `.fugue-local/` (codex gate ledger), `.env`.

## In flight (open W ids)
W-005 S3 identity, W-006 S4 outbox and webhooks, W-007 S5 REST hardening (all parked, not started); W-009 MapStatus (blocked on owner).

## Pending owner questions
- Implement `ApiControllerBase.MapStatus` (D-009, W-009).
- Review the "S1/S2 implementation choices" entries in `docs/ai-journal.md`.

## Tests (verified 2026-11-25)
`Fugue.ArchTests` 17 green (9 are fitness functions, baselines empty). `Fugue.Tests` 48 green, 16 red, all MapStatus-dependent: the 8 `MapStatusTests` cases, `Failure_is_a_problem_json_with_code_type_and_trace_id`, `Validation_failure_carries_an_errors_dictionary`, `Ping_with_an_out_of_range_count_is_a_400_validation_problem`, and 5 in `CustomersApiTests` (stale 412, missing If-Match 428, suspend twice 409, duplicate email 409, unknown id 404). Bruno: requests 06, 07, 09, 13. `npm run test:scripts` green.

## Owed inbox
- (empty)

## Seed prompt (copy-paste)
> Hydrate Fugue: read `docs/hot-state.md`, `hygiene.md`, `docs/ai/handoffs/2026-11-25-lead.md`, the S3 section of `docs/plans/roadmap.md` and ADR-003. Run `git status --short --branch` and `git config core.hooksPath` (empty). Open S3 identity as W-005: write `docs/plans/s3-identity.md` first (JWT bearer, a development-only token issuer, two role policies, fallback policy = authenticated, a resource-ownership handler returning 404 for cross-scope misses). Auth is a critical seam: independent cross-family plan review before code, `dual-gate-review` after. Use `agent-task-contract` for dispatch. Never implement `MapStatus`. Commit with `Refs: W-005` and `Co-Authored-By:`; never push; wrap with `wrap-session`.
