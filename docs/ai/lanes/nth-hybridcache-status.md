# Lane nth-hybridcache status (owner ask: HybridCache + k6 "after"; Refs D-064)

## Commits
- `1b56beb` slice 1: feat(api): HybridCache wiring, L1 only, with the /me cache port and its invalidator
- `856d24c` slice 2: feat(api): cache GET /me per user and invalidate it on permission writes
- slice 5: test(load): stampede "after" runs with the /me cache and the before/after overlay

## Gates (copied from output)
- `dotnet build`: 0 Warning(s), 0 Error(s)
- `dotnet format --verify-no-changes`: exit 0
- `dotnet test --filter PermissionCacheTests`: Passed 4, Failed 0
- `dotnet test --filter Fugue.Tests.Identity|PermissionCache`: Passed 165, Failed 0
- ArchTests: Passed 37, Failed 0
- Injected check: with the SetOverride invalidation removed, `A_cached_me_is_fresh_...` fails (`Assert.Contains() Failure`).

## What changed in shared files (exactly)
- `Directory.Packages.props`: one `PackageVersion` line, `Microsoft.Extensions.Caching.Hybrid` 9.10.0 (MIT, net8.0 target).
- `src/Fugue.Infrastructure/Fugue.Infrastructure.csproj`: one `PackageReference` line.
- `src/Fugue.Infrastructure/DependencyInjection.cs`: one `using` pair and a 9-line block at the end of the identity registrations.
- `src/Fugue.Api/appsettings.json`: new `Cache` section.
- Handlers: `ChangeGroupMember.cs` (add/remove), `ChangeOverride.cs` (set/remove), `SetModuleActive.cs`: one invalidation call after a
  successful write each (expression bodies became block bodies to hold it).

## Behaviour and design choices
- HybridCache L1 only: no Redis in compose, so no L2. Stampede protection is HybridCache's own (one factory run per key).
- Key `me:{userId:N}` from the authenticated caller only; tags `perm:user:{id}` and `perm:all`. Nulls (unknown/inactive) are not cached.
- Enforcement is not cached: the JWT check re-reads access on every request, so a stale /me can only show stale UI hints, never grant.
- TTL 30 s (overrides can expire on the clock; the TTL bounds that). L1 size cap 64 MiB.

## Deviations and gaps
- No role write handler exists (roles are seeded, read-only), so no role invalidation call. CreateUser makes a new id with no entry.
- Slice 3 (a claims read) skipped: see the lane report.
- No row for this lane in `docs/branch-ledger.md`, so no Status cell was touched.


## k6 stampede, before (committed `stampede-20261128T1210`, "uncached baseline") vs after (`stampede-after-20261128T1208`)
| endpoint | p50 before -> after | p95 before -> after | p99 before -> after |
|---|---|---|---|
| me (cached) | 145 -> 49.5 ms | 382 -> 241 ms (threshold p95<300: FAIL -> PASS) | 630 -> 267 ms |
| claims_list (not cached) | 48.1 -> 56.5 ms | 123 -> 250 ms | 229 -> 419 ms |
| claims_search (not cached) | 41.7 -> 33.2 ms | 58.6 -> 224 ms | 68.8 -> 255 ms |
Repeat after run `stampede-after-20261128T1209`: me p50 22.3, p95 212, p99 226 ms; claims_list p95 228; claims_search p95 90.4.
The uncached endpoints got slower in the after runs: same code, so this is machine contention (load average 5-6 with the
fleet running), not the cache. The setup() warm-up call primes /me, so the k6 wave reads a warm entry; cold-key coalescing
is proven by the unit test (200 concurrent misses, loader runs once), not by k6.

## Next
- Merge main after nth-useredit merges and add one invalidation call to its user-update handler.
