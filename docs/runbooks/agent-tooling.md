# Agent tooling: an MCP for exploring, the CLI for working fast

A demo script, about ten minutes. It shows how an engineer lets an AI agent look at the data safely (a read-only Postgres MCP server) and which command-line tools do the fast, repeatable work (psql, Bruno CLI, headless Playwright). Needs the Docker stack from [00](00-for-reviewers.md) running, Node 24 and [uv](https://docs.astral.sh/uv/) (`uvx`) for the MCP server.

Run from the repo root. Load the settings once per shell (`set -a; source .env; set +a`).

## 1. The role the stack creates

`docker-compose.yml` mounts `scripts/db/docker-initdb-readonly.sh` into the Postgres container's `/docker-entrypoint-initdb.d/`. On an EMPTY volume (first start, or after `docker compose down -v`) it runs `scripts/db/fugue-readonly-role.sql`: a `fugue_readonly` login role that can `SELECT` and cannot write, and whose sessions are read-only transactions (`default_transaction_read_only = on`). The script runs before the EF migrations exist, so it also sets `ALTER DEFAULT PRIVILEGES`: every table the migrating owner (`fugue`) creates later is readable too.

The password is `FUGUE_READONLY_PASSWORD` from `.env`, default `fugue_readonly_dev`. It is a local-development value, not a secret. Check:
```bash
psql "postgresql://fugue_readonly:fugue_readonly_dev@localhost:${POSTGRES_PORT:-5432}/fugue" -c "select count(*) as claims from claims"
```
Expect one row with a count (20 on a freshly seeded stack, more once you have created claims).

A volume that already existed before this was added has no role (initdb does not rerun). Create it once by hand, see [03](03-local-database.md#sql-files-migration-script-read-only-access-for-agents), or recreate the stack with `docker compose down -v && docker compose up -d --build`.

## 2. Part A, the MCP server: let an agent explore

**What it is for.** An agent that has to answer "what does the data look like?" or "why is this query slow?" needs to read the database. The MCP server `postgres-readonly` (registered in `.mcp.json`) gives it typed tools (`list_schemas`, `list_objects`, `get_object_details`, `execute_sql`, `explain_query`, `analyze_db_health`) instead of a raw connection string and a prayer.

**Two layers of defence, so one mistake is not enough**
1. The database refuses writes. `fugue_readonly` holds only `SELECT`, and its transactions are read-only. This holds even if the MCP server were replaced by something careless.
2. The server runs with `--access-mode=restricted`: it parses each statement and rejects anything that is not a read.

Prove layer 1 with no MCP involved (each line must fail):
```bash
U="postgresql://fugue_readonly:fugue_readonly_dev@localhost:${POSTGRES_PORT:-5432}/fugue"
psql "$U" -c "insert into customers(id) values (gen_random_uuid())"
psql "$U" -c "update customers set name = 'x'"
psql "$U" -c "create table t(i int)"
psql "$U" -c "SET default_transaction_read_only = off; insert into customers(id) values (gen_random_uuid())"
psql "$U" -c "BEGIN READ WRITE; insert into customers(id) values (gen_random_uuid()); COMMIT"
```
Expect `cannot execute INSERT in a read-only transaction`, `cannot execute UPDATE ...`, `cannot execute CREATE TABLE ...`, again `cannot execute INSERT ...` (turning the setting off inside the session does not help), and last `permission denied for table customers` (even a read-write transaction has no `INSERT` privilege).

**Start it.** In Claude Code the repo's `.mcp.json` registers it; it reads the URI from your environment, nothing secret is in the file. Put the URI in `.env` (it is in `.env.example`; change the port if you changed `POSTGRES_PORT`), load it, and approve the server on first use:
```bash
set -a; source .env; set +a
echo "$FUGUE_READONLY_DATABASE_URI"
claude mcp list        # first time: "Pending approval" - run `claude` once and approve it; afterwards it shows Connected
```
The server is pinned (`postgres-mcp==0.3.0`) with `scripts/db/postgres-mcp-overrides.txt`: that release pins `pglast==7.2.0`, which PyPI no longer serves, and needs the `mcp` 1.x API. Without the overrides `uvx postgres-mcp` fails to install or crashes on import. To start it by hand, outside any agent:
```bash
DATABASE_URI="$FUGUE_READONLY_DATABASE_URI" uvx --overrides scripts/db/postgres-mcp-overrides.txt --from postgres-mcp==0.3.0 postgres-mcp --access-mode=restricted
```
It speaks MCP over stdin/stdout, so it waits silently for a client; stop it with Ctrl-C. The first start downloads the packages (about a minute).

**Three questions to put to the agent** (it answers by calling the tools, you can watch each SQL statement):
1. "List the tables in the public schema and tell me which ones hold claims data." (`list_objects`)
2. "How many claims are in each status, and which status has the most?" (`execute_sql`: `select status, count(*) from claims group by 1`)
3. "Which audit actions are most common, and what columns does the table have?" (`execute_sql` on `audit_log`, then `get_object_details`)

Then show the refusal: ask it to "set every claim to Settled". The restricted server rejects the statement (`Error validating query`), and if it did not, the database would.

## 3. Part B, the CLI: the fast loop

**psql** answers exact questions in one line and can be scripted. Same role, same refusals:
```bash
psql "$FUGUE_READONLY_DATABASE_URI" -c "select status, count(*) from claims group by 1 order by 2 desc"
psql "$FUGUE_READONLY_DATABASE_URI" -c "select action, count(*) from audit_log group by 1 order by 2 desc limit 5"
```
Every runtime SQL statement of the API is a file under `src/Fugue.Infrastructure/Persistence/Sql/`; paste one into psql after replacing the `@name` parameters.

**Bruno CLI** runs the HTTP regression suite against the stack (`npm ci` once at the root):
```bash
cd bruno/fugue
npx bru run --env compose                    # the whole suite, 105 requests
npx bru run claims --env compose             # one folder (try auth, audit, webhooks, idempotency)
npx bru run --env compose --env-var baseUrl=http://localhost:5081   # against another API port
```
Expect `Status ✓ PASS` and every request passed (last measured: 105 requests, 127 tests). The sign-in limiter allows 20 sign-ins per IP per minute and the full suite signs in often, so wait a minute before running it a second time. Folders are independent, which is why one folder is the fast loop while you work on one feature.

**Headless Playwright** drives the real console without a window. Chromium once: `cd web && npx playwright install chromium`. The repo's journeys are `web/e2e/*.spec.ts` (`cd web && npm run e2e`, runbook [09](09-console.md); needs the API on 5081 and the dev server). For a quick check of one screen write a short script. This one signs in as the manager, opens the claims queue, counts console errors and saves a screenshot (point `BASE_URL` at the console, default the Docker stack on 8080):
```bash
cd web
SHOT=/tmp/queue.png node --input-type=module <<'JS'
import { chromium } from '@playwright/test';

const base = process.env.BASE_URL ?? 'http://localhost:8080';
const browser = await chromium.launch({ headless: true });
const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
const errors = [];
page.on('console', (m) => m.type() === 'error' && errors.push(m.text()));
page.on('pageerror', (e) => errors.push(String(e)));

await page.addInitScript(() => localStorage.setItem('fugue.tour.seen.manager', '1'));
await page.goto(`${base}/sign-in`);
await page.getByLabel('Email').fill('manager@fugue.test');
await page.getByLabel('Password').fill('Fugue-Dev-Passw0rd!');
await page.getByRole('button', { name: 'Sign in', exact: true }).click();
await page.waitForURL((u) => !u.pathname.includes('sign-in'));
await page.goto(`${base}/claims/queue`);
await page.waitForLoadState('networkidle');
console.log('url:', page.url(), '| heading:', await page.getByRole('heading', { level: 1 }).first().innerText());
await page.screenshot({ path: process.env.SHOT ?? 'queue.png' });
console.log('console errors:', errors.length);
await browser.close();
process.exit(errors.length ? 1 : 0);
JS
```
Expect `url: http://localhost:8080/claims/queue | heading: Claims queue`, `console errors: 0`, exit code 0, and the screenshot at `/tmp/queue.png`. It only reads, so it is safe against the demo stack.

**CLI or MCP?**

| You want | Use |
|---|---|
| to explore an unfamiliar schema, or ask in plain language | MCP: the agent picks the tool and shows its work |
| an exact, repeatable answer you can paste into a ticket | psql: the output is the output |
| a check that runs in CI or a hook | CLI: `bru run` and `playwright` exit non-zero on failure; an MCP session cannot |
| speed in a loop (edit, run, look) | CLI: no model round trip per step |
| to review what the agent did | either, but MCP calls are logged as tool calls in the transcript |

Rule of thumb: the MCP is for questions, the CLI is for anything you will run more than once.

## 4. The safety story

- Local, throwaway database only. `fugue_readonly` can read every table, including refresh-token digests and stored idempotency bodies. Never create it on a shared or production database and never put production credentials in `.env`.
- No write path. The role has no write privilege and read-only transactions, the MCP server is in restricted mode, and the connection string in `.mcp.json` is an environment reference, not a value.
- Secrets stay out of git. `.env` is untracked, `.env.example` carries a dev-only default marked `secrets-scan:ignore`, and the commit hook runs `scripts/spine/secrets-scan.sh` over every staged change.
- Rotate or remove: re-running `scripts/db/fugue-readonly-role.sql` with a new `ro_password` rotates the password; `drop owned by fugue_readonly; drop role fugue_readonly;` removes the role.
