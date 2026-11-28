# Lane nth-k6 status (k6 load, stampede and percentile overlay)

## Commits
- slice 1: scripts (`load/lib.js`, `load/scenarios/*`, `load/render.mjs`, `report.mjs`, `compare.mjs`, `run.sh`, `start-api.sh`), runbook `docs/runbooks/load-testing.md`, README link
- slice 2: recorded runs in `load/results/`, `load/report/compare.html`, this file

## Shared files changed
- `README.md`: one row in the runbook table.

## Deviations
- No `nth-k6` row exists in `docs/branch-ledger.md`, so none was updated (the brief says to touch only your own row).
- Meilisearch runs on :55532 (not in the lane's port list) because claims search needs it.
- `compare.html` overlays two stampede runs (baseline and repeat), not smoke vs load: like-with-like shows run-to-run variance.
