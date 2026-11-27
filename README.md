# Fugue: a claims platform on a .NET 8 + Angular starter

Motor and property claims across six APAC markets (SG, MY, TH, HK, ID, PH), for three audiences in one Angular app: **claimants** report an incident, track it and answer questions; **claims officers** work a queue, pick up, reserve, ask, approve or reject; **managers** settle and see exposure per currency. Backend: .NET 8, Postgres 17, a guarded state machine, an append-only claimant timeline, a transactional outbox, and REST for every command.

**Start here:** [`docs/CLAIMS.md`](docs/CLAIMS.md) (the domain decisions, including REST versus Kafka) and [`docs/runbooks/00-for-reviewers.md`](docs/runbooks/00-for-reviewers.md) (clean clone to a running, seeded stack in about ten minutes).

## Disclosure: what was built before the window, and what in it

- **The starter (built 2026-11-25 to 2026-11-27, before the window, disclosed and confirmed acceptable):** layered vertical slices, typed errors as RFC 9457 problems, ETag/If-Match concurrency, keyset paging, JWT auth with roles, groups, overrides and module toggles, an append-only audit log with field diffs, a transactional outbox with signed webhooks, idempotency keys, an OpenAPI snapshot with a generated Angular client, the Angular console shell, and a one-command Docker stack. It carries a `Customers` example and no claims code.
- **The timed window** starts at git tag **`claims-window-start`** (dd46eca, 2026-11-27 16:56 +08), with a 5-hour hard stop. `git diff claims-window-start..HEAD` is everything built in it: the claims domain, API, seed, tests, the claims screens and their docs.
- **AI use:** the whole repo was built with AI agents under a lead, with mechanical gates and cross-family review. The working journal (a deliverable) is [`docs/ai-journal.md`](docs/ai-journal.md): what was asked, what the AI proposed, what was accepted, overridden or challenged, and why. The operating model is in [`docs/ai/how-we-use-ai.md`](docs/ai/how-we-use-ai.md).

## Quickstart (Docker only)

```bash
cp .env.example .env
sed -i.bak "s|REPLACE_ME|$(openssl rand -hex 16)|g; s|GENERATE_WITH_openssl_rand_base64_32|$(openssl rand -base64 32)|g" .env && rm .env.bak
docker compose up -d --build     # postgres, a one-shot migrate, the API (Development, seeded) and the console
docker compose ps                # postgres, api, web (healthy)
```

Open **http://localhost:8080** (console; the API is on :5081, Swagger at `/swagger`). The sign-in page lists the seeded accounts; all share the development-only password `Fugue-Dev-Passw0rd!`.

| Account | Role | Try |
|---|---|---|
| `self@fugue.test` | claimant | My claims, report an incident, answer the open question on a seeded claim |
| `staff@fugue.test` | claims officer | the queue, pick up, set a reserve, ask, approve or reject; Workload |
| `manager@fugue.test` | manager | settle an approved claim; Exposure per currency |
| `admin@fugue.test` | admin | everything, including the audit log and module toggles |

Curl walk-through of every guard (404, 409, 412, 428, the required Idempotency-Key): [`docs/runbooks/11-claims.md`](docs/runbooks/11-claims.md). Day-to-day development without Docker for the API: [runbook 01](docs/runbooks/01-local-setup.md) and [runbook 09](docs/runbooks/09-console.md).

## Tests

```bash
dotnet test                       # unit, integration (Testcontainers Postgres) and architecture tests; needs Docker
cd web && npm ci && npm test      # console unit tests (Vitest)
npm ci && (cd bruno/fugue && npx bru run --env compose)   # the API regression suite against the stack (runbook 00, step 5)
```

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
