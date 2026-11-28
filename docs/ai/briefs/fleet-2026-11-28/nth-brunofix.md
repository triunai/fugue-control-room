# Lane nth-brunofix (demo hygiene) — Sonnet — worktree .worktrees/nth-brunofix, branch nth-brunofix, base 38348a3
Read HEADER.md (incl. Addenda) and OWNERS.md (Addendum 7) first. No web or .NET change; no node_modules needed (use `npx bru`
from `bruno/fugue`; if npx needs packages, use the main checkout's `bruno/fugue` node setup as runbook 00 describes).

Objective: running the full Bruno suite live during the demo no longer adds a fake officer to Team and exposure.

Current state (verify): `bruno/fugue/users/04-admin-creates-user.bru` creates "Bruno Created" (unique bruno-<ts>@fugue.test) with
roles ["STAFF"], asserts `roles` eql ["STAFF"], then signs in as that user (200). `05-duplicate-email-409.bru` re-posts the same
email uppercased with ["STAFF"] and expects 409 user.email_taken. A STAFF user then shows as a row on /claims/team.

Desired state: both requests use `roles: ["SELF_SERVICE"]` (verify the exact role code in the seed / roles API), the 04 assertion
and test title follow ("201 with Location, the self-service role and no password"), display names say what they are
("Bruno test claimant"). Every other assertion unchanged (201, Location, no password, sign-in 200; 409 code).

Acceptance: `cd bruno/fugue && npx bru run users --env compose` against :8080 → all requests/tests pass (paste the summary);
then check /api/v1/claims/workload (or the Team page data) as manager does NOT list the new user. If the login limiter bites
(429), wait 60 s and rerun. One commit `test(bruno): users suite creates a self-service user, not a staff officer` + status file.
Non-goals: no other Bruno folders, no API change, no cleanup endpoint.

Recite: two .bru files, role SELF_SERVICE, same assertions otherwise, users folder green, no new row on Team.
