# AI working journal

Purpose: an honest record of where an AI assistant shaped this repo and what the owner did with its suggestions. Each entry says what was asked, what the AI proposed, whether the owner accepted, challenged or overrode it, and why. Reviewers can use it to see which decisions are the owner's own.

Entry format: date, **Asked**, **AI proposed**, **Decision** (accepted | challenged | overrode), **Why**. Seed entries come first; later entries append at the bottom.

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

## 2026-11-25 - S1 implementation choices (review these)
- **Asked:** validation and logging behaviors, exception handler, Serilog, correlation and saga ids.
- **AI proposed / chose:**
  - `ValidationBehavior` builds the failure with `where TResponse : ResultBase, new()` and `new TResponse()` (FluentResults results have public parameterless constructors), no reflection of our own; the same shape lets one behavior serve `Result` and `Result<T>`.
  - Mediator keeps `ICommand`/`IQuery`/`IRequest` as separate hierarchies, so `ApiControllerBase.Send*` gained one overload per hierarchy (all funnel into one private mapper).
  - Added a real `GET /api/v1/diagnostics/ping` feature so the pipeline can be exercised end to end through the real mediator; this goes beyond "Features/ empty" from S0.
  - Serilog bootstrap logger is a plain (non-reloadable) logger, to avoid the "logger is already frozen" failure when several `WebApplicationFactory` hosts start in one test process.
  - Client abort answers 499 with no body; the framework's own "unhandled exception" log is silenced so each failure logs once.
  - Saga id set by a handler is pushed into the log context at call time; it appears on later log lines in the same request flow and on the response header, but not on lines logged before the call.
- **Decision:** pending owner review (not yet accepted or challenged).
- **Why:** these are the points where the brief left a choice. Also observed, not changed: framework-generated 400s from `[ApiController]` are served as `application/json`, not `application/problem+json`, because of the controller-level `[Produces]` (same cause as the earlier content-type bug).

## 2026-11-25 - Dapper for writes, EF for reads (S2)
- **Asked:** the data-access approach for persistence.
- **AI proposed:** an external Fable review recommended deferring Dapper until a measured hot path (EF alone).
- **Decision:** overrode: the owner chose EF for reads and concurrency-checked updates, Dapper for inserts, upserts and set-based writes. The AI added two conditions: optimistic concurrency stays on the EF path (Postgres `xmin` as the token, ETag/`If-Match`), and Dapper must run on EF's connection and current transaction through a small `IUnitOfWork`, so mixed writes are atomic and the S4 outbox has a seam (ADR-006, D-004).
- **Why:** practising both tools is the point of the project; the two conditions remove the lost-update and split-commit hazards that the reviewer's concern was really about.

## 2026-11-25 - S2 implementation choices (review these)
- **Asked:** local Postgres, persistence foundation, a Customers vertical slice, seed, tests, Bruno suite.
- **AI proposed / chose:**
  - Framework 400s fixed two ways: dropped the controller-level `[Produces]` and routed `InvalidModelStateResponseFactory` through `ProblemResult` (adds `code: validation.failed`, trace and correlation ids). Also `UseStatusCodePages()` so empty 404/405/415 are problem+json, and lower-case URLs.
  - `Version` on the DTO is `xmin` as `long`; the entity keeps it as `uint RowVersion`. `xid` cannot be cast to `bigint` in SQL, so Dapper uses `xmin::text::bigint` and EF projections widen in memory.
  - `If-Match` is mandatory for PUT and DELETE (428 when missing) but optional for suspend/reactivate (POST), because the state machine already guards them; if sent it is enforced. The brief listed 428 only for PUT/PATCH/DELETE.
  - Typed ids are registered by scanning the Domain assembly for `IStronglyTypedId<>` (zero lines to add the next id), using a C# 11 static abstract `From(Guid)`; MVC binding needs a `TypeConverter` (an `IParsable` struct is treated as a complex type by MVC), so both are provided.
  - Seeding is a hosted service enabled only in Development with `--seed` (or `Seed:Enabled`), not in tests; it keeps the API running after seeding.
  - A host Postgres already listened on 127.0.0.1:5432 on the AI's machine and shadowed the container; compose publishes `${POSTGRES_PORT:-5432}` and the local `.env` uses 5433. Not changed: the default stays 5432.
  - Because `MapStatus` throws, every handler-failure HTTP assertion is red. The same failure logic is pinned green at the Result level in `CustomerSeamTests` so it is verified without writing `MapStatus`.
  - Bruno CLI 4.4.0 pulls a transitive `@faker-js/faker` with a high advisory (`npm audit`); dev-only, local; left as is.
- **Decision:** pending owner review (not yet accepted or challenged).
- **Why:** these are the points where the brief left a choice or reality differed from it.

## 2026-11-25 - H1: porting my AI engineering harness
- **Asked:** bring the working practices from my multi-tenant consumer SaaS repo and my mobile app repo into Fugue as machine checks and project skills, public-safe.
- **AI proposed / chose:**
  - Commit gates (`Refs:` trailer, fix-has-test), a PreToolUse safety guard, an advisory seam-triggered Codex gate, four project skills, fitness functions with shrink-only baselines.
  - The guard is a reduced port (the original is about 900 lines of hardening; this one keeps the protocol and seven narrow rules).
  - `bd init` committed its own config to `main` and could have rewritten `core.hooksPath`; the AI reset that one local commit (mixed reset, nothing pushed), gitignored `.beads/` and re-installed lefthook. Recorded as failure mode 8.
  - The Codex invocation was taken from `codex exec review --help` and was NOT run end to end (dry-run and unit tests only).
  - Fitness rules found zero violations on first run, so every baseline starts empty; the ratchet was proved by adding a stale line and watching the test fail.
- **Decision:** accepted; the owner fixed the shape: beads local only, `Refs:` ids limited to committed `W-`/`D-`/`ADR-`, the Codex gate advisory, project-log plus CHANGELOG as additive ledgers with a closing script, `hot-state.md` as the resume point.
- **Why:** each rule matches a failure already seen in the sibling repos; owner calls (ledger shape, advisory gate) were not the AI's to make.

## 2026-11-25 - S3 identity plan (UAM)
- **Asked:** a reviewable S3 plan for authentication, authorization and ownership.
- **AI proposed:** a slim role and permission module with per-user overrides deferred.
- **Decision:** overrode. The owner chose a lean port of their UAM v3 design (modules, resources, actions, permissions, roles, user roles, overrides with deny winning), with overrides as the last-cut slice; tenants, sessions, groups, audit tables and domain tables are parked in `docs/archive/deferred.md`.
- **Why:** the owner's design is the model being rehearsed; the AI added only what Fugue conventions need (timestamptz, typed ids, audit stamps, app-generated UUIDv7), a written precedence rule and `expires_at`. D-016 to D-021 are proposed, not accepted.

## 2026-11-25 - S3 plan: cross-family review and scope changes
- **Asked:** review the S3 identity plan with another model family, then build.
- **AI proposed:** the first plan (cached permission lookup, rate limiting deferred to S5, staff-supplied owner on create, lean scope with refresh, groups, tenants and audit deferred).
- **Decision:** Codex REJECTED it (10 findings: 2 HIGH, 7 MED, 1 LOW); the owner accepted all of them. Resolutions: owner bound to the caller on create unless effective `CUSTOMERS_ALL`; built-in rate limiter before password verification; no permission cache in S3 (one query per request, HybridCache in S5); email change staff-only; one canonical email key with an active-row unique index; deleted catalog rows excluded; HTTP contracts need green tests (MapStatus now implemented); mandatory acceptance gates and one complete security slice first; signing key base64 of at least 32 random bytes with placeholder refusal. The owner then OVERRODE the earlier "defer it" advice a second time: tenants, refresh tokens, groups, grant_audit and access_audit are built basic, plus config-backed module management, 204s on mutations, and an IAuditable base-entity contract.
- **Why:** the review caught design holes a self-review would have missed (the cache race and the create bypass); the owner prefers a fuller UAM port to rehearse the whole surface. Risk accepted: a bigger S3, mitigated by building the core security slice first and cutting overrides or later slices if time runs out.

## 2026-11-25 - S3 build and review
- **Asked:** build S3 per the revised plan, add module management, 204 mutations, tenants, refresh tokens, groups and audit tables, then review.
- **AI proposed:** the security vertical slice first, then modules, groups, refresh, audit, overrides; a Sonnet read plus a Codex read of the final diff.
- **Decision:** accepted by the owner's scope; reviews found one HIGH the author's tests had missed (a refresh-family revoke racing a rotation under READ COMMITTED), fixed with an advisory lock and pinned. Two MEDs (create-path email oracle, limiter DoS) were deferred to W-013 because they need an owner call.
- **Why:** sequential tests cannot see snapshot races; cross-family review earned its cost again. Also learned: a Bruno script that throws can skip its own cleanup, and secret scanners treat dev passwords as secrets.

## 2026-11-26 - W-012 rich audit log
- **Asked:** a "really good and rich" append-only audit log: who, what, field-level change, outcome, request context, PII safety, same transaction, a permissioned query API.
- **AI proposed / chose:**
  - One `audit_log` table beside `grant_audit` and `access_audit` (not folded in), an EF interceptor that adds the rows to the same SaveChanges, and `IAuditTrail` for the Dapper paths.
  - A mediator behavior for FAILED commands only (successes come from the interceptor, so no double rows), ordered Logging, Audit, Validation so validation failures count.
  - Masking at write time rather than read time, so the table and the API cannot leak; `AUDIT_READ` for admin only (the brief left "staff read?" open).
  - A fitness rule that fails when a new raw-SQL write does not call `IAuditTrail`, because the interceptor cannot see Dapper.
  - One trap hit while building: the behavior's eager DbContext dependency broke every host without a database (failure mode 18); another process trap, a pathspec commit that skipped new files (19).
- **Decision:** accepted as ADR-010 (D-027 to D-033); the cross-family review verdict and fixes are in `docs/reviews/codex/2026-11-26-w012-audit.md`.
- **Why:** the audit has to be atomic with the change or it lies; the three tables answer different questions, so merging them would trade clarity for one query surface.

## 2026-11-26 - W-013 S3 review follow-ups
- **Asked:** close the deferred S3 review findings; the owner decided email uniqueness (per owner) and the login limiter (strict per IP and email plus a loose per-email ceiling).
- **AI proposed / chose:**
  - Two partial-style unique indexes (a plain `(user_id, email)` treats NULL owners as distinct), and a ceiling that only unsuccessful attempts charge, so a victim's own logins never count; refresh has no email and keeps only the per-IP limit.
  - `If-Match` required on override changes and deletes (create sends none), matching Customers and Modules; races made deterministic in tests by running the competing write inside a decorated `IGrantAuditWriter`, which fires between the handler's check and its save.
  - Module config fails closed (a typo must not leave a kill switch open) and ignores SYSTEM; the email hash gets an HMAC subkey and no fallback key.
  - Two traps hit: Npgsql scaffolds `AddColumn xmin` that emits no DDL (failure mode 21), and commit messages need 95-column wrapping and a listed scope (22).
- **Decision:** accepted as D-034 to D-036 (ADR-008 amended). Not reviewed by another model family yet (auth and migrations are critical seams): in the hot-state inbox.
- **Why:** each fix pins a state a stale client, a typo or a race could otherwise produce.

## 2026-11-26 - W-006 S4 outbox and signed webhooks
- **Asked:** a transactional outbox with signed outbound webhooks, the way a real integration would be built, but no broker yet.
- **AI proposed / chose:**
  - The outbox row is written in the same transaction as the change (an EF interceptor for entities, `IOutbox` for the Dapper path), then a two-stage dispatcher: relay with `FOR UPDATE SKIP LOCKED` and a savepoint per message, then leased deliveries with the HTTP call outside any transaction and a result write fenced by attempt number.
  - The outbox is the single retry authority: the resilience handler keeps its timeout and circuit breaker but its own retry is off, so attempts are durable, counted and visible.
  - SSRF checked at subscribe time and again at connect time (`ConnectCallback` dials the verified address), redirects off; secrets encrypted with Data Protection, key ring in the database, shown once.
  - A Development-only anonymous sink whose credential is the signature, so the whole loop can be demonstrated without an outside receiver.
  - Traps: Dapper cannot map `timestamptz` to a `DateTimeOffset` constructor parameter (failure mode 24); the Docker daemon stopped answering after the delivery tests were added (23, cause not proven).
- **Decision:** accepted as D-037 to D-040 (ADR-011). The cross-family review verdict and fixes are in `docs/reviews/codex/2026-11-26-s4-webhooks.md`.
- **Why:** a webhook sent from inside the request would lose events on a crash and block on slow receivers; the outbox makes delivery at least once and the signature plus event id make it safe for receivers.


## 2026-11-26 - W-015 Codex W-013 findings
- **Asked:** fix the two MED findings the cross-family review raised on the W-013 diff.
- **AI proposed / chose:**
  - A no-op override PUT now marks one property modified so EF still issues the UPDATE and the xmin guard runs; an accepted no-op bumps the version like any PUT.
  - The per-email login ceiling became a small fixed-window counter that reserves a slot before password verification and refunds it on success, so concurrent attempts cannot all pass the check.
- **Decision:** covered by D-035 and D-036; both regression tests were shown RED first.
- **Why:** a guard that only runs when a write happens, and a limit that is checked before it is charged, are both bypassable by the exact race they exist for.

## 2026-11-26 - W-016 OpenAPI contract, console CORS and /me
- **Asked:** the console (W-017) needs a machine-readable contract, CORS and a `/me` endpoint before it can build against the API.
- **AI proposed / chose:**
  - Swashbuckle 6.9 (net8) with the document pinned by a committed snapshot and a test that regenerates it in memory; one command refreshes it, and the diff is the contract review.
  - Statuses and headers that follow from the pipeline (401, 403, module-gate 404, 412, 428, 429, 503, ETag, Location, paging, Retry-After) come from one operation filter and shared component responses; an action declares only what it decides itself, so 35 actions stay readable.
  - CORS: exact origins validated at start, no credentials (bearer in a header), localhost:4200 in Development only; `/me` reads the same effective-permission query the authorizer uses, so the console and the API cannot disagree.
  - The fitness rule for permission literals now reads the migration's seed data (not just the constants list); "signed in only" became an explicit, closed set.
  - Traps hit: Bruno's module check still expected four modules after S4 added WEBHOOKS; XML doc generation turned two doc-comment slips into build errors; the empty `security: []` of an anonymous operation is dropped by the serializer, so the bearer requirement is added per operation instead of globally.
- **Decision:** accepted as D-042 to D-044 (ADR-012).
- **Why:** a contract that the build checks cannot drift silently, and one server-side definition of "effective permission" keeps the UI honest.

## 2026-11-26/27 - Console and wrap decisions (lead)
- **Asked:** a frontend with Zustand. **AI proposed:** Zustand (vanilla) + TanStack Query, with NgRx SignalStore as the Angular-native alternative. **Decision: overrode** - owner chose SignalStore + TanStack Query (D-046). **Why:** avoids "why a React library in Angular?" with an Angular shop.
- **Asked:** design via Lovable. Lovable ran out of credits mid-way. **AI proposed:** a five-palette study on one rendered screen, crimson rejected because red must mean error. **Decision: accepted** - Vellum light (D-045). **Why:** projector legibility; status colours never collide with the brand.
- **AI proposed:** a live permissions SSE stream with Postgres LISTEN/NOTIFY. **Decision: overrode** - owner cancelled it before any code (D-047). **Why:** protect the submission timeline.
- **AI proposed:** patterns-only import from the owner's earlier work repo. **Decision: overrode** - owner has authority; direct copy allowed, names scrubbed (D-048).
- **AI caught:** a secrets-scan false positive on `web/package-lock.json`; fixed narrowly (package-name lines only) instead of a blanket file exemption, and tested that a token in a lockfile is still rejected.
- **AI caught at wrap:** 232 red tests in 6 s were an OrbStack outage, not code - confirmed from the exception, re-ran green (406/406) after starting the runtime; recorded as failure mode 26.

## 2026-11-27 17:00 - Opening the claims window
- **Asked:** after reading the actual brief (motor and property claims, six APAC markets, one Angular app, 5 h cap), whether to spend the evening polishing the scaffold or to build the claims platform now with a team of agents.
- **AI proposed:** freeze the scaffold, open the window in this repo with a tag (`claims-window-start`) so the window is one `git diff`, and build: Claim aggregate with a guarded state machine, append-only claimant timeline, info requests, claimant-only visibility, REST for commands and queries with an outbox event per transition, workload and exposure as plain SQL, then the Angular claimant/staff/manager screens. Earlier the same session it had argued against building claims before the window, to keep the time cap honest.
- **Decision:** accepted, with the owner's timing (now, not tomorrow) and the owner's confirmation that the panel accepts a disclosed starter. Challenged the AI's scope: a Lovable/TPMS-style claims UI was tempting; the backend stays first, per the brief.
- **Why:** the brief says a strong backend with a partial frontend beats a rushed pair, and the scaffold already carries the plumbing, so the window goes to the domain decisions the panel will probe.

## 2026-11-27 17:12 - Owner code review mid-window
- **Asked:** the owner opened the code and challenged it: the Auth folder had seven files nobody could read and a "hardcoded model"; an audit list action stacked nine `[FromQuery]` attributes; raw SQL was inline in C#; too many tiny files.
- **AI proposed:** measured before agreeing: every controller action is already a one-liner (enforced by arch tests), the audit list is the only outlier; "Claims" in `ClaimsCurrentUser` meant JWT claims and would clash with the new insurance Claims feature; 28 inline SQL statements in 11 files; 250 source files, 53 under 20 lines.
- **Decision:** accepted all four critiques. Renamed the adapter to `JwtCurrentUser`, centralised claim-type names, added a folder map (done). Owner chose `.sql` files over static SQL classes or stored procedures (D-054) and asked for a read-only Postgres MCP for agents; consolidation by concept replaces "one type per file" (D-055). Overrode the AI's first instinct to keep the old convention.
- **Why:** readability for the panel and for agents beats a convention adopted by default; the AI's measurements kept the change targeted instead of a rewrite.

## 2026-11-27 17:20 - Verification caught three false claims; wrap mid-window
- **Asked:** the owner asked for a core doc pack, CI, a reviewer runbook and Bruno for the panel, then to wrap and continue headless.
- **AI proposed:** writer agents verified every doc claim against code and found: rule files said Argon2id but the code uses PBKDF2; the explainer placed 428 in MVC (the handler returns it); the audit diagram drew the success path in the wrong order. The lead ran the new reviewer Bruno command itself and found 2 of 56 requests failing (webhook sink unreachable from the container).
- **Decision:** accepted all corrections; the owner approved amending the Argon2id wording to match the code. Challenged the CI lane's "verified" claim: the YAML parsed but nothing had run, so the lead ran the runbook command end to end (56/56 after the fix).
- **Why:** the panel probes every line; a doc that overclaims is worse than a missing doc.


## 2026-11-27 17:40 - Headless continuation: verify, then run the frontend and the review in parallel
- **Asked:** continue headless from the seed prompt; mid-turn the owner also allowed commits and pushes as the work goes.
- **AI proposed:** re-run the backend lane's numbers before trusting them (build 0 warnings, ArchTests 37 + Tests 616, lead run in `.worktrees/claims` at 506fc47: they matched the lane report). The seed said "rebase, then dispatch the remaining backend slices"; the lane had already done both, so that step was skipped instead of re-run. Because the backend is finished, the Codex seam review and the Angular lane start together instead of in sequence: the review is read-only, the web builder owns only `web/**`, and a later backend fix round commits by pathspec in the same worktree.
- **Decision:** accepted. Deleted the stray `pnpm-lock.yaml`/`pnpm-workspace.yaml` (a pnpm run left them; the repo uses npm). No push: the repo has no remote yet, and creating one (account, visibility) is left to the owner.
- **Why:** about four hours remain; running the review alongside the UI saves the longest serial wait, and the merge surface between `src/**` and `web/**` is empty.

## 2026-11-27 17:50 - Codex reviewed the claims seam; the lead verified before folding
- **Asked:** (headless) a cross-family review of the state machine, visibility and permissions before the merge.
- **AI proposed:** Codex returned five findings and no illegal transition. The lead checked each against the code instead of folding them: #1 (a same-value reserve skips the optimistic-concurrency check) is real because the load takes no row lock and EF sends no UPDATE for an unchanged property; #2 (handlers do not re-check permissions) conflicts with the codebase's one authorization model at the HTTP edge, which an arch test pins; #4 (403 instead of 404) leaks nothing because the 403 is the same for every id.
- **Decision:** accepted #1 and #5 (claim submit now requires an Idempotency-Key: a duplicate claim is real harm), #3 as a plan amendment; declined #2 and #4 with reasons (D-056, `docs/reviews/codex/2026-11-27-claims-seam.md`). The lead also ran the claims Bruno folder against an API built from the branch on a throwaway database: 25/25 requests, 29/29 tests.
- **Why:** a review is evidence, not a verdict; two of five findings would have added a second authorization model or a contract change that buys nothing.

## 2026-11-27 18:00 - Fix round and console land; the merge
- **Asked:** (headless) fix the accepted review findings, finish the claims screens, merge.
- **AI proposed:** two Sonnet lanes side by side in one worktree with disjoint paths (`src/**`+`tests/**` versus `web/**`), commits by pathspec. The fix lane showed each new test RED before its fix (a same-value reserve under a racing write now gets 412; submit without a key gets 400) and kept the OpenAPI diff to one `required: true` line. The web lane's "verify the premises" step caught a false claim in the lead's own brief: the ETag interceptor did NOT send `If-Match` on the new claim command routes (they would all have been 428), so it fixed and pinned the interceptor. It chose to show every currency with two decimals and its ISO code rather than `Intl`'s zero-decimal IDR, because the API stores two minor digits and "$" would make SGD and HKD look alike.
- **Decision:** accepted both lanes after the lead re-ran everything (ArchTests 37 + Tests 618, web 62/62, lint, build, history checked in the reflog: a mistaken amend by the web lane had been undone before anything built on it). Merged `claims-core` with `--no-ff` so the window's claims work is one visible unit. Two process slips (that amend, a broad `pkill` that stopped the lead's test API) are recorded as failure modes, not hidden.
- **Why:** parallel lanes paid off only because ownership was by path and every report was checked against git and a fresh test run, not taken on its word.
