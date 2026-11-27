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
- Headless proof against :8080, report.
