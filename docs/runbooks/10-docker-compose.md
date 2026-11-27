# 10 - One command: Postgres, migrations, API and console in containers

Goal: `docker compose up -d --build` brings up a migrated, seeded Development API and the Angular console, so the demo path in runbook 09 works with no .NET or Node install. Use it to look at Fugue; for day-to-day coding keep the DB-only flow (runbook 03) and run the API and `ng serve` on the host.

## 1. Prerequisites

- Docker with Compose v2 (OrbStack on macOS: `orb start`; `docker ps` must answer).
- `.env` at the repo root (untracked). Copy `.env.example` and set both secrets:
  ```bash
  cp .env.example .env            # skip if you already have one
  ```
  - `POSTGRES_PASSWORD`: letters and digits only (it is embedded in a connection string).
  - `AUTH_SIGNING_KEY`: `openssl rand -base64 32`. Required by the API container: without it `api` exits at startup with a signing-key error and stays unhealthy. The DB-only `docker compose up -d postgres` does not need it.

## 2. Start

```bash
docker compose up -d --build
docker compose ps
```
Expect `postgres`, `api` and `web` `(healthy)`; `migrate` is not listed because it ran once and exited 0 (`docker compose logs migrate` ends with `Done.`). Order: postgres healthy, then `migrate` (EF migrations bundle), then `api`, then `web`.

| Service | Host port | What |
|---|---|---|
| web | 8080 (`WEB_PORT`) | The console, and `/api/*` proxied to the API: the browser sees one origin, so no CORS |
| api | 5081 (`API_PORT`) | The API directly (Swagger at `/swagger`, Development only) |
| postgres | 5433 or `POSTGRES_PORT` from `.env` | The dev database, same volume `fugue-pgdata` as runbook 03 |

Smoke:
```bash
curl -fsS -o /dev/null -w '%{http_code}\n' http://localhost:8080/                    # 200, the console
curl -fsS -o /dev/null -w '%{http_code}\n' http://localhost:8080/api/v1/diagnostics/ping   # 200 through nginx
```

## 3. Use it

Open http://localhost:8080. The login page lists the seeded accounts (`admin@fugue.test`, `staff@fugue.test`, `support@fugue.test`, `self@fugue.test`) and their shared dev-only password; the table and the demo path are in runbook 09 (skip its sections 1 to 3). Keep `WEB_PORT` at 8080 for the Webhooks > Dev sink step: the console tells the API to post to the page's own origin, which the API container reaches only on its internal port 8080.

The containers run the API with `ASPNETCORE_ENVIRONMENT=Development`, `Seed__Enabled=true` and `Webhooks__AllowInsecureLocal=true`. That is deliberate for a demo stack and is why the console is built with the `docker` configuration (`web/src/environments/environment.docker.ts`: demo accounts shown, same-origin API). Never deploy this stack.

## 4. Stop and reset

```bash
docker compose down        # stops and removes containers; the fugue-pgdata volume (your data) stays
```
Never add `-v` to a project that shares the dev database: it deletes `fugue-pgdata`. To start from an empty database, use a throwaway project name so it gets its own volume:
```bash
docker compose -p fugue-scratch up -d --build     # own containers and volume; set WEB_PORT, API_PORT and POSTGRES_PORT so they do not clash
docker compose -p fugue-scratch down -v           # safe: removes only fugue-scratch_fugue-pgdata
```

## 5. Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| `Cannot connect to the Docker daemon`, `docker ps` hangs | OrbStack/Docker is not running (failure mode 26) | `orb start` (or open Docker), wait for `docker ps`, retry |
| `port is already allocated` / `address already in use` | Something listens on 5081 or 8080 (a host `dotnet run`, `ng serve` on 4200 is fine) | Stop it, or set `API_PORT` / `WEB_PORT` in `.env` |
| `api` unhealthy, logs say the signing key is invalid | Missing, placeholder or too-short `AUTH_SIGNING_KEY` | Generate a real one, `docker compose up -d` |
| `migrate` exits non-zero | Wrong `POSTGRES_PASSWORD` for the existing volume | The password was fixed when the volume was created; restore the old value in `.env` |
| Console shows an empty login list | A `production` build (no demo accounts) was served | `docker compose build web --no-cache`; the compose build uses `--configuration docker` |
| Code changed but the container did not | Images are not rebuilt by `up -d` | `docker compose up -d --build` |
