# Lane nth-brunoci — Sonnet — worktree .worktrees/nth-brunoci, base main 8e0993b — FIX, READY by 12:40
Read HEADER.md (incl. Addenda) and OWNERS.md (Addendum 13). Ports api 5234 / pg 55534. No src/, no tests/, no web/.

Failure (owner-pasted CI log, job `api-contract`, `npx bru run --env compose`, .github/workflows/ci.yml:97):
127 requests, 115 passed, 12 failed. `import/01-sign-in.bru` and `letters/01-sign-in-the-accounts.bru` pre-request scripts
got HTTP 429 from POST /api/v1/auth/token; every import request then answered 401. Letters survived on stale vars.
Root cause (dispatcher, verify it): ~15 .bru files each call /auth/token themselves (collection.bru, auth/04-06,
roles/02, audit/08+10, users/03+04+06, import/01, claims/01, letters/01 …) and the run takes ~27 s, so one run makes
more sign-ins than the sign-in limiter allows (20/min per IP — find the real numbers in src/ appsettings, read-only).

Fix (collection-side, the limiter stays ON and unchanged — it is a feature the suite must coexist with):
1. One per-run token cache: a sign-in for an email reuses `bru.getVar("tok_<account>")` when set (and younger than
   ~10 min if the collection already tracks age — collection.bru line ~11 does), else signs in once and stores it.
   Bruno cannot import shared JS between .bru files unless the collection already has a scripts/ folder or
   `bru.runRequest`; check what this Bruno version supports and pick the smallest honest pattern (inline the 5-line
   cache in each sign-in script is acceptable).
2. KEEP live sign-ins where the test IS sign-in: auth/01-token, 02-wrong-password, 07-refresh-rotates, 08, 09.
3. No assertion text or expected status changes. Folder-alone runs (`bru run import`) must still work (cache miss → sign in).
Proof: run the FULL collection once against your own API on 5234 (`--seed`, DEFAULT limiter settings, compose-like env),
all green; count the /auth/token calls in that run (e.g. API log or a counter) and report before vs after; then run
`bru run import` and `bru run letters` alone, green. Paste the Execution Summary tables. Commit `fix(bruno): ...`
(AGENTS check-fix-has-test: the .bru files ARE the tests — if the hook still objects, say so in the report, never --no-verify).
Report + READY per HEADER. Proposed D-row: "Bruno reuses one token per account per run; the sign-in limiter stays on in CI".
