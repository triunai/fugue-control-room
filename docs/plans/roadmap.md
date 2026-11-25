# Fugue roadmap

Context: a 3-day horizon before a backend-focused take-home that will import this scaffold as a disclosed first commit. The template stays domain-neutral. Out of scope: Kafka, any frontend.

Each phase ends with a green build (0 warnings), tests, and a commit. Statuses live in `docs/workstreams.md`, not here.

## S0 - retarget, FluentResults, neutral docs
- .NET 8 (`global.json` 8.0.428), `Fugue.sln`, repinned packages, `CheckEolTargetFramework=false` (deliberate).
- `Fugue.Application` project; slice shape documented.
- FluentResults + `FugueError`/`ErrorType` (ADR-004); `ApiControllerBase` returns `ActionResult<T>`.
- Neutral docs, ADR-003 (tenancy toggle), runbook 01, AI journal.

## S1 - validation, exceptions, logging, correlation
- Mediator `ValidationBehavior` (FluentValidation) and `LoggingBehavior`.
- `GlobalExceptionHandler` (RFC 9457 500), ProblemDetails enrichment with `traceId`/`correlationId`.
- Serilog (readable console in Development, compact JSON otherwise), request logging.
- `X-Correlation-Id` + `X-Saga-Id` + W3C trace context (ADR-005). Runbook 02.
- `GET /api/v1/diagnostics/ping`: the one real endpoint, proving the pipeline end to end through the real mediator.

## S2 - persistence (done; ADR-006)
- Local Postgres 17 via docker compose; EF Core 8 + Npgsql (snake_case); `dotnet-ef` as a local tool.
- Data-access split: EF for reads and `xmin`-checked updates, Dapper for inserts/upserts, one shared connection and transaction through `IUnitOfWork`.
- `IAuditable` stamped by an EF interceptor (and by `IAuditStamp` in Dapper SQL), `TimeProvider`-based.
- Hand-written strongly-typed ID pattern (UUIDv7) with `CustomerId` only.
- Customers vertical slice (ETag/If-Match, keyset paging, guarded transitions), idempotent dev seeder, Testcontainers fixture, Bruno suite, migration catalog discipline.

## S3 - identity
- JWT bearer + a dev token issuer (development only).
- Two role policies; fallback policy = authenticated.
- Resource-ownership authorization handler (404 for cross-scope misses).

## S4 - transactional outbox and signed webhooks
- Outbox table written in the same EF transaction as the business change.
- Background dispatcher with `IEventPublisher` seam.
- Outbound webhooks: subscription CRUD; HMAC-SHA256 `X-Fugue-Signature` plus a timestamp header (replay window).
- Delivery through a typed `HttpClient` with `AddStandardResilienceHandler` (Microsoft.Extensions.Http.Resilience, Polly v8: retry, circuit breaker, timeouts).
- Dead-letter after N attempts.
- `SagaId` and `CorrelationId` propagate as webhook headers.

## S5 - REST hardening
- Idempotency-key store with replay for POSTs.
- API versioning (Asp.Versioning.Mvc, v1/v2) with `Deprecation` and `Sunset` headers for backward compatibility.
- Rate limiting, partitioned by user/client.
- HybridCache (Microsoft.Extensions.Caching.Hybrid, net8-compatible) + Redis, with stampede protection.
- OpenTelemetry tracing and metrics.
- HTTPS and HSTS; health checks (live/ready).
- OpenAPI + Swagger UI, plus an OpenAPI snapshot test for contract regression.
- CORS with `Access-Control-Expose-Headers: ETag, Link, X-Next-Cursor, X-Correlation-Id`.
- Dockerfile + one-command compose.
- Extend the Bruno suite to new endpoints (the collection and `npm run api:test` exist since S2).

## S6 (optional) - gateway
- YARP API gateway in compose, load-balancing two API replicas.

## Deferred
- Per-user deny overrides in authorization.
- Audit sampling: never. Audit records are not sampled.
- Tenancy toggle implementation (`Tenancy:Enabled`, ADR-003): after S3 if a target domain needs it.
