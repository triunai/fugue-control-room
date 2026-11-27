# Fugue: a claims platform on a .NET 8 + Angular starter

Motor and property claims across six APAC markets (SG, MY, TH, HK, ID, PH), for three audiences in one Angular app: **claimants** report an incident, track it and answer questions; **claims officers** work a queue, pick up, reserve, ask, approve or reject; **managers** settle and see exposure per currency. Backend: .NET 8, Postgres 17, a guarded state machine, an append-only claimant timeline, a transactional outbox, and REST for every command.

**Start here:** [`docs/CLAIMS.md`](docs/CLAIMS.md) (the domain decisions, including REST versus Kafka) and [`docs/runbooks/00-for-reviewers.md`](docs/runbooks/00-for-reviewers.md) (clean clone to a running, seeded stack in about ten minutes).

## Disclosure: what was built before the window, and what in it

- **The starter (built 2026-11-25 to 2026-11-27, before the window, disclosed and confirmed acceptable):** layered vertical slices, typed errors as RFC 9457 problems, ETag/If-Match concurrency, keyset paging, JWT auth with roles, groups, overrides and module toggles, an append-only audit log with field diffs, a transactional outbox with signed webhooks, idempotency keys, an OpenAPI snapshot with a generated Angular client, the Angular console shell, and a one-command Docker stack. It carries a `Customers` example and no claims code.
- **The timed window** starts at git tag **`claims-window-start`** (dd46eca, 2026-11-27 16:56 +08), with a 5-hour hard stop. `git diff claims-window-start..HEAD` is everything built in it: the claims domain, API, seed, tests, the claims screens and their docs.
- **AI use:** the whole repo was built with AI agents under a lead, with mechanical gates and cross-family review. The working journal (a deliverable) is [`docs/ai-journal.md`](docs/ai-journal.md): what was asked, what the AI proposed, what was accepted, overridden or challenged, and why. The operating model is in [`docs/ai/how-we-use-ai.md`](docs/ai/how-we-use-ai.md).

## Quickstart (Docker only)

You need Docker and nothing else: Postgres, the schema migrations and the seed data all run in containers.

```bash
./start.sh   # creates .env with fresh secrets, moves any port already in use, builds, waits, prints the URLs
```

The same by hand (or on Windows without Git Bash):

```bash
cp .env.example .env
sed -i.bak "s|REPLACE_ME|$(openssl rand -hex 16)|g; s|GENERATE_WITH_openssl_rand_base64_32|$(openssl rand -base64 32)|g" .env && rm .env.bak
docker compose up -d --build     # postgres, a one-shot migrate, the API (Development, seeded) and the console
docker compose ps                # postgres, api, web (healthy)
```

Open **http://localhost:8080** (console; the API is on :5081, Swagger at `/swagger`). The sign-in page lists the seeded accounts; all share the development-only password `Fugue-Dev-Passw0rd!`.

| Account | Role | Try |
|---|---|---|
| `self@fugue.test` | claimant | lands on the claim that needs them ("We need one answer from you"), answers it; reports an incident; follows the claim and money tracks |
| `staff@fugue.test` | claims officer | lands on My work (needs my action, pick up next, waiting on claimant); the claim record's next-step card says what to do and why; assessment worksheet; reserve sheet; SLA calendar |
| `manager@fugue.test` | manager | lands on To settle; Team and exposure (who is overloaded, what is breaching, where the money is, per currency) |
| `admin@fugue.test` | admin | everything: users and the roles x permissions matrix, audit log with search, module toggles, webhooks with the dev sink showing `claim.*` events |

Curl walk-through of every guard (404, 409, 412, 428, the required Idempotency-Key): [`docs/runbooks/11-claims.md`](docs/runbooks/11-claims.md). Day-to-day development without Docker for the API: [runbook 01](docs/runbooks/01-local-setup.md) and [runbook 09](docs/runbooks/09-console.md).

## Beyond the brief (added after the claims core, at the owner's request)

The timed claims build is the range between the tags `claims-window-start` and `claims-window-end` (`git log claims-window-start..claims-window-end`); what follows the second tag is listed here.

Kept out of the demo script (`docs/DEMO.md`) on purpose; each is documented where it lives.

| Extra | Where |
|---|---|
| Console shell from the owner's reference design; a reviewer tour per persona and per page (Help, `?tour=off`) | `web/src/app/features/shell/` |
| Univer reserve sheet and claim assessment worksheet, both with Open in Excel; FullCalendar SLA calendar (lazy-loaded; bundle cost in `web/README.md`) | `web/src/app/features/claims/`, D-059 |
| Meilisearch audit search (optional service, indexer from the append-only log) | runbook 06, D-058 |
| Kafka publisher behind the event port (optional compose profile) | runbook 12, ADR-011 amendment |
| Mutation testing (Stryker.NET), pairwise cases (PICT), k6 load scripts | `docs/quality/`, `perf/k6/` |
| How the AI harness runs this repo | `docs/ai/harness.md` |

## Tests

```bash
dotnet test                       # unit, integration (Testcontainers Postgres) and architecture tests; needs Docker
cd web && npm ci && npm test      # console unit tests (Vitest)
npm ci && (cd bruno/fugue && npx bru run --env compose)   # the API regression suite against the stack (runbook 00, step 5)
```

| Suite | Size (2026-11-27, v0.0.2) | Runs against |
|---|---|---|
| `Fugue.Tests` (xUnit) | 740 cases from 489 methods: 374 methods against a real Postgres 17 (Testcontainers) or the full HTTP host, 115 in-process (state machine, validators, mapping) | `dotnet test`; the pre-push hook runs it |
| `Fugue.ArchTests` (NetArchTest) | 37 fitness functions: layering, one-line controllers, every action declares a permission; baselines may only shrink | `dotnet test` |
| Bruno | 101 requests, 123 tests: every guard (404, 409, 412, 428, 422, the required Idempotency-Key) over HTTP | `npx bru run --env compose` against the Docker stack |
| Console (Vitest) | 208 tests | `cd web && npm test` |
| Console e2e (Playwright) | 20 flows in a browser, claimant to settlement | `web/e2e/`, runbook 09 |

**Why this many, and why these.** The code was written mostly by AI agents, so the tests are the specification they cannot argue with: each pins an invariant of the domain ("a stale ETag is 412 and nothing is written", "outside your scope is 404, never 403", "exposure is never summed across currencies", "the audit log cannot be written through the API"). They are deterministic on purpose (a real database instead of mocks of it; time-dependent rules on a fake clock, `FakeTimeProvider`; waits are bounded polls or timeouts, never a sleep-then-assert), so a red test means the code drifted, not that the test flaked; the hooks run them before every push, so drift does not reach the remote. Each page of the console names its own tests in the "Under the hood" panel.

## Documents

| Read | For |
|---|---|
| [`docs/CLAIMS.md`](docs/CLAIMS.md) | The claims domain: state machine, visibility, concurrency, REST vs Kafka, workload and exposure, money, what was cut |
| [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) | Layers, the request pipeline, persistence, auth, audit, outbox |
| [`docs/HOW-WE-WORK.md`](docs/HOW-WE-WORK.md) | Workflow, gates, commits, reviews |
| [`docs/ai-journal.md`](docs/ai-journal.md) | The AI working journal |
| [`docs/plans/claims-window.md`](docs/plans/claims-window.md) | The window's plan, decisions A to L, cut lines |
| [`docs/decisions.md`](docs/decisions.md), [`docs/adr/`](docs/adr/) | Every decision (D-NNN) and the architecture records |
| [`docs/reviews/codex/`](docs/reviews/codex/) | Cross-family reviews and how each finding was dispositioned |
| [`docs/runbooks/`](docs/runbooks/) | Step-by-step guides with expected output (00 reviewers, 10 Docker, 11 claims) |
| [`docs/failure-modes.md`](docs/failure-modes.md) | Traps hit while building, and the defusing move for each |

## Layout

```
src/Fugue.Domain          aggregates (Claim and its state machine), typed ids, errors
src/Fugue.Application     feature slices (Features/Claims: commands, queries, scope), pipeline behaviours, ports
src/Fugue.Infrastructure  EF Core + Dapper persistence, migrations, outbox and webhooks, audit, seeders
src/Fugue.Api             one-line controller actions, auth, idempotency filter, problem details
web/                      the Angular console (TanStack Query, NgRx SignalStore, generated client)
tests/                    Fugue.Tests (unit + Testcontainers integration), Fugue.ArchTests (layering and fitness rules)
bruno/fugue               HTTP regression collection (claims/ is the window's folder)
docs/                     the documents above
```

## With more time

- An exposure projection fed by the claim events instead of live SQL; a Kafka `IEventPublisher` adapter keyed by claim id, and an authorizer for any consumer that dispatches commands.
- A settlement saga with a payments consumer and payment failure; fraud and SLA rules as event consumers.
- Property-specific fields, attachments (object storage, virus scan), per-market business calendars for ageing, manager drill-down from exposure to claims.
- Splitting claims into its own deployable along the module line it already has.
- Running the CI workflow (`.github/workflows/ci.yml`) on a real remote; it is written and not yet run.

## License

Not yet chosen.
