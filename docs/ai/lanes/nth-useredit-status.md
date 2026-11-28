# Lane nth-useredit status (Refs D-064)

## Commits
- `340cc55` feat(api): edit a user with PUT /users/{id} and If-Match
- `3db1fb9` feat(web): edit users from the access table, show/hide the initial password
- `3f553a0` merge main (66db90a); this status update

## Gates (copied from output)
- `dotnet build`: Build succeeded (0 warnings).
- `dotnet format --verify-no-changes`: exit 0.
- Targeted: `UpdateUserHandlerTests|UserEditTests|UserCreationTests|UsersAndRolesTests`: Passed 20, Failed 0.
- `dotnet test tests/Fugue.ArchTests`: Passed 37, Failed 0.
- `npm run api:openapi` (1 passed) and `cd web && npm run api:gen` (regenerated: `updateUserRequest.ts`, `userDto.ts` gains `version`).
- Bruno `npx bru run users --env local --env-var baseUrl=http://localhost:5232` on a throwaway postgres:17 (55532, `--seed`):
  Requests 9 (9 Passed), Tests 9/9 (07 edit 200, 08 stale 412, 09 escalation 403).

- Web (slice 2): `npm run lint` All files pass linting; `npm test` Test Files 58 passed (58), Tests 388 passed (388);
  `npm run build` exit 0.
- Headless Playwright (dev server 4432 -> API 5232): admin edits support@fugue.test name + adds STAFF, reload shows
  "Support Edited 33514 active STAFF SUPPORT", add-mode toggle flips type and aria-pressed, consoleErrors [].
  Screenshots `.worktrees/_briefs/reports/nth-useredit-{1-table,2-edit-drawer,3-after-reload,4-add-show-password}.png`.

- Full suite once under the lock at 3db1fb9: ArchTests Passed 37/37; Fugue.Tests Passed 858, Failed 0, Total 858.
- After merging main 66db90a: build succeeded; Users tests + OpenApiContractTests Passed 21, Failed 0.

## What changed in shared files (exactly)
- `src/Fugue.Infrastructure/Identity/UserAdmin.cs`: `UpdateAsync` (row lock + xmin check + roles diff + grant_audit), create
  returns the version.
- `src/Fugue.Infrastructure/Identity/UserDirectory.cs`: one extra page read for each row's xmin (`identity/user-versions.sql`).
- `src/Fugue.Infrastructure/Persistence/Sql/identity/lock-user.sql`, `user-versions.sql` (new).
- `src/Fugue.Api/Contracts/UserRequests.cs`: `UpdateUserRequest`.
- `tests/Fugue.Tests/Identity/UsersAndRolesTests.cs`: the exact list-row field set gains `version` (still fails if any
  other field, for example a password hash, appears).
- `web/src/app/api/access.api.ts`: `user(id)` and `updateUser(id, body, version)` (explicit If-Match).
- `web/src/app/features/access/access-page.{html,ts}`: an Edit button per users row (USERS_MANAGE), colspan 7.
- No migration: `users.xmin` is read with SQL, not mapped in the EF model (mapping it would be a snapshot migration).

## Deviations
- The brief's premise "ETag from the user's xmin, same as other ETag'd PUTs" was half true: `users` has no RowVersion
  mapping (`IdentityConfigurations.cs:38-55`), so the version is read by SQL under `SELECT ... FOR UPDATE`.

## Next slice
- None. Slice 3 (console add/edit audit) is in `.worktrees/_briefs/reports/nth-useredit.md`. READY.
