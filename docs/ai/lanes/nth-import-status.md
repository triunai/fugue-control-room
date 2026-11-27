# Lane nth-import status (Refs D-064, overnight item 11a)

## Commits
- slice 1/4: parsers + tests (this commit)

## Gates (copied from output)
- `dotnet test --filter FullyQualifiedName~Fugue.Tests.Import`: Passed! - Failed: 0, Passed: 40, Skipped: 0, Total: 40
- `dotnet build`: 0 Warning(s), 0 Error(s)
- `dotnet format --verify-no-changes`: clean

## What changed in shared files (exactly)
- none in this slice (new files only: `src/Fugue.Domain/Claims/ClaimImport.cs`, `src/Fugue.Application/Features/Import/Parsing/*`,
  `tests/Fugue.Tests/Import/*`); `docs/branch-ledger.md`: the nth-import row only.

## Design so far
- No new package: .xlsx is read as a zip of XML parts (first worksheet, shared strings, date styles); .eml/.mbox by a small MIME
  reader (folding, RFC 2047, multipart, quoted-printable, base64, charset); WhatsApp Android and iOS export lines.
- Every draft carries a server-computed SHA-256 key of its source item (spreadsheet row cells; email Message-ID, else the raw
  message; the claimant's first chat message), the basis of the idempotent create in slice 2.
- Inferred values carry hints ("Market HK routed from the sender domain ..."), for the reviewer to confirm.

## Deviations
- See the final report (`.worktrees/_briefs/reports/nth-import.md`).

## Next slice
- 2/4 API: parse, check and create endpoints, the `claim_imports` ledger (migration), audit, 403 for self-service.
