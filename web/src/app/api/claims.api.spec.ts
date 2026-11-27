import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ClaimsApi, EMPTY_CLAIM_FILTER } from './claims.api';

function setup() {
  TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
  return { api: TestBed.inject(ClaimsApi), ctl: TestBed.inject(HttpTestingController) };
}

describe('ClaimsApi', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('sends only the filters that are set, and "any" assignee as no filter', async () => {
    const { api, ctl } = setup();
    const page = api.list({ ...EMPTY_CLAIM_FILTER, status: 'Submitted', market: 'SG', q: ' CLM-1 ' }, null, 20);
    const req = ctl.expectOne((r) => r.url.endsWith('/claims'));
    expect(req.request.params.get('status')).toBe('Submitted');
    expect(req.request.params.get('market')).toBe('SG');
    expect(req.request.params.get('q')).toBe('CLM-1');
    expect(req.request.params.has('assigned')).toBe(false);
    expect(req.request.params.has('line')).toBe(false);
    req.flush([]);
    expect((await page).items).toEqual([]);
  });

  it('maps "Assigned to me" and "Unassigned" onto the API values', async () => {
    const { api, ctl } = setup();
    for (const [assigned, wire] of [
      ['me', 'me'],
      ['none', 'none'],
    ] as const) {
      const p = api.list({ ...EMPTY_CLAIM_FILTER, assigned }, null);
      const req = ctl.expectOne((r) => r.url.endsWith('/claims'));
      expect(req.request.params.get('assigned')).toBe(wire);
      req.flush([]);
      await p;
    }
  });

  it('counts per status from one full page each, flagging "more" when a cursor remains', async () => {
    const { api, ctl } = setup();
    const counts = api.counts({ assigned: 'any', market: '', line: '', q: '' });
    const reqs = ctl.match((r) => r.url.endsWith('/claims'));
    expect(reqs.map((r) => r.request.params.get('status'))).toEqual([
      'Submitted',
      'UnderReview',
      'InfoRequested',
      'Approved',
      'Rejected',
      'Settled',
    ]);
    for (const r of reqs) expect(r.request.params.get('limit')).toBe('100');
    const row = { id: 'x', version: 1 };
    reqs[0].flush([row, row]);
    reqs[1].flush([row], { headers: { 'X-Next-Cursor': 'abc' } });
    for (const r of reqs.slice(2)) r.flush([]);
    const out = await counts;
    expect(out.Submitted).toEqual({ n: 2, more: false });
    expect(out.UnderReview).toEqual({ n: 1, more: true });
    expect(out.Settled).toEqual({ n: 0, more: false });
  });
});
