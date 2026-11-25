# Fugue — hot state (the resume point)

OVERWRITTEN at every wrap or pause, never appended; at most 40 lines. After a wrap or days away: read this first, then do "Next action" exactly. History lives in `docs/project-log.md` and `CHANGELOG.md`.

## ▶ RESUME HERE — 2026-11-26
Branch: `main` (no other worktrees).
Last green sha: 3766cbc (build 0/0; `dotnet test` 260 green (Fugue.ArchTests 29, Fugue.Tests 231); Bruno 40 requests / 56 tests on a live API with `--seed`; `npm run test:scripts` 83; `dotnet format` clean).

## Last action done
W-013 closed (D-034 to D-036, ADR-008 amended). Owner calls built: customer email unique per owner (migration `CustomerEmailPerOwner`), login limited per (IP, email) plus a loose per-email ceiling. Also: override `If-Match` (migration `OverrideConcurrencyToken`, W-012 gap), group self-target, 23505 races, tenant `is_active`, fail-closed module config, `access_audit` clip and HMAC subkey. Both migrations are applied to the local compose DB.

## Next action (exact)
1. `git status --short --branch`; `git config core.hooksPath` must print nothing.
2. W-006 (S4 outbox and webhooks): it writes through `IUnitOfWork`, and every raw SQL write must call `IAuditTrail` or be baselined (fitness rule `raw-sql-writes`). Plan in `docs/plans/` first, then review, then code. Then W-007 (S5).
3. Ask for (or run) a Codex review of the W-013 diff `3b7dcca..HEAD` (auth and migrations are critical seams).

## Uncommitted work in progress
None expected. Local-only and ignored: `.beads/`, `.fugue-local/`, `.env`.

## In flight (open W ids)
W-011 README (other agent); W-006 S4 outbox and webhooks, W-007 S5 REST hardening, W-010 verify the Codex gate (all parked).

## Pending owner questions
- Clone the work repo to study its model/entity file conventions (naming, file extensions/partials). Patterns only - never copy files or names into Fugue.
- Amend the `PasswordHasher<T>` / Argon2id wording in CLAUDE.md (PasswordHasher<T> is PBKDF2).
- Staff read access to the audit log was left admin-only (D-031); say if staff should get `AUDIT_READ`.
- Review the "S1/S2 implementation choices" entries in `docs/ai-journal.md`.

## Owed inbox
- Unverified: W-013 changes (limiter, override If-Match, tenant enforcement, access_audit hash key) have NO cross-family review yet; covered by tests only. The W-012 review fixes were also not re-reviewed.
- Email hashes in `access_audit` written before 3766cbc use the old key and do not correlate with new ones.
- The append-only trigger does not stop the table owner or a superuser; a non-owner API role is in `docs/archive/deferred.md`.

## Seed prompt (copy-paste)
> Hydrate Fugue: read `docs/hot-state.md`, `hygiene.md`, ADR-008, ADR-009 and ADR-010. Run `git status --short --branch` and `git config core.hooksPath` (empty). S3, the audit log and the S3 review follow-ups are done; start the next batch (S4 outbox W-006) after a Codex review of `3b7dcca..HEAD`. Auth and migrations are critical seams: plan review before code, cross-family review after. Commit by pathspec (`git add` new files first) with `Refs: W-NNN` and `Co-Authored-By:`; body lines at most 95 columns; never push.
