# Lane nth-tooling (items 17a) — Sonnet — worktree .worktrees/nth-tooling, branch nth-tooling, base dacfb06

Objective: the owner can demo, live, how engineers use an MCP safely and the CLI to work fast: a read-only Postgres MCP
through a least-privilege role, plus psql / Bruno CLI / headless Playwright for fast backend and frontend loops — and every
command in the runbook works on a freshly recreated demo stack.

Current state (relay checked 03:18; verify): `.mcp.json` defines `postgres-readonly` = `uvx postgres-mcp
--access-mode=restricted` with `DATABASE_URI=${FUGUE_READONLY_DATABASE_URI}`. `scripts/db/fugue-readonly-role.sql` creates
`fugue_readonly` (SELECT only, default_transaction_read_only). The role does NOT exist in the fugue-demo database;
FUGUE_READONLY_DATABASE_URI is in neither `.env` nor `.env.example`; docker-compose.yml (postgres:17-alpine) does not run the
script, so `docker compose -p fugue-demo down -v` (the 14:00 recreate) would lose a hand-made role. Runbook 03 documents the
role; runbook 00 and README show `npx bru run --env compose`.

Desired state:
1. The stack creates the role itself on a fresh volume (initdb mount `/docker-entrypoint-initdb.d/` or an idempotent startup
   step — pick what fits; note the initdb script runs before EF migrations create tables, so grants must cover tables
   created later: `ALTER DEFAULT PRIVILEGES` for the migrating owner, or a grant step after migrations — verify which works).
   The role's password comes from env with a dev default, never a real secret.
2. `.env.example` has `FUGUE_READONLY_DATABASE_URI` pointing at the compose Postgres with the role.
3. New `docs/runbooks/agent-tooling.md`, written as a demo script: (a) MCP: what it is for (agent exploring data), the two
   layers of defence (DB role refuses writes; restricted mode on top), how to start it, 3 example questions; (b) CLI: psql via
   the same role, `bru run` for API regressions (whole suite and one folder), the headless Playwright check scripts for UI;
   when to choose CLI over MCP (speed, scriptable, CI, exact output); (c) the safety story (local throwaway DB only, no prod
   credentials, no write path, secrets scan). Copy-paste commands that actually work.
4. Links from README (one short "Agent tooling" paragraph) and runbook 00.

Acceptance (binary): on a throwaway Postgres you start from the compose definition or `postgres:17-alpine` on port 55501
with the same init: role exists; `SELECT count(*) FROM <a real table>` works as fugue_readonly AFTER migrations ran;
INSERT, UPDATE, CREATE TABLE and `SET default_transaction_read_only=off; INSERT` are all refused; `uvx postgres-mcp
--access-mode=restricted` starts with that URI and answers a list-tables / simple query (drive it via its stdio protocol or
the MCP inspector CLI — show the output); every runbook command was executed and its output pasted.

Do NOT recreate or modify the running fugue-demo stack (the integrator does that after merge and re-runs your runbook).

Non-goals: no app code, no new MCP servers, no changes to Bruno requests themselves, no CI changes.
Stop if: the init approach needs an app code change (report the design instead), or uvx/postgres-mcp cannot run offline.
Commits: 1 stack + env (compose/scripts/.env.example), 2 runbook + links.

Recite: role created by the stack, survives down -v, writes refused at the DB, MCP answers, runbook commands proven.
