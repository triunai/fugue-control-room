# Next-session seed — 2026-11-25 (after S2)

## 1. Read first
1. `hygiene.md` — router and verbs.
2. `docs/hot-state.md` — resume block and the known-red list.
3. `docs/workstreams.md` — WS-04 is done; the board is otherwise empty.
4. `docs/adr/ADR-006-ef-core-reads-dapper-writes.md` — the data-access rule.
5. `docs/runbooks/03-local-database.md` — env vars, `.env`, compose, migrate, seed.

## 2. State of the tree
- `main`, S2 committed in three commits (problem+json fix, persistence + Customers slice, Bruno + docs).
- Clean except untracked local files (`.env`, `.omc/`).

## 3. TRAPS
- `/var/run/docker.sock` may not exist: set `DOCKER_HOST=unix://$HOME/.orbstack/run/docker.sock` or Testcontainers cannot find Docker.
- A host Postgres on 5432 shadows the container for `localhost`: use `POSTGRES_PORT` and the matching `Port=` in `.env`.
- `.env` values containing `;` must be quoted or `source .env` splits them. dotnet does not read `.env`; export it.
- `dotnet ef database update` needs `ConnectionStrings__Postgres` in the environment; without it a password-less fallback is used.
- `xmin` is a system column: EF migrations do not create it; never cast it in SQL with `::bigint` (use `::text::bigint`).
- `dotnet format --verify-no-changes` also covers tests; migrations are excluded from analyzers via `.editorconfig`.

## 4. FACTS — do not re-derive
- Testcontainers works with `DOCKER_HOST` set; `TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE` is optional — verified 2026-11-25 by running the suite with and without it.
- Handler failures return 500 `unexpected` until `MapStatus` exists, because `Problem()` calls it first — verified 2026-11-25 by Bruno and `CustomersApiTests`.

## 5. NOT DONE
- `MapStatus` (owner). After it, the red list in `docs/hot-state.md` should go green; re-run `dotnet test` and `npm run api:test`.
- S3 identity: `ICurrentUser` has no real authentication; audit actor is always `"system"` over HTTP until then.

## 6. Owner decisions owed
- Implement `ApiControllerBase.MapStatus`.
- Review the "S2 implementation choices" entry in `docs/ai-journal.md` (If-Match optional on suspend/reactivate, seeding approach, lowercase URLs).

## 7. Staffing rules
- Authoring and review are separate passes; migrations and `ApiControllerBase` are critical seams (independent review before merge).

## 8. Start here
1. `git status --short --branch`.
2. `docker compose up -d`, load `.env`, `dotnet test`.
3. Open S3 with "new task: …".

## 9. How to write the next seed
Copy `docs/templates/seed-template.md`; carry forward only facts still true.
