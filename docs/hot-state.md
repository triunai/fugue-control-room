# Fugue — hot state (the resume point)

OVERWRITTEN at every wrap or pause, never appended; at most 40 lines. After a wrap or days away: read this first, then do "Next action" exactly. History lives in `docs/project-log.md` and `CHANGELOG.md`.

## ▶ RESUME HERE — 2026-11-26
Branch: `main` (no other worktrees).
Last green sha: c35a022 (build 0/0; `dotnet test` 222 green (Fugue.ArchTests 29, Fugue.Tests 193); Bruno 40 requests / 56 tests on a live API with `--seed`; `npm run test:scripts` 83).

## Last action done
W-012 rich append-only `audit_log` built and closed (ADR-010, D-027 to D-033): table + trigger + `AUDIT_READ` (admin only), EF interceptor in the same SaveChanges, `IAuditTrail` for Dapper writes, `AuditBehavior` failure rows, write-time masking, `GET /api/v1/audit` with keyset paging, runbook `06-audit.md`. Codex review (1 HIGH, 3 MED) all fixed and pinned, fixes not re-reviewed: `docs/reviews/codex/2026-11-26-w012-audit.md`. The migration is applied to the local compose DB.

## Next action (exact)
1. `git status --short --branch`; `git config core.hooksPath` must print nothing.
2. Owner call needed for W-013: should customer email uniqueness stay global (create-path oracle) or become per owner? Then fix the W-013 MEDs first (add the missing `OverrideAdmin` concurrency check).
3. Then W-006 (S4 outbox; it writes through `IUnitOfWork`, and every raw SQL write must call `IAuditTrail` or be baselined: fitness rule `raw-sql-writes`), then W-007 (S5).

## Uncommitted work in progress
None expected. Local-only and ignored: `.beads/`, `.fugue-local/`, `.env`.

## In flight (open W ids)
W-013 S3 review follow-ups; W-011 README (other agent); W-006 S4 outbox and webhooks, W-007 S5 REST hardening, W-010 verify the Codex gate (all parked).

## Pending owner questions
- Clone the work repo to study its model/entity file conventions (naming, file extensions/partials). Patterns only - never copy files or names into Fugue.
- Amend the `PasswordHasher<T>` / Argon2id wording in CLAUDE.md (PasswordHasher<T> is PBKDF2).
- Customer email uniqueness: global or per owner (W-013)?
- Staff read access to the audit log was left admin-only (D-031); say if staff should get `AUDIT_READ`.
- Review the "S1/S2 implementation choices" entries in `docs/ai-journal.md`.

## Owed inbox
- Unverified: the W-012 review fixes were not re-reviewed by Codex (covered by tests); `OnForbidden` behaviour is covered by tests only on .NET 8.
- The append-only trigger does not stop the table owner or a superuser; a non-owner API role is in `docs/archive/deferred.md`.

## Seed prompt (copy-paste)
> Hydrate Fugue: read `docs/hot-state.md`, `hygiene.md`, ADR-008, ADR-009 and ADR-010. Run `git status --short --branch` and `git config core.hooksPath` (empty). S3 and the audit log are done; decide W-013 (email uniqueness), then start the next batch (S4 outbox W-006). Auth and migrations are critical seams: plan review before code, cross-family review after. Commit by pathspec (`git add` new files first) with `Refs: W-NNN` and `Co-Authored-By:`; never push.
