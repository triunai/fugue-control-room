import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AuditApi } from '../../api/audit.api';
import { ClaimsApi } from '../../api/claims.api';
import type { AuditSearchResultDto, ClaimSearchResultDto } from '../../api/generated/model';
import { AccessService } from '../../core/auth/access.service';
import { ApiProblem } from '../../core/http/problem';
import { GlobalSearch } from './global-search';
import { facetSummary, isSearchOff, searchVisible, toClaimRows, toRows } from './global-search-logic';

const hit = (i: number, over: object = {}) => ({
  id: `h${i}`,
  action: 'customer.update',
  occurredAt: '2026-11-27T00:00:00Z',
  outcome: 'Success',
  ...over,
});

describe('global search', () => {
  it('shows with CLAIMS_READ or AUDIT_READ', () => {
    expect(searchVisible((c) => c === 'AUDIT_READ')).toBe(true);
    expect(searchVisible((c) => c === 'CLAIMS_READ')).toBe(true);
    expect(searchVisible(() => false)).toBe(false);
  });

  it('maps at most 8 hits, with <em> highlights as segments and the entity to link to', () => {
    const result = {
      estimatedTotalHits: 40,
      hits: Array.from({ length: 12 }, (_, i) =>
        i === 0
          ? hit(0, {
              highlights: { action: 'customer.<em>upd</em>ate' },
              entityType: 'Customer',
              entityId: 'c1',
              actorDisplay: 'Dev Admin',
            })
          : hit(i),
      ),
    } as unknown as AuditSearchResultDto;
    const rows = toRows(result);
    expect(rows).toHaveLength(8);
    expect(rows[0].action).toEqual([
      { text: 'customer.', em: false },
      { text: 'upd', em: true },
      { text: 'ate', em: false },
    ]);
    expect(rows[0]).toMatchObject({ entityType: 'Customer', entityId: 'c1', actor: 'Dev Admin' });
    expect(rows[1]).toMatchObject({ actor: 'system', entityId: '' });
  });

  it('treats a 503 as "Search is off"', () => {
    expect(
      isSearchOff(
        new ApiProblem(503, 'Unavailable', 'search.unavailable', null, {}, null, null, null, 'GET', '/audit/search'),
      ),
    ).toBe(true);
    expect(isSearchOff(new Error('x'))).toBe(false);
  });
});

const CLAIMS: ClaimSearchResultDto = {
  estimatedTotalHits: 1,
  hits: [
    {
      id: 'c9',
      number: 'CLM-SG-2026-000009',
      policyNumber: 'POL-9',
      line: 'Motor',
      market: 'SG',
      status: 'UnderReview',
      currency: 'SGD',
      incidentDescription: 'Rear-ended at the lights',
      incidentLocation: 'Orchard Road',
      submittedAt: '2026-11-21T10:00:00Z',
    },
  ],
};
const AUDIT = { estimatedTotalHits: 1, hits: [hit(1)] } as unknown as AuditSearchResultDto;
const OFF = () =>
  Promise.reject(new ApiProblem(503, 'Unavailable', 'search.unavailable', null, {}, null, null, null, 'GET', '/x'));

async function typeAndSearch(perms: string[], claims: () => Promise<unknown>, audit: () => Promise<unknown>) {
  const claimSearch = vi.fn(claims);
  const auditSearch = vi.fn(audit);
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      { provide: AccessService, useValue: { has: (c: string) => perms.includes(c) } },
      { provide: ClaimsApi, useValue: { search: claimSearch } },
      { provide: AuditApi, useValue: { search: auditSearch } },
    ],
  });
  const fixture = TestBed.createComponent(GlobalSearch);
  fixture.detectChanges();
  const root = fixture.nativeElement as HTMLElement;
  const input = root.querySelector<HTMLInputElement>('input')!;
  input.value = 'orchard';
  input.dispatchEvent(new Event('input'));
  input.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter' }));
  await fixture.whenStable();
  fixture.detectChanges();
  return { root, input, claimSearch, auditSearch };
}

describe('global search dropdown', () => {
  it('a claimant sees only the Claims group and never calls the audit API', async () => {
    const { root, input, claimSearch, auditSearch } = await typeAndSearch(
      ['CLAIMS_READ'],
      () => Promise.resolve(CLAIMS),
      () => Promise.resolve(AUDIT),
    );
    expect(input.placeholder).toBe('Try: SG motor, CLM-SG-…');
    expect(claimSearch).toHaveBeenCalledWith('orchard', 8);
    expect(auditSearch).not.toHaveBeenCalled();
    const menu = root.querySelector('[data-testid="global-search-menu"]')!;
    expect(menu.textContent).toContain('Claims');
    expect(menu.textContent).not.toContain('Audit');
    expect(menu.querySelector('[data-testid="claim-hit"]')!.textContent).toContain('CLM-SG-2026-000009');
  });

  it('a caller with both permissions sees Claims first and Audit below', async () => {
    const { root } = await typeAndSearch(
      ['CLAIMS_READ', 'AUDIT_READ'],
      () => Promise.resolve(CLAIMS),
      () => Promise.resolve(AUDIT),
    );
    const heads = Array.from(root.querySelectorAll('.gs-head')).map((h) => h.textContent!.trim());
    expect(heads).toEqual(['Claims', 'Audit']);
    expect(root.textContent).toContain('customer.update');
  });

  it('a 503 on claims shows "Search is unavailable" in the dropdown', async () => {
    const { root } = await typeAndSearch(['CLAIMS_READ'], OFF, () => Promise.resolve(AUDIT));
    expect(root.querySelector('[data-testid="claims-search-off"]')!.textContent).toContain('Search is unavailable');
  });
});

const HIGHLIGHTED: ClaimSearchResultDto = {
  estimatedTotalHits: 3,
  facets: { market: { SG: 2, MY: 1 }, line: { Motor: 3 }, status: { UnderReview: 2, Submitted: 1, Settled: 0 } },
  hits: [
    {
      ...CLAIMS.hits[0],
      claimantName: 'Sam <b>Lee</b>',
      highlights: {
        number: 'CLM-SG-2026-<em>000009</em>',
        line: '<em>Motor</em>',
        incidentDescription: '…<em>Rear</em>-ended at the lights',
        marketName: '<em>Singapore</em>',
        claimantName: 'Sam <b><em>Lee</em></b>',
      },
    },
  ],
};

describe('claim hits say why they matched', () => {
  it('marks the matched words in the number, meta and description, and lists matches in hidden fields', () => {
    const [row] = toClaimRows(HIGHLIGHTED);
    expect(row.number).toEqual([
      { text: 'CLM-SG-2026-', em: false },
      { text: '000009', em: true },
    ]);
    expect(row.meta).toEqual([
      { text: 'SG', em: false },
      { text: ' · ', em: false },
      { text: 'Motor', em: true },
      { text: ' · ', em: false },
      { text: 'Orchard Road', em: false },
    ]);
    expect(row.description[1]).toEqual({ text: 'Rear', em: true });
    expect(row.matched.map((m) => m.label)).toEqual(['Claimant', 'Market']);
    // Anything but <em> stays literal text (Angular escapes it), so a name can never inject markup.
    expect(row.matched[0].text).toEqual([
      { text: 'Sam <b>', em: false },
      { text: 'Lee', em: true },
      { text: '</b>', em: false },
    ]);
  });

  it('without highlights a row shows the plain values and no "matched on" line', () => {
    const [row] = toClaimRows(CLAIMS);
    expect(row.number).toEqual([{ text: 'CLM-SG-2026-000009', em: false }]);
    expect(row.description).toEqual([{ text: 'Rear-ended at the lights', em: false }]);
    expect(row.matched).toEqual([]);
  });

  it('summarises the facet counts, largest first, with status labels and zero counts dropped', () => {
    expect(facetSummary(HIGHLIGHTED)).toBe('Market SG 2, MY 1 · Line Motor 3 · Status Under review 2, Submitted 1');
    expect(facetSummary(CLAIMS)).toBe('');
  });

  it('the dropdown renders <em> marks, the "matched on" line and the counts', async () => {
    const { root } = await typeAndSearch(
      ['CLAIMS_READ'],
      () => Promise.resolve(HIGHLIGHTED),
      () => Promise.resolve(AUDIT),
    );
    const hitEl = root.querySelector('[data-testid="claim-hit"]')!;
    expect(Array.from(hitEl.querySelectorAll('em')).map((e) => e.textContent)).toEqual([
      '000009',
      'Motor',
      'Rear',
      'Lee',
      'Singapore',
    ]);
    expect(hitEl.querySelector('b')).toBeNull();
    // No padding around the marks: the number reads exactly as stored.
    expect(hitEl.querySelector('.gs-action fg-gs-hl')!.textContent).toBe('CLM-SG-2026-000009');
    expect(root.querySelector('[data-testid="claim-why"]')!.textContent).toContain('Market:');
    expect(root.querySelector('[data-testid="claim-facets"]')!.textContent).toContain('3 matches · Market SG 2, MY 1');
  });
});

describe('global search first-use explainer', () => {
  const KEY = 'fugue.search.intro';
  function mount() {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: AccessService, useValue: { has: (c: string) => c === 'CLAIMS_READ' } },
        { provide: ClaimsApi, useValue: { search: vi.fn(() => Promise.resolve(CLAIMS)) } },
        { provide: AuditApi, useValue: { search: vi.fn() } },
      ],
    });
    const fixture = TestBed.createComponent(GlobalSearch);
    fixture.detectChanges();
    const root = fixture.nativeElement as HTMLElement;
    return { fixture, root, input: root.querySelector<HTMLInputElement>('input')! };
  }
  const intro = (root: HTMLElement) => root.querySelector('[data-testid="global-search-intro"]');

  beforeEach(() => localStorage.removeItem(KEY));

  it('shows on first focus with both Meilisearch links, says what it is and is not, then stays dismissed', () => {
    const { fixture, root, input } = mount();
    input.dispatchEvent(new Event('focus'));
    fixture.detectChanges();
    const box = intro(root)!;
    const hrefs = Array.from(box.querySelectorAll('a')).map((a) => [a.getAttribute('href'), a.getAttribute('rel')]);
    expect(hrefs).toEqual([
      ['https://www.meilisearch.com/', 'noopener'],
      ['https://www.meilisearch.com/docs/getting_started/sdks/dotnet', 'noopener'],
    ]);
    expect(box.textContent).toContain('keyword search, not AI or semantic');
    expect(box.textContent).not.toMatch(/hybrid/i);

    root.querySelector<HTMLButtonElement>('[data-testid="global-search-intro-dismiss"]')!.click();
    fixture.detectChanges();
    expect(intro(root)).toBeNull();
    expect(localStorage.getItem(KEY)).toBe('1');

    // A later visit (new component, same browser) does not show it again; the "?" reopens it.
    TestBed.resetTestingModule();
    const again = mount();
    again.input.dispatchEvent(new Event('focus'));
    again.fixture.detectChanges();
    expect(intro(again.root)).toBeNull();
    again.root.querySelector<HTMLButtonElement>('[data-testid="global-search-help"]')!.click();
    again.fixture.detectChanges();
    expect(intro(again.root)).not.toBeNull();
  });

  it('never blocks typing: typing closes it', () => {
    const { fixture, root, input } = mount();
    input.dispatchEvent(new Event('focus'));
    fixture.detectChanges();
    expect(intro(root)).not.toBeNull();
    input.value = 'sg';
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    expect(intro(root)).toBeNull();
  });

  it('"/" and Ctrl+K focus the box; "/" is ignored while typing in another field', () => {
    const { fixture, input } = mount();
    document.body.appendChild(fixture.nativeElement);
    document.dispatchEvent(new KeyboardEvent('keydown', { key: '/', bubbles: true }));
    expect(document.activeElement).toBe(input);
    input.blur();
    const other = document.createElement('input');
    document.body.appendChild(other);
    other.focus();
    other.dispatchEvent(new KeyboardEvent('keydown', { key: '/', bubbles: true }));
    expect(document.activeElement).toBe(other);
    other.dispatchEvent(new KeyboardEvent('keydown', { key: 'k', ctrlKey: true, bubbles: true }));
    expect(document.activeElement).toBe(input);
    other.remove();
  });
});
