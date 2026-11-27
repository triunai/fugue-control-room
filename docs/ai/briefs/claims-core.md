# Lane claims-core (Sonnet builder): the claims backend inside the timed window

## Objective
Build the backend of the claims platform described in `docs/plans/claims-window.md` (decisions A-L) on top of the Fugue scaffold, fast and correct, committing per slice. This is the graded part of a take-home: every line must be defensible, tested, and follow the scaffold's own rules.

## Lane facts
- Worktree: `~/Repos/personal-work/fugue/.worktrees/claims` (branch `claims-core`, base = tag `claims-window-start`, dd46eca). Work and commit ONLY here. The lead merges to main.
- Env: `export DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$HOME/.orbstack/bin:$PATH DOTNET_CLI_TELEMETRY_OPTOUT=1 DOCKER_HOST=unix://$HOME/.orbstack/run/docker.sock TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE=/var/run/docker.sock`. Probe docker: `perl -e 'alarm 15; exec @ARGV' docker ps`. Baseline at the tag: build 0 warnings, ArchTests 36 + Tests 400 green.
- READ FIRST (rules you must follow): `CLAUDE.md`, `AGENTS.md`, `docs/plans/claims-window.md` (THE SPEC), `src/Fugue.Application/Features/README.md` (slice shape).
- TEMPLATES to copy the shape from (the Customers slice is the reference implementation of every pattern you need):
  - aggregate + guarded transitions + domain events: `src/Fugue.Domain/Customers/{Customer.cs,CustomerEvents.cs,CustomerStatus.cs}`; typed id: `src/Fugue.Domain/Ids/CustomerId.cs` (CLAUDE.md "Adding a strongly-typed ID").
  - slices: `src/Fugue.Application/Features/Customers/**` (commands with validator + handler in one file, `OwnerScope`, `CustomerErrors`, queries with keyset paging).
  - persistence: `src/Fugue.Infrastructure/Persistence/Customers/` (EF reads, xmin-checked updates, Dapper insert via `SqlExecutor` inside `IUnitOfWork`), `Configurations/CustomerConfiguration.cs`.
  - controller: `src/Fugue.Api/Controllers/CustomersController.cs` (one-liners, `[Module]`, `[HasPermission]`, `[IfMatch]`, `[ProblemResponse]`, `[Idempotent]` on create).
  - permissions/modules/roles seed: `src/Fugue.Infrastructure/Persistence/IdentityCatalog.cs` + the `Permissions` constants (find them) + how the S3/W-016 migrations seeded them; dev users: `src/Fugue.Infrastructure/Identity/IdentitySeeder.cs`; dev data: `src/Fugue.Infrastructure/Persistence/Seeding/CustomerSeeder.cs`.
  - append-only trigger: the `audit_log` trigger in migration `20261125162316_AddAuditLog.cs` and its schema test.
  - outbox: `src/Fugue.Application/Abstractions/IOutbox.cs`, `src/Fugue.Infrastructure/Outbox/*` (domain events -> outbox rows in the same SaveChanges; Dapper path calls IOutbox).
  - fitness rules you will hit: `tests/Fugue.ArchTests/FitnessTests.cs` + `Baselines/` (shrink-only; never widen; fix the code instead).
- NOTE: the idempotency `[Idempotent]` filter lives on branch `w007-idem` and may not be on your base yet. If `[Idempotent]` does not exist at your base, leave a `// TODO(lead): [Idempotent] after W-007 merge` comment is NOT allowed (no placeholders): instead simply omit it and report it; the lead adds the attribute at merge.
- Another builder works on main in `web/` (shell). The frontend lane for claims starts after your CONTRACT commit (slice 3). Nobody else touches `src/`.

## Slices (commit each, suite green at each)
1. `feat(domain): Claim aggregate with its state machine` — ClaimId, ClaimInfoRequestId (and ClaimTimelineEntryId if an entity), enums (ClaimStatus, LineOfBusiness, Market with its currency), Claim with transitions returning `Result` (409 `claim.invalid_transition` with from/to in the message, no user data), domain events per transition. Unit tests: the full transition table (every legal move and every illegal move), approve-amount rules (needs reserve; 0 < amount <= reserve), reserve > 0, terminal states.
2. `feat(persistence): claims, timeline and info requests` — EF configurations, ONE migration (`AddClaims`): tables `claims`, `claim_timeline` (append-only trigger: UPDATE/DELETE/TRUNCATE refused, same as audit_log), `claim_info_requests`; indexes for the queue (status, assigned_officer_id, created/submitted at, id) and for the claimant (claimant_user_id, submitted_at desc, id desc) and exposure (status, market, line); CLAIMS module + resources/actions + the seven permissions + MANAGER role + grants per decision G seeded in the migration the way earlier migrations seed the catalog; `docs/migrations/migration-catalog.md` row in the SAME commit; schema tests (trigger refuses update/delete/truncate; seeded permissions exist; MANAGER role exists). Number sequence: a Postgres sequence per year is fine, or `claims_number_seq` with year prefix; keep it simple and race-safe.
3. `feat(api): claims contract` — controller `ClaimsController` (`[Module("CLAIMS")]`) with every endpoint below wired to real handlers, DTOs, `[ProblemResponse]` codes, OpenAPI snapshot refreshed (`npm run api:openapi`). Claimant DTO hides the reserve; staff DTO shows it (one DTO with a nullable reserve filled only for `CLAIMS_ALL` is fine). Endpoints (all under `/api/v1/claims`):
   - `POST /` submit (CLAIMS_SUBMIT; claimant = caller; market, line, policyNumber, incidentDate (not future, not older than 1 year), description, location?) -> 201 + Location + ETag.
   - `GET /` list (CLAIMS_READ; claimant: own only; staff with CLAIMS_ALL: all; filters `status` (multi), `assigned` = `me|none|any`, `market`, `line`, `q` (number or policy number); keyset paging like customers; newest first for claimants, OLDEST first for the staff queue when `status=Submitted`... keep one order: `submitted_at, id` ascending for staff queue views, descending for claimants; document it).
   - `GET /{id}` (own or CLAIMS_ALL; otherwise 404), `GET /{id}/timeline` (same visibility; claimant-safe notes).
   - `POST /{id}/pickup` (CLAIMS_HANDLE, If-Match) Submitted -> UnderReview, assigns caller.
   - `PUT /{id}/reserve` (CLAIMS_HANDLE, If-Match) body `{amountMinor}`; allowed in UnderReview/InfoRequested.
   - `POST /{id}/info-requests` (CLAIMS_HANDLE, If-Match) body `{question}` -> InfoRequested.
   - `POST /{id}/info-requests/{requestId}/response` (CLAIMS_RESPOND, own claim, If-Match) body `{answer}` -> UnderReview.
   - `POST /{id}/decision` (CLAIMS_HANDLE, If-Match) body `{outcome: approve|reject, amountMinor?, reason}` -> Approved | Rejected.
   - `POST /{id}/settle` (CLAIMS_SETTLE, If-Match) Approved -> Settled.
   - Only the ASSIGNED officer may request info / decide unless the caller also has CLAIMS_SETTLE (manager override); otherwise 409 `claim.not_assigned_to_you`.
4. `feat(application): claim handling slices` — if not already complete in 3: handlers, validators, timeline rows (one per transition, same transaction), info-request rows, outbox events `claim.*.v1` with thin masked payloads (claim id, number, status, market; never description or amounts beyond what the scaffold's masking allows), audit rows come from the scaffold automatically. Integration tests (Testcontainers): claimant submits -> staff picks up -> requests info -> claimant answers -> reserve -> approve -> settle, asserting statuses, timeline rows, outbox rows per event; another claimant gets 404 on GET/timeline/respond; a second officer picking up the same claim with the old ETag gets 412, with a fresh ETag gets 409; deciding without a reserve 409; approve over reserve 400/409 (pick and document); missing If-Match 428; staff without CLAIMS_SETTLE cannot settle (403); module CLAIMS off -> 404.
5. `feat(application): workload and exposure` — decisions I and J. `GET /claims/workload`, `GET /claims/exposure`, DTOs, SQL via EF (`FromSql`/LINQ) or Dapper-READ is NOT allowed (no reads through Dapper; use EF LINQ or EF raw SQL), snapshot refreshed. Tests: numbers on a known dataset; exposure never sums across currencies; terminal claims excluded from outstanding reserve; approved-not-settled counted separately.
6. `feat(infra): seed claims demo data` — Development seeder (same pattern and guard as CustomerSeeder; idempotent): ~20 claims across the six markets, both lines, every status, realistic amounts in minor units per currency, timelines consistent with status, some assigned to `staff@fugue.test`, three owned by `self@fugue.test`, one InfoRequested for `self@` with an open question; `manager@fugue.test` seeded with the MANAGER role (IdentitySeeder). Runbook line in `docs/runbooks/05-auth.md` (accounts table) and a new `docs/runbooks/11-claims.md` (endpoints, curl walk-through of the happy path, the 404/409/412/428 demos).
7. `test(api): Bruno claims collection` — `bruno/fugue/claims/` covering the happy path and the error demos (run against a live API built from your branch on a free port if feasible; otherwise say so).
Trailers on every commit: `Refs: W-020, D-053` then `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>`. Body per AGENTS.md (root cause, scope, verification with real counts, `Do better:`).

## Validation (every slice; paste real counts in the commit body)
```
dotnet build 2>&1 | grep -E "Warning\(s\)|Error\(s\)"
dotnet test --no-build 2>&1 | grep -E "Passed!|Failed!"
dotnet format --verify-no-changes && echo FORMAT_OK
bash scripts/spine/secrets-scan.sh
```
After slice 3 and 5: `npm run api:openapi` and confirm the snapshot test passes.

## Non-goals
Frontend (another lane), attachments/files, a payment saga or payment-failure state, Kafka, market-scoped toggles, delegated authority limits, notifications beyond the outbox event, changing scaffold behaviour (customers, audit, auth) except to register the new module/permissions.

## Stop conditions (halt and report)
A fitness rule can only pass by widening a baseline; a scaffold invariant would have to change; suite red you cannot attribute after one OrbStack restart; `--no-verify`/force/reset of a non-throwaway DB would be needed.

## Time
The window's hard stop is 21:56 +08. Aim: slice 3 (contract) committed by ~18:15, slice 5 by ~19:30, 6-7 by ~20:15. If you are behind, cut in this order: Bruno (7), median decision time, `q` search, location field; never cut tests for transitions, visibility, 412/409/428.

## Verify the premises
Verify every claim in this brief and the plan against the code before acting. If something is false, say so with file:line and follow the truth (and the plan's intent).

## Report (final message)
Commits (sha + subject), counts, deviations from the plan and why, cuts made, "What I could NOT verify". After slice 3, ALSO send the lead a one-line message (SendMessage to "main" if available, otherwise include it in your final report) saying the contract commit sha so the frontend lane can start.

## Recite
Claims backend per docs/plans/claims-window.md: aggregate + state machine (tests for every move), schema + append-only timeline + seeded permissions/MANAGER role, the claims REST contract early, handling slices with timeline + outbox events + visibility/412/409/428 tests, workload + exposure (per currency), dev seed + runbook, Bruno. Commit per slice in `.worktrees/claims`, `Refs: W-020, D-053`.
