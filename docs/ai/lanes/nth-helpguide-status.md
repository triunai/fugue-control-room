# Lane nth-helpguide status (owner ask, Addendum 19)

## Commits
- slice A: feat(web): Help opens a guide to the app by module
- slice B+C: fix(web): Kafka shown as an optional adapter that is off; API v1 labelled

## Gates (web)
- `npm run lint`: All files pass linting
- `npm test`: 59 files, 397 tests passed
- `npm run build`: Application bundle generation complete

## Shared files changed
- `web/src/app/features/shell/shell.ts`: `help()` opens `HelpDialog` via `Overlays.modal` (the tour moved to the dialog's button).
- `web/src/app/features/shell/shell.html`: env chip label `dev . .NET 8 . API v1`.
- `web/src/app/features/shell/stack-info.ts`: Kafka note says optional adapter, OFF.

## Deviations
- No `docs/branch-ledger.md` row exists for this lane, so the ledger is untouched.
