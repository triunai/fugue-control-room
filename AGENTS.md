# AGENTS.md

Instructions for coding agents working in this repo. Pairs with **`CLAUDE.md`** (the constitution: security model and hard rules) and **`hygiene.md`** (the doc/state router).

## Setup

- `npm install` installs lefthook and commitlint (the `prepare` script runs `lefthook install`). Do not set `core.hooksPath`.
- `dotnet restore && dotnet build --no-restore && dotnet test --no-build` once a solution exists (Phase 1).
- Copy `.env.example` to `.env` (untracked) or use `dotnet user-secrets`. Never commit real values.

## The guardrail stack

Gates fire at junctures, never per edit. All are scripts under `scripts/spine/` (and `.githooks/commit-msg`) called by `lefthook.yml`.

| Juncture | Runs | Blocks? |
|---|---|---|
| commit | `secrets-scan`, `dotnet format --verify-no-changes` (only with `*.cs` staged and a solution present), `test-integrity` | yes; test-integrity is advisory |
| commit-msg | kit rule + commitlint (Conventional Commits, scope warn) | yes |
| push | `stack-check --test` (restore, build, test; skips with no solution), `ledger-check`, `spine-nudge` | yes; nudge is warn-only |

### Toggle

- Tags: `baseline` (always on) and `guardrail` (advisory). Pause the advisory layer for one command: `LEFTHOOK_EXCLUDE=guardrail git push`.
- `git commit --no-verify` skips everything. Do not use it to dodge a real finding.

## The doc control-plane (each file answers ONE question)

| File | The ONE question it answers |
|---|---|
| `hygiene.md` | Where is everything, and what is each verb's step order? |
| `docs/conventions/doc-spine.md` | What are the rules and why? |
| `docs/hot-state.md` | Where did we stop? |
| `docs/workstreams.md` | What threads exist? |
| `docs/decisions.md` | Why is it this way? |
| `docs/state-backlog.md` | What is still open? |
| `docs/project-log.md` | What happened, and when? |
| `docs/adr/` | Which architecture decisions were made? |
| `docs/migrations/migration-catalog.md` | Which migrations exist? |

A fact lives in one file; others link to it.

## Review gate matrix

Critical seams (always get an independent review before merge): **migrations**, **auth** (JWT validation, policies, password hashing), **tenancy** (tenant resolution, query filters, anything touching `tenant_id`), `ApiControllerBase`, payments and idempotency.

- Plan gate: before coding anything on a critical seam, write the plan in `docs/plans/`.
- Final-diff gate: a different agent or model family reviews the diff of a critical seam; Critical, High and Medium findings go into a fix round and get re-reviewed.
- Migration PRs: catalog updated in the same commit, rollback written, tenancy checklist in the PR template.
- Author and reviewer are separate passes. Never self-approve.

## Pin every catch

A review catch you do not pin is a bug you rent. No High/Medium finding closes without a **regression test** (or an architecture test for structural rules). Record heavy catches in `docs/decisions.md` or `docs/postmortems/`; lighter ones in `docs/state-backlog.md`.

## Hard rules (full detail in CLAUDE.md)

- Tenant comes from the verified JWT claim after authentication. Never from a header, query or body.
- `tenant_id` on every tenant-owned row; EF global query filter; cross-tenant-leak test per module.
- Fallback authorization policy is "authenticated".
- Controllers inherit `ApiControllerBase`; actions are one-liners that dispatch; logic lives in handlers.
- Money is `bigint` minor units + currency code. Keyset pagination. Idempotency keys on `POST /orders` and payments.
- Passwords via `PasswordHasher<T>` / Argon2id only.
- Any migration updates `docs/migrations/migration-catalog.md` in the same commit.
- No secrets in the repo; no commit or push unless the owner asks.

## Session wrap

End of a session: follow the WRAP row in `hygiene.md` (hot-state, board, decisions, log, backlog, seed; `bash scripts/spine/ledger-check.sh` must exit 0). `spine-nudge` warns at push when code changed but no spine file did.
