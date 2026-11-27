# Lane nth-fixes (items 1b, 3a, 7a, 11f + claimant wording) — Sonnet — worktree .worktrees/nth-fixes, base dacfb06

Objective: five owner-reported defects are gone, each proven headless.

1. (1b) Owner saw "Open claim" (Closest-to-SLA card on Home) and "Pick up" (Pick up next) not navigate — probably a stale tab
   from before a redeploy. Prove it in a fresh signed-in, reloaded headless session (staff@fugue.test) on the demo :8080
   and on your dev server: both must land on `/claims/<id>`. If both pass, no code change; say so. If one fails, fix it.
2. (3a) Users & access → "Roles and permissions" matrix: granted cells show a navy dot. Make them the check icon from
   `web/src/app/ui/icon.ts`, aria-label/title "Granted"; empty cells get sr-only "Not granted".
3. (7a) Claim page header (navy banner): the Handling (Reported→Picked up→Decided→Closed) and Payout (Reserved→Approved→Paid)
   step tracks draw the connector line PAST the last node to the track's right edge. The line must end at the last node.
   Check that "4 of 4"/"3 of 3" counters do not collide with the line; check partly-complete tracks and 390px width.
4. (11f) /modules as admin, after toggling AUDIT off then on: the "Last changed" cell for a row (seen on Claims) stuck at
   "…"; during the off window something kept calling `GET /audit?limit&entityType&entityId` and got 404s. The Last-changed
   column likely fetches audit per row: when AUDIT is off it must not spin forever — show a plain fallback (e.g. "—" with a
   title "audit is off") and stop requesting; when back on, refresh. (The floating "/audit?…" tooltip over the page header
   belongs to the hood sidebar: NOT yours, nth-hooda owns it.)
5. Claimant wording: the "How a claim moves" modal shows the staff term "Reserved" to claimants. Claimants must see claimant
   wording (check what the claimant's own track already calls that step and match it).

Acceptance: headless script covering all five (staff, admin, self@fugue.test), screenshots per fix, 0 console errors; specs
updated/added for 2, 3, 4, 5; web gates green.
Non-goals: no other page restyles, no hood sidebar, no forms, no backend change (if 4 needs one, stop and report).
Commits: one per fix (fix(web): ...), 1b only if code changed.

Recite: dots→checks, track line ends at last node, modules audit fallback + no polling while off, claimant wording, 1b proven.
