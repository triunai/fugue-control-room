# claims-core lane status

Branch `claims-core`, rebased onto main (after `AddIdempotencyKeys`). All seven slices of the lane packet are done; nothing is stashed.

| Slice | Subject |
|---|---|
| 1 | feat(domain): Claim aggregate with its state machine |
| 2 | feat(persistence): claims, timeline and info requests (migration `AddClaims`, catalog row 10) |
| 3 | feat(api): claims contract |
| 4 | fix(application): ETag checked before the state machine, plus the workflow/visibility suite |
| 5 | feat(application): claims workload and exposure |
| 6 | feat(infra): seed claims demo data (+ runbook 11, manager@ user) |
| 7 | test(api): Bruno claims collection |
| 8 | feat(api): idempotent claim submit and a flat list filter |

(Shas changed with the rebase: `git log --oneline main..claims-core`.)

Tests at this head: ArchTests 37, Tests 616, build 0 warnings, format clean.

## Left / open
- Nothing left from slices 4-7. Not done: regenerating the Angular client (frontend lane), a second run of the Bruno suite after the rebase (last green run was 74/74 before it; the sign-in limiter allows 20 per minute per IP, so wait a minute between full runs).
- Deviations: event names `claim.info.requested.v1` / `claim.info.provided.v1` (fitness rule forbids underscores); assigned-officer guard also covers the reserve; commands answer 200 + claim + ETag; median days to decision is over the last 30 days.
- Codex review of the state machine, visibility and permissions seam is still to do (lane 3 of the plan).
- Untracked `pnpm-lock.yaml` / `pnpm-workspace.yaml` in the worktree are not mine.
