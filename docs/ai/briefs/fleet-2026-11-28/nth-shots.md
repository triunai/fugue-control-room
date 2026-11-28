# Lane nth-shots — Sonnet — worktree .worktrees/nth-shots, base main 3b903ba (stack recreated clean 13:05) — WRAP FIX, READY in 12 min
Read HEADER.md (incl. Addenda) and OWNERS.md (Addendum 16). No dev server: shoot the demo stack on :8080 (READ-ONLY use:
never create, edit or delete data — the stack was just recreated clean for the 14:03 verification).

Problem: the 5 README screenshots merged in b4a4376 were shot on a dirtied demo (officer "Dev Staff (edited)", claims made
by Bruno runs). The stack is now recreated clean (`down -v`, fresh seed).
Do:
1. Headless Playwright (one sign-in per persona: staff for queue / record-officer / sheet / calendar, self for
   record-claimant; pace for the limiter; credentials from docs/runbooks/05 or 00), `?tour=off`, same viewport and same
   framing as the existing files (read their pixel size with `sips -g pixelWidth -g pixelHeight` and match it), save over:
   `web/docs/screenshots/claims-{calendar,queue,record-claimant,record-officer,sheet}.png`.
2. Before committing, LOOK at each PNG (Read tool) and confirm: no "(edited)", no "BRU-" policy numbers, no error toast,
   no welcome/tour overlay. Console errors must be 0 for each page (collect and paste).
3. If README's xUnit count is stale vs `dotnet test tests/Fugue.Tests --list-tests` on current main, fix that number
   only (badge + table row), from that command's output. Touch nothing else in README — in particular NOT the Bruno
   line or the HybridCache bullet (the integrator owns those from its own clean-stack compose run). Do NOT run Bruno.
Commit `docs(readme): screenshots re-shot on the clean demo stack`. Report + READY per HEADER (to overnight-lead AND aspnet-cc).
