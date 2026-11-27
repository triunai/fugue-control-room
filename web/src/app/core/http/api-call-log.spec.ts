import { HttpClient, HttpHeaders, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { QueryClient, provideTanStackQuery } from '@tanstack/angular-query';
import { firstValueFrom } from 'rxjs';
import { SessionStore } from '../auth/session.store';
import { ApiCallLog, MAX_CALLS, shortenPath } from './api-call-log';
import { EtagStore } from './etag.store';
import { consoleInterceptors } from './interceptors';

const ID = '3f2b1c9e-7a4d-4e1b-9c3a-0d5e6f7a8b9c';

function setup() {
  sessionStorage.clear();
  TestBed.configureTestingModule({
    providers: [
      provideRouter([{ path: '**', children: [] }]),
      provideHttpClient(withInterceptors(consoleInterceptors)),
      provideHttpClientTesting(),
      provideTanStackQuery(() => new QueryClient()),
    ],
  });
  TestBed.inject(SessionStore).signedIn('secret-access-token', 900, 'secret-refresh');
  return {
    http: TestBed.inject(HttpClient),
    ctl: TestBed.inject(HttpTestingController),
    log: TestBed.inject(ApiCallLog),
    etags: TestBed.inject(EtagStore),
  };
}

describe('shortenPath', () => {
  it('replaces ids, drops the base and keeps only query parameter names', () => {
    expect(shortenPath(`/api/v1/claims/${ID}/info-requests/42/response`, '/api/v1')).toBe(
      '/claims/{id}/info-requests/{id}/response',
    );
    expect(shortenPath('/api/v1/claims?status=Open&q=jane%40x.test&limit=20', '/api/v1')).toBe(
      '/claims?status&q&limit',
    );
    expect(shortenPath('/api/v1/audit', '/api/v1', ['entityId'])).toBe('/audit?entityId');
  });
});

describe('callLogInterceptor', () => {
  it('records method, shortened path, status and ms', async () => {
    const { http, ctl, log } = setup();
    const done = firstValueFrom(http.get(`/api/v1/claims/${ID}`));
    ctl.expectOne(`/api/v1/claims/${ID}`).flush({ id: ID, reserveMinor: 5000, claimant: 'Jane Doe' });
    await done;
    const [c] = log.calls();
    expect(c).toMatchObject({ method: 'GET', path: '/claims/{id}', status: 200, ifMatch: false, code: null });
    expect(c.ms).toBeGreaterThanOrEqual(0);
  });

  it('never stores query values, bodies, tokens or any header value (only three yes/no markers)', async () => {
    const { http, ctl, log } = setup();
    const done = firstValueFrom(
      http.post(
        `/api/v1/claims?email=jane@x.test`,
        { incidentDescription: 'private words' },
        { headers: { 'Idempotency-Key': 'k-123' } },
      ),
    );
    ctl
      .expectOne((r) => r.url.startsWith('/api/v1/claims'))
      .flush({ id: ID }, { status: 201, statusText: 'Created', headers: new HttpHeaders({ ETag: '"77"' }) });
    await done;
    const [c] = log.calls();
    expect(c).toMatchObject({ method: 'POST', path: '/claims?email', status: 201, idempotencyKey: true, etag: true });
    const dump = JSON.stringify(c);
    for (const secret of ['jane', 'private words', 'secret-access-token', 'k-123', '"77"', ID]) {
      expect(dump).not.toContain(secret);
    }
    expect(Object.keys(c).sort()).toEqual(
      ['code', 'etag', 'idempotencyKey', 'ifMatch', 'method', 'ms', 'path', 'seq', 'status'].sort(),
    );
  });

  it('marks If-Match when the ETag store supplied one, and keeps the problem code on a 412', async () => {
    const { http, ctl, log, etags } = setup();
    etags.set(`/api/v1/claims/${ID}`, '"5"');
    const done = firstValueFrom(http.post(`/api/v1/claims/${ID}/pickup`, null)).catch(() => null);
    ctl
      .expectOne(`/api/v1/claims/${ID}/pickup`)
      .flush({ code: 'claim.version_mismatch', detail: 'x' }, { status: 412, statusText: 'Precondition Failed' });
    await done;
    expect(log.calls()[0]).toMatchObject({
      method: 'POST',
      path: '/claims/{id}/pickup',
      status: 412,
      ifMatch: true,
      code: 'claim.version_mismatch',
    });
  });

  it('shows no code on other statuses, ignores non-API URLs, keeps the newest 20 first', async () => {
    const { http, ctl, log } = setup();
    const bad = firstValueFrom(http.get('/api/v1/claims/x')).catch(() => null);
    ctl.expectOne('/api/v1/claims/x').flush({ code: 'claim.not_found' }, { status: 404, statusText: 'Not Found' });
    await bad;
    expect(log.calls()[0].code).toBeNull();

    const other = firstValueFrom(http.get('https://elsewhere.test/a'));
    ctl.expectOne('https://elsewhere.test/a').flush({});
    await other;
    expect(log.count()).toBe(1);

    for (let i = 0; i < MAX_CALLS + 5; i++) log.record({ ...log.calls()[0], path: `/p${i}` });
    expect(log.count()).toBe(MAX_CALLS);
    expect(log.calls()[0].path).toBe(`/p${MAX_CALLS + 4}`);
    log.clear();
    expect(log.count()).toBe(0);
  });
});
