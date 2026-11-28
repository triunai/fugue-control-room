// Smoke: 1 VU, 30 s, every hot read once a second. Proves the script, the sign-in and the report work.
import { sleep } from 'k6';
import { ENDPOINTS, SUMMARY_STATS, hit, setupData, summaryHandler, thresholdsFor } from '../lib.js';

const names = Object.keys(ENDPOINTS);
export const options = {
  scenarios: { smoke: { executor: 'constant-vus', vus: 1, duration: '30s' } },
  thresholds: thresholdsFor(names),
  summaryTrendStats: SUMMARY_STATS,
};
export const setup = setupData;
export default function (data) {
  for (const n of names) hit(data, n);
  sleep(1);
}
export const handleSummary = summaryHandler('smoke', names);
