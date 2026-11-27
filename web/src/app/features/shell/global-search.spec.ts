import type { AuditSearchResultDto } from '../../api/generated/model';
import { ApiProblem } from '../../core/http/problem';
import { isSearchOff, searchVisible, toRows } from './global-search-logic';

const hit = (i: number, over: object = {}) => ({
  id: `h${i}`,
  action: 'customer.update',
  occurredAt: '2026-11-27T00:00:00Z',
  outcome: 'Success',
  ...over,
});

describe('global search', () => {
  it('shows only with AUDIT_READ', () => {
    expect(searchVisible((c) => c === 'AUDIT_READ')).toBe(true);
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
