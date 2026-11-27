import { TestBed } from '@angular/core/testing';
import { QueryClient, provideTanStackQuery } from '@tanstack/angular-query';
import { AuditApi, EMPTY_AUDIT_FILTER } from '../../api/audit.api';
import type { AuditSearchResultDto } from '../../api/generated/model';
import { ApiProblem } from '../../core/http/problem';
import { AuditFilterStore } from './audit-filter.store';
import { AuditPage } from './audit-page';

const ENTRY = {
  id: 'e1',
  occurredAt: '2026-11-27T10:00:00Z',
  action: 'customers.update',
  actorType: 'user',
  actorDisplay: 'Dev Admin',
  outcome: 'success',
};

const HOSTILE: AuditSearchResultDto = {
  estimatedTotalHits: 42,
  hits: [
    {
      id: 'h1',
      action: 'customers.create',
      actorDisplay: 'Dev Admin',
      outcome: 'failure',
      occurredAt: '2026-11-27T10:00:00Z',
      errorCode: 'email_taken',
      highlights: { errorCode: '<img src=x onerror=alert(1)><em>email</em>_taken<script>alert(2)</script>' },
    },
  ],
};

function setup(search: () => Promise<AuditSearchResultDto>) {
  const list = vi.fn(() => Promise.resolve({ items: [ENTRY], nextCursor: null }));
  TestBed.configureTestingModule({
    providers: [provideTanStackQuery(() => new QueryClient()), { provide: AuditApi, useValue: { list, search } }],
  });
  TestBed.inject(AuditFilterStore).apply({ ...EMPTY_AUDIT_FILTER, q: 'email' });
  const fixture = TestBed.createComponent(AuditPage);
  return { fixture, list, root: fixture.nativeElement as HTMLElement };
}

async function settle(fixture: { whenStable(): Promise<unknown>; detectChanges(): void }) {
  fixture.detectChanges();
  await fixture.whenStable();
  fixture.detectChanges();
}

describe('Audit search', () => {
  it('renders hits newest first with the total, and a hostile highlight never becomes markup', async () => {
    const { fixture, root, list } = setup(() => Promise.resolve(HOSTILE));
    await settle(fixture);
    const hits = root.querySelector('[data-testid="search-hits"]')!;
    expect(hits.querySelectorAll('em').length).toBe(1);
    expect(hits.querySelector('em')!.textContent).toBe('email');
    expect(hits.querySelector('img, script')).toBeNull();
    expect(hits.textContent).toContain('<img src=x onerror=alert(1)>');
    expect(hits.textContent).toContain('<script>alert(2)</script>');
    expect(root.querySelector('[data-testid="search-total"]')!.textContent).toContain('42');
    expect(list).not.toHaveBeenCalled();
  });

  it('falls back to the filtered log with a one-line note on 503 search.unavailable', async () => {
    const problem = new ApiProblem(
      503,
      'Search unavailable',
      'search.unavailable',
      null,
      {},
      null,
      null,
      null,
      'GET',
      '/audit/search',
    );
    const { fixture, root, list } = setup(() => Promise.reject(problem));
    await settle(fixture);
    await settle(fixture);
    expect(root.querySelector('[data-testid="search-unavailable"]')).not.toBeNull();
    expect(list).toHaveBeenCalled();
    expect(root.textContent).toContain('customers.update');
    expect(root.querySelector('[data-testid="search-hits"]')).toBeNull();
  });
});
