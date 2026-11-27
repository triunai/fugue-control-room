# Lane nth-formsfix status (Refs D-064, item 5 follow-up)

## Commits
- slice 1: test(web): e2e locators for the claimant next-step card (claims.spec.ts, 06-claims-flow.spec.ts)

## Gates
- pending (slice 2)

## Shared files changed
- `web/e2e/claims.spec.ts`, `web/e2e/06-claims-flow.spec.ts`: two locators made exact (`getByText('...', { exact: true })`).

## Deviations
- `docs/branch-ledger.md` had no row for this lane; one was added.

## Next
- slice 2: queue Market/Line filters on fg-select, calendar day jump on fg-date.
