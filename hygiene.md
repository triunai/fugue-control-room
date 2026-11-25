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
ledger   CHANGELOG     Every closed-thread entry, forever?   copy of each project-log entry; never pruned
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
| Branch ledger | `docs/branch-ledger.md` | Which branches and worktrees exist right now? (row deleted on merge) |
| Failure modes | `docs/failure-modes.md` | What trap have we already hit, and what defuses it? |
| Handoff | `docs/ai/handoffs/<date>-lead.md` | Today's summary and the seed prompt for the next session |
| Changelog | `CHANGELOG.md` | Every closed-thread entry, additively, forever (never pruned or archived) |
| AI narrative | `docs/ai/how-we-use-ai.md` | How do we use AI here, with evidence? |
| Cold index | `docs/archive/INDEX.md` | What is in cold storage? Grep this before opening anything there |
| Project skills | `.claude/skills/` | Which repeatable procedures have a skill (contract, wrap, review, migration QA)? |

## Doc lifecycle (how a fact ages)
```
 new ask ──> docs/workstreams.md   (W-NNN row, live board only; added FIRST, before any work)
                  │  W done: node scripts/spine/close-workstream.mjs W-NNN --outcome "..." ...
                  ▼
 ONE entry, copied verbatim to BOTH:   docs/project-log.md  (warm; additive, entries never edited)
                                       CHANGELOG.md         (every entry, forever; never pruned or archived)
                  │  row deleted from the board; follow-ups become new W rows
                  ▼
 log > 20 entries or > 30 days old: archive-project-log.mjs MOVES them verbatim to
 docs/archive/project-log/YYYY-MM.md  (+ a line in docs/archive/INDEX.md). CHANGELOG keeps everything.
 docs/hot-state.md = the resume point, OVERWRITTEN at every wrap or pause.
```
- **Entry shape** (written by the lead, supplied as flags): `## <date> — Closed W-NNN: <title>`, `WS: WS-NN`, `Closed: W-NNN (WS-NN) <title>`, `Outcome:` (2-5 lines), `Commits:` (`sha subject` list, gathered from git by `Refs: W-NNN`, plus `--commits` for pre-gate history), `Decisions:` (D-NNN / ADR-NNN), `Follow-ups:` (each becomes a new W row). The script prompts nothing and refuses to close a W twice. CHANGELOG is NOT generated from `git log`: it is a copy of the project-log entry.
- **Verification, not generation**: `node scripts/spine/commit-link-check.mjs` (advisory, pre-push) reports commits since the `<!-- commit-link:since=... -->` marker in CHANGELOG that are neither listed in an entry nor `Refs:` an open W ("unlinked"), ADRs named in an entry that do not exist, project-log entries missing from CHANGELOG, and closed W still on the board. `--advance` moves the marker when clean. `node scripts/spine/prune-check.mjs` (advisory, pre-commit) flags a done thread left on the board and a stale hot-state.
- **Traceability**: `docs/traceability.md` is GENERATED (`npm run trace`, `scripts/spine/trace-index.mjs`; never hand-edited). It links every commit to its W, D and ADR ids and back (per-W commits and produced ADR/D, per-ADR introduced/amended-by commits, per-D linked ADR, a commit-to-ids table, and an "Unlinked" list for pre-gate or human commits). It reads `git log` of HEAD plus the docs as committed, so regenerate AFTER the spine commit and commit it alone (`docs(trace): ...`); a commit touching only that file is excluded from the index, which keeps re-runs byte-identical. The pre-push `trace-stale` check warns when it is stale and never blocks or writes.
- **Multi-id `Refs:`**: one trailer may carry several ids (`Refs: W-012, ADR-010, D-022`); every id must resolve (`check-refs-trailer.mjs`). Cite the W plus each D/ADR the commit introduces or amends. `close-workstream.mjs` adds the D/ADR ids found in the W's commits to the entry's `Decisions:` line.
- **A pruned id still resolves** in a `Refs:` trailer through its `Closed:` line (warm log or archive).
- **Cold storage**: `docs/archive/` is cold. Never read it without a grep hit in `docs/archive/INDEX.md`. Deferred and far-ahead items live in `docs/archive/deferred.md` with a "revive when" trigger. Superseded ADRs stay in `docs/adr/`, marked Superseded.
- **hot-state = resume point**: overwritten, at most 40 lines. It holds the last action done, the exact next action (command, file, line), uncommitted work in progress (branch, dirty files, why), open W ids in flight, pending owner questions, `Last green sha:` with test counts and red reasons, and a copy-paste seed prompt. After a wrap or days away: read it first, then resume its next action.

## Decisions and ADRs
A decision is captured THE SAME TURN it is made as a `D-NNN` entry in `docs/decisions.md` (date, decision, why, who: owner/AI/review, status, promote). It is promoted to an `ADR-NNN` file (`docs/adr/_template.md`) when it changes architecture, a public contract, the data model, security posture or a cross-cutting convention; the D entry links the ADR. `node scripts/spine/adr-check.mjs` (pre-push) fails on an ADR no D entry links or a `Promote: yes` with no ADR. Superseded ADRs stay in place, marked Superseded.

## Context tiers
| Tier | Files | Rule |
|---|---|---|
| Active (every session) | `CLAUDE.md`, `docs/hot-state.md` | always read |
| Warm (hydration ladder) | `docs/workstreams.md`, `docs/decisions.md`, `docs/plans/roadmap.md`, `docs/failure-modes.md`, skills (by description) | read as deep as the task needs |
| Cold | `docs/archive/` (index first), project-log archive, `docs/reviews/`, `docs/ai/handoffs/` older than 7 days, `docs/postmortems/` | grep `docs/archive/INDEX.md` or `git log --grep`, open one file. Never bulk-load |

## Phrase → action
Say the phrase, run the row. Do the steps in order. Do not skip a step; write "n/a" in the wrap note if a step has nothing to do.

### WRAP — "wrap", "wrap up", "wrap the session"
0. Walk the **Owed inbox** in `docs/hot-state.md`. Handle or carry each item.
   **Capture:** every call that settled this session is a `D-NNN` entry (promote to an ADR per "Decisions and ADRs").
   **Gate:** run `bash scripts/spine/ledger-check.sh`. It must exit 0. If not, fix the findings it names, then continue. Do not skip the gate.
1. **hot-state.** Keep ONE `▶ RESUME HERE` block. Keep at most ONE `(previous)` block. Move any older block into today's `project-log` entry. Never delete it.
2. **workstreams.** Update `Status:` and `Resume:` for every thread touched. Set the glyph. Mark `✅ DONE` only when finished.
3. **decisions.** Prepend a `D-NNN` for every call that settled this session.
4. **project-log.** Prepend today's entry. First body line is `WS: …`. Closed threads use `close-workstream.mjs` (Doc lifecycle); other entries are written by hand.
5. **backlog.** Add new open items. Move settled items out to decisions.
6. **findings / tax** — only if triggered: a fact must outlive the handoff (findings), or something was knowingly left behind (tax ledger).
7. **memory** — only if a durable fact about the owner or project was learned.
8. **router / convention** — only if a rule or an artifact TYPE changed. Never for state.
9. **vault mirror** — optional. Copy a distilled note to your notes vault.
10. **seed / handoff.** Write the next-session seed prompt into `docs/ai/handoffs/<today>-lead.md` (or `docs/NEXT-SESSION-SEED-PROMPT-<today>.md` from `docs/templates/seed-template.md`). Overwrite `docs/hot-state.md`. Commit the handoff and the spine files in the SAME commit.
11. **close threads.** For each finished thread run `node scripts/spine/close-workstream.mjs W-NNN --outcome ...` (appends the entry to the project log AND CHANGELOG, deletes the board row), then `node scripts/spine/commit-link-check.mjs` (and `--advance` when clean).
12. **traceability.** After the spine commit: `npm run trace`, then commit `docs/traceability.md` alone (see Doc lifecycle, Traceability).
13. **local queue.** Close the finished beads (`bd close <id> --reason <sha>`). Beads stay local; never cite one in a commit.

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

## Commit trailers
Agent commits (a `Co-Authored-By: Claude` trailer) end with `Refs: W-NNN` (or `D-`/`ADR-`), resolvable in `docs/workstreams.md`, `docs/project-log.md` (pruned W), `docs/decisions.md` or `docs/adr/`. A `fix(...)` touching `src/` also touches `tests/` or carries `Tests: none (<reason>)`. Body norm: `AGENTS.md`.

## Fugue extras beyond the kit spine
`docs/adr/` (architecture decisions) · `docs/migrations/migration-catalog.md` · `docs/plans/` · `docs/postmortems/` · `docs/reviews/` · `CHANGELOG.md` (append at top, ISO dates).
