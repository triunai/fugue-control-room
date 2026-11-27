# Fugue — hot state (the resume point)

OVERWRITTEN at every wrap or pause, never appended; at most 40 lines. After a wrap or days away: read this first, then do "Next action" exactly. History lives in `docs/project-log.md` and `CHANGELOG.md`.

## ▶ RESUME HERE — 2026-11-28 03:10 +08 (submission today 15:00; owner asleep, lead runs the overnight loop)
Mode: D-064 asks past the freeze, run as parallel lanes under ADR-014 / D-065. Task list: `docs/plans/overnight-2026-11-28.md` (work it top to bottom, tick with shas).
main = 3dcdb02 + spine commit (local); origin/main = fb10684 (push pending, item 2). Verified on main before the last two commits: web 241 Vitest, lint, build; full .NET suite 747 + ArchTests 37 at the fb10684 push.
Demo stack `fugue-demo` on :8080 (web rebuilt at cda483c; rebuild after each merge). Login limiter: `docker compose -p fugue-demo restart api` after many test sign-ins (failure mode 25).

## Live lanes (ledger rows in docs/branch-ledger.md)
- nth-uam (Opus): admin creates users; full suite was running at 02:54; nothing committed yet. Merge after its report.
- nth-score (Opus): The Score inspector; not reported yet.
- search-research (Haiku, no worktree): asked to write `docs/research/2026-11-28-meilisearch.md` (commit it with the spine).
Retired today: nth-money, nth-prelude, nth-onboard, nth-readme (merged), nth-shellfix, nth-claimsearch.

## Next action (exact)
1. Overnight list item 1: browser re-check of 2ba4e19 on rebuilt :8080 (welcome centred, outside click closes it, nav spam works).
2. Push main with `GIT_SSH_COMMAND="ssh -o ServerAliveInterval=20 -o ServerAliveCountMax=90"`, then `git ls-remote origin main`.
3. Continue the list; spawn lanes for items 5 to 10 (briefs: goal, allowed files, gates, headless screenshot, trailer `Refs: D-064`).

## Owner facts to keep straight
"nth" = nice-to-have lane prefix. The owner believes Postgres 18 and timestamped GUIDs: verify (compose image, id generation) and state the truth in the Stack modal and the handoff. Owner wants ideas weighed before building. Refactor plan: backlog #010 (after lanes, today).

## Traps hit
33 (signed-in + reloaded browser check), 34 (push keepalive + ls-remote), 35 (shared test clock, background indexer, crowded DB: prove flaky fixes by injecting the condition). A native modal `<dialog>` makes the page inert: it must be visible and dismissable by an outside click. zsh does not word-split `$F`: use `bash -c`. Git hooks need dotnet on PATH in the committing shell.
