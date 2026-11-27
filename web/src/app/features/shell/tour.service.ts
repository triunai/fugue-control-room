import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import { AccessService } from '../../core/auth/access.service';
import { Persona, TourPage, TourStep, personaOf, stepsFor, tourSeenKey } from './tour-steps';

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

  persona(): Persona {
    return personaOf((code) => this.access.has(code));
  }

  /** First sign-in: start the persona's tour once. Personas without a tour of their own (member) are not nagged. */
  maybeAutoStart(): void {
    const persona = this.persona();
    if (persona === 'member' || seen(persona) || this.running) return;
    markSeen(persona);
    setTimeout(() => void this.start(), 800);
  }

  async start(): Promise<void> {
    if (this.running) return;
    this.running = true;
    const persona = this.persona();
    const steps = stepsFor(persona);
    markSeen(persona);
    try {
      const { driver } = await import('driver.js');
      await this.goTo(steps[0]);
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
              if (i + 1 >= steps.length) return d.destroy();
              await this.goTo(steps[i + 1]);
              d.moveNext();
            },
            onPrevClick: async () => {
              await this.goTo(steps[i - 1]);
              d.movePrevious();
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
