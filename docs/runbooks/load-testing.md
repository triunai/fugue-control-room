# Load testing (k6): smoke, load, stampede, and the percentile overlay

Scripts live in `load/`. They complement the older correctness-under-concurrency scripts in `perf/k6/` (pickup race, idempotent submit); these three measure latency percentiles of the hot **read** endpoints. Needs k6 v2 (`brew install k6`; recorded with v2.4.0), Docker, Node 20+.

## What is measured

| Endpoint tag | Request (staff token) |
|---|---|
| `me` | `GET /api/v1/me` |
| `claims_list` | `GET /api/v1/claims?status=Submitted&limit=50` |
| `claims_search` | `GET /api/v1/claims/search?q=SG motor&limit=20` (needs Meilisearch) |
| `claims_counts` | `GET /api/v1/claims/counts` |
| `claim_detail` | `GET /api/v1/claims/{id}` (tagged by the route template, never the id) |

Thresholds (a run exits non-zero when one is crossed, and the report shows PASS or FAIL per rule): p95 < 300 ms, p99 < 800 ms, HTTP failures < 1 %, checks > 99 %, globally and per endpoint. They are starting limits chosen for a laptop, not a requirement.

| Scenario | Shape | Model |
|---|---|---|
| `smoke` | 1 VU, 30 s, the five reads once a second | closed |
| `load` | mixed staff session with 0.5 to 1.5 s think time: 0 to 50 VUs over 2 min, hold 3 min, ramp down 30 s (`SCALE=0.1` shortens it for a dry run) | closed (VUs) |
| `stampede` | 200 VUs start at the same instant (`per-vu-iterations`), same token, same URL, no think time, three waves 12 s apart: `me`, `claims_list`, `claims_search`. Each VU makes 5 requests: the first is tagged `burst:first` (the simultaneous one) | closed, deliberately synchronised |

k6 has no barrier primitive. The stampede is as simultaneous as k6 can start VUs (all begin together after `setup()`), not a guaranteed single-instant release. The load scenario is a closed model, so under overload its latencies are understated (coordinated omission); read it as "what do 50 busy users feel", not as a capacity limit.

## 1. A throwaway target (the load profile)

Never aim a load test at the demo stack on :8080 (it is shared and its limiter will answer 429). Run your own API against a throwaway Postgres and Meilisearch:

```bash
export DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$HOME/.orbstack/bin:$PATH DOCKER_HOST=unix://$HOME/.orbstack/run/docker.sock
docker run -d --rm --name k6-pg -e POSTGRES_PASSWORD=pw -p 55531:5432 postgres:17-alpine
docker run -d --rm --name k6-meili -e MEILI_MASTER_KEY=k6-load-test-master-key-123456 -e MEILI_NO_ANALYTICS=true -p 55532:7700 getmeili/meilisearch:v1.12.8
docker exec k6-pg psql -U postgres -c "create role fugue login password 'pw' superuser" -c "create database fugue owner fugue"
ConnectionStrings__Postgres="Host=localhost;Port=55531;Database=fugue;Username=fugue;Password=pw" \
  dotnet ef database update --project src/Fugue.Infrastructure          # after: dotnet tool restore
load/start-api.sh                                                       # API on :5431, seeded; leave it running
```

**Load profile: the limiter is raised, never in prod.** The only rate limiter in the API guards sign-in (`RateLimiting:Auth:*`, `src/Fugue.Api/Auth/AuthRateLimiting.cs`); read endpoints are not limited. `start-api.sh` raises it with environment variables (`RateLimiting__Auth__PerIpPermits=100000`, and the per-email and ceiling values) so a sign-in storm cannot fail a run. The scripts sign in **once**, in `setup()`, and share the token (15 minute lifetime; the longest scenario is under 7 minutes). The signing key in the script is a throwaway; the seeded development password comes from runbook 05.

Stop everything afterwards: `docker stop k6-pg k6-meili` and stop the API by its port (`lsof -ti tcp:5431 | xargs kill`).

## 2. Run a scenario

```bash
load/run.sh smoke                      # 30 s
load/run.sh load "before cache"        # about 6.5 min; the optional label names the run in compare.html
load/run.sh stampede "before cache"    # about 40 s
BASE_URL=http://localhost:5431 PW=... load/run.sh smoke   # other target or password
```

`run.sh` runs k6 with `--out json`, then `load/report.mjs` adds the latency-over-time series. Output, per run, in `load/results/`: `<scenario>-<runid>.json` (the data) and `<scenario>-<runid>.html` (the report, self-contained: inline CSS and SVG, opens offline). A bare `k6 run load/scenarios/smoke.js` also writes both files but without the time series. For k6's own live view add `K6_WEB_DASHBOARD=true K6_WEB_DASHBOARD_EXPORT=dash.html` to the command (needs a run longer than three dashboard periods for graphs); the Fugue report does not depend on it.

## 3. Read the report

- Cards: requests, requests per second (first to last request; for the stampede the waves idle between each other), peak requests per second, p50/p95/p99/max, HTTP failure rate, thresholds.
- Percentile table and bars per endpoint. A bar over its dashed gold threshold marker turns red.
- Over time: p50/p95/p99 and throughput/failure % per bucket (about 60 buckets per run).
- Latency is k6 `http_req_duration` for requests in the run phase (sign-in and warm-up calls are excluded). Percentiles come from every request; never read the average.
- 100 % checks and 0 % failures with a failed latency threshold means "correct but slow"; the reverse means errors (look at the status mix first).

## 4. Compare two runs (the overlay)

```bash
node load/compare.mjs load/results/stampede-<before>.json load/results/stampede-<after>.json
open load/report/compare.html
```

`compare.html` overlays the two percentile curves per endpoint (x: p50, p90, p95, p99, max), a table with the percentage change, and p95 over time. A is navy and B is gold dashed. Compare like with like: the same scenario, the same data, the same limiter settings, the same machine load, only the change under test different, and note the commit and settings of each run. The page warns when the two runs are different scenarios. One run each shows direction, not significance; repeat before claiming a small difference.

Limitation: k6, the API, Postgres and Meilisearch share one machine, so numbers include contention from the load generator itself.
