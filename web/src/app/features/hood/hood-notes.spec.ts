import { Route } from '@angular/router';
import { routes } from '../../app.routes';
import { HoodNote } from './hood-note';

function find(path: string): Route | undefined {
  return routes.flatMap((r) => r.children ?? []).find((r) => r.path === path);
}

describe('hood notes registered by route data', () => {
  const pages = ['', 'claims/mine', 'claims/new', 'claims/queue', 'claims/team', 'claims/:id', 'audit', 'access'];

  it.each(pages)('route "%s" carries a well-formed note', (path) => {
    const note = find(path)?.data?.['hood'] as HoodNote | undefined;
    expect(note).toBeDefined();
    expect(note!.why).toHaveLength(2);
    expect(note!.decisions.length).toBeGreaterThanOrEqual(2);
    expect(note!.decisions.length).toBeLessThanOrEqual(4);
    expect(note!.guards.length).toBeGreaterThan(0);
    for (const d of note!.decisions) expect(d.path).toMatch(/^docs\/[\w./-]+(#[\w-]+)?$/);
  });

  it('pages without a note (customers, modules) show only the live calls', () => {
    expect(find('customers')?.data?.['hood']).toBeUndefined();
    expect(find('modules')?.data?.['hood']).toBeUndefined();
  });
});
