# Lane nth-useredit status (Refs D-064)

## Commits
- slice 1: feat(api): edit a user with PUT /users/{id} and If-Match

## Gates (copied from output)
- `dotnet build`: Build succeeded (0 warnings).
- `dotnet format --verify-no-changes`: exit 0.
- Targeted: `UpdateUserHandlerTests|UserEditTests|UserCreationTests|UsersAndRolesTests`: Passed 20, Failed 0.
- `dotnet test tests/Fugue.ArchTests`: Passed 37, Failed 0.
- `npm run api:openapi` (1 passed) and `cd web && npm run api:gen` (regenerated: `updateUserRequest.ts`, `userDto.ts` gains `version`).
- Bruno `npx bru run users --env local --env-var baseUrl=http://localhost:5232` on a throwaway postgres:17 (55532, `--seed`):
  Requests 9 (9 Passed), Tests 9/9 (07 edit 200, 08 stale 412, 09 escalation 403).

## What changed in shared files (exactly)
- `src/Fugue.Infrastructure/Identity/UserAdmin.cs`: `UpdateAsync` (row lock + xmin check + roles diff + grant_audit), create
  returns the version.
- `src/Fugue.Infrastructure/Identity/UserDirectory.cs`: one extra page read for each row's xmin (`identity/user-versions.sql`).
- `src/Fugue.Infrastructure/Persistence/Sql/identity/lock-user.sql`, `user-versions.sql` (new).
- `src/Fugue.Api/Contracts/UserRequests.cs`: `UpdateUserRequest`.
- `tests/Fugue.Tests/Identity/UsersAndRolesTests.cs`: the exact list-row field set gains `version` (still fails if any
  other field, for example a password hash, appears).
- No migration: `users.xmin` is read with SQL, not mapped in the EF model (mapping it would be a snapshot migration).

## Deviations
- The brief's premise "ETag from the user's xmin, same as other ETag'd PUTs" was half true: `users` has no RowVersion
  mapping (`IdentityConfigurations.cs:38-55`), so the version is read by SQL under `SELECT ... FOR UPDATE`.

## Next slice
- 2: web add/edit drawer, show-password toggle.
