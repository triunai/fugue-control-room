// Shared pieces of the Fugue load scenarios (k6 v2, plain ES modules, no remote imports).
import http from 'k6/http';
import { check, fail } from 'k6';
import { renderReport } from './render.mjs';

export const BASE_URL = (__ENV.BASE_URL || 'http://localhost:5431').replace(/\/$/, '');
const DEV_PW = __ENV.PW || 'Fugue-Dev-Passw0rd!'; // secrets-scan:ignore (seeded development password, runbook 05)
const STAFF = __ENV.STAFF || 'staff@fugue.test';
const JSON_HEADERS = { 'Content-Type': 'application/json' };

// Hot read endpoints. `endpoint` is the ROUTE TEMPLATE used as the metric tag (never an id).
export const ENDPOINTS = {
  me: { path: () => '/api/v1/me' },
  claims_list: { path: () => '/api/v1/claims?status=Submitted&limit=50' },
  claims_search: { path: () => '/api/v1/claims/search?q=SG%20motor&limit=20' },
  claims_counts: { path: () => '/api/v1/claims/counts' },
  claim_detail: { path: (d) => `/api/v1/claims/${d.claimId}` },
};

export const LIMITS = { p95: 300, p99: 800, failRate: 0.01 };

/** k6 thresholds: global (run-phase requests) plus one set per endpoint so each gets its own submetrics in the summary. */
export function thresholdsFor(endpointNames) {
  const t = {
    'http_req_duration{phase:run}': [`p(95)<${LIMITS.p95}`, `p(99)<${LIMITS.p99}`],
    'http_req_failed{phase:run}': [`rate<${LIMITS.failRate}`],
    'http_reqs{phase:run}': ['count>=0'],
    checks: ['rate>0.99'],
  };
  for (const n of endpointNames) {
    t[`http_req_duration{endpoint:${n}}`] = [`p(95)<${LIMITS.p95}`, `p(99)<${LIMITS.p99}`];
    t[`http_req_failed{endpoint:${n}}`] = [`rate<${LIMITS.failRate}`];
    t[`http_reqs{endpoint:${n}}`] = ['count>=0'];
  }
  return t;
}

export const SUMMARY_STATS = ['avg', 'min', 'med', 'p(50)', 'p(90)', 'p(95)', 'p(99)', 'max'];

/** Signs in once (the sign-in limiter), warms the JIT with one call per endpoint, and picks one claim id for the detail read. */
export function setupData() {
  const res = http.post(`${BASE_URL}/api/v1/auth/token`, JSON.stringify({ email: STAFF, password: DEV_PW }),
    { headers: JSON_HEADERS, tags: { endpoint: 'setup_login', phase: 'setup' } });
  if (res.status !== 200) fail(`sign-in as ${STAFF} answered ${res.status}: ${res.body}`);
  const token = res.json('accessToken');
  const auth = { Authorization: `Bearer ${token}` };
  const list = http.get(`${BASE_URL}/api/v1/claims?status=Submitted&limit=50`, { headers: auth, tags: { endpoint: 'setup', phase: 'setup' } });
  if (list.status !== 200 || !list.json('0.id')) fail(`claims list answered ${list.status} (is the database seeded?)`);
  const data = { token, claimId: list.json('0.id') };
  for (const e of Object.values(ENDPOINTS)) {
    http.get(`${BASE_URL}${e.path(data)}`, { headers: auth, tags: { endpoint: 'setup', phase: 'setup' } });
  }
  return data;
}

/** One tagged GET with a status check. `extraTags` e.g. { burst: 'first' }. */
export function hit(data, name, extraTags = {}) {
  const res = http.get(`${BASE_URL}${ENDPOINTS[name].path(data)}`, {
    headers: { Authorization: `Bearer ${data.token}` },
    tags: { endpoint: name, phase: 'run', ...extraTags },
  });
  check(res, { [`${name} 200`]: (r) => r.status === 200 }, { endpoint: name });
  return res;
}

const val = (m, key) => (m && (m.values ? m.values[key] : m[key]));
const okOf = (t) => (t && typeof t === 'object' ? !!t.ok : !t);

function stats(m) {
  return { min: val(m, 'min'), avg: val(m, 'avg'), p50: val(m, 'p(50)') ?? val(m, 'med'), p90: val(m, 'p(90)'), p95: val(m, 'p(95)'), p99: val(m, 'p(99)'), max: val(m, 'max') };
}

/** data (handleSummary input) -> the summary JSON the reports are built from. */
export function buildSummary(data, scenario, endpointNames) {
  const dur = (data.state?.testRunDurationMs ?? 0) / 1000;
  const M = data.metrics;
  const count = (key) => val(M[`http_reqs{${key}}`], 'count') ?? 0;
  const failRate = (key) => val(M[`http_req_failed{${key}}`], 'rate') ?? 0;
  const thresholds = [];
  for (const [metric, m] of Object.entries(M)) {
    for (const [expr, t] of Object.entries(m.thresholds || {})) thresholds.push({ metric, expr, ok: okOf(t) });
  }
  const endpoints = {};
  for (const n of endpointNames) {
    endpoints[n] = { count: count(`endpoint:${n}`), failRate: failRate(`endpoint:${n}`), ...stats(M[`http_req_duration{endpoint:${n}}`]) };
  }
  const requests = count('phase:run');
  return {
    schema: 1,
    scenario,
    label: __ENV.LABEL || '',
    startedAt: new Date(Date.now() - dur * 1000).toISOString(),
    durationSec: dur,
    target: BASE_URL,
    k6Version: __ENV.K6_VERSION || '',
    env: { note: __ENV.ENV_NOTE || '' },
    limits: LIMITS,
    totals: { requests, rps: dur ? requests / dur : 0, failedRate: failRate('phase:run'), checksRate: val(M.checks, 'rate'), droppedIterations: val(M.dropped_iterations, 'count') ?? 0 },
    thresholds,
    overall: stats(M['http_req_duration{phase:run}']),
    endpoints,
    timeline: null,
  };
}

/** handleSummary factory: writes load/results/<scenario>-<runId>.json and .html (relative to the cwd, which load/run.sh sets to the repo root). */
export function summaryHandler(scenario, endpointNames) {
  return function handleSummary(data) {
    const sum = buildSummary(data, scenario, endpointNames);
    const id = __ENV.RUN_ID || new Date().toISOString().replace(/[-:]/g, '').slice(0, 15);
    const base = `${__ENV.RESULTS_DIR || 'load/results'}/${scenario}-${id}`;
    const lines = [`\n${scenario}: ${sum.totals.requests} requests, ${sum.totals.rps.toFixed(1)} req/s, failed ${(sum.totals.failedRate * 100).toFixed(2)} %`,
      `  overall p50 ${sum.overall.p50?.toFixed(1)} / p90 ${sum.overall.p90?.toFixed(1)} / p95 ${sum.overall.p95?.toFixed(1)} / p99 ${sum.overall.p99?.toFixed(1)} / max ${sum.overall.max?.toFixed(1)} ms`];
    for (const [n, e] of Object.entries(sum.endpoints)) lines.push(`  ${n.padEnd(14)} n=${String(e.count).padEnd(6)} p50 ${e.p50?.toFixed(1)} p95 ${e.p95?.toFixed(1)} p99 ${e.p99?.toFixed(1)} max ${e.max?.toFixed(1)} ms`);
    for (const t of sum.thresholds.filter((x) => !x.ok)) lines.push(`  THRESHOLD FAILED: ${t.metric} ${t.expr}`);
    return {
      [`${base}.json`]: JSON.stringify(sum, null, 2),
      [`${base}.html`]: renderReport(sum),
      stdout: `${lines.join('\n')}\n`,
    };
  };
}
