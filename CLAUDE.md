# Fugue — Claude Code Context

A domain-neutral .NET Web API scaffold for practising API/CRUD craft. It carries no business domain: features are added as vertical slices on top of a shared core. No frontend in scope.

## Tech Stack

.NET 8 · ASP.NET Core Web API using **MVC controllers** (not FastEndpoints, not minimal APIs) · abstract `ApiControllerBase` · source-generated `Mediator` (martinothamar, MIT; not MediatR, which is commercial since v13) · **FluentResults** (+ our `FugueError`/`ErrorType`, ADR-004) · FluentValidation · Serilog · EF Core 8 + Npgsql (snake_case) and Dapper against local Postgres 17 (docker compose; ADR-006) · xUnit + Testcontainers · NetArchTest · Bruno CLI for API regression (`npm run api:test`). Repo tooling only: lefthook + commitlint via npm. Plan and phase order: `docs/plans/roadmap.md`.

## Architecture

Vertical slices inside a clean-architecture 4-project layout:

- `Fugue.Domain`: entities, strongly-typed ids (`Ids/`), `IAuditable`/`IAuditStamp`, `ErrorType`, `FugueError`, `PagedResult<T>`, `IVersioned`. Depends on FluentResults only.
- `Fugue.Application`: feature slices, validators, handlers, pipeline behaviors, repository interfaces. Depends on Domain. **Never references ASP.NET Core.**
- `Fugue.Infrastructure`: `FugueDbContext`, migrations, Dapper `SqlExecutor`, `EfUnitOfWork`, audit stamping, repository implementations, seeder. Depends on Application and Domain.
- `Fugue.Api`: controllers, `ApiControllerBase`, composition root. Depends on Application and Infrastructure.
- `Fugue.Tests`, `Fugue.ArchTests`.

### Slice shape

```
Fugue.Application/Features/<Feature>/I<Feature>Repository.cs
Fugue.Application/Features/<Feature>/Commands/<Verb><Noun>.cs   (also Queries/)
    public sealed record <Verb><Noun>Command(...) : ICommand<Result<T>>   // contract
    <Verb><Noun>Validator : AbstractValidator<<Verb><Noun>Command>
    <Verb><Noun>Handler                                                   // business logic, calls the feature repo, returns FluentResults
Fugue.Infrastructure/Persistence/<Feature>/                              repository implementations
Fugue.Api/Controllers/<Feature>Controller.cs                             one-liner actions
```

Pipeline behaviors (`Fugue.Application/Common/Behaviors`, registered in `Program.cs` via Mediator `PipelineBehaviors`): `LoggingBehavior` (outermost; name, elapsed, outcome, error codes; never payloads), then `AuditBehavior` (failed commands write one `audit_log` failure row; successes are written by the SaveChanges interceptor in the same transaction; ADR-010), then `ValidationBehavior` (all `IValidator<T>`, aggregated into one `FugueErrors.Validation`, camelCase field keys). Both require `TResponse : ResultBase, new()`, i.e. handlers return FluentResults.

Correlation (ADR-005): `CorrelationIdMiddleware` is first; `X-Correlation-Id` (request chain) and `X-Saga-Id` (business process, `ICorrelationContext.SetSaga`) are validated, echoed, and pushed to Serilog and `Activity`. Unhandled exceptions become a 500 `application/problem+json` via `GlobalExceptionHandler`. Mediator keeps `ICommand`/`IQuery`/`IRequest` separate; `ApiControllerBase.Send*` has an overload for each.

Request flow: controller action (one line) -> `Send(command/query)` via the mediator -> pipeline behaviors (logging, validation) -> handler -> `FluentResults.Result<T>` -> `ApiControllerBase` maps to a response or `ProblemDetails`.

## Data access (ADR-006)

- **EF Core for all reads** (`AsNoTracking` projections, keyset paging) **and for concurrency-checked single-aggregate updates/deletes**: Postgres `xmin` is the concurrency token (`IsRowVersion`), the ETag is its value, `If-Match` supplies the expected version (stale = `PreconditionFailed`, missing on PUT/DELETE = `PreconditionRequired`).
- **Dapper for inserts, upserts (`INSERT ... ON CONFLICT`) and complex or set-based writes.** Dapper runs ONLY through `SqlExecutor`, which uses EF's connection and current transaction.
- **`IUnitOfWork`** (`BeginAsync`/`CommitAsync`; dispose without commit rolls back) wraps both so mixed writes are atomic. The S4 outbox uses this seam.
- **Audit fields** (`IAuditable`: CreatedAt/By, UpdatedAt/By): EF writes are stamped by `AuditableSaveChangesInterceptor`; Dapper SQL must bind `IAuditStamp.Capture()` values explicitly. Actor is the user id or `"system"`.
- `Fugue.Domain` and `Fugue.Application` never reference EF, Dapper or Npgsql (arch test). Unique violations (SQLSTATE `23505`) become `Conflict` in the repository.
- Migrations: `dotnet ef migrations add <Name> --project src/Fugue.Infrastructure --output-dir Persistence/Migrations`, and update `docs/migrations/migration-catalog.md` in the same commit.

### Adding a strongly-typed ID

1. Copy `src/Fugue.Domain/Ids/CustomerId.cs` to `<Aggregate>Id.cs` and rename (`readonly record struct XId(Guid Value) : IStronglyTypedId<XId>, IParsable<XId>` with the same `[JsonConverter]` and `[TypeConverter]` attributes; `New()` uses `Uuid7`).
2. Use `XId` as the entity key and in commands, DTOs and controller route parameters.

That is all: EF value conversion and Dapper type handlers are registered by scanning the Domain assembly for `IStronglyTypedId<>` (`Persistence/Ids/TypedIds.cs`); JSON and MVC route/query binding come from the two attributes. Ids are UUIDv7 (`Uuid7`; .NET 8 has no `Guid.CreateVersion7`). No `Id` on a shared base type.

## Tenancy & Security Model

- **Single-tenant by default.** Tenancy is a configuration toggle (`Tenancy:Enabled=false`), implemented later as an EF query filter plus tenant-from-JWT. See `docs/adr/ADR-003-single-tenant-default-tenancy-toggle.md` (supersedes ADR-001).
- When tenancy is enabled: the tenant comes from a **verified JWT claim AFTER authentication**, never from a client header, query string or body; every tenant-owned entity gets the query filter and a leak test.
- **Fallback authorization policy = authenticated.** Anonymous endpoints are explicit `[AllowAnonymous]` exceptions (token, refresh, logout, ping). Every action carries `[HasPermission(Permissions.X)]`; effective permissions are read per request (no cache, ADR-008); customer rows are scoped by owner unless `CUSTOMERS_ALL`; feature controllers carry `[Module(...)]` (ADR-009).
- Money (if a domain needs it) is `bigint` minor units + an ISO currency code. Never `float`/`double`/`decimal` on the wire.
- **Keyset pagination** only (no offset).
- Credentials are hashed with `PasswordHasher<T>` only (PBKDF2-HMAC-SHA512, v3 format, behind `IPasswordVerifier`; Argon2id is a "more time" upgrade, ADR-008). Never roll your own hashing; never log secrets, tokens or request payloads.
- Secrets live in env vars / `dotnet user-secrets` / untracked `.env`. `.env.example` holds placeholders only.
- Authorization, migrations and `ApiControllerBase` are **critical seams** (see `AGENTS.md` review gate matrix).

## Coding Rules

### Must Follow

- Controllers inherit `ApiControllerBase`. **Actions are one-liners that dispatch a command or query. Business logic lives in handlers**, never in controllers.
- `ApiControllerBase` owns: `Result<T>` -> `ProblemDetails`, `CreatedAtAction`, ETag / `If-Match` concurrency, pagination headers, `CurrentUser`, dispatch. `Send*` return `ActionResult<T>` so OpenAPI keeps response schemas. Do not re-implement these per controller.
- Handlers return FluentResults: `Result.Ok(value)` or `Result.Fail<T>(FugueErrors.NotFound(code, message))`. Always fail with a `FugueError`; any other error type is reported as a 500 `unexpected`.
- Errors are `ProblemDetails` (RFC 9457). Validation via FluentValidation in the mediator pipeline.
- Nullable reference types on; warnings from the build are fixed, not suppressed.
- Every migration updates `docs/migrations/migration-catalog.md` in the SAME commit.
- Pin every review catch with a regression test.
- Verify before claiming done: build, tests, arch tests.

### Conventions

File-scoped namespaces · PascalCase types/members, `_camelCase` private fields · one type per file (a command, its validator and its handler share one file by the slice convention) · async methods take `CancellationToken` · Conventional Commits with scopes (see `commitlint.config.cjs`).
Domain code uses plain names. Theming is brand-only (the name Fugue appears in namespaces and docs, not in domain types).

### Don't

- No business logic or data access in controllers; no `DbContext` in `Fugue.Api` actions.
- No ASP.NET Core references in `Fugue.Application` or `Fugue.Domain`.
- No tenant identity from headers, query or body.
- No offset pagination; no floating-point money.
- No FastEndpoints, minimal-API endpoints or MediatR.
- No `Skip =` on tests without a `RED:` comment; no deleting tests to go green.
- `ApiControllerBase.MapStatus` (ErrorType -> HTTP status, D-009) is implemented and approved by the owner; every named `ErrorType` must stay mapped (the build breaks otherwise). No test is red by design.
- No Dapper outside `SqlExecutor` (it must share EF's connection and transaction); no reads through Dapper.
- Don't commit `.env`, real connection strings, keys, `.pfx`/`.pem`.
- Don't push to a remote. Commit only when asked. Agent commits end with `Refs: W-NNN` (or `D-`/`ADR-`) and `Co-Authored-By:`; a `fix(...)` touching `src/` touches `tests/` or says `Tests: none (<reason>)`.
- Beads (`bd`) is a local queue: never commit `.beads/`, never cite a bead id in a commit or a doc.

## Documentation System

`hygiene.md` is the router (verbs: hydrate, park, wrap, ...). Rules and reasons: `docs/conventions/doc-spine.md`. Spine docs live in `docs/` (hot-state, workstreams, decisions, state-backlog, project-log). Doc lifecycle (W row -> project-log + CHANGELOG, cold archive): `hygiene.md`. Extra dirs: `docs/adr/`, `docs/migrations/`, `docs/plans/`, `docs/postmortems/`, `docs/reviews/`. Each fact has one home. `CHANGELOG.md`: append at top, ISO dates.

## Session Start Ritual

1. Read `docs/hot-state.md` FIRST (it is the resume point), then resume its "Next action" exactly. Then `hygiene.md` and the newest `docs/ai/handoffs/*` (or `docs/NEXT-SESSION-SEED-PROMPT-*.md`).
2. Read `docs/workstreams.md` (the live board; every new ask becomes a `W-NNN` row FIRST).
3. Run `git status --short --branch` and `git config core.hooksPath` (must be empty); confirm branch and worktree before editing.
4. Pick a thread; its `Resume:` line says the next action.
5. Never read `docs/archive/` without a grep hit in `docs/archive/INDEX.md`.

## Current Work

Do NOT snapshot live status here (it rots). Read `docs/workstreams.md` and `docs/hot-state.md`.

Phase order lives in `docs/plans/roadmap.md`; status in the spine files.

## Gotchas & Warnings

- .NET 8 is past end of support (2026-11-10). `CheckEolTargetFramework=false` in `Directory.Build.props` is a deliberate choice, not an oversight. NuGet audit stays on.
- `global.json` pins SDK 8.0.428 (`rollForward: latestFeature`). The solution is `Fugue.sln` (`.slnx` needs SDK 9.0.200+).
- Source-generated `Mediator` needs the handler assembly referenced at the composition root; a missing handler fails at compile time.
- Hooks are client-side; `--no-verify` skips them. CI (when added) is the real backstop.
- Do not set `core.hooksPath`: lefthook is the only hook orchestrator.

## Testing

xUnit + Testcontainers (real Postgres, not in-memory) for integration; unit tests for domain, handlers and behaviors; NetArchTest in `Fugue.ArchTests` for layering and "controllers contain no logic" rules. When tenancy is enabled, each tenant-owned feature gets a cross-tenant-leak test. `node scripts/check-test-integrity.mjs` warns on skipped or weakened tests; `npm run test:scripts` tests the scripts. `tests/Fugue.ArchTests/FitnessTests.cs` holds the fitness functions (`Fitness_<module>_<rule>`) with shrink-only baselines in `tests/Fugue.ArchTests/Baselines/`: a stale baseline line fails the test. Structural review catches are pinned there.

## Commands

The .NET 8 SDK lives in `$HOME/.dotnet` and is not on PATH. Prefix .NET commands in a shell with:
`export DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH DOTNET_CLI_TELEMETRY_OPTOUT=1;`
For Docker/Testcontainers on OrbStack also add `PATH=$HOME/.orbstack/bin:...`, `DOCKER_HOST=unix://$HOME/.orbstack/run/docker.sock`, `TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE=/var/run/docker.sock` (runbook 03). Load `.env` with `set -a; source .env; set +a` (the connection string is `ConnectionStrings__Postgres`).

```
dotnet restore && dotnet build --no-restore    # 0 warnings expected (warnings are errors)
dotnet test --no-build                          # Fugue.Tests (Testcontainers Postgres) + Fugue.ArchTests
docker compose up -d                            # local Postgres (needs .env; runbook 03)
dotnet tool restore && dotnet ef database update --project src/Fugue.Infrastructure
dotnet user-secrets set Auth:SigningKey "$(openssl rand -base64 32)" --project src/Fugue.Api   # once; the host refuses to start without a valid key
dotnet run --project src/Fugue.Api -- --seed    # Development: seeds 25 customers + dev users (runbook 05; set ASPNETCORE_URLS=http://localhost:5080 for Bruno)
npm run api:test                                # Bruno suite against the running API
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
| commit-msg | kit rule (`.githooks/commit-msg`) + commitlint + `check-refs-trailer` + `check-fix-has-test` | yes |
| push | stack-check (restore, build, test), ledger-check, adr-check; advisory: spine-nudge, commit-link-check, codex-gate (seam review, detached, capped) | yes, except advisory |
| every Bash tool call | `scripts/safe-command-guard.mjs` (Claude Code PreToolUse hook, `.claude/settings.json`) | yes: force push, `--no-verify`, hard reset on main, stray `rm -rf`, `curl \| sh`, secret printing, undry-run `ef database drop` |

Orchestrator: `lefthook.yml`. Pause the advisory layer once: `LEFTHOOK_EXCLUDE=guardrail git push`. Details: `AGENTS.md`.
