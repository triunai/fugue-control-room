# k6 load scripts for the claims API

Three scripts, each with pass/fail thresholds (k6 exits non-zero when one is crossed). Plain k6 JavaScript, no remote imports; shared helpers in `lib.js`. Recorded with k6 v2.4.0 (`brew install k6`).

| Script | What it does | Creates rows | Run against |
|---|---|---|---|
| `claims-queue.js` | staff work queue (`GET /claims?status=Submitted&assigned=none&limit=50`) then one claim's detail; ramp to 20 VUs, 45 s | no | the default compose stack, `http://localhost:5081` |
| `claims-submit.js` | `POST /claims` with a fresh `Idempotency-Key`; every 5th iteration replays the same key and body and expects the same claim back with `Idempotent-Replayed: true`; 10 VUs, 30 s | yes (thousands) | a throwaway stack |
| `claims-pickup-race.js` | two officers (`staff@`, `support@`) `POST /pickup` the same Submitted claim at the same moment with the same `If-Match`; 50 races on 5 VUs; exactly one must win | yes (50 claims) | a throwaway stack |

## Run

```bash
npm run perf:queue                                   # = k6 run perf/k6/claims-queue.js ; BASE_URL defaults to http://localhost:5081
BASE_URL=http://localhost:5181 npm run perf:submit   # throwaway stack, below
BASE_URL=http://localhost:5181 npm run perf:race     # RACES=100 to change the number of races
```

`PW=...` overrides the seeded development password (runbook 05). Sign-in is rate limited to 20 per minute per IP; each script signs in a handful of times in `setup()`, so wait a minute between runs.

### A throwaway stack for the scripts that create rows

The stack's `docker-compose.yml` pins `container_name` on the services, so a second project cannot start from it unchanged. Use a copy without those lines, a filled copy of `.env` that is never committed, other host ports, and its own project name (so `down -v` only touches its own volume):

```bash
grep -v container_name docker-compose.yml > /tmp/fugue-perf-compose.yml
cp .env /tmp/fugue-perf.env
API_PORT=5181 WEB_PORT=8181 POSTGRES_PORT=5481 docker compose -p fugue-perf -f /tmp/fugue-perf-compose.yml \
  --project-directory . --env-file /tmp/fugue-perf.env up -d --build api     # postgres + migrate + API (seeded); the web console is not needed
# ... run the scripts with BASE_URL=http://localhost:5181 ...
docker compose -p fugue-perf -f /tmp/fugue-perf-compose.yml --project-directory . --env-file /tmp/fugue-perf.env down -v
```

Never run `down -v` without `-p fugue-perf`: that would delete the default project's database volume.

## Thresholds

| Script | Threshold (a run fails when crossed) |
|---|---|
| queue | `http_req_failed` rate < 1 %; checks > 99 %; p95 of the queue read < 200 ms; p95 of the claim read < 100 ms |
| submit | `http_req_failed` rate < 1 %; checks > 99 %; p95 submit < 250 ms; p95 replay < 100 ms |
| race | double wins = 0; races without a winner = 0; `http_req_failed` = 0 % (only 200, 409 and 412 count as expected); checks = 100 %; p95 pickup < 1000 ms |

The latency limits are about ten times the p95 measured on the recorded run below: they catch a regression, not a hardware difference. They were set after the first run, not derived from a requirement.

## Recorded run (2026-11-27, one machine, one run each)

Apple silicon laptop, k6 v2.4.0, API and Postgres 17 in Docker (OrbStack), everything on localhost, other agents' containers running at the same time. The queue run used the default stack on :5081 (a build the lead runs; its commit is not recorded here); submit and race ran on a throwaway stack built from this branch (source identical to `b875f49` for `src/`) with a freshly seeded database. All thresholds passed.

| Script | Requests | Rate | p95 | Failed | Checks |
|---|---|---|---|---|---|
| queue (45 s, up to 20 VUs) | 6 913 (3 456 iterations) | 153 req/s | queue read 21.7 ms, claim read 15.5 ms | 0.00 % | 10 369 of 10 369 |
| submit (30 s, 10 VUs) | 16 157 (13 466 iterations) | 537 req/s | submit 26.9 ms, replay 10.1 ms | 0.00 % | 48 469 of 48 469 |
| race (50 races, 5 VUs) | 153 (3 sign-ins, 50 submits, 100 pickups) | 171 req/s | pickup 30.1 ms | 0.00 % | 103 of 103 |

Race outcome: 50 races, 50 winners (staff 32, support 18), 0 double wins, 0 races without a winner; all 50 losers were answered `412` (`claim.version_mismatch`: both officers sent the version they read, and the loser's version was stale by the time it was checked), none `409`. Submit: every replay returned the original claim flagged `Idempotent-Replayed: true`.

What this does and does not show: correctness under concurrent writes for the pickup (the optimistic version check held 50 times out of 50) and for idempotent replay, and that these endpoints answer in tens of milliseconds at this load on a laptop. It is not a capacity test: the load is small, the client and server share one machine, and there is one run each, so no variance is reported.
