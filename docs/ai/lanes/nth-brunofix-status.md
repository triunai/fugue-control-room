# Lane nth-brunofix status (Refs D-064)

## Commits
- `test(bruno): users suite creates a self-service user, not a staff officer` (single slice)

## Gates (copied from output)
- `npx bru run users --env compose` (api :5081 compose env, same stack as :8080): Status PASS, Requests 6 (6 Passed), Tests 6/6.
- `GET /api/v1/claims/workload` as manager after the run: officers list has no "Bruno test claimant";
  the only Bruno row is one pre-existing "Bruno Created" from earlier STAFF runs (not cleaned up: non-goal).

## Shared files changed
- None. Only `bruno/fugue/users/04-admin-creates-user.bru` and `05-duplicate-email-409.bru`.

## Deviations
- `docs/branch-ledger.md` has no `nth-brunofix` row on this base, so no Status cell was updated.
- `06-staff-cannot-create-users.bru` still uses roles ["STAFF"] but is a 403, creates nothing; left alone (not in lane).

## Next slice
- None.
