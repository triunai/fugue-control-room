# Lane nth-useredit — Opus — worktree .worktrees/nth-useredit, base = main AFTER nth-refactor merged
Read HEADER.md (incl. Addenda) and OWNERS.md (Addendum 12). Ports web 4432 / api 5232 / pg 55532. HARD STOP 12:50: READY or
BLOCKED by then, whatever is committed and green ships; unfinished parts go in the report as a proposed D-row.
Owner (11:2x): "users can't be edited"; "show password" toggle on Add user; reuse the Add-user drawer; upsert?

Premises to verify first (rule 1): `src/Fugue.Api/Controllers/UsersController.cs` has GET list, GET {id}, POST only;
`Features/Users/{Commands,Models,Queries,Repos}` is the post-refactor layout; CreateUserCommand does the role-escalation
check (`user.role_escalation`) and writes grant_audit; the web Add-user drawer lives in `web/src/app/features/access/**`.

Slices (commit each when its gates pass):
1. API: `PUT /api/v1/users/{id}` with `If-Match` (ETag from the user's xmin, same pattern as the other ETag'd PUTs — find
   one and copy it, incl. 428 when If-Match is missing if that is the house rule, 412 on mismatch). Body: displayName, roles,
   active (only if an existing field; do not invent). Email is READ-ONLY (not in the body). USERS_MANAGE. The SAME
   escalation rule as create for any role ADDED; the caller cannot remove their own last USERS_MANAGE-granting role
   (lock-out guard) — if that rule is too big, record it as a D-row instead. Role grant changes go to grant_audit; the
   command is audited + outboxed in the same transaction like create. GET {id} returns the ETag header.
   Tests: handler unit tests (happy, 404, 412, escalation 403, validation 400) + one integration test; ArchTests stay 37.
   OpenAPI regen (`npm run api:openapi && npm run api:gen`) committed. Bruno `bruno/fugue/users/` new requests: edit OK,
   stale If-Match 412, escalation 403.
2. Web: the Add-user drawer becomes add/edit: an Edit action per row in the users table opens the same drawer prefilled
   (email shown read-only, no password field in edit), saves via PUT with the row's ETag, shows 412 as "changed by someone
   else, reload". Add mode gets a show/hide password toggle (button with aria-pressed + aria-label, not a checkbox hack).
   TanStack Query invalidation of the users list on success. Specs for the drawer modes.
3. Audit only (no fixes): which console entities lack add and/or edit (users, groups, roles, overrides, webhooks,
   customers, modules). Table in your report: entity | list | add | edit | delete | API supports edit? | gap.

D-row to propose (report, integrator records): "Edit user is PUT + If-Match, not upsert" — ids are server-generated
UUIDv7, create needs a password + escalation check, update must not reset a password; idempotent creates already show
the retry-safety idea (ADR-013). Upsert would merge two authorization paths into one ambiguous endpoint.

Gates: HEADER .NET + web gates, full suite once under the lock (on battery a green counts; FM36 for Docker reds),
Bruno against your own API on 5232 (`--seed`), headless Playwright: admin edits a user's display name + role, reload shows
it, console errors 0, screenshots `reports/nth-useredit-*.png`. Report + READY line per HEADER.
Merge note: nth-hybridcache (parallel) caches GET /me; it will add an invalidation call in YOUR update handler after you
merge — expose nothing special, just keep the handler in `Features/Users/Commands/`.
