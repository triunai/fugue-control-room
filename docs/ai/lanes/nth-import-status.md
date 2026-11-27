# Lane nth-import status (Refs D-064, overnight item 11a)

## Commits
- `1121233` feat(api): file readers for intake imports (spreadsheet, inbox, WhatsApp)
- `2754f2b` feat(api): import endpoints, claim_imports idempotency ledger, audited creates
- `efd03bd` feat(web): Import page for spreadsheet, inbox and WhatsApp file imports
- slice 4/4: Bruno `bruno/fugue/import/` (this commit)

## Gates (copied from output)
- `dotnet test --filter FullyQualifiedName~Fugue.Tests.Import`: Passed! - Failed: 0, Passed: 40, Skipped: 0, Total: 40
- `dotnet build`: 0 Warning(s), 0 Error(s)
- `dotnet format --verify-no-changes`: clean
- slice 2: `dotnet test --filter FullyQualifiedName~Fugue.Tests.Import`: Passed! - Failed: 0, Passed: 50, Skipped: 0, Total: 50
- slice 2: `dotnet test tests/Fugue.ArchTests`: Passed! - Failed: 0, Passed: 37, Skipped: 0, Total: 37
- slice 2: web after `npm run api:gen`: lint "All files pass linting."; `npm test` 44 files, 266 tests passed; build Initial total 458.34 kB

- slice 3: web lint "All files pass linting."; `npm test` 45 files, 274 tests passed; build Initial total 458.61 kB
  (120.64 kB gzip); headless (dev server 4403 -> API 5203, fresh DB): 23 PASS, 0 FAIL, console errors: 0

- slice 4: `npx bru run import --env local --env-var baseUrl=http://localhost:5203`: Status PASS, Requests 11 (11 Passed),
  Tests 13/13 (twice in a row: each run uses a fresh key and policy)

## What changed in shared files (exactly)
- slice 1: none (new files only); `docs/branch-ledger.md`: the nth-import row only.
- slice 2: `src/Fugue.Domain/Claims/Claim.cs` (+1 method `RecordImport`, a timeline note with no status change);
  `src/Fugue.Infrastructure/Persistence/FugueDbContext.cs` (+1 DbSet); `src/Fugue.Infrastructure/DependencyInjection.cs`
  (+4 contiguous lines after `IClaimSeeder`); `src/Fugue.Api/appsettings.json` (+`Import:MarketByDomain`);
  `Persistence/Migrations/FugueDbContextModelSnapshot.cs` (scaffolded); `docs/migrations/migration-catalog.md` (+row 13);
  `docs/api/openapi.v1.json` and `web/src/app/api/generated/**` (regenerated).
- slice 3: `web/src/app/app.routes.ts` (+1 route block `claims/import`, before `claims/calendar`);
  `web/src/app/features/shell/nav.ts` (+1 item "Import" in Claims, before SLA calendar). New: `web/src/app/features/import/**`.

## Design so far
- No new package: .xlsx is read as a zip of XML parts (first worksheet, shared strings, date styles); .eml/.mbox by a small MIME
  reader (folding, RFC 2047, multipart, quoted-printable, base64, charset); WhatsApp Android and iOS export lines.
- Every draft carries a server-computed SHA-256 key of its source item (spreadsheet row cells; email Message-ID, else the raw
  message; the claimant's first chat message), the basis of the idempotent create in slice 2.
- Inferred values carry hints ("Market HK routed from the sender domain ..."), for the reviewer to confirm.

## Deviations
- See the final report (`.worktrees/_briefs/reports/nth-import.md`).

## Migration (critical seam, rule 4)
- `20261127194749_AddClaimImports`: new table `claim_imports` only; catalog row 13; rollback test
  `ClaimImportsMigrationRollbackTests`. The integrator runs the migration QA gate.

## Next slice
- none: full .NET suite under the fleet lock, then the report.
