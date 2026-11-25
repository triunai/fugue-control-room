# AGENTS.md

Instructions for coding agents working in this repo. Pairs with **`CLAUDE.md`** (the constitution: security model and hard rules) and **`hygiene.md`** (the doc/state router).

## Setup

- `npm install` installs lefthook and commitlint (the `prepare` script runs `lefthook install`). Do not set `core.hooksPath`.
- Prefix .NET commands with `export DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH DOTNET_CLI_TELEMETRY_OPTOUT=1;` then `dotnet restore && dotnet build --no-restore && dotnet test --no-build` (solution: `Fugue.sln`, .NET 8). Full walkthrough: `docs/runbooks/01-local-setup.md`.
- Copy `.env.example` to `.env` (untracked) or use `dotnet user-secrets`. Never commit real values.

## The guardrail stack

Gates fire at junctures, never per edit. All are scripts under `scripts/spine/` (and `.githooks/commit-msg`) called by `lefthook.yml`.

| Juncture | Runs | Blocks? |
|---|---|---|
| commit | `secrets-scan`, `dotnet format --verify-no-changes` (only with `*.cs` staged and a solution present), `test-integrity` | yes; test-integrity is advisory |
| commit-msg | kit rule + commitlint (Conventional Commits, scope warn) + `check-refs-trailer` + `check-fix-has-test` | yes |
| push | `stack-check --test` (restore, build, test; skips with no solution), `ledger-check`, `adr-check`; advisory: `spine-nudge`, `commit-link-check`, `codex-gate` | yes; the advisory ones never block |
| every Bash tool call (Claude Code) | `scripts/safe-command-guard.mjs` via `.claude/settings.json` PreToolUse | yes (force push, `--no-verify`, hard reset on main, stray `rm -rf`, `curl \| sh`, secret printing, undry-run `ef database drop`) |

### Toggle

- Tags: `baseline` (always on) and `guardrail` (advisory). Pause the advisory layer for one command: `LEFTHOOK_EXCLUDE=guardrail git push`.
- `git commit --no-verify` skips everything. Do not use it to dodge a real finding.

## Commit messages

Subject: `type(scope): summary` (Conventional Commits). Body, in this order:

1. Root cause (why the change exists; for a fix, what was actually wrong).
2. Scope (what is in, what is deliberately out).
3. Verification: the commands you ran and the counts (`dotnet build` 0 warnings; `dotnet test` 48 green / 16 red known; `npm run test:scripts` N pass).
4. `Do better:` one line, the process lesson (what would have caught this earlier).
5. Trailers, last paragraph, one per line: `Refs: W-NNN` (or `D-NNN`, `ADR-NNN`; must resolve), `Tests: none (<reason, 10+ chars>)` when a `fix(...)` touches `src/` without `tests/`, and `Co-Authored-By: <model> <noreply@anthropic.com>`.

Agent commits (any `Co-Authored-By: Claude`) without a resolvable `Refs:` are rejected by `scripts/check-refs-trailer.mjs`; human commits are exempt. Beads ids never appear in a commit. Scope a commit by pathspec (`git commit -- <paths>`), not by the whole index.

## The doc control-plane (each file answers ONE question)

| File | The ONE question it answers |
|---|---|
| `hygiene.md` | Where is everything, and what is each verb's step order? |
| `docs/conventions/doc-spine.md` | What are the rules and why? |
| `docs/hot-state.md` | Where did we stop? |
| `docs/workstreams.md` | What threads are live? (`W-NNN`; closed ones are pruned into the project log) |
| `docs/decisions.md` | Why is it this way? |
| `docs/state-backlog.md` | What is still open? |
| `docs/project-log.md` | What happened, and when? (additive; closed-W entries) |
| `CHANGELOG.md` | Every project-log entry, forever (never pruned) |
| `docs/failure-modes.md` | What trap have we hit, and what defuses it? |
| `docs/branch-ledger.md` | Which branches and worktrees are live? |
| `docs/archive/INDEX.md` | What is in cold storage? (grep it; never bulk-read the folder) |
| `docs/adr/` | Which architecture decisions were made? |
| `docs/migrations/migration-catalog.md` | Which migrations exist? |

A fact lives in one file; others link to it.

## Review gate matrix

Critical seams (always get an independent review before merge): **migrations**, **auth** (JWT validation, policies, password hashing), **tenancy** (when enabled: tenant resolution, query filters, anything touching `tenant_id`), `ApiControllerBase`, idempotency and webhook signing.

- The Codex pre-push review (`scripts/codex-gate.mjs`, D-014) is ADVISORY: seam-triggered, deduped, capped at 8 a day, never blocks. It supplements, never replaces, the final-diff gate below.
- Plan gate: before coding anything on a critical seam, write the plan in `docs/plans/`.
- Final-diff gate: a different agent or model family reviews the diff of a critical seam; Critical, High and Medium findings go into a fix round and get re-reviewed.
- Migration PRs: catalog updated in the same commit, rollback written, tenancy checklist in the PR template.
- Author and reviewer are separate passes. Never self-approve.

## Pin every catch

A review catch you do not pin is a bug you rent. No High/Medium finding closes without a **regression test** (or an architecture test for structural rules). Record heavy catches in `docs/decisions.md` (D-012) or `docs/postmortems/`; lighter ones in `docs/state-backlog.md`. Structural catches (layering, controller shape, migration catalog, typed ids) are pinned as fitness functions in `tests/Fugue.ArchTests/FitnessTests.cs`; their baselines may only shrink.

## Hard rules (full detail in CLAUDE.md)

- Single-tenant by default; tenancy is a toggle (ADR-003). If enabled, the tenant comes from the verified JWT claim after authentication, never from a header, query or body.
- Fallback authorization policy is "authenticated".
- Controllers inherit `ApiControllerBase`; actions are one-liners that dispatch; logic lives in handlers.
- Layering: Domain (FluentResults only) <- Application (no ASP.NET Core) <- Infrastructure / Api. Arch tests enforce it.
- Handlers return FluentResults with `FugueError` (ADR-004). Slice shape is in `src/Fugue.Application/Features/README.md`.
- `ApiControllerBase.MapStatus` is the owner's to implement. Never fill it in.
- Keyset pagination. Passwords via `PasswordHasher<T>` / Argon2id only.
- Any migration updates `docs/migrations/migration-catalog.md` in the same commit.
- No secrets in the repo; no commit or push unless the owner asks.
- AI decisions are logged in `docs/ai-journal.md`. A decision is captured as a `D-NNN` the turn it is made; architecture-level ones are promoted to an ADR (`docs/adr/_template.md`).
- Project skills live in `.claude/skills/`: `agent-task-contract` (before dispatching), `dual-gate-review`, `migration-retrofit-qa`, `wrap-session`.
- Beads (`bd`) is a LOCAL queue; `.beads/` is gitignored; never run `bd init` without the flags in `docs/failure-modes.md` #8.

## Session wrap

End of a session: use the `wrap-session` skill / the WRAP row in `hygiene.md` (decisions, close threads with `scripts/spine/close-workstream.mjs`, overwrite hot-state as the resume point, handoff; `bash scripts/spine/ledger-check.sh` must exit 0). `spine-nudge` warns at push when code changed but no spine file did.
