# Lane nth-paid (item 11c, backlog #009 part) — Opus — worktree .worktrees/nth-paid, branch nth-paid, base f3d7637
Ports web/api/pg: 4410 / 5210 / 55510. Read HEADER.md (incl. Addendum 1) and OWNERS.md first.

Objective: a manager on Team and exposure sees what has actually been paid out (settled), per market and in total, next to
the outstanding reserve and approved-not-settled figures — the gap a panel reviewer flagged (backlog #009).

Current state (verify): `GET /api/v1/claims/exposure` (`src/Fugue.Api/Controllers/ClaimsController.cs:72`) →
`GetClaimExposureHandler` (`src/Fugue.Application/Features/Claims/Queries/GetClaimExposure.cs`) → `IClaimReports` (EF raw SQL
under `src/Fugue.Infrastructure/Persistence/Sql/**`) → `ClaimExposureDto(AsOf, Rows, Totals)` with `ExposureRowDto` and
`ExposureTotalDto` (`src/Fugue.Application/Features/Claims/ClaimReportDtos.cs:38-50`). The team page
(`web/src/app/features/claims/team-page.ts` ~line 211) shows "outstanding reserve, with approved-not-settled under it", each
market in its own currency. Find where the settled amount is stored (settle command / claim columns) — verify, never assume.

Desired state: exposure rows and totals gain a settled (paid) figure — sum of settled amounts per market in that market's
currency, plus a count; optionally a time window if the existing DTO has one (do not invent one). Additive DTO change only
(new fields; existing clients unaffected); OpenAPI snapshot + generated client regenerated. The team page shows it in the same
cell/row pattern with a clear label ("Paid (settled)"), and the indicative USD view (if the page has one) includes it only if
that view's existing conversion applies cleanly — otherwise leave USD alone and say so.

Allowed files: the exposure query/handler/DTO/SQL and their tests, `bruno/fugue/claims/` exposure request assertions,
`web/src/app/features/claims/team-page.*` (NOT `team-page.hood.ts`: nth-hoodb owns it), the regenerated OpenAPI/client files,
your status file + own ledger row. Nothing else.

Acceptance: integration test: settle two claims in one market (and one in another) → exposure shows the right paid sums and
counts per currency; manager 200, staff/claimant 403 unchanged; Bruno exposure request asserts the new fields against your API
on 5210; headless as manager@fugue.test on your dev server proxied to your API: the figure renders, 0 console errors,
screenshot; full suite once under the lock.
Non-goals: notifications (#009 part 2 — the integrator records a decision), claimant SLA calendar (by design), any other page.
Stop if: settled amounts are not stored anywhere reliable (report the gap instead of deriving them from audit text).
Commits: 1 query + DTO + tests, 2 OpenAPI/client regen + Bruno, 3 team page.

Recite: settled (paid) per market and total, additive DTO, team page shows it, scoped as before, tested, nothing invented.
