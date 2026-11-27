import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import { AccessService } from '../../core/auth/access.service';
import {
  PAGE_TOURS,
  Persona,
  TourPage,
  TourStep,
  personaOf,
  stepsFor,
  tourSeenKey,
  tourSuppressed,
} from './tour-steps';

/** The key of the persona tour (the default of `start`). */
export const PERSONA_TOUR = 'persona';

const CLAIM_URL = /^\/claims\/[0-9a-f]{8}-[0-9a-f-]{27}$/i;

function seen(persona: Persona): boolean {
  try {
    return localStorage.getItem(tourSeenKey(persona)) === '1';
  } catch {
    return true;
  }
}

function markSeen(persona: Persona): void {
  try {
    localStorage.setItem(tourSeenKey(persona), '1');
  } catch {
    /* best effort */
  }
}

function waitFor(selector: string, ms = 4000): Promise<void> {
  return new Promise((resolve) => {
    const start = Date.now();
    const tick = () => {
      if (document.querySelector(selector) || Date.now() - start > ms) resolve();
      else setTimeout(tick, 100);
    };
    tick();
  });
}

/**
 * The reviewer tour (driver.js, loaded on first use). It walks the pages that matter for the signed-in persona,
 * navigating between them. Shown once per persona on first sign-in and from the account menu any time.
 */
@Injectable({ providedIn: 'root' })
export class TourService {
  private readonly access = inject(AccessService);
  private readonly router = inject(Router);
  private running = false;

  constructor() {
    // Hook for pages that cannot import this service (lane nth-claimui looks it up dynamically): window.startTour(key).
    (window as unknown as { startTour?: (key: string) => Promise<void> }).startTour = (key) => this.start(key);
  }

  persona(): Persona {
    return personaOf((code) => this.access.has(code));
  }

  /** First sign-in: start the persona's tour once. Personas without a tour of their own (member) are not nagged. */
  maybeAutoStart(): void {
    const persona = this.persona();
    if (persona === 'member' || seen(persona) || this.running || tourSuppressed()) return;
    markSeen(persona);
    setTimeout(() => void this.start(PERSONA_TOUR), 800);
  }

  /**
   * Start a tour: `PERSONA_TOUR` (the default) walks the signed-in persona's pages; any key of `PAGE_TOURS`
   * ('landing', 'queue', 'claim-detail', 'team-exposure') explains the page on screen. Steps whose target is not on
   * the page are skipped; an unknown key or a tour with nothing to show does nothing.
   */
  async start(key: string = PERSONA_TOUR): Promise<void> {
    if (this.running) return;
    const persona = this.persona();
    const all = key === PERSONA_TOUR ? stepsFor(persona) : (PAGE_TOURS[key] ?? []);
    if (all.length === 0) return;
    this.running = true;
    if (key === PERSONA_TOUR) markSeen(persona);
    try {
      const { driver } = await import('driver.js');
      // A page tour keeps only the steps whose target is on screen now; the persona tour checks as it goes.
      const steps = key === PERSONA_TOUR ? all : all.filter((s) => document.querySelector(s.selector));
      if (steps.length === 0) {
        this.running = false;
        return;
      }
      const reach = async (from: number, dir: 1 | -1): Promise<number> => {
        for (let i = from + dir; i >= 0 && i < steps.length; i += dir) {
          await this.goTo(steps[i]);
          if (document.querySelector(steps[i].selector)) return i;
        }
        return -1;
      };
      if (key === PERSONA_TOUR) await this.goTo(steps[0]);
      const d = driver({
        showProgress: true,
        allowClose: true,
        overlayOpacity: 0.45,
        popoverClass: 'fg-tour',
        nextBtnText: 'Next',
        prevBtnText: 'Back',
        doneBtnText: 'Done',
        onDestroyed: () => (this.running = false),
        steps: steps.map((s, i) => ({
          element: s.selector,
          popover: {
            title: s.title,
            description: s.description,
            onNextClick: async () => {
              const n = await reach(i, 1);
              if (n < 0) return d.destroy();
              d.moveTo(n);
            },
            onPrevClick: async () => {
              const n = await reach(i, -1);
              if (n >= 0) d.moveTo(n);
            },
          },
        })),
      });
      d.drive();
    } catch {
      this.running = false;
    }
  }

  /** Bring the page a step needs on screen and wait for its element. */
  private async goTo(step: TourStep): Promise<void> {
    const page = step.page;
    if (page) await this.openPage(page);
    await waitFor(step.selector);
  }

  private async openPage(page: TourPage): Promise<void> {
    const path = this.router.url.split(/[?#]/)[0];
    if ('path' in page) {
      if (path !== page.path) await this.router.navigateByUrl(page.path);
      return;
    }
    if (CLAIM_URL.test(path)) return;
    await this.router.navigateByUrl(page.claimFrom);
    await waitFor('main a[href^="/claims/"]');
    const links = [...document.querySelectorAll<HTMLAnchorElement>('main a[href^="/claims/"]')].filter((a) =>
      CLAIM_URL.test(a.getAttribute('href') ?? ''),
    );
    const pick = links.find((a) => page.prefer?.test(a.closest('tr, li, article')?.textContent ?? '')) ?? links[0];
    if (pick) await this.router.navigateByUrl(pick.getAttribute('href') as string);
  }
}
