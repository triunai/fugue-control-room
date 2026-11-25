# 03 - Local database

Goal: a running local Postgres with the schema applied and sample data, reachable from the API and from a GUI client. Needs runbook 01 done and Docker (OrbStack) running.

Run every command from the repo root. Start each shell with:
```bash
export DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$HOME/.orbstack/bin:$PATH DOTNET_CLI_TELEMETRY_OPTOUT=1 DOCKER_HOST=unix://$HOME/.orbstack/run/docker.sock TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE=/var/run/docker.sock
```
- `DOCKER_HOST` points the docker CLI and Testcontainers at OrbStack's socket. On a machine where `/var/run/docker.sock` does not exist (OrbStack does not always create it), this is required. Verified working on 2026-11-25 with OrbStack and Docker 29.8.2.
- `TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE` tells Testcontainers' Ryuk cleanup container which socket path to mount inside Docker. The tests also pass without it on this setup; keep it, it is harmless and fixes the case where Ryuk cannot mount the host path. Last resort if Ryuk still fails: `TESTCONTAINERS_RYUK_DISABLED=true` (then remove leftover containers yourself with `docker ps -a`).

## 1. Create your `.env`

```bash
cp .env.example .env
```
Edit `.env` (it is gitignored; never commit it): replace `REPLACE_ME` with one password of your choice and put the same value in both places.
```
POSTGRES_PASSWORD=REPLACE_ME
POSTGRES_PORT=5432
ConnectionStrings__Postgres="Host=localhost;Port=5432;Database=fugue;Username=fugue;Password=REPLACE_ME"
```
The connection string is quoted because it contains `;`.

**Port clash.** If something on your machine already listens on 5432 (for example a Homebrew Postgres), the container would be shadowed for `localhost`. Check with `lsof -nP -iTCP:5432 -sTCP:LISTEN`. If it is taken, use another host port in both lines: `POSTGRES_PORT=5433` and `Port=5433`.

Load it into the current shell whenever you run `dotnet ef` or the API:
```bash
set -a; source .env; set +a
```

The password lives only in `.env` (or in `dotnet user-secrets`, below). `appsettings.json` carries no connection string. Alternative to env vars for the API:
```bash
dotnet user-secrets set "ConnectionStrings:Postgres" "Host=localhost;Port=5432;Database=fugue;Username=fugue;Password=..." --project src/Fugue.Api
```

## 2. Start, stop, reset

```bash
docker compose up -d
```
Expect: `Container fugue-postgres  Started`. Check health (takes a few seconds):
```bash
docker compose ps
```
Expect: `fugue-postgres ... Up ... (healthy)`.

Stop (keeps data): `docker compose down`.
Reset (deletes the named volume `fugue-pgdata`, so all data): `docker compose down -v`, then `docker compose up -d` and migrate again.

## 3. Apply the migrations

Restore the pinned `dotnet-ef` local tool once, then update:
```bash
dotnet tool restore
dotnet ef database update --project src/Fugue.Infrastructure
```
Expect: `Applying migration '20261125124928_InitialCustomers'.` then `Done.` (a second run prints nothing to apply).

Add a migration after changing the model (and update `docs/migrations/migration-catalog.md` in the SAME commit):
```bash
dotnet ef migrations add <Name> --project src/Fugue.Infrastructure --output-dir Persistence/Migrations
```
Undo the last unapplied one: `dotnet ef migrations remove --project src/Fugue.Infrastructure`. Roll the database back to empty: `dotnet ef database update 0 --project src/Fugue.Infrastructure`.

## 4. Seed sample data

The seeder is a Development-only hosted service. It is opt-in so tests and other environments never touch the database. Run the API with `--seed`:
```bash
export ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5080
dotnet run --project src/Fugue.Api -- --seed
```
Expect in the log: `Seeded 25 development customers`, then `Now listening on: http://localhost:5080`. It upserts 25 customers with stable ids (`01900000-0000-7000-8000-0000000000NN`) using Dapper `INSERT ... ON CONFLICT (id) DO UPDATE`, so running it again leaves 25 rows. The database must already be migrated (step 3).

## 5. Connect with DBeaver or pgAdmin 4

| Field | Value |
|---|---|
| Host | `localhost` |
| Port | `5432` (or your `POSTGRES_PORT`) |
| Database | `fugue` |
| Username | `fugue` |
| Password | the `POSTGRES_PASSWORD` value in your `.env` |

DBeaver: New Database Connection, PostgreSQL, fill the table above, Test Connection. pgAdmin 4: Register, Server, Connection tab, same values. Tables are in schema `public`: `customers` and `__ef_migrations_history`.

Command line instead of a GUI:
```bash
docker exec -it fugue-postgres psql -U fugue -d fugue -c "select id, name, status, xmin from customers order by created_at limit 3;"
```
Expect: three rows; `xmin` is the number that appears as the `ETag`.

## 6. Run the tests against a throwaway database

```bash
dotnet test
```
The integration tests start their own `postgres:17-alpine` through Testcontainers, apply the migrations once, and remove the container afterwards. They never touch the compose database.
