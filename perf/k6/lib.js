// Shared helpers for the Fugue k6 scripts. Plain k6 JavaScript: no remote imports, so a run needs nothing but the k6 binary.
import http from 'k6/http';
import { check, fail } from 'k6';

export const BASE_URL = (__ENV.BASE_URL || 'http://localhost:5081').replace(/\/$/, '');
// The seeded development password (runbook 05). Override with PW=... for any other deployment.
const DEV_PW = __ENV.PW || 'Fugue-Dev-Passw0rd!'; // secrets-scan:ignore

export const JSON_HEADERS = { 'Content-Type': 'application/json' };

/** Signs in and returns the access token; stops the test run when sign-in fails (the sign-in limiter allows 20 per minute per IP). */
export function login(email) {
  const res = http.post(
    `${BASE_URL}/api/v1/auth/token`,
    JSON.stringify({ email, password: DEV_PW }), // secrets-scan:ignore (a variable, not a value)
    { headers: JSON_HEADERS, tags: { name: 'login' } },
  );
  if (!check(res, { [`signed in as ${email}`]: (r) => r.status === 200 })) {
    fail(`sign-in as ${email} answered ${res.status}: ${res.body}`);
  }
  return res.json('accessToken');
}

export function bearer(token, extra = {}) {
  return { Authorization: `Bearer ${token}`, ...extra };
}

/** A key of the bare-token form the API accepts: letters, digits, '.', '_', ':', '-' (1 to 255 characters). */
export function idempotencyKey(prefix) {
  return `${prefix}-${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 12)}`;
}

/** The body of a valid claim (the incident date must not be in the future or older than a year). */
export function claimBody(label) {
  const yesterday = new Date(Date.now() - 86400000).toISOString().slice(0, 10);
  return JSON.stringify({
    market: 'SG',
    line: 'Motor',
    policyNumber: `K6-${label}`,
    incidentDate: yesterday,
    incidentDescription: 'Load test: rear-ended at the lights.',
  });
}

/** Submits a claim as the claimant; returns { id, etag } or stops the run. */
export function submitClaim(token, label) {
  const res = http.post(`${BASE_URL}/api/v1/claims`, claimBody(label), {
    headers: bearer(token, { ...JSON_HEADERS, 'Idempotency-Key': idempotencyKey('k6-setup') }),
    tags: { name: 'submit-setup' },
  });
  if (res.status !== 201) {
    fail(`submit answered ${res.status}: ${res.body}`);
  }
  return { id: res.json('id'), etag: res.headers['Etag'] };
}
