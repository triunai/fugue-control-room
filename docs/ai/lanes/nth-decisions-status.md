# Lane nth-decisions status (Refs D-064, item 11d)

## Commits
- `cfc198b` feat(web): generate the decision timeline data from the repo docs
- `b80dba3` feat(web): decisions timeline page, route and nav entry
- this status file

## Gates (copied from output)
- `npm run lint`: All files pass linting
- `npm test` (web): 45 files, 270 tests passed
- `npm run build`: Application bundle generation complete
- `npm run test:scripts` (root): pass 88, fail 0 (5 are scripts/gen-decisions.test.mjs)

## What changed in shared files (exactly)
- `web/src/app/app.routes.ts`: one `decisions` route block before `webhooks`.
- `web/src/app/features/shell/nav.ts`: one `Decisions` item in Platform, before Webhooks.
- `web/package.json`: one script, `gen:decisions`.

## Deviations and gaps
- Generator test is a node test under scripts/, not a web spec. No hood note (nth-hooda not merged).
- `docs/branch-ledger.md` has no row for this lane, so no Status cell was updated.
- The JSON must be regenerated (`npm run gen:decisions` in web/) after docs change; no CI drift check.

## Next slice
None; ready for the integrator.
