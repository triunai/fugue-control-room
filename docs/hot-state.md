# Fugue — hot state (the resume point)

OVERWRITTEN at every wrap or pause, never appended; at most 40 lines. After a wrap or days away: read this first, then do "Next action" exactly. History lives in `docs/project-log.md` and `CHANGELOG.md`.

## ▶ RESUME HERE — 2026-11-25
Branch: `main` (no other worktrees).
Last green sha: 4e01024 (build 0/0; `dotnet test` 171 green (Fugue.ArchTests 24, Fugue.Tests 147); Bruno 32 requests / 45 assertions; `npm run test:scripts` 74).

## Last action done
S3 identity (W-005) built and closed: JWT + refresh, uncached effective permissions, ownership 404, module management (ADR-009), tenants, groups, grant/access audit, overrides, 204 mutations, ADR-008. Cross-family review done (Codex plan REJECT -> all 10 accepted; Codex + Sonnet build reviews, HIGH fixed). Dispositions: `docs/reviews/s3/rv-s3-build.md`.

## Next action (exact)
1. `git status --short --branch`; `git config core.hooksPath` must print nothing; read `docs/runbooks/05-auth.md` (needs `dotnet user-secrets set Auth:SigningKey "$(openssl rand -base64 32)" --project src/Fugue.Api`).
2. Owner call needed for W-013: should customer email uniqueness stay global (create-path oracle) or become per owner? Then fix W-013 MEDs first.
3. The rich general `audit_log` batch is next (another agent): build on `IUnitOfWork` + `ICorrelationContext` + the two audit tables. Then W-006 (S4 outbox), W-007 (S5).

## Uncommitted work in progress
None expected. Local-only and ignored: `.beads/`, `.fugue-local/`, `.env`.

## In flight (open W ids)
W-013 S3 review follow-ups; W-011 README (other agent), W-012 rich audit log (other agent); W-006 S4 outbox and webhooks, W-007 S5 REST hardening, W-010 verify the Codex gate (all parked).

## Pending owner questions
- Clone the work repo to study its model/entity file conventions (naming, file extensions/partials). Patterns only - never copy files or names into Fugue.
- Amend the `PasswordHasher<T>` / Argon2id wording in CLAUDE.md (PasswordHasher<T> is PBKDF2).
- Customer email uniqueness: global or per owner (W-013)?
- Review the "S1/S2 implementation choices" entries in `docs/ai-journal.md`.

## Owed inbox
- Unverified: Codex ran on the committed HEAD before the review-fix commit (the fixes are covered by tests, not re-reviewed); `OnForbidden` behaviour is covered by tests only on .NET 8.

## Seed prompt (copy-paste)
> Hydrate Fugue: read `docs/hot-state.md`, `hygiene.md`, `docs/ai/handoffs/2026-11-25-lead.md`, ADR-008 and ADR-009. Run `git status --short --branch` and `git config core.hooksPath` (empty). S3 is done; decide W-013 (email uniqueness), then start the next batch (rich `audit_log`, or S4 outbox W-006). Auth is a critical seam: plan review before code, `dual-gate-review` after. Commit by pathspec with `Refs: W-NNN` and `Co-Authored-By:`; never push.
