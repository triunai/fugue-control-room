---
name: dual-gate-review
description: Use when reviewing a Fugue diff, plan or migration before it lands, especially critical seams (migrations, auth, ApiControllerBase, idempotency, webhook signing, hooks and scripts). Runs reviewer rounds from a different model family until there are no HIGH or MED findings, applies the strict maintainability rubric, and pins every catch (D-012). Triggers - "review this", "dual gate", "thermonuclear review", "is this ready to merge", "adversarial review".
---

# Dual-gate review (Fugue)

Two independent reads, then a fix loop. Authoring and review are separate passes; never self-approve. The author never reviews its own diff.

## Gate A: correctness (cross-family)
1. Reviewer is a different model family or tier from the author (Claude builder -> Codex or Fable reviewer; or a fresh Opus reviewer). The Codex pre-push gate (`scripts/codex-gate.mjs`, D-014) is ADVISORY and capped; it does not replace this gate. If Codex quota is out, say so and record the override in the handoff and use a Fable or Opus reviewer.
2. Give the reviewer the diff, the contract and `CLAUDE.md`, not your conclusion. Ask for findings with severity (HIGH, MED, LOW), `file:line` and a reproduction. Reports go to `docs/reviews/<batch>/rv-<lane>.md`.
3. Verify every finding against the source before acting. Two reviews can disagree and one can be wrong. Re-check any "not found" claim.

## Gate B: strict maintainability rubric
Reject or flag when you see:
- A giant file, class or method that mixes concerns (a handler doing validation, persistence and mapping; a controller with logic).
- Spaghetti conditions: growing `if`/`switch` ladders where a rule table, polymorphism or a pipeline behavior fits.
- Abstractions without two users, or generic base types hiding the CRUD being practised.
- Layering leaks: EF, Dapper, Npgsql or ASP.NET types in Application or Domain; Dapper outside `SqlExecutor`; `DbContext` in an action.
- Error handling that bypasses FluentResults/`FugueError`; swallowed exceptions; secrets or payloads in logs.
- Dead code, commented-out code, duplicated helpers, suppressed warnings, `Skip =` without `RED:`.
- Tests that assert nothing, mock the database where Testcontainers belongs, or were weakened to go green.

## The loop
1. Round N: reviewer reports. Triage: fix, defer with a backlog item (LOW only), or refute with evidence.
2. Every HIGH and MED gets its own fix commit (finding id in the subject) and a PIN (D-012): a regression test, an architecture or fitness test (`tests/Fugue.ArchTests/FitnessTests.cs`, shrink-only baselines), or a type that makes the bug unrepresentable. Structural catches pin as fitness functions; behavioural ones as tests; SQL and state machines get a RED-first test.
3. Re-review. Repeat until a round has NO HIGH or MED. A green suite is not a review.
4. Record: findings and pins in the review file; a heavy catch also as a `D-NNN` (promote to an ADR if it changes a convention); a new trap in `docs/failure-modes.md`.
5. Land only after the approving round. The owner is told what was overridden or skipped.

## Seams that always get this gate
Migrations (also run `migration-retrofit-qa`), auth and identity, `ApiControllerBase`, idempotency, webhook signing, `lefthook.yml`, `scripts/**`, `.claude/**`.
