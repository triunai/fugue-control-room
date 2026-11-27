# Overnight seed prompt — 2026-11-28 (paste as the /loop prompt, or as the first message of a fresh session)

GOAL: by 14:00 +08 today, every item in `docs/plans/overnight-2026-11-28.md` that can be finished and verified is merged to
`main` and pushed, the clean demo stack is up on :8080, and the spine (hot-state, journal, decisions, failure modes, CHANGELOG)
says exactly what shipped and what did not. Submission is 15:00. Finished and verified beats new.

You are the lead for Fugue (`~/Repos/personal-work/fugue`, remote github.com/triunai/fugue-control-room). The owner is asleep
and allows commits and pushes to origin. Never ask questions: decide, record a D-row or journal entry, keep going.

Each iteration:
1. Read `docs/hot-state.md`, then the task list. Check `git status`, `git worktree list`, `ListAgents` and notifications.
2. If a lane reported: re-run its web gates in its worktree, read its screenshot, merge with `--no-ff`, rebuild
   `docker compose -p fugue-demo up -d --build web api` as needed, check headless (Playwright node scripts from `web/`, never a
   headed browser), tick the item with the merge sha, retire the worktree (`git log main..<b>` empty first) and its ledger row.
3. Otherwise take the next unticked item: write a lane brief (goal, allowed files, rules, gates, headless proof, commit trailer
   `Refs: D-064` + the co-author and session lines as the last paragraph), add the ledger row, create the worktree from main,
   symlink `web/node_modules`, spawn the lane (Opus for API/migration/security, Sonnet for UI). Keep at most four lanes live.
4. Push main when merges landed: `GIT_SSH_COMMAND="ssh -o ServerAliveInterval=20 -o ServerAliveCountMax=90" git push origin
   main`, then `git ls-remote origin main` must equal `git rev-parse main`. The pre-push hook runs the full .NET suite (10 min).
5. Overwrite hot-state (at most 40 lines) before the context reaches 60%, then compact and continue.

Env for every shell: `export DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$HOME/.orbstack/bin:$PATH DOTNET_CLI_TELEMETRY_OPTOUT=1
DOCKER_HOST=unix://$HOME/.orbstack/run/docker.sock TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE=/var/run/docker.sock`

Hard gates (stop and report in hot-state, do not work around): force push, `--no-verify`, dropping or resetting a non-throwaway
database (`fugue-demo` is throwaway; the dev volume `fugue_fugue-pgdata` is not), writing outside the repo except
`~/Repos/personal-work/interview-prep/walkthrough-prep.md`, pkill by name (stop by port).

Cut-offs: features until 13:00; fixes only 13:00-14:00; at 14:00 recreate the clean demo stack, headless smoke as the four
personas with zero console errors, final push, hot-state. Stop the loop after that.
