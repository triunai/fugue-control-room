# Fugue — hot state (the resume point)

OVERWRITTEN at every wrap or pause, never appended; at most 40 lines. After a wrap or days away: read this first, then do "Next action" exactly. History lives in `docs/project-log.md` and `CHANGELOG.md`.

## ▶ RESUME HERE — 2026-11-26
Branch: `main` (no other worktrees). A frontend agent works in `web/**` (untracked there; never touch it from backend work).
Last green sha: ac9845d (build 0/0; `dotnet test` 406 green (Fugue.ArchTests 35, Fugue.Tests 371); Bruno 49 requests / 67 tests on a live API with `--seed`; `npm run test:scripts` 83; `dotnet format` clean; ledger-check findings=0).

## Last action done
W-006 (S4 outbox and signed webhooks, ADR-011), W-015 (Codex W-013 MED fixes) and W-016 (OpenAPI contract, console CORS, `GET /api/v1/me`, ADR-012, D-042 to D-044) are closed. The console's unblock point is committed: `docs/api/openapi.v1.json` (refresh with `npm run api:openapi`; a test fails when it is stale), CORS policy `Console`, and `/me`. All migrations are applied to the local compose DB.

## Next action (exact)
1. `git status --short --branch`; `git config core.hooksPath` must print nothing.
2. W-007 (rest of S5: `Idempotency-Key` store and replay, rate limits beyond auth, response caching, telemetry, API versioning). Plan in `docs/plans/` first, then review, then code. The contract is test-gated: any API change must refresh `docs/api/openapi.v1.json` in the same commit.
3. W-017 is the frontend agent's (`web/**`, `docs/runbooks/09-console.md`); check `git log --grep "Refs: W-017"` before touching the board.

## Uncommitted work in progress
None expected from backend work. Local-only and ignored: `.beads/`, `.fugue-local/`, `.env`. `web/` belongs to the frontend agent.

## In flight (open W ids)
W-007 S5 REST hardening (parked, next), W-010 verify the Codex gate (parked), W-011 README (other agent), W-017 console (frontend agent).

## Pending owner questions
- Clone the work repo to study its model/entity file conventions (patterns only, never copy files or names into Fugue).
- Amend the `PasswordHasher<T>` / Argon2id wording in CLAUDE.md (PasswordHasher<T> is PBKDF2).
- Staff read access to the audit log was left admin-only (D-031); say if staff should get `AUDIT_READ`.
- Review the "S1/S2 implementation choices" entries in `docs/ai-journal.md`.

## Owed inbox
- Email hashes in `access_audit` written before 3766cbc use the old key and do not correlate with new ones.
- The append-only trigger does not stop the table owner or a superuser; a non-owner API role is in `docs/archive/deferred.md`.
- Outbox retention and archival (backlog #001, deferred). Development CORS allows `http://localhost:4200` only; add other console origins via `Cors:Origins`.
- Docker note: the daemon once stopped answering after the delivery tests (failure mode 23); wrap docker and test commands in `perl -e 'alarm 900; exec @ARGV'`.

## Seed prompt (copy-paste)
> Hydrate Fugue: read `docs/hot-state.md`, `hygiene.md`, ADR-011 and ADR-012. Run `git status --short --branch` and `git config core.hooksPath` (empty). S4 and the console contract are done; start W-007 (S5 REST hardening) with a plan in `docs/plans/` and a plan review before code. Any API change refreshes `docs/api/openapi.v1.json` (`npm run api:openapi`). Commit by pathspec (`git add` new files first) with `Refs: W-NNN` and `Co-Authored-By:`; body lines at most 95 columns; never push.
