import pkg from '../../../../package.json';
import { STACK } from './stack-info';

describe('stack info', () => {
  const find = (name: string) => STACK.flatMap((g) => g.items).find((i) => i.name === name);

  it('names the Angular major that package.json pins', () => {
    const major = /(\d+)\./.exec(pkg.dependencies['@angular/core'].replace(/^[^\d]*/, ''))?.[1];
    expect(find('Angular')?.version).toBe(major);
    expect(find('Angular CDK')?.version).toBe(major);
  });

  it('keeps the other web majors in step with package.json', () => {
    expect(pkg.dependencies['@tanstack/angular-query']).toMatch(/^\^?5\./);
    expect(find('TanStack Query')?.version).toBe('5');
    expect(pkg.dependencies['exceljs']).toMatch(/^4\.4/);
    expect(find('exceljs')?.version).toBe('4.4');
    expect(pkg.dependencies['@univerjs/presets']).toMatch(/^1\.6/);
    expect(pkg.devDependencies['vitest']).toMatch(/^\^?5\./);
  });

  it('lists every group with at least one item and no duplicates', () => {
    for (const g of STACK) expect(g.items.length).toBeGreaterThan(0);
    const names = STACK.flatMap((g) => g.items.map((i) => i.name));
    expect(new Set(names).size).toBe(names.length);
  });

  it('shows Kafka as an optional adapter that is off, not as a running broker', () => {
    const note = find('Confluent.Kafka')?.note ?? '';
    expect(note).toContain('OFF');
    expect(note).not.toContain('events on Redpanda');
  });

  it('states the real data-access and mapping split, and the in-house and quality sections', () => {
    expect(find('Dapper')?.note).toContain('writes');
    expect(find('EF Core + Npgsql')?.note).toContain('reads');
    expect(find('Mapping')?.note).toContain('no mapper library');
    expect(find('FluentResults')).toBeDefined();
    expect(find('Http.Resilience (Polly v8)')?.note).toContain('outbox is the retry authority');
    expect(STACK.map((g) => g.title)).toEqual(expect.arrayContaining(['Built in-house', 'Quality and delivery']));
    expect(pkg.dependencies['driver.js']).toMatch(/^1\.14/);
    expect(find('driver.js')?.version).toBe('1.14');
  });
});
