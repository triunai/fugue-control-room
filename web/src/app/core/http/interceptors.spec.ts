import { HttpClient, HttpContext, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { QueryClient, provideTanStackQuery } from '@tanstack/angular-query';
import { firstValueFrom } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { SessionStore } from '../auth/session.store';
import { SEND_IF_MATCH, TRACK_ETAG } from './context';
import { CorrelationService } from './correlation.service';
import { EtagStore, resourceKey } from './etag.store';
import { consoleInterceptors } from './interceptors';
import { ApiProblem, camelKey, parseRetryAfter } from './problem';

const C = '/api/v1/customers/c1';

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
  const session = TestBed.inject(SessionStore);
  session.signedIn('access-1', 900, 'refresh-1');
  return {
    http: TestBed.inject(HttpClient),
    ctl: TestBed.inject(HttpTestingController),
    etags: TestBed.inject(EtagStore),
    session,
  };
}

describe('resourceKey', () => {
  it('strips origin, query and state-transition suffixes', () => {
    expect(resourceKey('http://localhost:4200/api/v1/customers/c1?x=1')).toBe(C);
    expect(resourceKey(`${C}/suspend`)).toBe(C);
    expect(resourceKey('/api/v1/webhooks/subscriptions/s1/rotate-secret')).toBe('/api/v1/webhooks/subscriptions/s1');
  });

  it('maps every claim command back to the claim', () => {
    const claim = '/api/v1/claims/k1';
    for (const action of ['pickup', 'reserve', 'decision', 'settle', 'info-requests', 'info-requests/r1/response']) {
      expect(resourceKey(`${claim}/${action}`)).toBe(claim);
    }
    expect(resourceKey(`${claim}/timeline`)).toBe(`${claim}/timeline`);
  });
});

describe('ETag interceptor', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('remembers the ETag of a GET and sends it as If-Match on the next PUT', async () => {
    const { http, ctl } = setup();
    const get = firstValueFrom(http.get(C));
    ctl.expectOne(C).flush({}, { headers: { ETag: '"7"' } });
    await get;

    const put = firstValueFrom(http.put(C, {}));
    const req = ctl.expectOne(C);
    expect(req.request.headers.get('If-Match')).toBe('"7"');
    req.flush(null, { status: 204, statusText: 'No Content', headers: { ETag: '"8"' } });
    await put;

    const again = firstValueFrom(http.delete(C));
    const del = ctl.expectOne(C);
    expect(del.request.headers.get('If-Match')).toBe('"8"');
    del.flush(null, { status: 204, statusText: 'No Content' });
    await again;
  });

  it('keys a 201 by its Location and uses the resource tag for suspend', async () => {
    const { http, ctl } = setup();
    const post = firstValueFrom(
      http.post('/api/v1/customers', {}, { context: new HttpContext().set(SEND_IF_MATCH, false) }),
    );
    const create = ctl.expectOne('/api/v1/customers');
    expect(create.request.headers.has('If-Match')).toBe(false);
    create.flush(
      {},
      { status: 201, statusText: 'Created', headers: { ETag: '"1"', Location: `http://localhost:5081${C}` } },
    );
    await post;

    const suspend = firstValueFrom(http.post(`${C}/suspend`, null));
    const req = ctl.expectOne(`${C}/suspend`);
    expect(req.request.headers.get('If-Match')).toBe('"1"');
    req.flush(null, { status: 204, statusText: 'No Content' });
    await suspend;
  });

  it('sends the claim tag on every claim command and stores the new tag against the claim', async () => {
    const { http, ctl, etags } = setup();
    const K = '/api/v1/claims/k1';
    etags.set(K, '"5"');
    const sent: string[] = [];
    const commands: [string, 'POST' | 'PUT'][] = [
      ['pickup', 'POST'],
      ['reserve', 'PUT'],
      ['info-requests', 'POST'],
      ['info-requests/r1/response', 'POST'],
      ['decision', 'POST'],
      ['settle', 'POST'],
    ];
    for (const [action, method] of commands) {
      const call = firstValueFrom(http.request(method, `${K}/${action}`, { body: {} }));
      const req = ctl.expectOne(`${K}/${action}`);
      sent.push(req.request.headers.get('If-Match') ?? 'MISSING');
      req.flush({}, { headers: { ETag: `"${6 + sent.length}"` } });
      await call;
    }
    expect(sent).toEqual(['"5"', '"7"', '"8"', '"9"', '"10"', '"11"']);
    expect(etags.get(K)).toBe('"12"');
  });

  it('does not record a peek (TRACK_ETAG=false), so a stale edit still fails with 412', async () => {
    const { http, ctl, etags } = setup();
    etags.set(C, '"3"');
    const peek = firstValueFrom(http.get(C, { context: new HttpContext().set(TRACK_ETAG, false) }));
    ctl.expectOne(C).flush({}, { headers: { ETag: '"4"' } });
    await peek;
    expect(etags.get(C)).toBe('"3"');
  });

  it('forgets the tag after a successful DELETE', async () => {
    const { http, ctl, etags } = setup();
    etags.seed(C, 9);
    const del = firstValueFrom(http.delete(C));
    const req = ctl.expectOne(C);
    expect(req.request.headers.get('If-Match')).toBe('"9"');
    req.flush(null, { status: 204, statusText: 'No Content' });
    await del;
    expect(etags.get(C)).toBeUndefined();
  });
});

describe('problem interceptor', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('maps a validation problem to camelCase field errors with code and correlation ref', async () => {
    const { http, ctl } = setup();
    const call = firstValueFrom(http.put(C, {}));
    const req = ctl.expectOne(C);
    req.flush(
      {
        title: 'Validation failed',
        status: 400,
        code: 'validation.failed',
        correlationId: 'abc123',
        errors: { Email: ['Email is not valid.'], 'EventTypes[0]': ['Unknown event type.'] },
      },
      { status: 400, statusText: 'Bad Request' },
    );
    const err = await call.catch((e: unknown) => e);
    expect(err).toBeInstanceOf(ApiProblem);
    const p = err as ApiProblem;
    expect(p.isValidation).toBe(true);
    expect(p.errors).toEqual({ email: ['Email is not valid.'], eventTypes: ['Unknown event type.'] });
    expect(p.code).toBe('validation.failed');
    expect(p.correlationId).toBe('abc123');
  });

  it('says "Not found" for a 404 whatever the server title, and reads Retry-After on 429', async () => {
    const { http, ctl } = setup();
    const missing = firstValueFrom(http.get(C)).then(
      () => null as unknown as ApiProblem,
      (e: unknown) => e as ApiProblem,
    );
    ctl
      .expectOne(C)
      .flush({ title: 'Forbidden-ish', code: 'customer.not_found' }, { status: 404, statusText: 'Not Found' });
    expect((await missing).title).toBe('Not found');

    const limited = firstValueFrom(http.get(C)).then(
      () => null as unknown as ApiProblem,
      (e: unknown) => e as ApiProblem,
    );
    ctl
      .expectOne(C)
      .flush({ title: 'Too many' }, { status: 429, statusText: 'Too Many', headers: { 'Retry-After': '12' } });
    expect((await limited).retryAfterSeconds).toBe(12);
  });

  it('falls back to the request correlation id when the body has none', async () => {
    const { http, ctl } = setup();
    const id = TestBed.inject(CorrelationService).begin();
    const call = firstValueFrom(http.get(C)).then(
      () => null as unknown as ApiProblem,
      (e: unknown) => e as ApiProblem,
    );
    const req = ctl.expectOne(C);
    expect(req.request.headers.get('X-Correlation-Id')).toBe(id);
    req.flush('boom', { status: 500, statusText: 'Server Error' });
    expect((await call).correlationId).toBe(id);
  });
});

describe('problem helpers', () => {
  it('parses Retry-After and camelCases keys', () => {
    expect(parseRetryAfter('5')).toBe(5);
    expect(parseRetryAfter(new Date(Date.now() + 3000).toUTCString())).toBeGreaterThanOrEqual(2);
    expect(parseRetryAfter(null)).toBeNull();
    expect(camelKey('Address.Street')).toBe('address.street');
  });
});

describe('auth interceptor: single-flight refresh', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('never sends the bearer token, correlation id or If-Match to another origin', async () => {
    const { http, ctl, etags } = setup();
    etags.set('https://elsewhere.example/api/v1/customers/c1', '"1"');
    const call = firstValueFrom(http.put('https://elsewhere.example/api/v1/customers/c1', {}));
    const req = ctl.expectOne('https://elsewhere.example/api/v1/customers/c1');
    expect(req.request.headers.has('Authorization')).toBe(false);
    expect(req.request.headers.has('X-Correlation-Id')).toBe(false);
    expect(req.request.headers.has('If-Match')).toBe(false);
    req.flush(null, { status: 204, statusText: 'No Content' });
    await call;
  });

  it('refreshes ONCE for concurrent 401s and replays every request with the new token', async () => {
    const { http, ctl, session } = setup();
    const a = firstValueFrom(http.get('/api/v1/customers/a'));
    const b = firstValueFrom(http.get('/api/v1/customers/b'));

    const first = ctl.expectOne('/api/v1/customers/a');
    const second = ctl.expectOne('/api/v1/customers/b');
    expect(first.request.headers.get('Authorization')).toBe('Bearer access-1');
    first.flush({ code: 'auth.unauthenticated' }, { status: 401, statusText: 'Unauthorized' });
    second.flush({ code: 'auth.unauthenticated' }, { status: 401, statusText: 'Unauthorized' });

    const refresh = ctl.match('/api/v1/auth/refresh');
    expect(refresh.length).toBe(1);
    expect(refresh[0].request.body).toEqual({ refreshToken: 'refresh-1' });
    expect(refresh[0].request.headers.has('Authorization')).toBe(false);
    refresh[0].flush({ accessToken: 'access-2', tokenType: 'Bearer', expiresIn: 900, refreshToken: 'refresh-2' });
    await Promise.resolve();
    await new Promise((r) => setTimeout(r));

    const replayA = ctl.expectOne('/api/v1/customers/a');
    const replayB = ctl.expectOne('/api/v1/customers/b');
    expect(replayA.request.headers.get('Authorization')).toBe('Bearer access-2');
    expect(replayB.request.headers.get('Authorization')).toBe('Bearer access-2');
    replayA.flush({ id: 'a' });
    replayB.flush({ id: 'b' });
    await expect(a).resolves.toEqual({ id: 'a' });
    await expect(b).resolves.toEqual({ id: 'b' });
    expect(session.refreshToken()).toBe('refresh-2');
    ctl.match('/api/v1/me').forEach((r) => r.flush({}));
  });

  it('does not refresh twice when the replay is also refused; the session ends', async () => {
    const { http, ctl, session } = setup();
    const call = firstValueFrom(http.get('/api/v1/customers/a')).then(
      () => null as unknown as ApiProblem,
      (e: unknown) => e as ApiProblem,
    );
    ctl.expectOne('/api/v1/customers/a').flush({}, { status: 401, statusText: 'Unauthorized' });
    ctl
      .expectOne('/api/v1/auth/refresh')
      .flush({ accessToken: 'access-2', tokenType: 'Bearer', expiresIn: 900, refreshToken: 'refresh-2' });
    await new Promise((r) => setTimeout(r));
    ctl.expectOne('/api/v1/customers/a').flush({}, { status: 401, statusText: 'Unauthorized' });
    expect((await call).status).toBe(401);
    expect(ctl.match('/api/v1/auth/refresh').length).toBe(0);
    expect(session.isAuthenticated()).toBe(false);
    ctl.match('/api/v1/me').forEach((r) => r.flush({}));
  });

  it('keeps the refresh token when the refresh fails for a transient reason (503)', async () => {
    const { http, ctl, session } = setup();
    const call = firstValueFrom(http.get('/api/v1/customers/a')).then(
      () => null as unknown as ApiProblem,
      (e: unknown) => e as ApiProblem,
    );
    ctl.expectOne('/api/v1/customers/a').flush({}, { status: 401, statusText: 'Unauthorized' });
    ctl.expectOne('/api/v1/auth/refresh').flush({ title: 'Busy' }, { status: 503, statusText: 'Unavailable' });
    expect((await call).status).toBe(503);
    expect(session.refreshToken()).toBe('refresh-1');
    expect(session.isAuthenticated()).toBe(true);
  });

  it('does not sign the user back in when sign-out happens during an in-flight refresh', async () => {
    const { http, ctl, session } = setup();
    const call = firstValueFrom(http.get('/api/v1/customers/a')).catch(() => undefined);
    ctl.expectOne('/api/v1/customers/a').flush({}, { status: 401, statusText: 'Unauthorized' });
    const refresh = ctl.expectOne('/api/v1/auth/refresh');

    const signOut = TestBed.inject(AuthService).signOut();
    ctl.expectOne('/api/v1/auth/logout').flush(null, { status: 204, statusText: 'No Content' });
    await signOut;

    refresh.flush({ accessToken: 'access-2', tokenType: 'Bearer', expiresIn: 900, refreshToken: 'refresh-2' });
    await call;
    expect(session.isAuthenticated()).toBe(false);
    expect(sessionStorage.getItem('fugue.console.refresh')).toBeNull();
    ctl.match(() => true).forEach((r) => r.flush({}));
  });
});
