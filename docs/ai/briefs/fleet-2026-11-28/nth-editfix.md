# Lane nth-editfix — Sonnet — worktree .worktrees/nth-editfix, base main fe7845a (nth-hybridcache merged) — FIX (13:00-14:00 window), READY by 13:35
Read HEADER.md (incl. Addenda) and OWNERS.md (Addendum 15). Ports web 4434 / api 5235 / pg 55535.

Defect (integrator, live on :8080 after merge c7b1234): a user whose access comes only from a group (seeded
`support@fugue.test`, group SUPPORT) cannot be saved from Edit user without adding a direct role: the `atLeastOneRole`
rule copied from create rejects an empty direct-role list. Editing just the display name of a valid existing user fails.
D-080 records it as a consequence; this lane fixes it.

Fix (smallest honest change):
- API `UpdateUser`: an empty direct-role list is valid on EDIT when the user is a member of at least one active group
  (they keep access through the group); otherwise keep the 400 (a user with no role and no group would be locked out of
  everything — keep that guard). Create is unchanged (a new user has no groups yet). Self-lockout, escalation,
  If-Match, grant_audit unchanged. Keep nth-hybridcache's /me invalidation line intact.
- Web drawer (edit mode): the at-least-one-role validation matches the server (allow empty when the row shows groups;
  show a hint "Access comes from group SUPPORT"). Add mode unchanged.
- Tests: handler unit tests (group-only user, empty roles → 200; no groups, empty roles → 400), one integration test,
  a drawer spec; one Bruno request in `bruno/fugue/users/` (edit support@'s display name with roles [] → 200, then
  restore it) using the collection's per-run token cache pattern (see nth-brunoci).
Gates: HEADER .NET + web gates, full suite once under the lock (~2 min now), Bruno users folder against your own API,
headless check: edit support@ name on your API, 0 console errors. Commit `fix(api): ...` + `fix(web): ...` each with tests.
Proposed D-row text: amend D-080 — "edit allows no direct role when the user keeps access through a group".
