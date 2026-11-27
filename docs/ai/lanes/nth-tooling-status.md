# Lane nth-tooling status (Refs D-064, item 17a)

## Commits
- `573e0af` feat(db): compose creates the fugue_readonly role on a fresh volume
- `3ab5f5e` docs(runbooks): agent-tooling demo script, linked from README and runbook 00
- this status file and the ledger row

## Gates (copied from output)
- Role created by initdb on a throwaway postgres:17-alpine (55501): `CREATE ROLE`, `fugue_readonly|t`; SELECT works after migrations.
- INSERT, UPDATE, DELETE, CREATE TABLE, `SET default_transaction_read_only=off; insert`: `cannot execute ... in a read-only transaction`; `BEGIN READ WRITE; insert`: `permission denied for table customers`.
- postgres-mcp (restricted) over stdio: 9 tools listed, `select count(*) from audit_log` answered, an INSERT rejected with `Error validating query`.
- Bruno whole suite on a throwaway API: `Requests 105 (105 Passed)`, `Tests 127/127`.
- Headless Playwright script: `heading: Claims queue`, `console errors: 0`.
- No src/, tests/ or web/ change, so no dotnet or web gates.

## Shared files changed (exactly)
- `README.md`: one "Agent tooling" paragraph after "Where things live".
- `docs/runbooks/00-for-reviewers.md`: new section 6, later sections renumbered 7-9.
- `docker-compose.yml`: postgres service only (env var and two read-only mounts).
- `.env.example`, `.mcp.json`.

## Deviations
- `uvx postgres-mcp` was broken upstream (pglast==7.2.0 gone from PyPI, mcp 2.x API); pinned 0.3.0 with `scripts/db/postgres-mcp-overrides.txt`.
- Commit scope `db` is outside the commitlint scope-enum (warning only).

## Next
Integrator: recreate the stack with `down -v` and re-run the runbook. Full detail: `.worktrees/_briefs/reports/nth-tooling.md`.
