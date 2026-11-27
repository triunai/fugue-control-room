// Two officers pick up the SAME Submitted claim at the same moment, both with the version they read. Exactly one must win (200); the other must
// be refused with 409 (claim.invalid_transition) or 412 (claim.version_mismatch), never both 200 and never a 5xx. CREATES ROWS: run it against a
// throwaway stack (perf/k6/README.md).
//   BASE_URL=http://localhost:5181 k6 run perf/k6/claims-pickup-race.js
import http from 'k6/http';
import { check } from 'k6';
import { Counter } from 'k6/metrics';
import exec from 'k6/execution';
import { BASE_URL, bearer, login, submitClaim } from './lib.js';

const RACES = Number(__ENV.RACES || 50);

const doubleWins = new Counter('pickup_double_wins');
const noWinner = new Counter('pickup_no_winner');
const loserRefusals = { 409: new Counter('pickup_loser_409'), 412: new Counter('pickup_loser_412') };
const winsByOfficer = { staff: new Counter('pickup_wins_staff'), support: new Counter('pickup_wins_support') };

// 409 and 412 are the correct answer for the loser, so they are not failures (set per request: a global callback is not applied to http.batch).
const EXPECTED = http.expectedStatuses(200, 409, 412);

export const options = {
  scenarios: {
    race: { executor: 'shared-iterations', vus: 5, iterations: RACES, maxDuration: '2m' },
  },
  thresholds: {
    pickup_double_wins: ['count==0'],
    pickup_no_winner: ['count==0'],
    http_req_failed: ['rate==0'], // anything outside 200/409/412 (a 5xx, a 401, a 404) fails the run
    checks: ['rate==1'],
    'http_req_duration{name:pickup}': ['p(95)<1000'],
  },
};

export function setup() {
  const claimant = login('self@fugue.test');
  const claims = [];
  for (let i = 0; i < RACES; i++) {
    claims.push(submitClaim(claimant, `RACE-${Date.now()}-${i}`));
  }
  return { claims, staff: login('staff@fugue.test'), support: login('support@fugue.test') };
}

export default function (data) {
  const claim = data.claims[exec.scenario.iterationInTest];
  const pickup = (token) => ({
    method: 'POST',
    url: `${BASE_URL}/api/v1/claims/${claim.id}/pickup`,
    body: null,
    params: { headers: bearer(token, { 'If-Match': claim.etag }), tags: { name: 'pickup' }, responseCallback: EXPECTED },
  });

  const [a, b] = http.batch([pickup(data.staff), pickup(data.support)]);
  const statuses = [a.status, b.status];
  const wins = statuses.filter((s) => s === 200).length;

  if (wins > 1) doubleWins.add(1);
  if (wins === 0) noWinner.add(1);
  statuses.forEach((s) => loserRefusals[s] && loserRefusals[s].add(1));
  if (a.status === 200) winsByOfficer.staff.add(1);
  if (b.status === 200) winsByOfficer.support.add(1);

  check(statuses, {
    'exactly one officer wins': () => wins === 1,
    'the loser is refused with 409 or 412': () => statuses.filter((s) => s === 409 || s === 412).length === 1,
  });
}
