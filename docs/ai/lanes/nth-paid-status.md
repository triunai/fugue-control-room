# Lane nth-paid status (Refs D-064, item 11c, backlog #009 part 1)

## Commits
- slice 1: feat(api): paid (settled) count and sum on the exposure rows and totals, with tests, and this status file

## Gates (copied from output)
- `dotnet build`: 0 Warning(s), 0 Error(s)
- `dotnet format --verify-no-changes`: exit 0
- Targeted `dotnet test --filter ClaimReportsTests|ClaimPaidExposureTests|ClaimSeederTests`: Passed 29, Failed 0
- Rule 6 proof: with `OpenCount` counting settled claims again, `ClaimPaidExposureTests.Exposure_shows_the_paid_sum...` and the
  rewritten `ClaimReportsTests.Exposure_totals_are_per_currency_never_across_currencies` both fail (2 failed / 4); restored, they pass.

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
- 2: OpenAPI snapshot + generated client, Bruno exposure assertions run against the API on :5210.
- 3: team page "Paid (settled)" figure.
