# Lane nth-api status (2026-11-27)

Branch `nth-api`, base b875f49. Refs W-024 (claims reads), W-019 (users, roles).

## Commits
- e3885f2 `feat(api): claim status counts and the past questions list`
- 9978c1f `feat(identity): users list and roles with their permissions` (migration `AddUsersViewPermission`, catalog row 11)
- e925e63 `test(api): Bruno requests for counts, questions, users and roles`
- this status file

## Counts (from output)
- `dotnet build --no-incremental`: 0 warnings, 0 errors
- `dotnet test`: ArchTests 37 passed; Tests 637 passed (base 623, +8 counts/questions, +5 users/roles, +1 rollback)
- `dotnet format --verify-no-changes`: exit 0
- Snapshot (`npm run api:openapi`) and client (`npm run api:gen`) regenerated per slice; a final regen leaves no diff
- `cd web && npm test`: 62 passed; `npm run lint`: clean; `npm run build`: complete
- Bruno against a throwaway API (:5103) and Postgres (:5453): `bru run claims users roles`, 35 requests, 39/39 tests passed; container and process removed

## What shipped
- `GET /api/v1/claims/counts` (`CLAIMS_READ`): `{total, byStatus:[{status,count}]}`, every status present, one grouped query; shares a `Filtered` helper with the list (assigned, market, line, q; no status).
- `GET /api/v1/claims/{id}/info-requests` (`CLAIMS_READ`): newest first; `askedBy` is "Claims team" for a claimant, the real display name for staff or the asker; 404 `claim.not_found` outside scope.
- `USERS_VIEW` (resource `USERS`, module `IDENTITY`) appended to `Permissions.All`, seeded to ADMIN only; `GET /api/v1/users` keyset (created_at, id) ascending, `limit` 1-100, `q` over email and display name (LIKE-escaped), rows id/email/displayName/isActive/roles/groups/createdAt, deleted users hidden, no credential field; `GET /api/v1/roles` with `permissions:[{code,module}]` ordered by module then code.
- Runbooks 05 and 11, migration catalog, 13 Bruno requests.

## Deviations
- The brief's three commits became four (status file) as the header requires; the snapshot and client were committed with each contract slice, not only the last.
- Users list order is oldest account first (matches the keyset helper), not newest first; the brief did not fix an order.
- Runbook 11 said the Bruno claims suite had 25 requests; it had 26 before this lane, now 30; corrected.
- Migration was scaffolded with `dotnet ef migrations add` and left unedited (seed `InsertData` only; the Down deletes in FK order).

## Contract gaps for the web lanes
- The roles list has no "description" and no "isSystem" flag; add them if the matrix needs them.
- `roles` on a user is direct assignments only; roles inherited through groups appear via `groups` codes plus `GET /groups` (a user's effective permissions are not exposed).
- A user with a deleted group membership is not distinguished; there is no user detail endpoint.

## Not verified
- The migration retrofit QA dual adversarial review (thermonuclear + Codex) was not run by this lane; it is a critical seam per AGENTS.md. The rollback and re-apply are proven by `UsersViewMigrationRollbackTests`; the idempotent SQL script was not read. Settle by running the `migration-retrofit-qa` skill on `20261127102803_AddUsersViewPermission`.
- Merge note: nth-search also changes `docs/api/openapi.v1.json` and the generated client; regenerate both after merging.
