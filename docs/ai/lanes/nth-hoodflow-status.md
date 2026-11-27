# Lane nth-hoodflow status (Refs D-064, item 11e final part)

## Commits
- `84ac925` slice 1: report, claim detail and assessment flows
- slice 2: home, queue, my claims, team, reserve sheet, calendar flows (see `git log nth-hoodflow`)

## Gates (copied from output)
- hood-notes spec: 55 tests passed, all nine claims routes in `withFlow`; injecting a non-existent path made it fail (1 failed), restored.

## Shared files changed (exactly)
- `web/src/app/features/hood/hood-notes.spec.ts`: the `withFlow` list only.
- `docs/branch-ledger.md`: own row added.

## Deviations
- Ledger row for this lane was missing on the base; added.
- Reserve sheet route is `claims/sheet` (not `claims/reserve-sheet`).

## Next
- Full web gates and headless proof (in the lane report).
