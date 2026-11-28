# Lane nth-changelog status (Refs D-064)

## Commits
- slice 1: generator parses CHANGELOG.md release notes and closed work items; `gen:changelog` script (`gen:decisions` kept as alias)
- slice 3: fix empty timeline when filtered to release notes; list numbering and spacing in the explainer
- slice 2: web: Decisions page becomes Changelog (`/changelog`, `/decisions` redirects), how-versioning-works explainer

## Gates
- web: lint clean; `npm test` 58 files, 369 tests passed; `npm run build` complete (Initial total 513.28 kB / 132.04 kB)
- scripts: `node --test scripts/gen-decisions.test.mjs` 6 pass, 0 fail
- headless (4424 proxied to the demo API): redirect, nav, explainer, versions, closed items, filters, 0 console errors

## Shared files changed exactly
- `web/src/app/app.routes.ts`: route `decisions` renamed `changelog`; one `redirectTo` line for `decisions`.
- `web/src/app/features/shell/nav.ts`: label and path of one nav item.
- `web/package.json`: `gen:changelog` added, `gen:decisions` now calls it.

## Deviations
- No row for this lane exists in `docs/branch-ledger.md`, so none was edited.

## Next
Done; see the report.
