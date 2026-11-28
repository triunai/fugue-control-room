# Lane nth-brunoci status (Refs D-064)

## Commits
- fix(bruno): reuse one token per account per run (see git log)
- this status file

## Gates (copied from output)
- Full collection, default limiter, own API on :5234, before (main 8e0993b files): 127 requests, 115 passed, 12 failed; /auth/token 16x200, 1x401, 2x429.
- Full collection, after: `Requests 127 (127 Passed)`, `Tests 153/153`; /auth/token 8x200 + 1x401 (the wrong-password test).
- `bru run import` alone: 11 (11 Passed), 3 sign-ins. `bru run letters` alone: 10 (10 Passed), 4 sign-ins.

## Shared files changed
- None outside `bruno/fugue/**` (11 .bru pre-request scripts).

## Deviations
- Bruno variable names reject "@", so the cache key is `tok_<email with @ replaced by _at_>`.
- No branch-ledger row exists for this lane, so the ledger was not touched.
