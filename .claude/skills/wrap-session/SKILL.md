---
name: wrap-session
description: Use at the end of a Fugue working session or when the owner says "wrap", "wrap up" or "wrap the session". Runs the wrap ritual - capture decisions, update hot-state, prune closed workstreams into the project log, close finished threads into the project log and CHANGELOG, write the resume point and handoff with the next seed prompt, close local beads. Triggers - "wrap", "wrap up", "end of session", "park this", "write the handoff".
---

# Wrap session (Fugue)

The step ORDER lives in `hygiene.md` (WRAP row). This skill adds the how. Follow the row; do not skip a step (write "n/a" in the handoff if empty). Only the lead writes spine files.

## Steps
0. **Owed inbox**: walk it in `docs/hot-state.md`; handle or carry each item.
1. **Capture decisions**: every call that settled this session becomes a `D-NNN` entry in `docs/decisions.md` (date, decision, why, `Who:`, `Status:`, `Promote:`). List worktrees first (`git worktree list`) before minting a number. If a decision changes architecture, a public contract, the data model, security posture or a cross-cutting convention, write the ADR from `docs/adr/_template.md` and set `Promote: yes (ADR-NNN)`. Run `node scripts/spine/adr-check.mjs`.
2. **Board**: update `Status:`/`Resume:` for every touched thread in `docs/workstreams.md`.
3. **Close finished threads**: for each `W-NNN` that is done, run `node scripts/spine/close-workstream.mjs W-NNN --outcome "<2-5 lines>" --decisions "D-NNN, ADR-NNN" --followups "<each becomes a new W row>"` (add `--commits sha,sha` only for pre-gate history). It gathers the commits by `Refs: W-NNN`, appends ONE identical entry to `docs/project-log.md` and `CHANGELOG.md`, and deletes the board row. Then add a new W row for each follow-up. Entries are never edited afterwards. The board holds live threads only.
4. **Hot-state = the resume point**: OVERWRITE `docs/hot-state.md` (never append; at most 40 lines) so the next session continues EXACTLY where this one stopped: last action done; exact next action (command, file, line); work in progress not yet committed (branch, dirty files, why); open W ids in flight; pending owner questions; `Last green sha:` with test counts and red reasons; and a copy-paste seed prompt. Verify the sha with `git log --oneline -1` after the final gates.
5. **Backlog and failure modes**: new open items to `docs/state-backlog.md`; any new trap to `docs/failure-modes.md` (symptom, cause, defusing move). Delete merged rows from `docs/branch-ledger.md`.
6. **Journal**: add an entry to `docs/ai-journal.md` for each point where the AI proposed and the owner accepted, challenged or overrode.
7. **Handoff**: write or extend `docs/ai/handoffs/<date>-lead.md`: today's summary, traps, facts with how they were verified, not-done, owner decisions owed, and a copy-pasteable SEED PROMPT for the next session.
8. **Link check and cold storage**: `node scripts/spine/commit-link-check.mjs` (every commit listed in an entry or `Refs:` an open W; fix or explain "unlinked", then `--advance`). If the project log has more than 20 entries or entries older than 30 days: `node scripts/spine/archive-project-log.mjs` (moves them verbatim to `docs/archive/project-log/`, adds an index line). CHANGELOG is never pruned.
9. **Gates**: `bash scripts/spine/ledger-check.sh` (exit 0), `node scripts/spine/prune-check.mjs`, `npm run test:scripts`, `dotnet build` (0 warnings), `dotnet test --no-build` (state counts).
10. **Commit** spine and handoff together (`docs(spine): ...`, trailers `Refs: W-NNN`, `Co-Authored-By:`). Never push unless the owner asks.
11. **Local beads**: `bd close <id> --reason "<short sha>"` for finished work; keep the epic open. Beads are local only: never write a bead id into a commit or a doc.

## Do not
Append to hot-state, edit old project-log or CHANGELOG entries, hand-write a closed-thread entry in only one of the two files, leave a done thread on the board, claim "tests pass" without the counts, or wrap with an unverified `Last green sha`.
