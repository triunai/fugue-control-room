# 00 - For reviewers: start here

Goal: from a clean clone to a running, seeded Fugue (API plus Angular console), the tests green and the API regression suite passing, in about ten minutes. Everything below runs from the repo root.

## Prerequisites

- Docker with Compose v2 (OrbStack on macOS: `orb start`; `docker ps` must answer). This is all you need to run the app.
- .NET 8 SDK (`global.json` pins 8.0.428, rolls forward within 8.0.x): only to run `dotnet test`. The backend tests start Postgres through Testcontainers, so Docker must be running.
- Node 24: only for the console tests and the script and API-suite commands (`npm ci` at the root and in `web/`).

## 1. Secrets

```bash
cp .env.example .env
sed -i.bak "s|REPLACE_ME|$(openssl rand -hex 16)|g; s|GENERATE_WITH_openssl_rand_base64_32|$(openssl rand -base64 32)|g" .env && rm .env.bak
```
This fills `POSTGRES_PASSWORD` (letters and digits only, it is embedded in a connection string) and the signing keys. The `.env` file is untracked; never commit it. Check with `grep -c REPLACE_ME .env` (expect `0`).

## 2. Start the stack

```bash
docker compose up -d --build
docker compose ps
```
Expect `postgres`, `api` and `web` `(healthy)` after a minute or two on first build (`migrate` runs once and exits).

| What | URL |
|---|---|
| Console | http://localhost:8080 |
| API | http://localhost:5081 (ping: `/api/v1/diagnostics/ping`) |
| Swagger | http://localhost:5081/swagger (the stack runs the API in Development) |
| Postgres | `localhost:5432` by default (`POSTGRES_PORT` in `.env`), user `fugue`, db `fugue`, password from `.env` |

## 3. Sign in

The console login page lists the seeded accounts. All share one development-only password, `Fugue-Dev-Passw0rd!`.

| Account | Role | Shows |
|---|---|---|
| `admin@fugue.test` | ADMIN | everything, all rows, module management |
| `staff@fugue.test` | STAFF | all customer operations, all rows |
| `support@fugue.test` | STAFF via group SUPPORT | permissions arriving through a group |
| `self@fugue.test` | SELF_SERVICE | view and edit only their own customer |
| `self2@fugue.test` | SELF_SERVICE | owns a different customer (ownership 404s) |

The claims personas, if present in your checkout, are listed in `docs/runbooks/11-claims.md`. Details of roles and ownership: [05-auth.md](05-auth.md).

## 4. Run the tests

```bash
dotnet test                      # backend (needs Docker for Testcontainers)
npm ci && npm run test:scripts   # repo tooling and doc-spine checks
cd web && npm ci && npm test     # console unit tests
```

## 5. API regression suite (Bruno)

With the stack from step 2 running (the Bruno `local` environment points at port 5080, the `dotnet run` port, so override it for the compose API on 5081):
```bash
npm ci
cd bruno/fugue && npx bru run --env compose
```
Expect every request to pass. `npm run api:test` runs the same suite but against `localhost:5080`; use it only when you run the API on the host (runbook 05).

## 6. Where to read next

- [README.md](../../README.md): what this is and the patterns it demonstrates.
- [docs/ARCHITECTURE.md](../ARCHITECTURE.md): how the pieces fit.
- [docs/HOW-WE-WORK.md](../HOW-WE-WORK.md): the engineering and AI-collaboration process.
- [docs/ai-journal.md](../ai-journal.md): the log of AI-assisted work, including what went wrong.
- [docs/decisions.md](../decisions.md): the decision ledger, with ADRs under `docs/adr/`.
- The timed claims build window: `git log --oneline claims-window-start..HEAD`.
- Other runbooks: [README.md](README.md).

## 7. Troubleshooting

| Symptom | Fix |
|---|---|
| `Cannot connect to the Docker daemon` | Start OrbStack (`orb start`) or Docker Desktop, wait for `docker ps` to answer |
| `port is already allocated` | Something uses 5081, 8080 or the Postgres port. Stop it, or set `API_PORT`, `WEB_PORT` or `POSTGRES_PORT` in `.env` (if you change `API_PORT`, change the Bruno `baseUrl` too) |
| `api` unhealthy, signing-key error in `docker compose logs api` | `AUTH_SIGNING_KEY` is missing or a placeholder; redo step 1, then `docker compose up -d` |
| `429` and "Too many attempts" after repeated sign-ins | The login rate limiter (20 per minute per IP, 5 per email). Wait 60 seconds and retry; do not loop failed sign-ins |
| Old code after pulling | `docker compose up -d --build` (images are not rebuilt by `up -d`) |

More: [10-docker-compose.md](10-docker-compose.md).

## 8. Stop

```bash
docker compose down
```
This keeps your data in the `fugue-pgdata` volume. Add `-v` only if you want an empty database next time.
