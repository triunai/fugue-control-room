import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { QueryClient, provideTanStackQuery } from '@tanstack/angular-query';
import { ClaimsApi } from '../../api/claims.api';
import type { ClaimSearchResultDto } from '../../api/generated/model';
import { ApiProblem } from '../../core/http/problem';
import { CLAIM_SEARCH_DEBOUNCE_MS } from './claim-search';
import { MyClaimsPage } from './my-claims-page';

const ROW = {
  id: 'c1',
  number: 'CLM-SG-2026-000001',
  policyNumber: 'POL-LIST',
  line: 'Motor',
  market: 'SG',
  status: 'Submitted',
  submittedAt: '2026-11-20T10:00:00Z',
  updatedAt: '2026-11-20T10:00:00Z',
};

const HIT: ClaimSearchResultDto = {
  estimatedTotalHits: 1,
  hits: [
    {
      id: 'c2',
      number: 'CLM-SG-2026-000009',
      policyNumber: 'POL-HIT',
      line: 'Property',
      market: 'MY',
      status: 'UnderReview',
      currency: 'MYR',
      incidentDescription: 'Burst pipe',
      incidentLocation: 'Bangsar',
      submittedAt: '2026-11-21T10:00:00Z',
    },
  ],
};

function setup(search: (q: string) => Promise<ClaimSearchResultDto>) {
  const list = vi.fn(() => Promise.resolve({ items: [ROW], nextCursor: null }));
  const searchSpy = vi.fn(search);
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      provideTanStackQuery(() => new QueryClient()),
      { provide: ClaimsApi, useValue: { list, search: searchSpy } },
    ],
  });
  const fixture = TestBed.createComponent(MyClaimsPage);
  const root = fixture.nativeElement as HTMLElement;
  const type = (text: string) => {
    const input = root.querySelector<HTMLInputElement>('input[type="search"]')!;
    input.value = text;
    input.dispatchEvent(new Event('input'));
  };
  return { fixture, root, list, searchSpy, type };
}

async function settle(fixture: { whenStable(): Promise<unknown>; detectChanges(): void }) {
  fixture.detectChanges();
  await fixture.whenStable();
  fixture.detectChanges();
}

const pause = (ms: number) => new Promise((r) => setTimeout(r, ms));

describe('Claim search box', () => {
  it('debounces typing into one search call and shows the hits instead of the list', async () => {
    const { fixture, root, searchSpy, type } = setup(() => Promise.resolve(HIT));
    await settle(fixture);
    expect(root.textContent).toContain('POL-LIST');

    for (const text of ['b', 'bu', 'bur', 'burst']) type(text);
    await settle(fixture);
    expect(searchSpy).not.toHaveBeenCalled();

    await pause(CLAIM_SEARCH_DEBOUNCE_MS + 80);
    await settle(fixture);
    await settle(fixture);
    expect(searchSpy).toHaveBeenCalledTimes(1);
    expect(searchSpy).toHaveBeenCalledWith('burst');
    const hits = root.querySelector('[data-testid="search-hits"]')!;
    expect(hits.textContent).toContain('POL-HIT');
    expect(hits.textContent).toContain('Bangsar');
    expect(root.textContent).not.toContain('POL-LIST');
  });

  it('falls back silently to the normal list with a one-line note on 503 search.unavailable', async () => {
    const problem = new ApiProblem(503, 'Search unavailable', 'search.unavailable', null, {}, null, null, null, 'GET', '/claims/search');
    const { fixture, root, searchSpy, type } = setup(() => Promise.reject(problem));
    await settle(fixture);

    type('burst');
    await pause(CLAIM_SEARCH_DEBOUNCE_MS + 80);
    await settle(fixture);
    await settle(fixture);
    expect(searchSpy).toHaveBeenCalled();
    expect(root.querySelector('[data-testid="search-unavailable"]')).not.toBeNull();
    expect(root.querySelector('[data-testid="search-hits"]')).toBeNull();
    expect(root.textContent).toContain('POL-LIST');
  });
});
