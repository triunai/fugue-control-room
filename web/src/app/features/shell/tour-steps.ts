/** The reviewer tour: one short run per persona, chosen from permission codes (never role names). */
export type Persona = 'claimant' | 'officer' | 'manager' | 'admin' | 'member';

/** Where a step lives: a route, or a claim detail opened from a list (the first row whose text matches `prefer`). */
export type TourPage = { path: string } | { claimFrom: string; prefer?: RegExp };

export interface TourStep {
  page?: TourPage;
  /** CSS selector of the element to highlight. A missing element shows the step centred instead. */
  selector: string;
  title: string;
  description: string;
}

/** The broadest persona the permissions allow: admin (modules) over manager (exposure) over officer over claimant. */
export function personaOf(has: (code: string) => boolean): Persona {
  if (has('MODULES_VIEW')) return 'admin';
  if (has('CLAIMS_EXPOSURE')) return 'manager';
  if (has('CLAIMS_HANDLE')) return 'officer';
  if (has('CLAIMS_SUBMIT')) return 'claimant';
  return 'member';
}

const NAV_STEP: TourStep = {
  selector: '[data-tour="nav"]',
  title: 'Built from your permissions',
  description:
    'Every item here is a permission check, never a role name. A claimant, an officer, a manager and an admin get a different console from the same build.',
};

const STEPS: Record<Persona, TourStep[]> = {
  claimant: [
    {
      page: { path: '/claims/new' },
      selector: 'main h1.page-title',
      title: 'Report an incident',
      description:
        'One short form. Submitting creates the claim with its number straight away, and a retried request never files it twice (Idempotency-Key).',
    },
    {
      page: { path: '/claims/mine' },
      selector: 'main h1.page-title',
      title: 'Only your own claims',
      description:
        'The list is scoped on the server: a claim that is not yours answers 404, so claim ids cannot be probed.',
    },
    {
      page: { claimFrom: '/claims/mine', prefer: /info requested/i },
      selector: '[data-testid="timeline"]',
      title: 'The timeline',
      description: 'Every status change in plain words, from submitted to settled.',
    },
    {
      selector: '[data-testid="answer-panel"]',
      title: 'Answer the question',
      description: 'When an officer needs more, the claim waits for you here. Your answer puts it back in review.',
    },
  ],
  officer: [
    {
      page: { path: '/claims/queue' },
      selector: '[data-testid="kpis"]',
      title: 'The queue at a glance',
      description:
        'Counts per status, longest-waiting first. Reserves stay in each claim currency and are never added across currencies.',
    },
    {
      selector: '.tabs',
      title: 'Tabs and filters',
      description: 'A tab per status with live counts, plus assignee, market and line filters.',
    },
    {
      page: { claimFrom: '/claims/queue', prefer: /submitted/i },
      selector: '.actions',
      title: 'Only the actions valid for the status',
      description:
        'A submitted claim can be picked up, an approved one settled. If someone changed the claim since you opened it, the server answers 412 and you see "Changed since you opened it" instead of an overwrite.',
    },
    {
      selector: '[data-testid="handling"]',
      title: 'Handling',
      description: 'Officer, internal reserve and approved amount, each in the claim currency.',
    },
  ],
  manager: [
    {
      page: { path: '/claims/team' },
      selector: '[data-testid="exposure-totals"]',
      title: 'Exposure per currency',
      description: 'One total for each currency, taken from the claims as they stand now.',
    },
    {
      selector: '[data-testid="exposure-summary"]',
      title: 'Why there is no grand total',
      description:
        'Adding PHP to THB needs an exchange rate and a date; without them the sum would be a made-up number. Each currency stays its own line.',
    },
    {
      page: { path: '/claims/team' },
      selector: '[data-testid="workload-kpis"]',
      title: 'Workload',
      description: 'Unassigned claims and what each officer carries, to spot an overloaded queue.',
    },
  ],
  admin: [
    {
      page: { path: '/audit' },
      selector: 'section.timeline.panel',
      title: 'The audit log',
      description:
        'Every write with who, what and the outcome; edits show a field-level diff. Sensitive fields are masked when written.',
    },
    {
      page: { path: '/modules' },
      selector: 'table.table',
      title: 'Module toggles',
      description:
        'Switch a module off and its permissions drop away and its menu items vanish for everyone, with no deploy.',
    },
    {
      page: { path: '/access' },
      selector: 'section.panel',
      title: 'Access',
      description:
        'Users, groups and per-user overrides. Permissions are read on every request, so a change lands on the next click.',
    },
    {
      selector: '[data-testid="roles-matrix"]',
      title: 'Roles and permissions',
      description:
        'A read-only grid of what each role grants, by module. A user holds the union of their roles and groups.',
    },
  ],
  member: [],
};

/** The steps for a persona, always opening on the permission-driven navigation. */
export function stepsFor(persona: Persona): TourStep[] {
  return [NAV_STEP, ...STEPS[persona]];
}

/** localStorage key remembering that a persona's tour was shown (finished or dismissed). */
export const tourSeenKey = (persona: Persona): string => `fugue.tour.seen.${persona}`;

/** Selector helper: a `data-tour` hook (preferred, added by the page) or the existing test id as a fallback. */
const hook = (name: string, testId?: string): string =>
  testId ? `[data-tour="${name}"], [data-testid="${testId}"]` : `[data-tour="${name}"]`;

/**
 * Tours per page key, started by `TourService.start(key)` or the header Help button. They only talk about the page
 * on screen (no navigation). A step whose target is not on the page is skipped, never an error.
 */
export const PAGE_TOURS: Record<string, TourStep[]> = {
  landing: [
    NAV_STEP,
    {
      selector: '[data-tour="help"]',
      title: 'Help is always here',
      description: 'Press Help on any page for a short tour of what is on screen. The account menu replays the full tour.',
    },
  ],
  queue: [
    {
      selector: hook('queue-kpis', 'kpis'),
      title: 'The queue at a glance',
      description: 'Counts per status, longest-waiting first. Reserves stay in each claim currency.',
    },
    {
      selector: '[data-tour="queue-tabs"], .tabs',
      title: 'Tabs and filters',
      description: 'A tab per status with live counts, plus assignee, market and line filters.',
    },
  ],
  'claim-detail': [
    {
      selector: '[data-tour="claim-actions"], .actions',
      title: 'Only the actions valid for the status',
      description: 'A stale tab gets a 412 and the banner "Changed since you opened it" instead of an overwrite.',
    },
    {
      selector: hook('claim-timeline', 'timeline'),
      title: 'The timeline',
      description: 'Every status change in plain words, from submitted to settled.',
    },
    {
      selector: hook('claim-answer', 'answer-panel'),
      title: 'Answer the question',
      description: 'When an officer needs more, the claim waits for the claimant here.',
    },
  ],
  'team-exposure': [
    {
      selector: hook('exposure-totals', 'exposure-totals'),
      title: 'Exposure per currency',
      description: 'One total for each currency, taken from the claims as they stand now.',
    },
    {
      selector: hook('exposure-note', 'exposure-summary'),
      title: 'Why there is no grand total',
      description: 'Adding currencies needs an exchange rate and a date; without them the sum would be made up.',
    },
    {
      selector: hook('team-workload', 'workload-kpis'),
      title: 'Workload',
      description: 'Unassigned claims and what each officer carries.',
    },
  ],
};

/** The tour key for a URL, used by the header Help button; pages without their own tour get the landing one. */
export function tourKeyFor(url: string): string {
  const path = url.split(/[?#]/)[0];
  if (path === '/claims/queue') return 'queue';
  if (/^\/claims\/[0-9a-f]{8}-[0-9a-f-]{27}$/i.test(path)) return 'claim-detail';
  if (['/claims/team', '/claims/exposure', '/claims/workload'].includes(path)) return 'team-exposure';
  return 'landing';
}

const OFF_KEY = 'fugue.tour.off';

/** `?tour=off` on any entry URL silences the auto-start for this browser session (explicit starts still work). */
export function suppressTourFromUrl(): void {
  try {
    if (new URLSearchParams(location.search).get('tour') === 'off') sessionStorage.setItem(OFF_KEY, '1');
  } catch {
    /* best effort */
  }
}

export function tourSuppressed(): boolean {
  try {
    return sessionStorage.getItem(OFF_KEY) === '1';
  } catch {
    return false;
  }
}

/** Per-user "welcome handled" flag (shown, then skipped, taken or finished). */
export const welcomeSeenKey = (userId: string): string => `fugue.welcome.seen.${userId}`;

/** First visit of this user, and nobody asked for quiet (`?tour=off`): show the welcome and autoplay the tour. */
export function shouldAutoplay(state: { suppressed: boolean; seen: boolean; signedIn: boolean }): boolean {
  return state.signedIn && !state.suppressed && !state.seen;
}
