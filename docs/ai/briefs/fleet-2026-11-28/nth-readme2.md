# Lane nth-readme2 — Sonnet — worktree .worktrees/nth-readme2, base main 7adb4d9 — plan item 11, READY by 12:55
Read HEADER.md (incl. Addenda) and OWNERS.md (Addendum 14). Web port 4433 (only if you need a dev server). Docs lane.

Goal: README.md tells a reviewer the truth about the repo as submitted. Counts and claims ONLY from real command output
run in this lane (paste the output lines in the report), never copied from other docs or reports.

1. Merge main LATE: nth-useredit and nth-hybridcache are merging ~12:35-12:50. At ~12:45 (and again before READY)
   `git merge main`, then take the counts from the merged tree. If one of them has not merged by 12:50, describe only
   what is on main; never describe an unmerged feature.
2. Counts (real output; don't run the full .NET suite, the lock is busy):
   - .NET test counts: `dotnet test --list-tests` per test project (count the listed tests) — say "listed", and quote
     the integrator's last full-suite result from `git log main` (e.g. push 9: 849 + 37) with its commit sha.
   - Web: `cd web && npm test` → Test Files / Tests lines.
   - Bruno: ONE `cd bruno/fugue && npx bru run --env local --env-var baseUrl=http://localhost:8080` against the demo
     (read-only use is allowed; space it >= 60 s from any other bru run) → Requests/Tests summary.
   - ArchTests: from `dotnet test tests/Fugue.ArchTests` (fast, no Docker).
   Replace every stale number in README with these, each with what produced it.
3. Wording rules: search is "keyword full-text search (Meilisearch: typo tolerance, synonyms, highlights)", NEVER
   "hybrid search". If HybridCache merged: "HybridCache (L1, in-process) on GET /me" — that is caching, not search.
4. Links (verify each path exists on main): Fugue, explained (`docs/explained/` + the /explained/ page), the Changelog
   page, `docs/runbooks/load-testing.md` (k6 + before/after compare), `docs/design/legacy-sync.md` (design only, KIV),
   `docs/DEMO.md` (walkthrough incl. step 15 software factory), CHANGELOG.md.
5. Screenshots: refresh ONLY those README already embeds, if the screen changed; headless Playwright against :8080 (one
   sign-in per persona, pace for the limiter, 0 console errors), save under the path README already uses.
6. One-line fix: `src/Fugue.Application/Features/Claims/Repos/IClaimSearch.cs:22` says ClaimantName is "null for a
   claimant" — FALSE (`src/Fugue.Infrastructure/Search/ClaimDocument.cs:91` ToHit returns it for everyone; a claimant
   only finds their own claims, so it is their own name; no leak — plan item 10 note). Reword to the truth. XML docs feed
   the OpenAPI document: run `npm run api:openapi && npm run api:gen` from the repo root, commit docs/api + the
   generated client if they changed, and `dotnet build` 0 warnings + `dotnet format --verify-no-changes`. Separate commit
   `docs(api): ...`.
Gates: web lint+test (if web touched), the commands above, markdown links checked (script or grep + ls). Commit slices:
(a) the doc-comment fix, (b) README. Report + READY per HEADER to overnight-lead AND aspnet-cc.
