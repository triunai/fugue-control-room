# Lane nth-notifydoc status (docs only, design only)

## Commits
- docs(design): notifications, attachments and the settlement saga, design only

## Gates
- Docs only: no code, no tests run. `cmp docs/explained/index.html web/public/explained/index.html` -> identical.

## Shared files changed (exactly)
- `README.md`: one clause appended to the "Not built, design only and parked" sentence (line 55).
- `docs/explained/index.html` and `web/public/explained/index.html`: the one "Not built" notifications row gains "Design only: `docs/design/notifications.md`" as code text (the page links no repo docs, so no href).

## Deviations
- `docs/branch-ledger.md` has no row for this lane, so it is untouched.
