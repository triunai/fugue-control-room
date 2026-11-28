// Stampede: 200 VUs start at the same instant (per-vu-iterations: all VUs begin together) and hit ONE hot read key,
// three waves one after another: GET /me, the claims list for a staff token, claims search "SG motor".
// Every VU uses the SAME token and URL, so a cache would see 200 concurrent requests for one key. The first request of each
// VU is tagged burst:first (the simultaneous one); the following four are burst:follow. No think time.
import { SUMMARY_STATS, hit, setupData, summaryHandler, thresholdsFor } from '../lib.js';

const VUS = Number(__ENV.VUS || 200);
const waves = [['me', '0s'], ['claims_list', '12s'], ['claims_search', '24s']];
const names = waves.map(([n]) => n);
const scenarios = {};
for (const [n, startTime] of waves) {
  scenarios[`stampede_${n}`] = { executor: 'per-vu-iterations', vus: VUS, iterations: 5, maxDuration: '10s', startTime, exec: n };
}
export const options = { scenarios, thresholds: thresholdsFor(names), summaryTrendStats: SUMMARY_STATS };
export const setup = setupData;
const wave = (name) => (data) => hit(data, name, { burst: __ITER === 0 ? 'first' : 'follow' });
export const me = wave('me');
export const claims_list = wave('claims_list');
export const claims_search = wave('claims_search');
export const handleSummary = summaryHandler('stampede', names);
