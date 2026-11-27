import { existsSync, readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { Route } from '@angular/router';
import { routes } from '../../app.routes';
import { HoodNote } from '../hood/hood-note';

function find(path: string): Route | undefined {
  return routes.flatMap((r) => r.children ?? []).find((r) => r.path === path);
}

/** The claims-area pages added after the first note set: sheet, calendar and assessment. */
describe('claims-area hood notes (sheet, calendar, assessment)', () => {
  const pages = ['claims/sheet', 'claims/calendar', 'claims/:id/assessment'];
  const repo = resolve(process.cwd(), '..');

  it.each(pages)('route "%s" carries a well-formed note', (path) => {
    const note = find(path)?.data?.['hood'] as HoodNote | undefined;
    expect(note).toBeDefined();
    expect(note!.why).toHaveLength(2);
    expect(note!.how.length).toBeGreaterThanOrEqual(2);
    expect(note!.how.length).toBeLessThanOrEqual(4);
    expect(note!.decisions.length).toBeGreaterThanOrEqual(2);
    expect(note!.decisions.length).toBeLessThanOrEqual(4);
    expect(note!.adrs.length).toBeGreaterThan(0);
    for (const a of note!.adrs) expect(existsSync(resolve(repo, a.path))).toBe(true);
  });

  it.each(pages)('route "%s": every test is a real test in a real file', (path) => {
    const note = find(path)?.data?.['hood'] as HoodNote;
    expect(note.tests.length).toBeGreaterThanOrEqual(3);
    expect(note.tests.length).toBeLessThanOrEqual(5);
    for (const x of note.tests) {
      expect(x.asserts.length).toBeLessThanOrEqual(140);
      const file = resolve(repo, x.path);
      expect(existsSync(file)).toBe(true);
      expect({ path: x.path, name: x.name, found: readFileSync(file, 'utf8').includes(x.name) }).toEqual({
        path: x.path,
        name: x.name,
        found: true,
      });
    }
  });

  it.each(pages)('route "%s": every guard excerpt is copied from a real file', (path) => {
    const note = find(path)?.data?.['hood'] as HoodNote;
    expect(note.guards.length).toBeGreaterThan(0);
    for (const g of note.guards) {
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
});
