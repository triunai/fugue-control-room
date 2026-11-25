# Doc spine — conventions for Fugue

> Owner: the owner · Created 2026-11-11 · Trunk branch: `main`
>
> **This file owns rules and reasons.** It never holds counts, current state or step lists.
> The step order of each verb lives in `hygiene.md` (the router) and nowhere else.
> **Origin:** field-proven on a large multi-service codebase built fast with AI agents.

## 1. TL;DR

- Split project memory by **how long it lives** (nine lifecycles, section 2). Never keep it in one big file.
- Every entry points to its work thread with a **fixed first line**: `WS: WS-NN`. A script checks the links, so a human does not have to.
- The **gates are part of the spine**. The hooks and the link checker protect the context the same way tests protect the code.
- **One orchestrator** writes the spine and mints every number. Helper agents ("lanes") build code in their own worktrees.
- **A different model family reviews the work.** Every serious finding gets fixed and re-reviewed.
- Each fact has **one home**. If a change needs edits in two files, duplication has crept back.

Why this exists: every documentation failure we saw came from **two hand-written places disagreeing**.
The fix is to write each thing once and check the links by script.

## 2. Lifecycles

Each kind of memory has its own question, rhythm and writer. Mixing them is what makes docs rot.

| # | Lifecycle | File | Question it answers | Cadence | Writer |
|---|---|---|---|---|---|
| 1 | **now** (hot-state) | `docs/hot-state.md` | What did I touch this session? | Each wrap: ONE current `▶ RESUME HERE` block, at most ONE `(previous)` block, then an `Owed` inbox. The older block **moves** into that day's journal entry. It is never deleted. | orchestrator |
| 2 | **board** (all threads) | `docs/workstreams.md` | What am I juggling? | Add a block at task **start**. Update at wrap. Prune when done. | orchestrator |
| 3 | **why** (decisions) | `docs/decisions.md` | Why is it this way? | Prepend `D-NNN` when a call settles. Supersede, never delete. | orchestrator |
| 4 | **open** (backlog) | `docs/state-backlog.md` | What is unbuilt or undecided? | Append. Settled items leave for decisions. | orchestrator |
| 5 | **journal** (project log) | `docs/project-log.md` | What happened, session by session? | Prepend a dated entry. Append-only. | orchestrator |
| 6 | **evidence** | `docs/ai/<thread>/` | What did an audit, trace or review find? | One folder per thread, a newest-first `README.md` router per folder. **Never edited after the fact.** | lanes write, orchestrator indexes |
| 7 | **deferred debt** (tax ledger) | `docs/ai/tax-ledger.md` | What did we knowingly leave behind, and who pays? | Entry = the tax · cost of leaving it · **paid by (named person + date)** · trigger to revisit. | orchestrator; owner rules |
| 8 | **staged facts** (findings) | `docs/findings/` | What do we now know about how the system behaves? | A row only when a fact must outlive the handoff. Every row names its permanent home. An empty "Promoted to" cell is unfinished work. | orchestrator |
| 9 | **handoff** (seed) | `docs/NEXT-SESSION-SEED-PROMPT-<date>.md` | What must the next cold session know first? | Written at wrap. Committed **with** the spine. Fixed shape (see `docs/templates/seed-template.md`). | orchestrator |

**Optional: leadership log.** Outcome-only, plain language. Produce it **on demand** when someone asks for a status.
Do not make it a wrap step. A wrap step nobody reads lapses, and a lapsed artifact that the router still lists misleads people.

Reasons behind the cadence rules:
- "Overwrite hot-state every session" turned into three stacked resume blocks and a 2,000-line file. People worked around the rule.
  So the rule is written as practised: **a cap, plus a defined place for old blocks to go.**
- A handoff file was truncated to 0 bytes and could not be restored, because it had never been committed.
  So the handoff is committed **in the same commit** as the spine.

## 3. The key graph

The thread key `WS-NN` is the **join key** of the whole spine. Every entry links back to a thread.

### 3.1 The fixed-line rule

The first non-blank body line of every journal, decision and backlog entry is:

```
WS: WS-55, WS-60
```

Use `WS: (pre-WS)` for entries that are older than the rule. Never guess a key.
Because the key sits on a fixed line, a short script can check the whole graph. A human cannot do that reliably.

Canonical heading forms (the checker parses exactly these):

| File | Heading form |
|---|---|
| Board | `### WS-NN — <title> — <STATUS> (<YYYY-MM-DD>)` — exactly three `#` |
| Decisions | `## D-NNN — <title> — <YYYY-MM-DD>` — latest first |
| Backlog | `### #NNN — <title>` — ONE heading form only |
| Journal | `## <YYYY-MM-DD> — <title>` — latest first |

Board status glyphs: `🟢 ACTIVE` · `⏸ PARKED` · `⛔ BLOCKED` · `✅ DONE`. A heading containing `✅` means done.
Board body lines: `Status:` · `Resume:` · `Refs:`. WS numbers have two digits at least (`WS-01`).
D-NNN and #NNN have three digits.

### 3.2 ID families

| Family | Minted in | Cited by | Trap |
|---|---|---|---|
| `WS-NN` thread | the board, at task start | every `WS:` line, code headers, seeds | Heading level drift (`##` vs `###`) hides threads from the parser. |
| `D-NNN` decision | decisions log | `WS:`-linked entries, code headers, comments | Parallel branches mint the same number. |
| `#NNN` backlog | backlog | decisions, headers, commit scopes | "Next free" goes stale across many lanes. Two heading forms in one file. |
| `T-N` deferred debt | tax ledger | decisions, seeds | Minted per branch, so numbers collide. |
| `V<N>` / `R-NNN` migrations (if you have them) | migrations folder | catalog, migrator manifest | The number is **execution order**, not a label. |
| `ADR-NNN` (if you keep a wiki) | the wiki | code headers | A different sequence from `D-NNN`. Map the two explicitly. |
| `YYYY-MM-DD#n` journal entry | journal heading order | code headers | Inserting a same-day entry renumbers the rest. `n` = position among that date's headings, top to bottom. |

### 3.3 Central minting and placeholders

- **Only the orchestrator mints numbers**, and only after sweeping **every** worktree (`git worktree list`).
- Lanes write placeholders such as `#NEW-A1` or `D-NEW-A1`. The orchestrator keeps the placeholder-to-real map on disk and rewrites it at merge.
- Why: backlog numbers collided four times in one day across parallel lanes. "Next free number" is stale the moment a second lane exists.
- Smell: two lanes report the same number. Someone other than the orchestrator minted.

### 3.4 Code provenance header (optional, for critical modules)

Use it only where "why is this here" matters: authorization, money, tenancy, data migration. Skip it elsewhere.
Every file in those modules opens with a fixed header, above the imports (adapt the comment syntax):

```
// ── Provenance ──────────────────────────────────
// Workstream : WS-31, WS-55
// Log        : 2026-08-19, 2026-09-11
// Decisions  : D-056, D-124
// Backlog    : #082
// Contract   : <design doc path + section>
// Vault      : ADR-017
// History    : <module>/CHANGELOG.md
// ────────────────────────────────────────────────
```

Rules:
- Seven fixed keys in a nine-line box. Append-only.
- **History leaves the comments** and goes to a per-module CHANGELOG. A comment states the live rule and a pointer. It never holds a count or a "used to say".
- A generated index of all headers may be produced on demand. **Never commit it.** A committed copy is a second hand-kept thing.
- The shipped `ledger-check.sh` does not check these headers. If you adopt them, add a check that every key is present and that the newest `Log` date is not older than the file's last commit.

## 4. Verbs

A verb is a named routine the owner can say in one word. This section gives the **intent** only.
**The step order of every verb lives in `hygiene.md`, and nowhere else.** Skills read that router row and run it in order. They never keep their own copy.

| Verb | Intent |
|---|---|
| **WRAP** | End of session. Bring every lifecycle up to date in a fixed order. Then commit the spine **and the seed** in one commit. It starts by walking the `Owed` inbox and running the link checker. A non-zero checker result stops the wrap. |
| **PARK** | Freeze a thread before you pivot. Set status `⏸ PARKED` and write one crisp `Resume:` line: where you stopped plus the literal next action. |
| **PICK UP** | Read the thread's block, jump to its refs, set it `🟢 ACTIVE`. No git archaeology. |
| **HYDRATE** | Cold start. **First pick the tree:** across `git worktree list`, the worktree whose `docs/hot-state.md` has the **newest commit** is the live one. (This stops "the main checkout is weeks stale" sessions.) Then read: newest seed, then `▶ RESUME HERE`, then the router's read-first list, then the board. |
| **LAND** | Merge work from many lanes. Run an independent check on a throwaway `verify/*` branch. Then write one **landing-ledger row** per promise. A row is ✅ only with proof (a commit SHA or a check branch) from someone **other than the builder**. |

## 5. One job per source

The wrap order used to be written in three places: the convention, the router and the wrap skill. They drifted apart,
and the skill (the thing that actually runs) drifted worst. It pointed at another project's folder layout and filed things wrongly on every wrap, unnoticed.
**Executors drift faster than docs, and do more damage.** So each source has one job.

| Source | Owns | Must never contain |
|---|---|---|
| **Convention** (this file) | Rules and why | Counts, current state, step lists |
| **Router** (`hygiene.md`) | The map, and each verb's step order (the ONLY home of the order) | Rules and reasons (it links here) |
| **Skills** (wrap, hydrate, review loop) | Running the steps: "read the router's row, do it in order", plus tool how-to | Their own copy of the file list or the order |
| **Script + hooks** | Enforcement | Policy that is not also written here |

Smell: changing the wrap order needs edits in more than one file.

## 6. Enforcement layer

The spine protects context the way tests protect code. The checker and the hooks are part of the spine.

### 6.1 The link checker (`scripts/spine/ledger-check.sh`)

Runs in pre-push. Blocking. Plain text parsing with bash, awk, grep and sed only, so it runs in Git Bash on Windows, macOS bash 3.2 and Linux.
It exits 1 on any finding and prints `LEDGER: <finding>` per finding, then one summary line:
`ledger-check: findings=N workstreams=M`.

Findings (exactly these five):
1. A log, decision or backlog entry whose first non-blank body line is not `WS: `.
2. A `WS:` line naming a WS key that has no `### WS-` heading.
3. A ✅ thread that no log `WS:` line claims.
4. A duplicate `D-NNN`, `#NNN` or WS heading.
5. A line starting with `#`, `##` or `####` + space + `WS-` (wrong heading level). A `##` heading is still read, but it is flagged.

**Canary.** `workstreams=M` is the number of threads the parser read. If it drops without a thread being removed, the parser has gone blind.
(One heading written with `##` instead of `###` once hid eight threads silently. Rule 5 and the canary exist for that.)
A fresh install reports `workstreams=1`.

**Ratchet, not big bang.** To adopt the checker on an existing repo, write `scripts/spine/ledger-check.conf`:

```
LOG_CUTOFF=2026-10-03
DECISION_CUTOFF=42
BACKLOG_CUTOFF=
WS_CUTOFF=
```

Empty means "enforce everything". Entries at or after a cutoff (`>= cutoff`) are enforced; entries **before** it are counted and reported, never blocked.
So a numbered cutoff = the highest existing number + 1 (above: newest decision is D-041), and `LOG_CUTOFF` = tomorrow's date (above: today is 2026-10-02).
Why: a pre-push check once found 144 old problems, so the branch was pushed with `--no-verify`. A gate that is red on day one gets bypassed.
Run a cleanup thread to bring the count to 0 first. Then move the cutoff back, one step at a time.

### 6.2 Hooks

Installed with `git config core.hooksPath .githooks`. No hook manager is needed. If `lefthook` is on PATH, a `lefthook.yml` that calls the same scripts is optional.

| Class | Hook | Gate | Notes |
|---|---|---|---|
| **Blocking** | commit-msg | `<type>(<scope>)?: <summary>`, 10 to 150 chars, no pasted file lists | Types: `build chore ci docs feat fix perf refactor revert style test wip`. Allows `Merge `, `Revert `, `fixup!`, `squash!`. Derive thresholds from **your own history**. After about 200 commits, re-derive them. (In one repo a 72-char cap would have rejected most of the existing commits.) |
| **Blocking** | pre-commit | secrets scan | Credential file paths and secret-shaped added lines. Exempt by **file**, never by folder, each with a dated reason. |
| **Blocking** | pre-commit | stack check | Detect the stack and run the cheapest compile or type check. Unknown stack: skip with one line. Make it blocking only once the project is at 0 errors. |
| **Blocking** | pre-push | link checker | The spine's own gate. |
| **WARN-only** | pre-push | "this push changes code but carries no wrap" | Fires when none of hot-state, workstreams or project-log changed. Always exits 0. **Silent on `lane/*` and `verify/*`**, because lanes are barred from the spine and warning there only teaches people to ignore warnings. |
| **WARN-only** | pre-push | "a seed file is untracked" | Always exits 0. |

Every failure message says **what is wrong, where, and the command that fixes it.**

### 6.3 Stated limits

- Hooks are **client-side**. `git commit --no-verify` and `git push --no-verify` skip them.
- If CI runs tests as non-blocking, write that down here. A gate people believe is enforced but is not is worse than no gate.
- The checker proves that links **resolve**. It cannot prove that the content is true.
- Hooks never replace review. They catch the mechanical mistakes so review can look at the hard ones.

## 7. AI operating model

How one human plus many agents move fast without corrupting the spine.

### 7.1 Orchestrator and lanes

- **One orchestrator owns the spine and all numbering.** Lanes are barred from the spine files (`hot-state`, `workstreams`, `decisions`, `state-backlog`, `project-log`) and from minting numbers.
  Before merging a lane, run a name-only diff of the spine files on its branch. It must be empty.
- **Parallel build lanes get their own git worktree.** Never split work by file path inside one tree.
  One writer per worktree, the orchestrator included. Never write into a live lane's worktree. An idle ping is not proof it stopped.
- **A worktree starts from the session HEAD, not the base named in the brief.**
  Every lane runs `git merge-base --is-ancestor <intended-base> HEAD` first and reports the answer.
  The orchestrator re-checks before merging.
- **Lanes commit every step that builds and passes.** Never one dirty-tree batch at the end. Small commits are the visible result.
- Lanes write deliverables under the evidence tree with the names given in the brief. The orchestrator indexes them.
- Before stopping a lane, check its worktree is clean and its tip has every instruction. "Done" reports can be wrong.

### 7.2 Model routing (a principle, not a roster)

- Use the **cheapest model that can do the job**. Small model: breadth sweeps and inventories. Mid model: must-not-miss passes. Top model: orchestration and genuinely hard design.
- **Always pass the model explicitly.** Sub-agents inherit the parent's model. That is how a run of expensive agents happens by accident.
- **One model family checks another's work.** Different families have different blind spots.
  Findings that several reviewers agree on are high-confidence. A single-source finding gets verified first.
- **The day's actual mix lives in memory, not in this file.** It changes with budget (in one project it flipped three times in five days).
  Writing it here would make this file stale on purpose.

### 7.3 The review and fix loop (cross-family)

1. The top model and the lanes it orchestrates **build**.
2. A **different model family reviews the finished work.** Make it a harsh maintainability review **scoped to the 3 to 4 seams most likely to drift**. Never "review the whole diff".
3. **Every change request (CR) and every Critical, High or Medium finding goes into the next fix round.**
   One commit per finding, with its ID and severity in the subject, for example `fix(spine): CX-2 High — rejects empty WS line`.
4. The reviewer **re-reviews the fix round**. The loop ends only when a review returns no Critical, High or Medium finding and no open CR.
5. **Lows** go to `docs/state-backlog.md` with their `WS:` line, or are rejected with one line of reasoning.
6. A finding the orchestrator disputes is **recorded with its evidence**, and the human rules on it. Never drop it silently.

The `review-fix-loop` skill (part 04) runs this loop.

### 7.4 Verification habits

- **Re-verify every negative** an agent reports ("not found", "zero usages"). Prove the query *can* match before you believe a zero. Three such claims were wrong in a single session.
- **Verify the premise.** A written claim about runtime behaviour is a hypothesis with a date. Re-measure before you fix it.
- **Teeth-check important tests.** Break the code, watch the test go RED, restore, watch it go GREEN.
- **Blind recounts** for any count an AI produced. If you skip it, write "single pass, unverified" in the entry itself.
- **A green suite is not evidence by itself.** Check that the tests ran, and that CI does not treat them as non-blocking.
- **"Done" is a claim.** Agent self-reports need independent proof (see LAND in section 4).

### 7.5 Machine hygiene

Fast work has physical limits.
- Cap concurrent build and test lanes at about 3 to 4. Run one production build on the integrated branch, not one per lane.
- Check free disk space before spawning lanes. Prune idle worktrees on a schedule. (One project filled a disk to 0 bytes with stale worktrees.)
- Never force-remove a worktree whose dependency folder is a junction or symlink. Unlink it first.
- Forbid recursive shell `grep` or `find` from the repo root in agent briefs. Orphaned search processes stall the machine. Use the agent's own search tools.

### 7.6 Context canary

Report context use after big turns. Stop and start fresh at about 50% of the window, after you write thread state out
to the spine (`Resume:` line, hot-state, seed). Trust the tool's context readout, not your own estimate.

## 8. Smell tests

Check these now and then. Each one points to a rule above.

- Returning to last week's parked work means reading `git log`. The board is not doing its job (section 2).
- Changing a verb's order needs edits in two files. Duplication is back (section 5).
- The link checker is red and pushes go out with `--no-verify`. Tighten nothing until a cleanup thread brings it to 0 (section 6.1).
- A skill or convention names a path that does not exist. This is executor drift. Run a path-existence check over every spine doc.
- Two lanes report the same backlog number. Someone other than the orchestrator minted (section 3.3).
- A listed artifact has not been written in a month. Retire it, or make it on-demand. Do not leave it routed.
- The `canary` count dropped and no thread was removed. The parser is blind (section 6.1).
- Hot-state has more than one `(previous)` block. Move the older blocks into the journal (section 2).

## 9. Adoption order (new repo)

1. Board + hot-state, with the **`WS:` first-line rule from day one**, so there is never a backfill.
2. Decisions + journal + backlog, each carrying `WS:`.
3. The router (`hygiene.md`) with the verb step orders, and skills that read it.
4. The link checker in pre-push. It starts at 0 findings, so no ratchet is needed on a new repo.
5. Seeds, committed with the spine.
6. The operating-model rules (who writes the spine, who mints, the review loop).
7. The WARN-only nudges.
8. The evidence tree, tax ledger, findings ledger and landing ledger, as the project grows into them.

On an **existing** repo, install the checker with `ledger-check.conf` cutoffs set to highest existing number + 1 (tomorrow's date for the log), then walk the cutoffs back as you clean up.

## 10. Where things are

- Router and verb step orders: `hygiene.md`
- Templates: `docs/templates/seed-template.md`, `docs/templates/landing-ledger-template.md`
- Gates: `.githooks/`, `scripts/spine/` (see `.githooks/README.md`)
- Skills (machine-local, in `~/.claude/skills/`): `wrap-session`, `hydrating-session-context`, `review-fix-loop`
- Working norms and the day's model mix: the agent's auto-memory, not this repo
