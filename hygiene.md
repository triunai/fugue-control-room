# Fugue — hygiene (the router)

This file is the map. It says where things live and, for each verb, the order of the steps. It is the ONLY place the step order is written. Skills and humans read the row and follow it. Rules and reasons live in `docs/conventions/doc-spine.md`.

**Anti-rot rule.** No counts and no current state in this file. State goes in `docs/hot-state.md`. If you add a number here, it will be wrong in a week.

## You are here
- Current state and where to resume: `docs/hot-state.md`
- What is in flight: `docs/workstreams.md`
- Rules and reasons for this system: `docs/conventions/doc-spine.md`
- Cold start: say "hydrate". End of session: say "wrap".

## Mental model (one block)
```
now      hot-state     What did I touch this session?        overwrite each wrap
board    workstreams   What am I juggling?                   add at task START
why      decisions     Why is it this way?                   prepend, never delete
open     backlog       What is unbuilt or undecided?         append
journal  project-log   What happened, session by session?    prepend, append-only
handoff  seed          What must the next cold session know? written at wrap, committed WITH the spine
```
One key joins all of it: the thread key `WS-NN`. Every journal, decision and backlog entry has `WS: WS-NN` as its first body line.

## Canonical artifacts
| Artifact | Path | Question it answers |
|---|---|---|
| Router | `hygiene.md` | Where is everything? What is the step order for each verb? |
| Hot state | `docs/hot-state.md` | Where did I stop? What is owed? |
| Board | `docs/workstreams.md` | What threads exist and what is their status? |
| Decisions | `docs/decisions.md` | Why did we choose this? |
| Backlog | `docs/state-backlog.md` | What is still open? |
| Project log | `docs/project-log.md` | What happened and when? |
| Seed | `docs/NEXT-SESSION-SEED-PROMPT-<YYYY-MM-DD>.md` | What must the next cold session read first? (shape: `docs/templates/seed-template.md`) |
| Findings | `docs/findings/README.md` | What facts about system behaviour do we hold, and where did each one go? |
| Evidence | `docs/ai/README.md` | What did an audit, trace or review actually find? |
| Tax ledger | `docs/ai/tax-ledger.md` | What did we knowingly leave behind, and who paid? |
| Landing ledger | `docs/templates/landing-ledger-template.md` | Did each promise merged from lanes get independent proof? |

## Phrase → action
Say the phrase, run the row. Do the steps in order. Do not skip a step; write "n/a" in the wrap note if a step has nothing to do.

### WRAP — "wrap", "wrap up", "wrap the session"
0. Walk the **Owed inbox** in `docs/hot-state.md`. Handle or carry each item.
   **Gate:** run `bash scripts/spine/ledger-check.sh`. It must exit 0. If not, fix the findings it names, then continue. Do not skip the gate.
1. **hot-state.** Keep ONE `▶ RESUME HERE` block. Keep at most ONE `(previous)` block. Move any older block into today's `project-log` entry. Never delete it.
2. **workstreams.** Update `Status:` and `Resume:` for every thread touched. Set the glyph. Mark `✅ DONE` only when finished.
3. **decisions.** Prepend a `D-NNN` for every call that settled this session.
4. **project-log.** Prepend today's entry. First body line is `WS: …`.
5. **backlog.** Add new open items. Move settled items out to decisions.
6. **findings / tax** — only if triggered: a fact must outlive the handoff (findings), or something was knowingly left behind (tax ledger).
7. **memory** — only if a durable fact about the owner or project was learned.
8. **router / convention** — only if a rule or an artifact TYPE changed. Never for state.
9. **vault mirror** — optional. Copy a distilled note to your notes vault.
10. **seed.** Write `docs/NEXT-SESSION-SEED-PROMPT-<today>.md` from `docs/templates/seed-template.md`. Commit the seed and the spine files in the SAME commit.

Numbers (`D-NNN`, `#NNN`) are minted by the orchestrator only, after listing all worktrees (`git worktree list`).

### PARK — "park this"
1. Set the thread to `⏸ PARKED` in `docs/workstreams.md`.
2. Write one `Resume:` line: where you stopped + the literal next action.
3. Add a line to the Owed inbox if something is half-done.

### PICK UP — "pick up WS-NN"
1. Read the thread block in `docs/workstreams.md`. Jump to its `Refs:`.
2. Set it to `🟢 ACTIVE`. Do not dig through git history.

### HYDRATE — "hydrate", "catch up", "continue"
1. **Pick the tree.** Run `git worktree list`. The worktree whose `docs/hot-state.md` has the newest commit is the live one. Work there.
2. Read the newest `docs/NEXT-SESSION-SEED-PROMPT-*.md`.
3. Read the `▶ RESUME HERE` block in `docs/hot-state.md`.
4. Skim the board `docs/workstreams.md`. Report: live tree, resume point, active threads, owed items.

### LAND — "land these lanes"
1. Make a throwaway `verify/*` branch. Check the merged work there.
2. Add one landing-ledger row per promise (shape: `docs/templates/landing-ledger-template.md`).
3. Mark a row `✅` only with proof (commit SHA or check branch) from someone other than the builder.

### STATUS — "give me a status"
On demand only. Write a short plain-language summary for a leader: outcomes, risks, what is owed. No file names, no IDs. This is not a wrap step.

### NEW TASK — "new task: …"
1. Add a thread block to `docs/workstreams.md` at the START, before any work. Next free `WS-NN`.
2. Add one line to `docs/hot-state.md` so the next session sees it.

## Working rules (lean)
- One writer for the spine: the orchestrator. Build lanes work in their own git worktree and never edit spine files or mint numbers.
- Commit every step that builds and passes. No dirty-tree batches.
- One model family reviews another's work. Every Critical, High or Medium finding goes into a fix round.
- Re-check any "not found" or "zero usages" claim from an agent before you trust it.
- Git hooks are client-side. `--no-verify` skips them. CI is the real backstop.
- Plain English first. Jargon in parentheses after the meaning.

## Hook wiring (Fugue decision)
- **lefthook is the single hook orchestrator** (`lefthook.yml`). The kit's hook logic stays as scripts (`.githooks/commit-msg`, `scripts/spine/*.sh`) that lefthook calls.
- `core.hooksPath` is deliberately NOT set. Setting it alongside lefthook would run two orchestrators and they would conflict. `.githooks/pre-commit` and `.githooks/pre-push` are retained as reference wrappers only; lefthook runs the scripts directly.
- `stack-check.sh` takes `--test` (used by pre-push) to also run `dotnet test`. It skips with exit 0 when no `.sln`/`.slnx`/`.csproj` exists.

## Fugue extras beyond the kit spine
`docs/adr/` (architecture decisions) · `docs/migrations/migration-catalog.md` · `docs/plans/` · `docs/postmortems/` · `docs/reviews/` · `CHANGELOG.md` (append at top, ISO dates).
