# AI working journal

Purpose: an honest record of where an AI assistant shaped this repo and what the owner did with its suggestions. Each entry says what was asked, what the AI proposed, whether the owner accepted, challenged or overrode it, and why. Reviewers can use it to see which decisions are the owner's own.

Entry format: date, **Asked**, **AI proposed**, **Decision** (accepted | challenged | overrode), **Why**. Newest entries go at the bottom of the seed list below; later entries append.

## 2026-11-11 - Audit of a previous template
- **Asked:** assess a previous work-derived .NET template for what to reuse.
- **AI proposed:** six parallel Haiku agents (core, infra, api, sql-docs, house-style, bootstrap-kit) returned keep / mid / skip verdicts.
- **Decision:** accepted the verdicts; challenged the headline claims by spot-checking three by hand (tenant middleware ran before authentication; the audit writer loop never exits on channel completion; a read query had no tenant filter). All three were confirmed.
- **Why:** agent claims are cheap to make and costly to trust. Result: clean-room rewrite, copy patterns not files (the old code had committed secrets and a foreign domain).

## 2026-11-11 - Naming
- **Asked:** a project name.
- **AI proposed:** five themed names.
- **Decision:** accepted "Fugue" (independent voices over one subject, like many features over one shared core).
- **Why:** rule accepted with it: theme the brand, keep domain names plain.

## 2026-11-11 - Tenancy (original)
- **Asked:** how to isolate tenants.
- **AI proposed:** shared database plus Postgres row-level security.
- **Decision:** overrode: shared database with `tenant_id` only. Later (2026-11-25) an external review argued markets are not tenants; the owner chose a toggle, off by default (ADR-003).
- **Why:** RLS adds operational weight the project does not need; and tenancy is not universal across target domains.

## 2026-11-11 - Repo operating system
- **Asked:** conventions and guardrails for the repo.
- **AI proposed:** lifted a personal AI bootstrap kit and conventions from another personal repo; flagged a conflict between lefthook and `core.hooksPath`.
- **Decision:** accepted the resolution: lefthook orchestrates the kit's scripts; `core.hooksPath` stays unset.
- **Why:** two hook orchestrators would conflict.

## 2026-11-11 - API style
- **Asked:** controller style and mediator.
- **AI proposed:** MVC controllers with an `ApiControllerBase` over a generic `CrudController` (which hides the CRUD being practised) and over FastEndpoints. For the mediator, `Mediator` (MIT, source-generated) because MediatR v13+ is commercially licensed.
- **Decision:** accepted both.
- **Why:** the project exists to practise the plumbing, so keep it visible; avoid licence risk.

## 2026-11-11 - Agent bug catch: problem content type
- **Asked:** (found in testing) why error responses were `application/json`.
- **AI proposed:** `[Produces("application/json")]` overwrote `application/problem+json`; fixed by returning a `ProblemResult` that builds its `ObjectResult` at execution time.
- **Decision:** accepted.
- **Why:** pinned by a content-type assertion in the tests.

## 2026-11-11 - MapStatus left to the owner
- **Asked:** who writes the ErrorType-to-HTTP-status mapping.
- **AI proposed:** a decision comment with suggested statuses, and the method left unimplemented.
- **Decision:** accepted: the owner hand-writes it (404-not-403 for cross-scope misses).
- **Why:** it is the one policy decision worth the owner's own hands; tests stay red until then on purpose.

## 2026-11-11 - Runtime version
- **Asked:** scaffold the solution.
- **AI proposed:** .NET 10 (the SDK at hand) and flagged that .NET 8 reaches end of support on 2026-11-10.
- **Decision:** overrode: .NET 8, to match CV and interview context.
- **Why:** alignment with the target audience outweighs support status; the EOL check is disabled deliberately and NuGet audit stays on.

## 2026-11-11 - Result type
- **Asked:** how handlers report failure.
- **AI proposed:** a hand-rolled `Result<T>`. An external Fable review recommended keeping it.
- **Decision:** overrode: FluentResults, with our `ErrorType` preserved (ADR-004).
- **Why:** ecosystem familiarity.

## 2026-11-25 - Fable architecture critique
- **Asked:** an external architecture review of the plan.
- **AI proposed:** defer Dapper, HTTP resilience and deny overrides; an `IAuditable` interceptor instead of a base-entity Id; typed IDs only for key aggregates; bring the outbox earlier; never sample audit; `.sln` over `.slnx`; OpenAPI-friendly return types; CORS expose headers; a domain-neutral template that is disclosed as such.
- **Decision:** accepted all of those. Challenged "no saga ID": the owner keeps `SagaId` as a business-process correlation key alongside W3C `traceparent` (ADR-005). Overrode "README is enough": the owner keeps runbooks for the interview operator.
- **Why:** the critique was mostly right; the two exceptions are about what the owner needs to demonstrate.

## 2026-11-25 - Container runtime
- **Asked:** which Docker runtime on macOS.
- **AI proposed:** after comparing Colima, OrbStack and Docker Desktop: OrbStack (fastest in benchmarks; auto-manages `/var/run/docker.sock`, so Testcontainers works without configuration).
- **Decision:** accepted.
- **Why:** least setup friction for a 3-day horizon.
