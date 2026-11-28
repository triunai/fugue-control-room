# Lane nth-hybridcache — Opus — worktree .worktrees/nth-hybridcache, base = main AFTER nth-refactor merged
Read HEADER.md (incl. Addenda) and OWNERS.md (Addendum 12). Ports api 5233 / pg 55533 (NOT 5433: compose Postgres).
HARD STOP 12:50: READY or BLOCKED by then; if the cache is not green by 12:35, ship nothing in src/ and report the design
as a proposed D-row ("deferred: HybridCache, reason, design") — the integrator records it.
Owner: "hybrid cache" = Microsoft.Extensions.Caching.Hybrid (L1 in-memory + optional L2, built-in stampede protection).
NOT related to search (search stays keyword full-text; never call anything "hybrid search").

Premises to verify (rule 1): the package version that supports net8.0 and its licence (MIT expected) — cite nuget.org
or the package's nuspec in the report; `Features/Me/Queries/GetMe.cs` computes effective permissions per request; which
claims read the k6 stampede hits (read `load/scenarios/*` on main — the staff claims list and/or GET /me).

Slices:
1. Wiring: package reference (central package management if the repo uses it), `AddHybridCache` in Infrastructure DI,
   `CacheOptions` via IOptions + validation (TTL for me-permissions, TTL for the claims read, L1 size cap), appsettings
   section. No L2 (no Redis in compose) — say so; HybridCache works L1-only.
2. GET /me: cache the effective-permissions result per user id. Invalidation: tag `perm:user:{id}` and a global tag
   `perm:all`; remove on role/group/override/user-role writes (find each write handler; ONE helper, e.g.
   `IPermissionCacheInvalidator`, called from those handlers or one pipeline behavior — not ad-hoc strings). If
   nth-useredit has merged when you finish, merge main and add the call to its update handler.
3. ONE hot claims read (the one the stampede scenario targets). The key MUST include everything that scopes the result
   (caller user id / scope, market, filters, page cursor) — a cache that leaks one user's claims to another is a fail.
   Short TTL; tag `claims`, invalidated after any claim command commits (one place). If correct scoping is not
   achievable by 12:35, skip slice 3 and say why.
4. Tests: invalidation tests (write → next read is fresh), scoping test (two users, different results), stampede test
   (N concurrent GetOrCreate on one key → the factory runs once). ArchTests stay 37.
5. Load: rerun the nth-k6 stampede + load scenario against YOUR API on 5233 (cache on, limiter raised exactly as
   `docs/runbooks/load-testing.md` says), produce `load/results/*-after-*.json` + HTML, and
   `node load/compare.mjs <before.json> <after.json>` → `load/report/compare.html` (before = the committed nth-k6 run).
   Report the real numbers; if after is not better, say so.

Gates: HEADER .NET gates, full suite once under the lock (FM36 for Docker reds), Bruno `me` + `claims` areas against 5233,
compare.html screenshot `reports/nth-hybridcache-compare.png`. Proposed D-row: "HybridCache L1-only for /me and <read>,
tag invalidation, why not IMemoryCache (no stampede protection) or Redis (not in compose)". Report + READY per HEADER.
