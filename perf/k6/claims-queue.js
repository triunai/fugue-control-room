// Read-only: the staff work queue (oldest Submitted claims first), then one claim's detail. Creates nothing, so it is safe against the
// shared compose stack:  k6 run perf/k6/claims-queue.js
import http from 'k6/http';
import { check, sleep } from 'k6';
import { BASE_URL, bearer, login } from './lib.js';

export const options = {
  stages: [
    { duration: '10s', target: 20 },
    { duration: '30s', target: 20 },
    { duration: '5s', target: 0 },
  ],
  thresholds: {
    http_req_failed: ['rate<0.01'],
    checks: ['rate>0.99'],
    'http_req_duration{name:queue}': ['p(95)<200'],
    'http_req_duration{name:claim}': ['p(95)<100'],
  },
};

export function setup() {
  return { token: login('staff@fugue.test') };
}

export default function (data) {
  const auth = { headers: bearer(data.token) };

  const queue = http.get(`${BASE_URL}/api/v1/claims?status=Submitted&assigned=none&limit=50`, { ...auth, tags: { name: 'queue' } });
  const ok = check(queue, {
    'queue is 200': (r) => r.status === 200,
    'queue is a JSON array': (r) => Array.isArray(r.json()),
  });

  if (ok) {
    const rows = queue.json();
    if (rows.length > 0) {
      const claim = http.get(`${BASE_URL}/api/v1/claims/${rows[Math.floor(Math.random() * rows.length)].id}`, { ...auth, tags: { name: 'claim' } });
      check(claim, { 'claim is 200 with an ETag': (r) => r.status === 200 && !!r.headers['Etag'] });
    }
  }

  sleep(0.2);
}
