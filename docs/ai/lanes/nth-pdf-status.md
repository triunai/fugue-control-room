# Lane nth-pdf status (Refs D-064, overnight item 11b)

## Commits
- `056ea6b` feat(api): sample claim letter template and PDF writer
- slice 2: `feat(api): GET /claims/{id}/letter downloads the sample letter` (this commit)

## Gates (copied from output)
- `dotnet build`: Build succeeded. 0 Warning(s) 0 Error(s)
- `dotnet format --verify-no-changes`: exit 0
- `dotnet test --filter Fugue.Tests.Letters`: Passed! Failed: 0, Passed: 15, Skipped: 0, Total: 15
- `dotnet test --filter Fugue.Tests.Letters` (slice 2, unit + Testcontainers API): Passed! Failed: 0, Passed: 20, Skipped: 0, Total: 20
- `dotnet test tests/Fugue.ArchTests`: Passed! Failed: 0, Passed: 37, Skipped: 0, Total: 37
- `npm run api:openapi`: Passed! 1/1 (snapshot regenerated); `cd web && npm run api:gen`: only `generated/fugue.service.ts` changed
- `npx bru run letters --env local --env-var baseUrl=http://localhost:5206`: Status PASS, Requests 10 (10 Passed), Tests 10/10
- Rendered both letters and opened them with poppler (`pdftotext -layout`, `pdfinfo`: no parse errors) and macOS `sips` (PNG)

## Decision: how the PDF is made
- Hand-written single-page PDF 1.4 (`ClaimLetterPdf`), standard Type 1 fonts (Times-Bold heading, Helvetica body), no new dependency.
- Rejected: QuestPDF (dual licence; Community licence v3.0, effective 6 July 2026, is free only for individuals, learning and
  evaluation, charities, and organisations under USD 1M revenue; a reviewing insurer adopting the code would need a paid licence),
  PDFsharp (MIT up to 6.2.4, AGPL-3.0-only from 6.5.0 and 7.0.0-preview-2 on nuget.org), and a client-side generator (needs a new
  npm package). Any of them is also a new dependency, which the lane contract says to stop for.

## What changed in shared files (exactly)
- `docs/branch-ledger.md`: this lane's own row (copied from main) with its Status cell.
- `src/Fugue.Api/Controllers/ClaimsController.cs`: one action `Letter` (`GET {id}/letter`) and one `using`.
- `docs/api/openapi.v1.json`, `web/src/app/api/generated/fugue.service.ts`: regenerated (one new operation `Claims_Letter`).

## Decision: the endpoint
- `GET /api/v1/claims/{id}/letter`, `CLAIMS_READ`, scope = `ClaimScope.From` exactly as `GET /claims/{id}` (own claim or `CLAIMS_ALL`,
  else 404 `claim.not_found`); undecided = 409 `claim.letter_not_available` (Conflict, like `claim.invalid_transition`).
- The action maps the result itself (`switch` to `File(...)` or the base `Problem(...)`): `ApiControllerBase` has no file helper and
  is a critical seam outside this lane. Proposed follow-up: a `SendFile` helper on the base.
- The letter's correlation id is the id of the request that made it (echoed as `X-Correlation-Id`), labelled "Request correlation id".

## Next slice
- 3: one "Download letter" button in the claim page header actions, shown only for decided claims; headless proof.
