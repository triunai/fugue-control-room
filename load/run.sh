#!/usr/bin/env bash
# usage: load/run.sh <smoke|load|stampede> [label]
# Runs one scenario against $BASE_URL (default http://localhost:5431), writes load/results/<scenario>-<runid>.{json,html}
# with the latency-over-time series added. LABEL names the run in compare.html ("before cache", "after cache").
set -uo pipefail
cd "$(dirname "$0")/.."
scenario="${1:?usage: load/run.sh <smoke|load|stampede> [label]}"
export LABEL="${2:-${LABEL:-}}" RUN_ID="${RUN_ID:-$(date +%Y%m%dT%H%M%S)}" K6_VERSION="$(k6 version | awk '{print $2}')"
export ENV_NOTE="${ENV_NOTE:-Load profile: sign-in limiter raised, API + Postgres + k6 on one laptop.}"
mkdir -p load/results
points="$(mktemp -t k6points).ndjson"
k6 run --out "json=$points" "load/scenarios/$scenario.js"
rc=$?
json="load/results/$scenario-$RUN_ID.json"
[ -f "$json" ] && node load/report.mjs "$json" "$points"
rm -f "$points"
echo "report: load/results/$scenario-$RUN_ID.html (k6 exit $rc)"
exit $rc
