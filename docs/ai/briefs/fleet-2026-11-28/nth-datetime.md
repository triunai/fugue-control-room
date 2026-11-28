# Lane nth-datetime (item 5 follow-up) — Sonnet — worktree .worktrees/nth-datetime, branch nth-datetime, base 8ed5cce
Ports: web 4413. Read HEADER.md (incl. Addenda) and OWNERS.md (Addendum 5) first.

Objective: no native OS date/time popup is left in any console form: the three `datetime-local` inputs use the console
date control with a time part.

Current state (verify): `fg-date` (`web/src/app/ui/date-picker.ts`, ControlValueAccessor, ISO date value, typed field +
keyboard calendar popover on CDK overlay, specs incl. a blur-guard mutation check) has no time part. Native `datetime-local`
remains in the access overrides form ("Until") and the audit filter ("From", "To") — find them with grep.

Desired state: `fg-date` gains an opt-in time mode (e.g. `withTime` input): a typed `HH:mm` segment next to the date (no
popup; validates 00:00-23:59), value in the SAME format the current `datetime-local` binding produces today (check what the
forms send to the API — local time vs UTC/offset — and keep it byte-identical so the API sees no change). Default mode
unchanged: every existing fg-date usage and spec stays green. Swap the three inputs.

Acceptance: specs for time mode (typing, invalid time, value format round-trip equal to the old native value, keyboard);
existing date-picker specs unchanged and green; headless as admin on your dev server proxied to :8080: set an override Until
date+time and save (then remove it), filter the audit log by From/To and see results change; 0 console errors; grep shows no
`datetime-local` left in features/; web gates green.
Non-goals: no time-zone redesign, no popup time picker, no other pages, no backend. nth-formsfix is editing the claims queue
and calendar pages in parallel; do not touch them.
Commits: 1 fg-date time mode + specs (feat(web)), 2 swap the three inputs (feat(web)); status file + own ledger row.

Recite: opt-in time mode, value format unchanged for the API, three inputs swapped, defaults untouched, 0 native popups.
