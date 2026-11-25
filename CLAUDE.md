# Fugue — Claude Code Context

Multi-tenant e-commerce Web API. A personal project for polishing .NET API/CRUD skills.

## Tech Stack

.NET 10 · ASP.NET Core Web API using **MVC controllers** (not FastEndpoints, not minimal APIs) · abstract `ApiControllerBase` · source-generated `Mediator` (martinothamar, MIT; not MediatR, which is commercial since v13) · EF Core (writes, migrations, global query filters) + Dapper (hot reads) · Npgsql · Supabase-hosted Postgres · FluentValidation · Serilog · Polly v8 · xUnit + Testcontainers · NetArchTest. Repo tooling only: lefthook + commitlint via npm.

## Architecture

Modular monolith. Projects: `Fugue.Api` (controllers, `ApiControllerBase`, composition root) / `Fugue.Domain` (entities, value objects, handlers' contracts, no infrastructure references) / `Fugue.Infrastructure` (EF Core, Dapper, Stripe, Npgsql) / `Fugue.Tests` / `Fugue.ArchTests`.

Modules, in build order: **Tenancy → Identity/RBAC → Catalog → Cart/Orders → Payments (Stripe test mode)**. A module depends only on earlier modules.

Request flow: controller action (one line) -> `Send(command/query)` via the mediator -> handler (all business logic) -> `Result<T>` -> `ApiControllerBase` maps to a response or `ProblemDetails`.

## Multi-tenancy & Security Model (CRITICAL)

- Single shared database. `tenant_id` on EVERY tenant-owned row. See `docs/adr/ADR-001-shared-db-tenant-id.md`.
- Tenant is resolved from a **verified JWT claim AFTER authentication**. Never from a client header, query string or body.
- EF Core **global query filter** on every tenant-owned entity. Dapper / raw SQL does not get the filter: add `tenant_id` explicitly.
- **Mandatory cross-tenant-leak test per module** (tenant A must not read or write tenant B). A module without one is not done.
- **Fallback authorization policy = authenticated.** Anonymous endpoints are explicit `[AllowAnonymous]` exceptions.
- Money is `bigint` minor units + an ISO currency code. Never `float`/`double`/`decimal` on the wire.
- **Keyset pagination** only (no offset).
- **Idempotency keys** on `POST /orders` and on payment calls.
- Credentials are hashed with `PasswordHasher<T>` / Argon2id only. Never roll your own hashing; never log secrets or tokens.
- Secrets live in env vars / `dotnet user-secrets` / untracked `.env`. `.env.example` holds placeholders only.
- Authorization, tenancy and migrations are **critical seams** (see `AGENTS.md` review gate matrix).

## Coding Rules

### Must Follow

- Controllers inherit `ApiControllerBase`. **Actions are one-liners that dispatch a command or query. Business logic lives in handlers**, never in controllers.
- `ApiControllerBase` owns: `Result<T>` -> `ProblemDetails`, `CreatedAtRoute`, ETag / `If-Match` concurrency, pagination headers, idempotency, `CurrentUser` / `Tenant`, dispatch. Do not re-implement these per controller.
- Errors are `ProblemDetails` (RFC 9457). Validation via FluentValidation in the pipeline.
- Nullable reference types on; warnings from the build are fixed, not suppressed.
- Every migration updates `docs/migrations/migration-catalog.md` in the SAME commit.
- Every tenant-owned entity gets the query filter, a `tenant_id` index prefix, and a leak test.
- Pin every review catch with a regression test.
- Verify before claiming done: build, tests, arch tests.

### Conventions

File-scoped namespaces · PascalCase types/members, `_camelCase` private fields · one type per file · commands/queries/handlers grouped by feature folder inside each module · async methods take `CancellationToken` · Conventional Commits with scopes (see `commitlint.config.cjs`).
Domain code uses plain names: `Tenant`, `Product`, `Order`. Theming is brand-only (the name Fugue appears in namespaces and docs, not in domain types).

### Don't

- No business logic or data access in controllers; no `DbContext` in `Fugue.Api` actions.
- No tenant identity from headers, query or body.
- No offset pagination; no floating-point money.
- No FastEndpoints, minimal-API endpoints or MediatR.
- No `Skip =` on tests without a `RED:` comment; no deleting tests to go green.
- Don't commit `.env`, real connection strings, `sk_live_`/`sk_test_` keys, service-role JWTs, `.pfx`/`.pem`.
- Don't push to a remote or commit without being asked.

## Documentation System

`hygiene.md` is the router (verbs: hydrate, park, wrap, ...). Rules and reasons: `docs/conventions/doc-spine.md`. Spine docs live in `docs/` (hot-state, workstreams, decisions, state-backlog, project-log). Extra dirs: `docs/adr/`, `docs/migrations/`, `docs/plans/`, `docs/postmortems/`, `docs/reviews/`. Each fact has one home. `CHANGELOG.md`: append at top, ISO dates.

## Session Start Ritual

1. Read `hygiene.md`, then the newest `docs/NEXT-SESSION-SEED-PROMPT-*.md` if present.
2. Read `docs/hot-state.md` (`RESUME HERE` block), then `docs/workstreams.md` (the board).
3. Run `git status --short --branch`; confirm branch and worktree before editing.
4. Pick a thread; its `Resume:` line says the next action.

## Current Work

Do NOT snapshot live status here (it rots). Read `docs/workstreams.md` and `docs/hot-state.md`.

- Phase 0 (repo spine): done.
- Phase 1 (solution skeleton, `ApiControllerBase`, Tenancy module): next.

## Gotchas & Warnings

- Supabase pooler (port 6543, transaction mode): no session-level state, no prepared-statement reliance; use `No Reset On Close=true` and keep connections short-lived.
- EF global query filters do not apply to Dapper or raw SQL, and `IgnoreQueryFilters()` bypasses them: grep for both in review.
- Source-generated `Mediator` needs the handler assembly referenced at the composition root; a missing handler fails at compile time.
- Hooks are client-side; `--no-verify` skips them. CI (when added) is the real backstop.
- Do not set `core.hooksPath`: lefthook is the only hook orchestrator.

## Testing

xUnit + Testcontainers (real Postgres, not in-memory) for integration; unit tests for domain and handlers; NetArchTest in `Fugue.ArchTests` for layering and "controllers contain no logic" rules. Every module has a cross-tenant-leak test. `node scripts/check-test-integrity.mjs` warns on skipped or weakened tests; `npm run test:scripts` tests it.

## Commands

The .NET SDK lives in `$HOME/.dotnet` and is not on PATH. Prefix .NET commands in a shell with:
`export DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH DOTNET_CLI_TELEMETRY_OPTOUT=1;`

```
dotnet restore && dotnet build --no-restore    # 0 warnings expected (warnings are errors)
dotnet test --no-build                          # Fugue.Tests + Fugue.ArchTests
dotnet format --verify-no-changes               # fix with: dotnet format
bash scripts/spine/ledger-check.sh      # doc spine link check
bash scripts/spine/secrets-scan.sh      # staged secrets scan
bash scripts/spine/stack-check.sh --test
npm install                              # installs lefthook hooks (prepare)
```

## Guardrail stack

| Juncture | Runs | Blocks? |
|---|---|---|
| commit | secrets-scan, `dotnet format --verify-no-changes` (if `*.cs` staged and a solution exists), test-integrity (advisory) | yes, except test-integrity |
| commit-msg | kit rule (`.githooks/commit-msg`) + commitlint | yes |
| push | stack-check (restore, build, test), ledger-check, spine-nudge (advisory) | yes, except nudge |

Orchestrator: `lefthook.yml`. Pause the advisory layer once: `LEFTHOOK_EXCLUDE=guardrail git push`. Details: `AGENTS.md`.
