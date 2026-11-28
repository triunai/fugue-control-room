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
