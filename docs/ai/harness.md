# The AI harness: how work is run in this repo

Facts only, each checked against the script or file named next to it. The narrative of why and what went wrong is in [`how-we-use-ai.md`](how-we-use-ai.md); the reader-facing summary and the "how to verify me" commands are in [`../HOW-WE-WORK.md`](../HOW-WE-WORK.md). This page does not repeat them: it says what the machinery is, where it lives, and what it does not do. Where this page and a script disagree, the script wins; fix this page.

## 1. The lead and the lanes

| Part | What it is | Where it lives |
|---|---|---|
| Lead | One long-running session on the integration branch (`main`, repo root). Plans, writes briefs, dispatches, verifies, merges. The only writer of the spine files. | [`docs/branch-ledger.md`](../branch-ledger.md) ("`main` ... the only tree that writes spine files") |
| Lane | One builder session in its own git worktree and branch, with an allowed-files list. Writes code and tests, commits in slices, reports. | `.worktrees/<lane>` (ignored by git); one row per live branch in the branch ledger |
| Brief | The lane's contract (section 2). | [`docs/ai/briefs/`](briefs/) |
| Lane report | A status file the lane commits at the end: commits, counts copied from output, deviations, what it could not verify. | [`docs/ai/lanes/`](lanes/) (`<lane>-status.md`) |
| Resume point | `docs/hot-state.md`, overwritten at every wrap; the dated handoffs in [`handoffs/`](handoffs/). | section 7 |

Rules the briefs of this wave state for every lane ([`briefs/nice-to-haves.md`](briefs/nice-to-haves.md), "Header (every lane)"):

- The spine files (`docs/hot-state.md`, `docs/decisions.md`, `docs/ai-journal.md`, `docs/workstreams.md`, `CLAUDE.md`, `AGENTS.md`) are written by the lead; lanes do not touch them.
- A lane verifies the premises of its brief against the repo and reports the false ones with `file:line`.
- The lead does not accept a lane's report as evidence: it re-runs build, tests and the contract checks and reconciles the lane against git (`how-we-use-ai.md` section 2, step 8; `HOW-WE-WORK.md` section 1).
- Models: Opus leads, Sonnet builds lanes, Haiku does bounded sweeps, Fable and Codex review ([`how-we-use-ai.md`](how-we-use-ai.md) section 1). In the git history of `main` at `3417421`, the `Co-Authored-By: Claude` trailer lines name Opus 125 times and Sonnet 61 times, and no Haiku or Fable (`git log main --format=%B | grep -c "^Co-Authored-By: Claude Opus"`, and the same for each model): Haiku and Fable output reached the repo as audits and journal entries, not as commits.

## 2. The contract

Before a lane starts, the lead writes a brief following [`.claude/skills/agent-task-contract/SKILL.md`](../../.claude/skills/agent-task-contract/SKILL.md): objective, current state, desired state, allowed files, architecture rules, acceptance criteria, literal validation commands, non-goals, stop conditions and commit slices, plus the instruction to verify the premises and to list what the lane could not verify. The skill is project-local (`.claude/skills/`); the briefs it produced are in [`briefs/`](briefs/). Why: a brief written from memory was already false once ([`docs/failure-modes.md`](../failure-modes.md), the entry cited in `HOW-WE-WORK.md` section 1).

A lane runs in phases (this wave's header): INSPECT (map, risks, plan), EXECUTE (the commit slices), VALIDATE (literal commands; read the output, never the exit code), then a deviation report. A hard stop time and a cut order (what to drop first) are in each brief.

## 3. Worktrees

- One worktree per write lane, created from an explicit base sha, recorded in [`docs/branch-ledger.md`](../branch-ledger.md) before `git worktree add` and deleted from it when the branch merges (the ledger's own header). `git worktree list` shows the live ones.
- Lanes own disjoint paths (the allowed-files list in the brief) so their merges do not collide; the lead resolves the few shared files (navigation, routes).
- Commits are scoped by pathspec: `git commit -- <paths>`, never the whole index, so a parallel lane's staged files cannot ride along ([`AGENTS.md`](../../AGENTS.md), "Commit messages"; failure modes #10 and #19 in [`docs/failure-modes.md`](../failure-modes.md)). `git add -A`, `--amend`, force and push are forbidden to lanes in the brief header.
- Shared machine resources are shared: one Docker daemon, one set of host ports. Briefs give each lane its own ports and its own throwaway database containers, and forbid touching another lane's or the compose stack's containers. Processes are stopped by port, not by name (failure mode 30).
- Concurrency limits: [`docs/conventions/doc-spine.md`](../conventions/doc-spine.md) section 7.5 says to cap concurrent build and test lanes at about 3 to 4. This wave ran more lanes than that; the cap is a convention, not enforced by any script.

## 4. The gates (what runs, with the real commands)

[`lefthook.yml`](../../lefthook.yml) is the only hook orchestrator (`npm install` runs `lefthook install`; `core.hooksPath` is deliberately unset). The commands below are copied from it.

| Juncture | Command | Blocks |
|---|---|---|
| pre-commit | `bash scripts/spine/secrets-scan.sh` | yes |
| pre-commit | `dotnet format "$SLN" --verify-no-changes --no-restore` when a `*.cs` file is staged | yes |
| pre-commit | `node scripts/check-test-integrity.mjs` (new `Skip =`, dropped assertions, deleted tests) | no (advisory, tag `guardrail`) |
| pre-commit | `node scripts/spine/prune-check.mjs` | no (advisory) |
| commit-msg | `bash .githooks/commit-msg {1}` (`<type>(<scope>)?: <summary>`, 10 to 150 characters, no pasted file lists) | yes |
| commit-msg | `npx --no -- commitlint --edit {1}` (types are enforced; the scope list in `commitlint.config.cjs` only warns; header at most 110 characters) | yes |
| commit-msg | `node scripts/check-refs-trailer.mjs {1}`: a commit carrying a `Co-Authored-By` naming Claude, Codex, Sonnet, Opus, Haiku or Fable needs a final-paragraph `Refs:` trailer whose ids all resolve (`W-NNN`, `D-NNN`, `ADR-NNN`) | yes |
| commit-msg | `node scripts/check-fix-has-test.mjs {1}`: a `fix(...)` touching `src/` must touch `tests/` or carry `Tests: none (<reason>)` | yes |
| pre-push | `bash scripts/spine/stack-check.sh --test`: `dotnet restore`, `dotnet build --no-restore -v q`, `dotnet test --no-build -v q` on the first `*.sln` | yes |
| pre-push | `bash scripts/spine/ledger-check.sh` (doc links) and `node scripts/spine/adr-check.mjs` (every ADR linked from a `D` entry; every `Promote: yes` has its ADR) | yes |
| pre-push | `node scripts/spine/commit-link-check.mjs`, `node scripts/spine/trace-index.mjs --check`, `node scripts/codex-gate.mjs`, `bash scripts/spine/spine-nudge.sh` | no (advisory) |
| every Bash call by Claude Code | `node "$CLAUDE_PROJECT_DIR"/scripts/safe-command-guard.mjs` (PreToolUse hook in `.claude/settings.json`): blocks force push, `--no-verify`, `git reset --hard` on main, a stray `rm -rf`, a download piped to a shell, printing secrets and an un-dry-run `dotnet ef database drop` | yes |

Beyond the hooks, the test suites are gates the lead runs: `dotnet test` (including `Fugue.ArchTests`, the fitness functions with shrink-only baselines) and the OpenAPI snapshot test, whose diff shows up next to any contract change ([`HOW-WE-WORK.md`](../HOW-WE-WORK.md) section 2).

Things that are easy to get wrong about these gates:

- The hooks are client-side. `git commit --no-verify` skips them; the guard blocks that flag for agents, but a human can still use it.
- `.githooks/pre-commit` and `.githooks/pre-push` exist, but `lefthook.yml` calls only `.githooks/commit-msg`; the pre-commit and pre-push work above is done by the lefthook commands in the table. [`.githooks/README.md`](../../.githooks/README.md) still tells a reader to run `git config core.hooksPath .githooks`, which `AGENTS.md` and `lefthook.yml` say not to do (two orchestrators would both fire). The README is the stale one.
- The mutation-testing project `tests/Fugue.MutationTests` is not in `Fugue.sln`, so the pre-push `stack-check` does not build or run it ([`docs/quality/README.md`](../quality/README.md)).

## 5. CI

[`.github/workflows/ci.yml`](../../.github/workflows/ci.yml) mirrors the gates: jobs `backend` (restore, `build -warnaserror`, `dotnet test`, `dotnet format --verify-no-changes`), `scripts` (`npm run test:scripts`, `ledger-check`, `adr-check`), `web` (`npm test`, `npm run lint`, `npm run build`) and `api-contract` (compose stack up, Bruno suite `--env compose`). It has not run: the repo has no remote ([`how-we-use-ai.md`](how-we-use-ai.md) section 4 and `docs/hot-state.md`). Until it does, the hooks and the lead's own runs are the only gates.

## 6. Codex review

- **By hand, on critical seams.** Plans and diffs of migrations, auth, `ApiControllerBase`, idempotency and webhook signing get a read-only review by a different model family ([`AGENTS.md`](../../AGENTS.md), "Review gate matrix"). The ten reviews so far are in [`docs/reviews/codex/`](../reviews/codex/) (`ls docs/reviews/codex | wc -l`), each with a disposition per finding. A High or Medium finding closes only with a regression test (D-012).
- **By hook, advisory.** `scripts/codex-gate.mjs` (D-014) runs at pre-push: if a changed path matches one of its `SEAM_GLOBS` (migrations, `*.csproj`, `Directory.*.props`, `global.json`, `ApiControllerBase.cs`, `src/**/Identity/**`, `src/**/Auth*/**`, `.claude/**`, `scripts/**`, `lefthook.yml`), the diff has not been seen before and fewer than `DAILY_CAP = 8` reviews started today, it launches a detached `codex exec review` and writes findings to `docs/reviews/codex/`. It always exits 0 and reads Codex's output, never its exit code. Its own header says its Codex invocation was checked against `--help` and "NOT yet run end-to-end here": treat it as unproven. It has tests (`scripts/codex-gate.test.mjs`) for seam matching, dedupe and the cap.
- **Author and reviewer are separate passes.** Nobody approves their own work (`AGENTS.md`); the second skill for this is `.claude/skills/dual-gate-review`.

## 7. Context discipline

The rule: write the state of the work into files before the model's context window fills, and start a fresh session that resumes from those files, not from memory.

- **The rule as written.** The brief for this document and the lead's handoff ([`handoffs/2026-11-27-lead.md`](handoffs/2026-11-27-lead.md), "W-022 ... incl. the 60% context cap") say about 60% of the window. [`docs/conventions/doc-spine.md`](../conventions/doc-spine.md) section 7.6 ("Context canary") says to stop and start fresh at about 50%, after writing thread state to the spine, and to trust the tool's context readout, not an estimate. The two numbers differ; this repo does not reconcile them. What both agree on is the sequence: write the state out first, then start fresh.
- **What carries the state.** [`docs/hot-state.md`](../hot-state.md): overwritten (never appended) at each wrap, at most 40 lines, ending in an exact "Next action". `wrap` in [`hygiene.md`](../../hygiene.md) is the ordered procedure (capture decisions as `D-NNN`, close finished threads with `scripts/spine/close-workstream.mjs`, overwrite hot-state, write the handoff, run `ledger-check`); the `wrap-session` skill executes it. Cold storage is tiered ([`hygiene.md`](../../hygiene.md), "Context tiers"): always read `CLAUDE.md` and `hot-state.md`; read warm files as deep as the task needs; grep, never bulk-load, the archive.
- **What enforces it.** Nothing in this repo measures context use or blocks a session at a threshold. The enforced parts are the file-side ones: `ledger-check` (links), `prune-check` (advisory: a done thread still on the board, a stale hot-state), `adr-check`, and the `Refs:` gate that ties every agent commit to a resolvable id. The 50 or 60 percent line itself is a practice the lead follows, evidenced only by the dated handoffs in [`handoffs/`](handoffs/) and the overwritten hot-state, not by a check.

## 8. What is evidenced here, and what is not

| Claim | Evidence in this repo | Not evidenced here |
|---|---|---|
| Lead and lane model with worktrees | `docs/branch-ledger.md`, `git worktree list`, `docs/ai/briefs/`, `docs/ai/lanes/*-status.md` | Large fleets (the repo ran a handful of lanes at a time with one lead) |
| Contracts | the briefs; the `agent-task-contract` skill | A measured effect on lane quality |
| Gates at commit and push | `lefthook.yml`, the scripts it names, their `*.test.mjs` tests | Gates running on a server: CI has never run (no remote) |
| Refs trailers | the gate; `git log main --format=%B \| grep -c "^Refs:"` counts the trailer lines | |
| Codex review | ten filed reviews with dispositions | The pre-push Codex gate running end to end; review-round statistics |
| Mutation, pairwise and load testing | [`docs/quality/`](../quality/README.md), [`perf/k6/`](../../perf/k6/README.md): tool output and committed reports | Anything about capacity or any other machine |
| Context discipline | hot-state and the handoffs exist and are dated | Any measurement of context use; a script that enforces the threshold |
| Practices of the owner's other repos (review-round counts, the larger fleets, the landing guard) | `how-we-use-ai.md` tags each one "practiced in my other repos" | All of it: it is not in this repo's history |

To re-check a row, run the command in it; do not trust a number on this page, including the counts in section 1 (they were taken from `main` at `3417421` on 2026-11-27 and move with every commit).
