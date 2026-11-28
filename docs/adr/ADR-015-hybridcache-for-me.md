# ADR-015 — HybridCache for GET /me, invalidated by tags; authorization is never cached

- Status: accepted
- Date: 2026-11-28
- Decision entry: D-081 in `docs/decisions.md` (the D entry links back here)

## Context
The k6 stampede scenario (`load/`, runbook [load-testing](../runbooks/load-testing.md)) failed on one endpoint only: cold, simultaneous `GET /me` calls reached p95 382 ms against a threshold of p95 < 300 ms. `/me` assembles the caller's effective permissions (roles, groups, overrides, switched-off modules) on every call, and the console calls it on every page load.

## Decision
Cache the `/me` permission payload with Microsoft.Extensions.Caching.Hybrid 9.10.0 (MIT, supports net8), in-process (L1) only, for `GET /me` only. Key = the authenticated user id; tags `perm:user:{id}` and `perm:all`; every write that can change someone's access invalidates them: group membership, overrides, module switches (`perm:all`) and user edits (`UpdateUser`). TTL 30 s bounds clock-expired overrides. Authorization is never cached: the JWT check re-reads the caller's access on every request (`JwtBearerSetup.cs:70`), so a stale entry can only delay what the console's navigation shows, never grant access.

## Alternatives considered
- `IMemoryCache`: no stampede protection (concurrent misses all hit the database) and no tags, so invalidation would be key-by-key.
- Redis or another L2: not in the compose stack; HybridCache accepts an `IDistributedCache` later without code changes.
- Caching claims reads too: claims are scoped per caller (claimant scope, officer workload), so a cache key mistake would leak data; `claims_list` already passed its threshold (p95 123 ms), so it was skipped.

## Consequences
Only `/me` was measured better: p50 145 -> 49.5 ms, p95 382 -> 241 ms (212 on a repeat), p99 630 -> 267 ms, threshold FAIL -> PASS. The uncached claims reads were slower in the after-runs (claims_list p95 123 -> 250 ms, claims_search 58.6 -> 224 ms), most likely host load with the fleet running; one run before and two after give a direction, not significance. k6 setup warms `/me`, so the single load under a cold stampede is proven by a unit test (200 concurrent misses, one loader call). Never claim the cache made the API faster in general. Every new write that changes access must invalidate the tags (pinned by handler tests). Reports: `load/report/compare.html`; decision D-081.

## Supersedes
None.
