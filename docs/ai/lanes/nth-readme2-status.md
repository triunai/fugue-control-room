# Lane nth-readme2 status (Refs D-064)

## Commits
- slice (a): `docs(api)` ClaimSearchHitDto comment says ClaimantName is returned to every caller (was "null for a claimant", false: `ClaimDocument.ToHit`)

## Gates (copied from output)
- `npm run api:openapi`: `Passed!  - Failed: 0, Passed: 1, Skipped: 0, Total: 1 - Fugue.Tests.dll (net8.0)`; `docs/api/openapi.v1.json` 1 line changed
- `npm run api:gen` (web): regenerated `claimSearchHitDto.ts` (comment only)
- `dotnet build`: 0 Warning(s), 0 Error(s); `dotnet format --verify-no-changes`: clean

## Shared files changed
- `src/Fugue.Application/Features/Claims/Repos/IClaimSearch.cs` (the doc comment only), `docs/api/openapi.v1.json`, `web/src/app/api/generated/model/claimSearchHitDto.ts`.

## Deviations
- `docs/branch-ledger.md` has no row for this lane, so it is not touched.

## Next
- slice (b): README refresh with counts from commands run in this lane.

## Slice (b): README
- Counts from this lane: `dotnet test --list-tests` Fugue.Tests 858, ArchTests 37 (run: `Passed! Failed: 0, Passed: 37`); web `npm test`: 58 files, 388 tests passed; `playwright test --list`: 23 tests in 9 files; Bruno on :8080 (second, spaced run): 127 requests (123 passed, 4 failed), tests 148/153 (first run was rate limited: 33 passed, 94 failed).
- Bruno misses: webhooks 05 and 06 (sink poll pending), claims 07 and import 07 (demo officer was renamed "Dev Staff (edited)" by other work).
- Screenshots refreshed headlessly on :8080 (5 of 6; `audit-diff.png` untouched, refreshed 12:24 today): 0 console errors.
- HybridCache not on main at this point, so README does not mention it.
