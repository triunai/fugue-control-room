# Lane nth-fixes status (Refs D-064)

## Commits
- `82545f6` fix(web): granted cells in the roles matrix are check icons (3a)
- `ea91049` fix(web): record banner step track ends at its last node (7a)
- `50858c3` fix(web): modules Last changed never spins, no audit calls while AUDIT is off (11f)
- `3342fcf` fix(web): claimants see Assessed, not Reserved, in How a claim moves
- this commit: test for the 7a CSS rule (the 7a fix commit touched no test) plus this file
- 1b: closed by integrator 592b29a (no code)

## Gates (copied from output)
- `npm test`: 271 passed before this commit; see report for the final count
- `npm run lint`: All files pass linting; `npm run build`: complete, no errors

## Shared files changed (exactly)
- `web/src/app/ui/icon.ts`: `check` added to IconName and PATHS.
- `docs/branch-ledger.md`: no nth-fixes row exists on this base; not touched.

## Deviations
- Stuck "..." root cause was CLAIMS missing from MODULE_ENTITY_IDS (disabled query is pending forever), not only AUDIT-off polling.
- Rule 'every fix commit touches a test': ea91049 did not; covered by the spec added in this commit.

## Next
Nothing; lane complete. Report: `.worktrees/_briefs/reports/nth-fixes.md`.

## Follow-up (integrator finding after merge 3149854)
- Switching AUDIT off fired 6 audit 404s before the list refetched: `invalidateQueries(['modules'])` prefix-matched the still-enabled
  last-changed queries. Fix: `refetchesAfterToggle` predicate skips them when AUDIT is switched off; spec `refetch-after-toggle.spec.ts`.
- Live (dev server :4405 on demo API, admin): AUDIT off -> cells all "—", 0 audit 404s; on -> cells refetch, 0 console errors.
- `npm test`: 278 passed; `npm run lint`: All files pass linting; build clean.
