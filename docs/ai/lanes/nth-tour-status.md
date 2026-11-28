# Lane nth-tour status (Refs D-064)

## Commits
- slice 2: fix claim/team step selectors (data-tour hooks), fix shared cache key between Home and My claims
- slice 1: tour step lists per persona (admin walks the claim flow first, platform last), welcome starts the persona tour, specs

## Gates (copied from output)
- `npm run lint`: All files pass linting.
- `npm test`: 58 files, 371 tests passed.

## Shared files changed
- `web/e2e/07-shell-screenshots.spec.ts`: three lines (expected step titles, step-loop limit 8 to 25); accepted by dispatcher.

## Deviations
- `docs/branch-ledger.md` has no nth-tour row, so no ledger cell was updated.

## Next
- Headless admin and self passes, claims/mine error root cause, final report.

## Root cause: claims/mine getNextPageParam error
Home (`injectQuery`) and My claims (`injectInfiniteQuery`) both used `claimKeys.list(EMPTY_CLAIM_FILTER)`. Visiting Home then My claims inside the cache window gave the infinite observer plain data without `pages`. No 429 involved. Fix: `claimKeys.listPaged`, spec in `claims.api.spec.ts`. Files: `api/claims.api.ts`, `features/claims/my-claims-page.ts`.
