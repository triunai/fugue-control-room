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
      page: { path: '/claims/exposure' },
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
      page: { path: '/claims/workload' },
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
  ],
  member: [],
};

/** The steps for a persona, always opening on the permission-driven navigation. */
export function stepsFor(persona: Persona): TourStep[] {
  return [NAV_STEP, ...STEPS[persona]];
}

/** localStorage key remembering that a persona's tour was shown (finished or dismissed). */
export const tourSeenKey = (persona: Persona): string => `fugue.tour.seen.${persona}`;
