---
name: agent-task-contract
description: Use BEFORE dispatching any agent, subagent, worktree lane or long autonomous run on Fugue, and when choosing one worktree versus several. Turns a request into a 10-part contract with verify-the-premises and a mandatory "What I could NOT verify" report. Triggers - "spawn an agent", "dispatch", "parallel lanes", "brief an agent", "task packet", "run this autonomously".
---

# Agent task contract (Fugue)

An agent that is handed a request guesses; an agent that is handed a contract delivers. Write the contract, then dispatch. Authoring and review are separate agents; the lead (the main session) is the only writer of spine files and the only minter of `W-`/`D-`/`ADR-` numbers.

## Before writing it
1. Capture the ask on the board: add or update a thread in `docs/workstreams.md` (`ID: W-NNN`). Capture any call the owner made this turn as a `D-NNN` entry NOW (promote to an ADR when it changes architecture, a public contract, the data model, security posture or a cross-cutting convention; `docs/adr/_template.md`).
2. Create a LOCAL bead if useful (`bd create`, labels `model:<tier>` and `role:builder|auditor|integrator`). Beads never appear in a commit.
3. Pick the topology: write lanes each get their own git worktree cut from an EXPLICIT sha (`git worktree add .claude/worktrees/<lane> -b feat/<x> <sha>`; add a row to `docs/branch-ledger.md` first). Read-only audits may share one frozen tree. Never let two agents write the same tree. Parallel only when the allowed files do not overlap.
4. Pick the model: Opus for critical seams and integration, Sonnet for builders, Haiku for bounded sweeps, Fable for adversarial architecture review. A different model family reviews.

## The 10 parts (all required)
1. **Objective**: one sentence, observable outcome.
2. **Current state**: with `file:line` references the lead has just read.
3. **Desired state**: what is true when done.
4. **Allowed files**: an explicit list or globs. Everything else is read-only.
5. **Architecture rules**: the relevant lines from `CLAUDE.md` (layering, `ApiControllerBase`, FluentResults, keyset paging, no `MapStatus`).
6. **Acceptance criteria**: checkable statements.
7. **Validation commands**: literal, copy-pasteable. Always: `export DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH`, `dotnet build` (0 warnings), `dotnet test --no-build`, `dotnet format --verify-no-changes`, `npm run test:scripts` when scripts change. State the expected counts.
8. **Non-goals**: what the agent must NOT do (no refactors, no new packages, no prod or destructive commands, no pushes, no `--no-verify`, no editing spine files).
9. **Stop conditions**: when to stop and report instead of improvising (a premise is false, a gate fails twice, a critical seam needs a decision).
10. **Commit slices**: ordered green seams (contract, behaviour, tests, cleanup), each commit passing the gates, with `Refs: W-NNN` and the `Co-Authored-By:` trailer. Commit at least every 30 minutes. Scope each commit by pathspec, not by the index.

## Verify the premises (mandatory clause)
Put this in every contract: "Before changing anything, re-check each fact in Current state against the repo. If one is false, STOP and report which one. A brief's facts can be stale." Re-check any "not found" or "zero usages" claim before trusting it.

## The report (mandatory shape)
Reports go in a file (path named in the contract), with a one-line reply. Sections: what changed (shas), validation output with counts, deviations, and **What I could NOT verify** (always present; write "nothing" only if true). The lead reconciles against git (head sha, diff scope, re-run the gates on the combined tree), never against the report alone.

## Fugue-specific hard stops
Agents never implement `ApiControllerBase.MapStatus`, never run `dotnet ef database drop` or touch a shared database, and never edit `.claude/settings*.json`, `lefthook.yml` or `scripts/**` unless the contract allows it (they are critical seams: advisory Codex review fires at push).
