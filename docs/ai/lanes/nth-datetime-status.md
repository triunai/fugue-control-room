# Lane nth-datetime status (item 5 follow-up: date + time)

## Commits
- slice 1: `fg-date` opt-in `withTime` mode + specs
- slice 2: swap the three `datetime-local` inputs (access override Until, audit From/To)

## Gates
- `npm run lint`: All files pass linting
- `npm test`: 54 files, 349 tests passed
- `npm run build`: complete

## Shared files changed
- `web/src/app/ui/date-picker.ts` + spec; `features/access/access-page.html|ts`; `features/audit/audit-page.html|ts`
- `docs/branch-ledger.md`: own row only

## Deviations
- The ledger had no `nth-datetime` row; added one.

## Next
- Done. Headless proof: access override PUT body expiresAt 2030-01-15T05:45:00.000Z for 13:45 local (+08), identical to native path; audit From/To 2099 range -> 0 rows; 0 console errors; screenshots in .worktrees/_briefs/reports/nth-datetime-*.png.
