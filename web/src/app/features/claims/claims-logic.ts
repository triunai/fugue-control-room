import { ClaimDto, ClaimStatus, Market } from '../../api/models';

/** Fixed per market by the API (plan decision B). The report form shows it read-only. */
export const MARKETS: readonly { code: Market; name: string; currency: string }[] = [
  { code: 'SG', name: 'Singapore', currency: 'SGD' },
  { code: 'MY', name: 'Malaysia', currency: 'MYR' },
  { code: 'TH', name: 'Thailand', currency: 'THB' },
  { code: 'HK', name: 'Hong Kong', currency: 'HKD' },
  { code: 'ID', name: 'Indonesia', currency: 'IDR' },
  { code: 'PH', name: 'Philippines', currency: 'PHP' },
];

export function currencyOf(market: string): string {
  return MARKETS.find((m) => m.code === market)?.currency ?? '';
}

export function marketName(market: string): string {
  return MARKETS.find((m) => m.code === market)?.name ?? market;
}

/**
 * Money on the wire is bigint minor units. All six currencies are treated as having TWO minor digits (ISO 4217),
 * so 250000 is "2,500.00" everywhere. We do NOT use Intl's own default digits: Intl says IDR has 0, which would
 * silently divide by 100 and round away the decimals the API stores. The currency CODE is always shown (never "$"),
 * because SGD and HKD would otherwise both read as a dollar sign.
 */
export const MINOR_DIGITS = 2;
const MINOR_FACTOR = 10 ** MINOR_DIGITS;

export function formatMoney(minor: number | null | undefined, currency: string): string {
  if (minor === null || minor === undefined) return 'Not set';
  return new Intl.NumberFormat('en', {
    style: 'currency',
    currency,
    currencyDisplay: 'code',
    minimumFractionDigits: MINOR_DIGITS,
    maximumFractionDigits: MINOR_DIGITS,
  }).format(minor / MINOR_FACTOR);
}

/** "2,500.50" or "2500" -> minor units; null when it is not a positive amount with at most two decimals. */
export function parseMoney(text: string): number | null {
  const t = text.trim().replace(/,/g, '');
  if (!/^\d+(\.\d{1,2})?$/.test(t)) return null;
  const [whole, frac = ''] = t.split('.');
  const minor = Number(whole) * MINOR_FACTOR + Number(frac.padEnd(MINOR_DIGITS, '0'));
  return minor > 0 && Number.isSafeInteger(minor) ? minor : null;
}

export const STATUS_LABEL: Record<ClaimStatus, string> = {
  Submitted: 'Submitted',
  UnderReview: 'Under review',
  InfoRequested: 'Info requested',
  Approved: 'Approved',
  Rejected: 'Rejected',
  Settled: 'Settled',
};

export type Tone = 'ok' | 'warn' | 'err' | 'acc' | 'muted';

export const STATUS_TONE: Record<ClaimStatus, Tone> = {
  Submitted: 'muted',
  UnderReview: 'acc',
  InfoRequested: 'warn',
  Approved: 'ok',
  Rejected: 'err',
  Settled: 'ok',
};

/** The claimant's "what happens next" line, in plain language, per status. */
export const WHAT_NEXT: Record<ClaimStatus, string> = {
  Submitted:
    'We have your claim. A claims officer will pick it up and start reviewing it; there is nothing you need to do yet.',
  UnderReview:
    'An officer is reviewing your claim. We will ask you here if we need anything more, and you will see the decision on this page.',
  InfoRequested:
    'We need a little more information before we can decide. Please answer the question below; the review resumes as soon as you do.',
  Approved: 'Your claim is approved. Payment is being arranged, and this page will show when it is settled.',
  Rejected:
    'Your claim was not approved. The reason is shown below; contact us quoting your claim number if you want to discuss it.',
  Settled: 'Your claim is settled and closed. Nothing more is needed from you.',
};

export type ClaimAction = 'pickup' | 'reserve' | 'requestInfo' | 'approve' | 'reject' | 'settle';

export interface ActionState {
  action: ClaimAction;
  enabled: boolean;
  /** Why it is disabled (shown as the button's hint). */
  hint?: string;
}

export interface Actor {
  userId: string | undefined;
  permissions: ReadonlySet<string>;
}

/**
 * The actions staff may take on THIS claim in THIS status. It mirrors the API's guards (the API still enforces them):
 * the aggregate's state machine, `CLAIMS_HANDLE` for working a claim, `CLAIMS_SETTLE` for settling, and the rule that only
 * the assigned officer, or a caller who also holds `CLAIMS_SETTLE` (the manager), works an assigned claim.
 */
export function availableActions(
  claim: Pick<ClaimDto, 'status' | 'assignedOfficerId' | 'reserveMinor'>,
  actor: Actor,
): ActionState[] {
  const can = (p: string) => actor.permissions.has(p);
  const handles = can('CLAIMS_HANDLE');
  const mine = !claim.assignedOfficerId || claim.assignedOfficerId === actor.userId || can('CLAIMS_SETTLE');
  const out: ActionState[] = [];
  switch (claim.status) {
    case 'Submitted':
      if (handles) out.push({ action: 'pickup', enabled: true });
      break;
    case 'UnderReview':
      if (handles && mine) {
        const noReserve = claim.reserveMinor === null || claim.reserveMinor === undefined;
        out.push(
          { action: 'reserve', enabled: true },
          { action: 'requestInfo', enabled: true },
          { action: 'approve', enabled: !noReserve, hint: noReserve ? 'Set a reserve first' : undefined },
          { action: 'reject', enabled: true },
        );
      }
      break;
    case 'InfoRequested':
      if (handles && mine) out.push({ action: 'reserve', enabled: true });
      break;
    case 'Approved':
      if (can('CLAIMS_SETTLE')) out.push({ action: 'settle', enabled: true });
      break;
  }
  return out;
}

/** Whole days since an instant (never negative). */
export function ageDays(iso: string, now = Date.now()): number {
  return Math.max(0, Math.floor((now - new Date(iso).getTime()) / 86_400_000));
}

/** Today in the browser's calendar, as YYYY-MM-DD (the report form's max incident date). */
export function localToday(now = new Date()): string {
  const m = String(now.getMonth() + 1).padStart(2, '0');
  const d = String(now.getDate()).padStart(2, '0');
  return `${now.getFullYear()}-${m}-${d}`;
}

/** Decision SLA used by the team page: an open claim older than this many days since submission is "past SLA". */
export const SLA_DECISION_DAYS = 14;

// ---- Record banner: the two tracks ----

export type StepState = 'done' | 'current' | 'next' | 'todo';

export interface TrackStep {
  label: string;
  state: StepState;
  /** The step is the current one AND in an error tone (a rejected decision). */
  error?: boolean;
  /** A sub-state dot on the step (InfoRequested on Under review). */
  dot?: string;
}

export interface Track {
  steps: TrackStep[];
  /** "2 of 4" */
  position: string;
}

function buildTrack(labels: string[], current: number, over?: Partial<Record<number, Partial<TrackStep>>>): Track {
  const steps = labels.map<TrackStep>((label, i) => ({
    label,
    state: i < current ? 'done' : i === current ? 'current' : i === current + 1 ? 'next' : 'todo',
    ...over?.[i],
  }));
  return { steps, position: `${current + 1} of ${labels.length}` };
}

/** Claim track: Submitted, Under review, Decided, Closed. InfoRequested is a dot on Under review; Rejected ends the track at Decided. */
export function claimTrack(status: ClaimStatus): Track {
  switch (status) {
    case 'Submitted':
      return buildTrack(['Submitted', 'Under review', 'Decided', 'Closed'], 0);
    case 'UnderReview':
      return buildTrack(['Submitted', 'Under review', 'Decided', 'Closed'], 1);
    case 'InfoRequested':
      return buildTrack(['Submitted', 'Under review', 'Decided', 'Closed'], 1, {
        1: { dot: 'Waiting on the claimant' },
      });
    case 'Approved':
      return buildTrack(['Submitted', 'Under review', 'Decided', 'Closed'], 2);
    case 'Rejected':
      return buildTrack(['Submitted', 'Under review', 'Decided'], 2, { 2: { label: 'Rejected', error: true } });
    case 'Settled':
      return buildTrack(['Submitted', 'Under review', 'Decided', 'Closed'], 3);
  }
}

/**
 * Money track. Staff: No reserve, Reserve set, Approved amount, Settled. The claimant never sees the reserve, so their
 * track is Assessing, Approved amount, Settled. A rejected claim ends the money track where it stood.
 */
export function moneyTrack(
  claim: Pick<ClaimDto, 'status' | 'reserveMinor' | 'approvedAmountMinor'>,
  staff: boolean,
): Track {
  const hasReserve = claim.reserveMinor !== null && claim.reserveMinor !== undefined;
  const approved = claim.status === 'Approved' || claim.status === 'Settled';
  if (staff) {
    if (claim.status === 'Rejected') {
      // The money track ends where it stood: no "Next" step after a rejection.
      return buildTrack(hasReserve ? ['No reserve', 'Reserve set'] : ['No reserve'], hasReserve ? 1 : 0);
    }
    const labels = ['No reserve', 'Reserve set', 'Approved amount', 'Settled'];
    return buildTrack(labels, claim.status === 'Settled' ? 3 : approved ? 2 : hasReserve ? 1 : 0);
  }
  if (claim.status === 'Rejected') return buildTrack(['Assessing'], 0);
  return buildTrack(['Assessing', 'Approved amount', 'Settled'], claim.status === 'Settled' ? 2 : approved ? 1 : 0);
}

// ---- Next step card ----

export type NextChip = 'Ready' | 'Waiting on claimant' | 'Waiting' | 'Blocked' | 'Done';

export interface Blocker {
  label: string;
  ok: boolean;
}

export interface NextStep {
  chip: NextChip;
  tone: Tone;
  instruction: string;
  why: string;
  blockers: Blocker[];
  /** The ONE primary button: an existing action's dialog, or 'answer' (focus the claimant's answer box). */
  primary?: { kind: ClaimAction | 'answer'; label: string };
}

const CHIP_TONE: Record<NextChip, Tone> = {
  Ready: 'acc',
  'Waiting on claimant': 'warn',
  Waiting: 'muted',
  Blocked: 'err',
  Done: 'ok',
};

function step(s: Omit<NextStep, 'tone'>): NextStep {
  return { ...s, tone: CHIP_TONE[s.chip] };
}

/** What the signed-in person should do with THIS claim now, from real data. One pure function: (claim, actor) -> card. */
export function nextStep(
  claim: Pick<
    ClaimDto,
    'status' | 'assignedOfficerId' | 'assignedOfficerName' | 'reserveMinor' | 'approvedAmountMinor'
  > & { openInfoRequest?: ClaimDto['openInfoRequest'] | null },
  actor: Actor,
): NextStep {
  const can = (p: string) => actor.permissions.has(p);
  const staff = can('CLAIMS_ALL');
  const hasReserve = claim.reserveMinor !== null && claim.reserveMinor !== undefined;
  const open = claim.openInfoRequest !== null && claim.openInfoRequest !== undefined;
  const work = availableActions(claim, actor);
  const mayWork = work.length > 0;

  if (claim.status === 'Rejected' || claim.status === 'Settled') {
    return step({
      chip: 'Done',
      instruction:
        claim.status === 'Rejected' ? 'This claim is closed: rejected.' : 'This claim is settled and closed.',
      why: staff ? 'Nothing more to do on it.' : WHAT_NEXT[claim.status],
      blockers: [],
    });
  }

  if (!staff) {
    switch (claim.status) {
      case 'InfoRequested':
        return step({
          chip: 'Ready',
          instruction: 'We need one answer from you.',
          why: 'Your claim is paused until you reply. As soon as you do, the review carries on.',
          blockers: [{ label: 'Answer the question below', ok: false }],
          primary: can('CLAIMS_RESPOND') ? { kind: 'answer', label: 'Answer now' } : undefined,
        });
      case 'Approved':
        return step({
          chip: 'Waiting',
          instruction: 'Your claim is approved. Nothing to do.',
          why: WHAT_NEXT.Approved,
          blockers: [],
        });
      default:
        return step({
          chip: 'Waiting',
          instruction: claim.status === 'Submitted' ? 'We have your claim.' : 'An officer is reviewing your claim.',
          why: WHAT_NEXT[claim.status],
          blockers: [],
        });
    }
  }

  switch (claim.status) {
    case 'Submitted':
      return mayWork
        ? step({
            chip: 'Ready',
            instruction: 'Pick up this claim.',
            why: 'Nobody is assigned yet. Picking it up makes you the officer and starts the review.',
            blockers: [{ label: 'Not yet assigned to an officer', ok: true }],
            primary: { kind: 'pickup', label: 'Pick up' },
          })
        : step({
            chip: 'Waiting',
            instruction: 'Waiting for an officer to pick it up.',
            why: 'You do not hold the permission to handle claims.',
            blockers: [{ label: 'Needs CLAIMS_HANDLE', ok: false }],
          });
    case 'UnderReview': {
      if (!mayWork) {
        return step({
          chip: 'Waiting',
          instruction: `${claim.assignedOfficerName ?? 'Another officer'} is working this claim.`,
          why: 'Only the assigned officer, or a manager, can act on it.',
          blockers: [{ label: 'Assigned to someone else', ok: false }],
        });
      }
      const blockers: Blocker[] = [
        { label: 'Reserve is set', ok: hasReserve },
        { label: 'No open question to the claimant', ok: !open },
        { label: 'Approved amount can stay within the reserve', ok: hasReserve },
      ];
      return hasReserve
        ? step({
            chip: 'Ready',
            instruction: 'Decide this claim.',
            why: 'The reserve is set, so you can approve (up to the reserve) or reject. Ask the claimant first if you need more.',
            blockers,
            primary: { kind: 'approve', label: 'Approve' },
          })
        : step({
            chip: 'Blocked',
            instruction: 'Set the reserve before you decide.',
            why: 'A claim cannot be approved without a reserve, and the approved amount may not exceed it.',
            blockers,
            primary: { kind: 'reserve', label: 'Set reserve' },
          });
    }
    case 'InfoRequested':
      return step({
        chip: 'Waiting on claimant',
        instruction: 'Waiting for the claimant to answer.',
        why: 'The review resumes by itself the moment they reply. You can still adjust the reserve meanwhile.',
        blockers: [{ label: 'Claimant has answered the open question', ok: false }],
      });
    case 'Approved':
      return can('CLAIMS_SETTLE')
        ? step({
            chip: 'Ready',
            instruction: 'Settle this claim.',
            why: 'It is approved. Settling records the payment as made and closes the claim.',
            blockers: [
              {
                label: 'Approved amount recorded',
                ok: claim.approvedAmountMinor !== null && claim.approvedAmountMinor !== undefined,
              },
            ],
            primary: { kind: 'settle', label: 'Settle' },
          })
        : step({
            chip: 'Waiting',
            instruction: 'Waiting for a manager to settle it.',
            why: 'Only a manager can record the payment.',
            blockers: [{ label: 'Needs CLAIMS_SETTLE', ok: false }],
          });
  }
}
