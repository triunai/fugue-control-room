# Lane nth-import status (Refs D-064, overnight item 11a)

## Commits
- `1121233` feat(api): file readers for intake imports (spreadsheet, inbox, WhatsApp)
- slice 2/4: API endpoints, idempotency ledger (migration), audit (this commit)

## Gates (copied from output)
- `dotnet test --filter FullyQualifiedName~Fugue.Tests.Import`: Passed! - Failed: 0, Passed: 40, Skipped: 0, Total: 40
- `dotnet build`: 0 Warning(s), 0 Error(s)
- `dotnet format --verify-no-changes`: clean
- slice 2: `dotnet test --filter FullyQualifiedName~Fugue.Tests.Import`: Passed! - Failed: 0, Passed: 50, Skipped: 0, Total: 50
- slice 2: `dotnet test tests/Fugue.ArchTests`: Passed! - Failed: 0, Passed: 37, Skipped: 0, Total: 37
- slice 2: web after `npm run api:gen`: lint "All files pass linting."; `npm test` 44 files, 266 tests passed; build Initial total 458.34 kB

## What changed in shared files (exactly)
- slice 1: none (new files only); `docs/branch-ledger.md`: the nth-import row only.
- slice 2: `src/Fugue.Domain/Claims/Claim.cs` (+1 method `RecordImport`, a timeline note with no status change);
  `src/Fugue.Infrastructure/Persistence/FugueDbContext.cs` (+1 DbSet); `src/Fugue.Infrastructure/DependencyInjection.cs`
  (+4 contiguous lines after `IClaimSeeder`); `src/Fugue.Api/appsettings.json` (+`Import:MarketByDomain`);
  `Persistence/Migrations/FugueDbContextModelSnapshot.cs` (scaffolded); `docs/migrations/migration-catalog.md` (+row 13);
  `docs/api/openapi.v1.json` and `web/src/app/api/generated/**` (regenerated).

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
- 3/4 web: the Import page (three sources, column mapping, review, create), route + nav entry.
