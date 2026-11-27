import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { Dialog } from '@angular/cdk/dialog';
import { TestBed } from '@angular/core/testing';
import { QueryClient, provideTanStackQuery } from '@tanstack/angular-query';
import { POLL_MS, livePoll } from './live-poll';

describe('livePoll', () => {
  const setup = (open: unknown[], mutating: number) => {
    TestBed.configureTestingModule({
      providers: [
        provideTanStackQuery(() => {
          const c = new QueryClient();
          c.isMutating = () => mutating;
          return c;
        }),
        { provide: Dialog, useValue: { openDialogs: open } },
      ],
    });
  };

  it('polls every 15 s and refetches on focus when nothing is in progress', () => {
    setup([], 0);
    const o = TestBed.runInInjectionContext(() => livePoll());
    expect(o.refetchInterval()).toBe(POLL_MS);
    expect(o.refetchOnWindowFocus()).toBe(true);
  });

  it('pauses while a dialog is open', () => {
    setup([{}], 0);
    const o = TestBed.runInInjectionContext(() => livePoll());
    expect(o.refetchInterval()).toBe(false);
    expect(o.refetchOnWindowFocus()).toBe(false);
  });

  it('pauses while a mutation runs, and while the caller is editing', () => {
    setup([], 1);
    expect(TestBed.runInInjectionContext(() => livePoll()).refetchInterval()).toBe(false);
    TestBed.resetTestingModule();
    setup([], 0);
    expect(TestBed.runInInjectionContext(() => livePoll(() => true)).refetchInterval()).toBe(false);
  });
});

describe('screens that show live claim numbers poll', () => {
  // Review catch 2026-11-27: Team and exposure (the manager's "real-time picture") and the persona landings never
  // refetched, so "as of just now" froze. Every page that reads claim lists, workload or exposure uses livePoll.
  const pages = [
    'src/app/features/claims/claims-queue-page.ts',
    'src/app/features/claims/my-claims-page.ts',
    'src/app/features/claims/claim-detail-page.ts',
    'src/app/features/claims/team-page.ts',
    'src/app/features/home/home-page.ts',
  ];
  it.each(pages)('%s spreads livePoll into its queries', (path) => {
    const src = readFileSync(resolve(process.cwd(), path), 'utf8');
    expect(src).toContain('livePoll(');
    const queries = (src.match(/injectQuery\(/g) ?? []).length;
    const live = (src.match(/\.\.\.this\.live,/g) ?? []).length;
    expect({ path, live: live >= queries }).toEqual({ path, live: true });
  });
});
