// Load: a mixed staff session (closed model with think time). 0 -> 50 VUs over 2 min, hold 3 min, ramp down 30 s.
import { sleep } from 'k6';
import { ENDPOINTS, SUMMARY_STATS, hit, setupData, summaryHandler, thresholdsFor } from '../lib.js';

const names = Object.keys(ENDPOINTS);
const SCALE = Number(__ENV.SCALE || 1); // SCALE=0.1 shortens every stage for a dry run
const secs = (s) => `${Math.max(1, Math.round(s * SCALE))}s`;
export const options = {
  scenarios: {
    load: {
      executor: 'ramping-vus',
      startVUs: 0,
      stages: [
        { duration: secs(120), target: 50 },
        { duration: secs(180), target: 50 },
        { duration: secs(30), target: 0 },
      ],
      gracefulRampDown: '10s',
    },
  },
  thresholds: thresholdsFor(names),
  summaryTrendStats: SUMMARY_STATS,
};
export const setup = setupData;
export default function (data) {
  hit(data, 'me');
  hit(data, 'claims_list');
  hit(data, 'claim_detail');
  hit(data, 'claims_counts');
  hit(data, 'claims_search');
  sleep(0.5 + Math.random()); // think time
}
export const handleSummary = summaryHandler('load', names);
