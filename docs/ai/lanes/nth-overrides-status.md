# Lane nth-overrides status (Refs D-064)

## Commits
- feat(web): explain the override formula on the access page (this commit)

## Gates (copied from output)
- `npm run lint`: All files pass linting.
- `npm test`: Tests 368 passed (368)
- `npm run build`: Application bundle generation complete.
- Headless on :4426 proxied to :8080 as admin: explainer renders, Until hint present, 0 console errors.

## Shared files changed
- None outside access-page.{html,ts,css}, access-page.hood.ts; this status file.

## Deviations
- hood note capped at 4 `how` lines by hood-notes.spec: the If-Match line was folded out (still in `flow`).
- No row for this lane in docs/branch-ledger.md, so none updated.
