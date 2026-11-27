// Submit claims with an Idempotency-Key. Every 5th iteration replays the same key and body and expects the SAME claim back, flagged
// Idempotent-Replayed. CREATES ROWS: run it against a throwaway stack (perf/k6/README.md).
//   BASE_URL=http://localhost:5181 k6 run perf/k6/claims-submit.js
import http from 'k6/http';
import { check } from 'k6';
import { BASE_URL, JSON_HEADERS, bearer, claimBody, idempotencyKey, login } from './lib.js';

export const options = {
  stages: [
    { duration: '5s', target: 10 },
    { duration: '20s', target: 10 },
    { duration: '5s', target: 0 },
  ],
  thresholds: {
    http_req_failed: ['rate<0.01'],
    checks: ['rate>0.99'],
    'http_req_duration{name:submit}': ['p(95)<250'],
    'http_req_duration{name:replay}': ['p(95)<100'],
  },
};

export function setup() {
  return { token: login('self@fugue.test') };
}

let iteration = 0;

export default function (data) {
  iteration += 1;
  const key = idempotencyKey('k6-submit');
  const body = claimBody(`${__VU}-${iteration}-${Date.now()}`);
  const params = (name) => ({ headers: bearer(data.token, { ...JSON_HEADERS, 'Idempotency-Key': key }), tags: { name } });

  const created = http.post(`${BASE_URL}/api/v1/claims`, body, params('submit'));
  const ok = check(created, {
    'submit is 201': (r) => r.status === 201,
    'submit has Location and ETag': (r) => !!r.headers['Location'] && !!r.headers['Etag'],
    'a fresh submit is not a replay': (r) => !r.headers['Idempotent-Replayed'],
  });

  if (ok && iteration % 5 === 0) {
    const replay = http.post(`${BASE_URL}/api/v1/claims`, body, params('replay'));
    check(replay, {
      'replay is 201': (r) => r.status === 201,
      'replay is flagged Idempotent-Replayed': (r) => r.headers['Idempotent-Replayed'] === 'true',
      'replay returns the same claim': (r) => r.json('id') === created.json('id'),
    });
  }
}
