# Fugue — hot state (the resume point)

OVERWRITTEN at every wrap or pause, never appended; at most 40 lines. After a wrap or days away: read this first, then do "Next action" exactly. History lives in `docs/project-log.md` and `CHANGELOG.md`.

## ▶ RESUME HERE — 2026-11-25
Branch: `main` (no other worktrees).
Last green sha: ad58e29
(build 0 warnings; arch tests 17 green; Fugue.Tests 48 green 16 red-by-design; only docs follow it.)

## Last action done
S3 identity plan written (W-005, docs only): `docs/plans/s3-identity.md`, D-016 to D-021 proposed. Before that: H1 harness (W-008) closed.

## Next action (exact)
1. `git status --short --branch`; `git config core.hooksPath` must print nothing.
2. S3 plan awaiting cross-family review: review `docs/plans/s3-identity.md` (`dual-gate-review`), fold findings in, confirm D-016 to D-021, then build slice 1.
3. Seed prompt below.

## Uncommitted work in progress
None expected after the final H1 commit. Local-only and ignored: `.beads/` (local queue), `.fugue-local/` (codex gate ledger), `.env`.

## In flight (open W ids)
W-005 S3 identity, W-006 S4 outbox and webhooks, W-007 S5 REST hardening, W-010 verify the Codex gate (all parked, not started); W-009 MapStatus (blocked on owner).

## Pending owner questions
- Clone the work repo to study its model/entity file conventions (naming, file extensions/partials). Patterns only - never copy files or names into Fugue.
- Amend the `PasswordHasher<T>` / Argon2id wording in CLAUDE.md (PasswordHasher<T> is PBKDF2).
- Review the "S1/S2 implementation choices" entries in `docs/ai-journal.md`.

## Tests (verified 2026-11-25)
`Fugue.ArchTests` 17 green (9 are fitness functions, baselines empty). `Fugue.Tests` 48 green, 16 red, all MapStatus-dependent: the 8 `MapStatusTests` cases, `Failure_is_a_problem_json_with_code_type_and_trace_id`, `Validation_failure_carries_an_errors_dictionary`, `Ping_with_an_out_of_range_count_is_a_400_validation_problem`, and 5 in `CustomersApiTests` (stale 412, missing If-Match 428, suspend twice 409, duplicate email 409, unknown id 404). Bruno: requests 06, 07, 09, 13. `npm run test:scripts` green.

## Owed inbox
- (empty)

## Seed prompt (copy-paste)
> Hydrate Fugue: read `docs/hot-state.md`, `hygiene.md`, `docs/ai/handoffs/2026-11-25-lead.md`, the S3 section of `docs/plans/roadmap.md` and ADR-003. Run `git status --short --branch` and `git config core.hooksPath` (empty). Continue S3 identity (W-005): the plan `docs/plans/s3-identity.md` exists (JWT bearer, lean UAM v3 port, cached permission lookup, ownership scoping with 404); get it reviewed, then build slice 1. Auth is a critical seam: independent cross-family plan review before code, `dual-gate-review` after. Use `agent-task-contract` for dispatch. Never implement `MapStatus`. Commit with `Refs: W-005` and `Co-Authored-By:`; never push; wrap with `wrap-session`.
