# Lane nth-paid status (Refs D-064, item 11c, backlog #009 part 1)

## Commits
- `174c311` feat(api): paid (settled) count and sum on the exposure rows and totals (slice 1, with this status file)
- slice 2: feat(api): OpenAPI snapshot, generated client and a Bruno request for the paid exposure fields

## Gates (copied from output)
- `dotnet build`: 0 Warning(s), 0 Error(s)
- `dotnet format --verify-no-changes`: exit 0
- Targeted `dotnet test --filter ClaimReportsTests|ClaimPaidExposureTests|ClaimSeederTests`: Passed 29, Failed 0
- Rule 6 proof: with `OpenCount` counting settled claims again, `ClaimPaidExposureTests.Exposure_shows_the_paid_sum...` and the
  rewritten `ClaimReportsTests.Exposure_totals_are_per_currency_never_across_currencies` both fail (2 failed / 4); restored, they pass.

- `npm run api:openapi`: Passed 1 (snapshot rewritten); `cd web && npm run api:gen`: orval v8.49.0 regenerated, 3 model files
  changed (`exposureRowDto.ts`, `exposureTotalDto.ts`, `claimExposureDto.ts` doc comment)
- Bruno `npx bru run claims --env local --env-var baseUrl=http://localhost:5210` (throwaway pg :55510, `--seed`):
  Requests 40 (40 Passed), Tests 46/46; new `claims/40-exposure-carries-paid-settled` 3/3
- Live as manager@fugue.test on :5210 after the Bruno run: SGD settled 2 / 265000 (seeded 185000 + the 80000 claim the Bruno
  claims folder settles), PHP 1 / 5400000, HKD 1 / 950000, IDR, MYR, THB 0 / 0

## What changed in shared files (exactly)
- None outside the lane's list. `ClaimsController.cs` (endpoint summary) is NOT changed: proposed text is in the lane report.

## Design choices
- Where the money is stored: `claims.approved_amount_minor` with `status = 'Settled'` (`Claim.Settle` takes no amount and never
  changes the approved amount; `Approve` is the only writer, check constraint `ck_claims_approved_positive`). `settled_at` is set.
- New fields `SettledCount` / `SettledMinor` on `ExposureRowDto` and `ExposureTotalDto`, appended last. All time: the DTO has no
  window (only `AsOf`), so none was invented.
- Settled claims now join the grouped query, so a market and line with only settled claims is a row whose exposure fields are
  zero. Paid is not exposure: open, reserve, approved and unreserved figures exclude settled claims exactly as before.

## Deviations
- The brief says exposure reads through "EF raw SQL under Sql/**": false. `ClaimReports.GetExposureAsync`
  (`src/Fugue.Infrastructure/Persistence/Claims/ClaimReports.cs:44`) is an EF LINQ GroupBy; only the workload uses raw SQL.
- Four assertions in `ClaimReportsTests` changed because the IDR (settled-only) group now appears as a zero-exposure row: the
  row and total lists gain it, and "no IDR row" became "IDR row has zero exposure and paid 1 / 7,000,000" (stronger).
- No ledger row for nth-paid exists in `docs/branch-ledger.md` (only rows for the dacfb06 lanes), so it was not touched.

## Next slice
- 3: team page "Paid (settled)" figure.
