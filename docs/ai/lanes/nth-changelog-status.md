# Lane nth-changelog status (Refs D-064)

## Commits
- slice 1: generator parses CHANGELOG.md release notes and closed work items; `gen:changelog` script (`gen:decisions` kept as alias)
- slice 2: web: Decisions page becomes Changelog (`/changelog`, `/decisions` redirects), how-versioning-works explainer

## Gates
See the final report; filled in at slice 2.

## Shared files changed exactly
- `web/src/app/app.routes.ts`: route `decisions` renamed `changelog`; one `redirectTo` line for `decisions`.
- `web/src/app/features/shell/nav.ts`: label and path of one nav item.
- `web/package.json`: `gen:changelog` added, `gen:decisions` now calls it.

## Deviations
- No row for this lane exists in `docs/branch-ledger.md`, so none was edited.

## Next
Headless proof and report.
