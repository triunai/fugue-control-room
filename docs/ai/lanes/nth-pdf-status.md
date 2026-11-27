# Lane nth-pdf status (Refs D-064, overnight item 11b)

## Commits
- slice 1: `feat(api): sample claim letter template and PDF writer` (this commit)

## Gates (copied from output)
- `dotnet build`: Build succeeded. 0 Warning(s) 0 Error(s)
- `dotnet format --verify-no-changes`: exit 0
- `dotnet test --filter Fugue.Tests.Letters`: Passed! Failed: 0, Passed: 15, Skipped: 0, Total: 15
- Rendered both letters and opened them with poppler (`pdftotext -layout`, `pdfinfo`: no parse errors) and macOS `sips` (PNG)

## Decision: how the PDF is made
- Hand-written single-page PDF 1.4 (`ClaimLetterPdf`), standard Type 1 fonts (Times-Bold heading, Helvetica body), no new dependency.
- Rejected: QuestPDF (dual licence; Community licence v3.0, effective 6 July 2026, is free only for individuals, learning and
  evaluation, charities, and organisations under USD 1M revenue; a reviewing insurer adopting the code would need a paid licence),
  PDFsharp (MIT up to 6.2.4, AGPL-3.0-only from 6.5.0 and 7.0.0-preview-2 on nuget.org), and a client-side generator (needs a new
  npm package). Any of them is also a new dependency, which the lane contract says to stop for.

## What changed in shared files (exactly)
- `docs/branch-ledger.md`: this lane's own row (copied from main) with its Status cell.

## Next slice
- 2: `GET /api/v1/claims/{id}/letter` (scope: same as `GET /claims/{id}`; undecided: 409), integration tests, Bruno, OpenAPI.
