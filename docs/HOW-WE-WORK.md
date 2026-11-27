# How we work

Fugue was built with AI, openly. This page says how, what stops the AI from doing damage, and how to check any claim yourself. The rule behind all of it: a model's report is not evidence. Git, tests and gates are.

Architecture is in [`ARCHITECTURE.md`](ARCHITECTURE.md). The longer narrative, with what is and is not proven, is in [`docs/ai/how-we-use-ai.md`](ai/how-we-use-ai.md).

## 1. The operating model

| Role | Does |
|---|---|
| Owner | Sets the goal, makes the calls, can overrule any model. |
| Lead (Opus) | Plans, writes a task contract per lane, dispatches, then re-verifies every report against git and the test run before accepting it. |
| Builders (Sonnet) | Implement one bounded lane from a contract, in their own worktree. |
| Sweeps (Haiku) | Bounded mechanical work and audits whose output is checked by a person or a script. |
| Cross-family reviewer (Codex) | Reviews plans and diffs on critical seams. A different model family from the author, on purpose. |

Model choice is routing, not loyalty: the expensive model where a mistake is costly (seams, integration, review), cheaper ones where a machine checks the output.

**The task contract.** Before a builder starts, the lead writes a contract ([`.claude/skills/agent-task-contract`](../.claude/skills/agent-task-contract/)): objective, current state, desired state, allowed files, architecture rules, acceptance criteria, literal validation commands, non-goals, stop conditions and commit slices. It also makes the builder verify the premises it was handed and list what it could not verify. Contracts exist because briefs written from memory are sometimes already false ([`docs/failure-modes.md`](failure-modes.md), #11).

**Review.** Author and reviewer are separate passes. Nobody approves their own work. Critical seams (migrations, auth, tenancy when on, `ApiControllerBase`, idempotency, webhook signing) get a written plan and an independent diff review ([`AGENTS.md`](../AGENTS.md), "Review gate matrix"). Critical, High and Medium findings go into a fix round and are re-reviewed.

**Codex reviews are kept.** Each one is filed in [`docs/reviews/codex/`](reviews/codex/) with a disposition per finding: folded in, accepted as low, or rejected with a reason. Example: the idempotency plan came back "revise" with five High findings, and the design changed from an action filter to a resource filter because of it ([review](reviews/codex/2026-11-27-w007-idempotency-plan.md), then [ADR-013](adr/ADR-013-idempotency-keys.md)).

**Pin every catch.** A High or Medium finding does not close without a regression test, or an architecture test for structural rules (D-012). That is how review turns into permanent checks.

## 2. The gates

Gates fire at junctures, not per edit. [`lefthook.yml`](../lefthook.yml) is the one hook orchestrator. `core.hooksPath` is deliberately unset, because two orchestrators would conflict.

| Juncture | What runs | Blocks? |
|---|---|---|
| commit | `scripts/spine/secrets-scan.sh`; `dotnet format --verify-no-changes` when `*.cs` is staged; `scripts/check-test-integrity.mjs` (new `Skip =`, dropped assertions, deleted tests); `scripts/spine/prune-check.mjs` | yes, except the last two (advisory) |
| commit-msg | `.githooks/commit-msg` (Conventional Commits shape, length, no pasted file lists); commitlint; `scripts/check-refs-trailer.mjs`; `scripts/check-fix-has-test.mjs` | yes |
| push | `scripts/spine/stack-check.sh --test` (restore, build, test); `scripts/spine/ledger-check.sh`; `scripts/spine/adr-check.mjs` | yes |
| push, advisory | `commit-link-check.mjs`, `trace-index.mjs --check`, `scripts/codex-gate.mjs` (seam-triggered Codex review, detached, capped at 8 a day), `spine-nudge.sh` | never blocks |
| every Bash call by Claude Code | `scripts/safe-command-guard.mjs`, a PreToolUse hook set in `.claude/settings.json` | yes |

What the commit-msg gates mean in practice:

- An agent commit (a `Co-Authored-By: Claude` trailer) must end with a `Refs:` trailer naming ids that resolve: `W-NNN`, `D-NNN` or `ADR-NNN`. A human commit is exempt.
- A `fix(...)` commit that touches `src/` must also touch `tests/`, or carry `Tests: none (<reason>)`.
- The guard refuses force pushes, `--no-verify`, a hard reset on `main`, a stray `rm -rf`, `curl | sh`, printing secrets and an unflagged `ef database drop`.

Hooks are client-side. `--no-verify` skips them, and there is no CI yet. The guard blocks the flag for agents, but it is a guard, not a backstop. Say so plainly: the real backstop would be CI, and it is not built.

**Architecture fitness functions.** [`tests/Fugue.ArchTests/FitnessTests.cs`](../tests/Fugue.ArchTests/FitnessTests.cs) holds rules named `Fitness_<module>_<rule>` (layering, controller shape, audit coverage, auth declarations, migration catalog rows, OpenAPI responses). Known exceptions sit in [`tests/Fugue.ArchTests/Baselines/`](../tests/Fugue.ArchTests/Baselines/) and may only shrink: a baseline line that is no longer a violation fails the test.

**OpenAPI snapshot.** [`docs/api/openapi.v1.json`](api/openapi.v1.json) is the committed contract. A test in `tests/Fugue.Tests/OpenApi/` builds the document and fails if it differs. `npm run api:openapi` refreshes it, so every contract change shows up as a diff next to its cause.

## 3. The doc spine and the hygiene router

State is kept in a small set of files, each answering one question. [`hygiene.md`](../hygiene.md) is the router: it maps a verb to ordered steps. The reasons are in [`docs/conventions/doc-spine.md`](conventions/doc-spine.md).

| Verb | Meaning |
|---|---|
| hydrate | Cold start: pick the live worktree, read the seed and `docs/hot-state.md`, skim the board. |
| wrap | End of session: capture decisions, close finished threads, overwrite hot-state, write the handoff, run the ledger check. |
| park, pick up | Pause a thread with a `Resume:` line; resume it later. |
| land | Check merged lanes on a throwaway branch, with proof from someone other than the builder. |
| new task | Add the thread to the board before any work starts. |

How a fact ages:

1. **Workstream.** Every ask becomes a `W-NNN` row on [`docs/workstreams.md`](workstreams.md) first, before work.
2. **Decision.** Every call that settles is a `D-NNN` entry in [`docs/decisions.md`](decisions.md) the same turn. If it changes architecture, a public contract, the data model, security posture or a cross-cutting convention, it is promoted to an ADR in [`docs/adr/`](adr/). `adr-check.mjs` fails a push on an ADR no `D` entry links, or a `Promote: yes` without its file.
3. **Close.** When a `W` is done, `node scripts/spine/close-workstream.mjs W-NNN --outcome "..."` writes one entry, copied verbatim into [`docs/project-log.md`](project-log.md) and [`CHANGELOG.md`](../CHANGELOG.md), and deletes the board row. It refuses to close a thread twice. Old log entries are moved to `docs/archive/` by `archive-project-log.mjs`; the CHANGELOG keeps everything.
4. **Migrations.** A migration updates `docs/migrations/migration-catalog.md` in the same commit. The fitness rule `Fitness_Migrations_every_migration_has_a_catalog_row` fails otherwise.
5. **Traceability.** [`docs/traceability.md`](traceability.md) is generated by `npm run trace` (`scripts/spine/trace-index.mjs`) and never edited by hand. It links every commit to its `W`, `D` and ADR ids and back, and lists commits with no link. Regenerate it after the spine commit and commit it alone.
6. **Traps.** [`docs/failure-modes.md`](failure-modes.md) holds each trap we actually hit, with the move that defuses it.
7. **Resume point.** [`docs/hot-state.md`](hot-state.md) is overwritten at each wrap. It says where we stopped and the exact next action. Read it first.

Rules about the spine: one writer (the lead) edits spine files and mints numbers; build lanes do not. A fact lives in one file and the others link to it. The router carries no counts or current state, because they rot.

## 4. The AI working journal

[`docs/ai-journal.md`](ai-journal.md) records where an AI shaped the repo and what the owner did with it. Each entry has: **Asked**, **AI proposed**, **Decision** (accepted, challenged or overrode) and **Why**.

To find the owner's overrides, search for the decision word:

```bash
grep -n "overrode" docs/ai-journal.md
```

Examples already in it: the AI proposed row-level security for tenancy and the owner chose a `tenant_id` column, later a config toggle ([ADR-003](adr/ADR-003-single-tenant-default-tenancy-toggle.md)); a review said keep a hand-rolled result type and the owner chose FluentResults ([ADR-004](adr/ADR-004-fluentresults-with-errortype.md)); a review said defer Dapper and the owner kept it, with two conditions the AI added ([ADR-006](adr/ADR-006-ef-core-reads-dapper-writes.md)). Entries marked "pending owner review" are exactly that. Nothing there is claimed beyond what it says.

## 5. Auditing a commit

Commit messages follow a fixed shape ([`AGENTS.md`](../AGENTS.md), "Commit messages"). Read one in full:

```bash
git log --format=%B -n 1 <sha>
```

Every body has, in order:

1. **Root cause**: why the change exists; for a fix, what was actually wrong.
2. **Scope**: what is in, what is deliberately out.
3. **Verification**: the commands that were run and the counts.
4. **`Do better:`** one line, the process lesson.
5. **Trailers**: `Refs:` (the `W`, `D` or ADR ids), `Tests: none (<reason>)` where needed, `Co-Authored-By:`.

Useful queries:

```bash
git log --grep='^Refs: W-007' --format='%h %s'      # everything done for one workstream
git log --grep='Co-Authored-By: Claude Sonnet' --format='%h %s'   # what a builder model wrote
git log --format='%h %s%n%b' | grep -A1 'Do better:'              # the lessons, in order
npm run trace && sed -n 1,40p docs/traceability.md                # commit <-> W/D/ADR index
```

A commit that says "tests pass" should name the command and the count. If it does not, treat the claim as unverified. Commits are scoped by pathspec (`git add -- <paths>` then `git commit -- <paths>`), so a parallel lane's staged files do not ride along (failure modes #10 and #19).

## 6. How to verify me

Prefix .NET commands in a shell that does not have the SDK on `PATH`:

```bash
export DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH DOTNET_CLI_TELEMETRY_OPTOUT=1
```

| Check | Command | Notes |
|---|---|---|
| Build | `dotnet restore && dotnet build --no-restore` | Warnings are errors; expect 0 warnings. |
| Tests and architecture rules | `dotnet test --no-build` | Needs a container runtime: `Fugue.Tests` uses Testcontainers with real Postgres. `Fugue.ArchTests` needs none. |
| Architecture rules only | `dotnet test tests/Fugue.ArchTests` | Fast, no Docker. |
| Gate scripts | `npm install && npm run test:scripts` | Tests the hook and spine scripts with `node --test`. |
| OpenAPI contract | `dotnet test tests/Fugue.Tests --filter "FullyQualifiedName~OpenApiContractTests"` | Fails if the snapshot drifted. |
| API regression suite | `npm run api:test` | Bruno suite against a running API (local `.env`, `docker compose up -d`, seeded). See [runbook 04](runbooks/04-customers-api.md). |
| Whole stack | `docker compose up -d --build` | Postgres, one-shot `migrate`, API, console. Console at `http://localhost:8080`, API at `http://localhost:5081`. Needs `.env` ([runbook 10](runbooks/10-docker-compose.md)). Stop with `docker compose down`; never `-v`, it deletes the data. |
| Console | `cd web && npm test` | Vitest unit tests ([`web/README.md`](../web/README.md)). |
| Doc links | `bash scripts/spine/ledger-check.sh` | Must exit 0. |
| Traceability fresh | `node scripts/spine/trace-index.mjs --check` | Warns if `docs/traceability.md` is stale. |

All of these are in [`CLAUDE.md`](../CLAUDE.md) under "Commands"; the local setup walkthrough is [runbook 01](runbooks/01-local-setup.md).

Do not trust a number in a document, including this one. Run the command.
