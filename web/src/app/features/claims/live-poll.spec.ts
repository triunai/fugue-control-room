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
