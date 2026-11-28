# Lane nth-searchhint status (owner ask: psst hint after a report)

## Commits
- `feat(web): after a report, a psst hint points at the search bar` (single slice)

## Gates (copied from output)
- `npm run lint`: All files pass linting
- `npm test`: 61 files, 402 tests passed
- `npm run build`: Application bundle generation complete

## Shared files changed
- `web/src/app/features/shell/tour.service.ts` (new public `hint`), `shell/global-search.ts` (`data-tour="search"` only),
  `claims/report-page.ts` (success path), two new specs.

## Deviations
- No `docs/branch-ledger.md` row exists for this lane, so none was touched.
