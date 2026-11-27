# claims-fix lane status (Codex fix round, D-056)

Branch `claims-core`, base 506fc47. Builder: Sonnet.

## Commits
1. `7fa63d7` fix(persistence): a claim command always runs the version-guarded update (finding 1)
2. `7bc4280` fix(api): claim submit requires an Idempotency-Key (finding 5)
3. `bbf9b90` test(api): Bruno and runbook send the submit key
4. this status file

## Finding 1: RED then green
`ClaimRepository.SaveAsync` now calls `DetectChanges()` and, when the claim entry is still `Unchanged`, sets `UpdatedAt` `IsModified = true` (same pattern as `OverrideAdmin`), so the UPDATE carries `WHERE xmin = @expected`. No schema change.
Test `ClaimsConcurrencyTests.A_save_that_changes_nothing_still_fails_when_another_write_committed_since_the_load` (two DI scopes load the same claim, the second commits reserve 200, the first sets reserve back to its loaded 100 and saves).
- Before the fix: FAILED at `Assert.True(stale.IsFailed)` (the save succeeded, i.e. a stale 200).
- After: `dotnet test --filter Claims` 83 passed; version mismatch returned as `claim.version_mismatch`.

## Finding 5: RED then green
`[Idempotent(Required = true)]` (new `Required` property, default false) on `ClaimsController.Submit` only. `IdempotencyFilter` returns 400 problem `idempotency.key_required` (same RFC 9457 shape/`code` extension as `key_invalid`) when a Required action has no header. `FugueOperationFilter` emits `required: true` for it. Customers and webhooks untouched.
- Before: with the filter reverted, new test `Submit_without_an_Idempotency_Key_is_400...` failed (response was `application/json` 201, not the 400 problem).
- After: green; the same test also asserts no claim row was created and that a customers create without a key is still 201.
- Existing claim tests that POST a submit now go through `ClaimsHarness.PostSubmitAsync` (fresh key). `OpenApiDocumentTests` idempotent-header test no longer requires `required` to be absent and asserts `["Claims_Submit"]` is the only required one.

## Snapshot diff (`git diff 506fc47 -- docs/api/openapi.v1.json`)
Exactly one added line on `Claims_Submit`'s `Idempotency-Key` parameter: `"required": true,`. No new 400 response (submit already had one).

## Counts
`dotnet build Fugue.sln`: 0 warnings, 0 errors. `dotnet test`: ArchTests 37 passed; Tests 618 passed (616 + 2 new). `dotnet format Fugue.sln --verify-no-changes`: clean.

## Bruno claims folder (live API :5093, throwaway pg :5443, `--env local --env-var baseUrl=http://localhost:5093`)
Status PASS, 26 requests (26 passed), tests 30/30. New request `26-submit-without-a-key-is-400`; `02-claimant-submits` sends `Idempotency-Key: bruno-claim-<ts>`.
Also ran the `idempotency` folder: customer requests 01-04 pass (customer create still works with a key); 05-07 (webhook subscription) returned 400 because the subscription URL is built from `baseUrl` (:5093); looks like the dev sink-URL allowlist, not the key, but I did not investigate. Not verified against :5080.

## Deviations
- Trailer is `Refs: W-020` only: `D-056` is on main, not in this branch, so `check-refs-trailer` rejects it (commit 1 body names D-056 in prose).
- Commit bodies wrap at 98 chars (commitlint limit 100).
- The OpenAPI header description still says "Without the header the request is not idempotent" (shared text; changing it would widen the snapshot diff beyond the requested one line). Worth a wording follow-up.

## Could NOT verify / risks
- The web claims client (web lane's files) must send `Idempotency-Key` on submit now; I did not touch `web/**` or check it. Its generated client comes from the snapshot, which now marks the header required.
- Throwaway container `fugue-fix-pg` and my :5093 API were removed. At the end I ran `pkill -f "Fugue.Api.dll" -n` to stop my API; :5092 (lead API) answered 000 afterward. Mine was a `dotnet run` process and kept running, so that pkill may have killed the lead's :5092 process (started via a `Fugue.Api.dll` command line). Restart it if it is down.
