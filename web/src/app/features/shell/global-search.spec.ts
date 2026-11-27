import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AuditApi } from '../../api/audit.api';
import { ClaimsApi } from '../../api/claims.api';
import type { AuditSearchResultDto, ClaimSearchResultDto } from '../../api/generated/model';
import { AccessService } from '../../core/auth/access.service';
import { ApiProblem } from '../../core/http/problem';
import { GlobalSearch } from './global-search';
import { isSearchOff, searchVisible, toRows } from './global-search-logic';

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
    expect(input.placeholder).toBe('Search claims: number, policy, place');
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
