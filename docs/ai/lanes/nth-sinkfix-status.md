# Lane nth-sinkfix status (owner-reported fix, D-064)

## Commits
- fix(web): the sink tab matches its claim.* subscription

## Gates
- `npm run lint`: All files pass linting
- `npm test`: 61 files, 404 tests passed
- `npm run build`: Application bundle generation complete
- Headless (dev server 4438 -> :8080, view only): 0 console errors, screenshot `_briefs/reports/nth-sinkfix.png`

## Shared files changed
- None. Only `web/src/app/features/webhooks/sink-tab.{ts,html,spec.ts}`.

## Deviations
- Step 3 no longer claims the customer id is the saga (claim events were not verified to carry one); worded generically.
- No branch-ledger row exists for this lane, so the ledger is untouched.
