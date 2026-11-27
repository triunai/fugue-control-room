import { existsSync, readFileSync } from 'node:fs';
import { resolve } from 'node:path';
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
    expect(note!.how.length).toBeGreaterThanOrEqual(2);
    expect(note!.how.length).toBeLessThanOrEqual(4);
    for (const line of note!.how) expect(line.length).toBeLessThanOrEqual(160);
    expect(note!.decisions.length).toBeGreaterThanOrEqual(2);
    expect(note!.decisions.length).toBeLessThanOrEqual(4);
    expect(note!.guards.length).toBeGreaterThan(0);
    for (const d of note!.decisions) expect(d.path).toMatch(/^docs\/[\w./-]+(#[\w-]+)?$/);
  });

  // The guard excerpts must be real: the file exists in the repository and every excerpt line is a line of it (indentation trimmed).
  const repo = resolve(process.cwd(), '..');
  it.each(pages)('route "%s": every guard excerpt is copied from a real file', (path) => {
    const note = find(path)?.data?.['hood'] as HoodNote;
    for (const g of note.guards) {
      expect(g.path).toMatch(/^(src|docs)\//);
      expect(g.code.trim().length).toBeGreaterThan(0);
      const lines = g.code.split('\n');
      expect(lines.length).toBeGreaterThanOrEqual(2);
      expect(lines.length).toBeLessThanOrEqual(8);
      const file = resolve(repo, g.path);
      expect(existsSync(file)).toBe(true);
      const real = new Set(readFileSync(file, 'utf8').split('\n').map((l) => l.trim()));
      for (const l of lines.map((x) => x.trim()).filter((x) => x && x !== '…')) {
        expect({ path: g.path, line: l, found: real.has(l) }).toEqual({ path: g.path, line: l, found: true });
      }
    }
  });

  it('pages without a note (customers, modules) show only the live calls', () => {
    expect(find('customers')?.data?.['hood']).toBeUndefined();
    expect(find('modules')?.data?.['hood']).toBeUndefined();
  });
});
