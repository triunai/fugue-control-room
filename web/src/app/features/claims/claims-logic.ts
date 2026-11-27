import { ClaimDto, ClaimStatus, Market } from '../../api/models';
import { SLA_DAYS } from './sla';

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

const FLAGS: Record<string, string> = { HK: '🇭🇰', ID: '🇮🇩', MY: '🇲🇾', PH: '🇵🇭', SG: '🇸🇬', TH: '🇹🇭' };

/** The market's flag emoji; empty for an unknown code. Render it aria-hidden beside the name. */
export function marketFlag(market: string): string {
  return FLAGS[market] ?? '';
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

/** SLA constants, in days, read from the one source (`sla.ts`, `SLA_DAYS`) that the calendar also uses. */
export const SLA_PICKUP_DAYS = SLA_DAYS.pickup;
export const SLA_DECISION_DAYS = SLA_DAYS.decision;
export const SLA_ANSWER_DAYS = SLA_DAYS.answer;
const DAY = 86_400_000;

export type SlaKind = 'pickup' | 'decision' | 'answer';

/** The next SLA a claim owes, from its status: pickup 2 d after submit, decision 14 d after submit, answer 7 d after the question. */
export function slaDue(
  claim: Pick<ClaimDto, 'status' | 'submittedAt'> & { openInfoRequest?: ClaimDto['openInfoRequest'] | null },
): { kind: SlaKind; due: Date } | null {
  const at = (iso: string, days: number) => new Date(new Date(iso).getTime() + days * DAY);
  switch (claim.status) {
    case 'Submitted':
      return { kind: 'pickup', due: at(claim.submittedAt, SLA_PICKUP_DAYS) };
    case 'UnderReview':
      return { kind: 'decision', due: at(claim.submittedAt, SLA_DECISION_DAYS) };
    case 'InfoRequested':
      return claim.openInfoRequest
        ? { kind: 'answer', due: at(claim.openInfoRequest.requestedAt, SLA_ANSWER_DAYS) }
        : null;
    default:
      return null;
  }
}

// ---- Record banner: milestones reached, on two tracks ----

export type StepState = 'done' | 'current' | 'next' | 'todo';

export interface TrackStep {
  label: string;
  state: StepState;
  /** Reached (ticked). The current step is the last reached one. */
  checked: boolean;
  tone?: 'warn' | 'error';
  /** Under a ticked step: when it happened (ISO). */
  date?: string | null;
  /** Under the step: who owes the Next step, or why the step is flagged. */
  caption?: string;
}

export interface Track {
  steps: TrackStep[];
  /** "2 of 4" ('' when the track is replaced by a note). */
  position: string;
  /** Replaces the steps (a rejected claim has no money track). */
  note?: string;
}

type Milestone = Partial<Pick<TrackStep, 'label' | 'tone' | 'date' | 'caption'>>;

function track(labels: string[], reached: number, extra: Record<number, Milestone> = {}): Track {
  const steps = labels.map<TrackStep>((label, i) => ({
    label,
    checked: i < reached,
    state: i < reached - 1 ? 'done' : i === reached - 1 ? 'current' : i === reached ? 'next' : 'todo',
    ...extra[i],
  }));
  return { steps, position: `${reached} of ${labels.length}` };
}

type TrackClaim = Pick<
  ClaimDto,
  'status' | 'assignedOfficerId' | 'reserveMinor' | 'submittedAt' | 'assignedAt' | 'decidedAt' | 'settledAt'
>;

/** Who owes the Next milestone, as the viewer reads it: "you", or the role that does. */
function owner(claim: TrackClaim, actor: Actor, what: 'pickup' | 'decide' | 'settle'): string {
  const can = (p: string) => actor.permissions.has(p);
  if (!can('CLAIMS_ALL')) return what === 'settle' ? 'manager' : 'claims team';
  if (what === 'settle') return can('CLAIMS_SETTLE') ? 'you' : 'manager';
  if (what === 'pickup') return can('CLAIMS_HANDLE') ? 'you' : 'claims team';
  return availableActions(claim, actor).length ? 'you' : 'claims team';
}

/** Claim track: Reported, Picked up, Decided, Closed. A milestone is reached or it is not; InfoRequested does not move the count. */
export function claimTrack(claim: TrackClaim, actor: Actor): Track {
  const labels = ['Reported', 'Picked up', 'Decided', 'Closed'];
  const staff = actor.permissions.has('CLAIMS_ALL');
  const dates: Record<number, Milestone> = {
    0: { date: claim.submittedAt },
    1: { date: staff ? claim.assignedAt : undefined },
    2: { date: claim.decidedAt },
    3: { date: claim.settledAt ?? (claim.status === 'Rejected' ? claim.decidedAt : undefined) },
  };
  const next = (i: number, who: string): Record<number, Milestone> => ({ ...dates, [i]: { caption: who } });
  switch (claim.status) {
    case 'Submitted':
      return track(labels, 1, next(1, owner(claim, actor, 'pickup')));
    case 'UnderReview':
      return track(labels, 2, next(2, owner(claim, actor, 'decide')));
    case 'InfoRequested':
      return track(labels, 2, {
        ...next(2, 'claims team'),
        1: { ...dates[1], tone: 'warn', caption: 'Waiting on claimant' },
      });
    case 'Approved':
      return track(labels, 3, { ...next(3, owner(claim, actor, 'settle')), 2: { ...dates[2], label: 'Approved' } });
    case 'Rejected':
      return track(labels, 4, { ...dates, 2: { ...dates[2], label: 'Rejected', tone: 'error' } });
    case 'Settled':
      return track(labels, 4, dates);
  }
}

/**
 * Money track. Staff: Reserved, Approved, Paid. The claimant never sees a reserve, so theirs is Assessed, Approved, Paid
 * and never an amount. A rejected claim has no money track, only a line saying so.
 */
export function moneyTrack(
  claim: Pick<
    ClaimDto,
    'status' | 'assignedOfficerId' | 'reserveMinor' | 'decidedAt' | 'settledAt' | 'submittedAt' | 'assignedAt'
  >,
  actor: Actor,
): Track {
  const staff = actor.permissions.has('CLAIMS_ALL');
  if (claim.status === 'Rejected') {
    return { steps: [], position: '', note: staff ? 'No payment. Reserve released.' : 'No payment' };
  }
  const hasReserve = claim.reserveMinor !== null && claim.reserveMinor !== undefined;
  const approved = claim.status === 'Approved' || claim.status === 'Settled';
  const reached = claim.status === 'Settled' ? 3 : approved ? 2 : staff && hasReserve ? 1 : 0;
  const labels = [staff ? 'Reserved' : 'Assessed', 'Approved', 'Paid'];
  const who =
    reached === 2
      ? owner(claim, actor, 'settle')
      : reached === 0 && staff
        ? owner(claim, actor, 'decide')
        : 'claims team';
  const extra: Record<number, Milestone> = reached < 3 ? { [reached]: { caption: who } } : {};
  if (reached >= 3) extra[2] = { date: claim.settledAt };
  if (reached >= 2) extra[1] = { date: claim.decidedAt };
  return track(labels, reached, extra);
}

/** The staff money chip: No reserve, Reserved <amount>, Approved <amount>, Paid. Claimants get none (no reserve to name). */
export function moneyChip(
  claim: Pick<ClaimDto, 'status' | 'reserveMinor' | 'approvedAmountMinor' | 'currency'>,
): string {
  switch (claim.status) {
    case 'Rejected':
      return 'No payment';
    case 'Settled':
      return 'Paid';
    case 'Approved':
      return `Approved ${formatMoney(claim.approvedAmountMinor, claim.currency)}`;
    default:
      return claim.reserveMinor === null || claim.reserveMinor === undefined
        ? 'No reserve'
        : `Reserved ${formatMoney(claim.reserveMinor, claim.currency)}`;
  }
}

// ---- Next step card ----

export type NextChip =
  | 'Ready'
  | 'Overdue'
  | 'Blocked'
  | 'Not yours'
  | 'Waiting'
  | 'Waiting on claimant'
  | 'Waiting on manager'
  | 'Action needed'
  | 'With us'
  | 'Approved'
  | 'Closed'
  | 'Paid'
  | 'Done';

export interface Blocker {
  label: string;
  ok: boolean;
}

export type NextAction = ClaimAction | 'answer';

export interface NextStep {
  chip: NextChip;
  tone: Tone;
  instruction: string;
  why: string;
  /** "Day 5 of 14", "Acting as manager on X's claim". */
  caption?: string;
  /** Set when the caption reports an overdue SLA: shown in the error tone. */
  captionOverdue?: boolean;
  /** The claimant's open question, shown on the card. */
  question?: string;
  blockers: Blocker[];
  /** The ONE primary button. Absent when there is nothing to do: never a disabled primary. */
  primary?: { kind: NextAction; label: string };
  secondary: { kind: ClaimAction; label: string }[];
}

const CHIP_TONE: Record<NextChip, Tone> = {
  Ready: 'acc',
  Overdue: 'err',
  Blocked: 'err',
  'Not yours': 'muted',
  Waiting: 'muted',
  'Waiting on claimant': 'warn',
  'Waiting on manager': 'muted',
  'Action needed': 'warn',
  'With us': 'muted',
  Approved: 'ok',
  Closed: 'muted',
  Paid: 'ok',
  Done: 'ok',
};

type CardClaim = Pick<
  ClaimDto,
  | 'status'
  | 'assignedOfficerId'
  | 'assignedOfficerName'
  | 'reserveMinor'
  | 'approvedAmountMinor'
  | 'submittedAt'
  | 'currency'
  | 'decisionReason'
> & { openInfoRequest?: ClaimDto['openInfoRequest'] | null };

function card(
  c: Omit<NextStep, 'tone' | 'secondary' | 'blockers'> & Partial<Pick<NextStep, 'secondary' | 'blockers'>>,
): NextStep {
  return { blockers: [], secondary: [], ...c, tone: CHIP_TONE[c.chip] };
}

/** "Day N of D" while within the SLA, "N days past SLA" (error tone) once over it. */
export function slaDayCaption(age: number, slaDays: number): { text: string; overdue: boolean } {
  if (age <= slaDays) return { text: `Day ${age} of ${slaDays}`, overdue: false };
  const over = age - slaDays;
  return { text: `${over} day${over === 1 ? '' : 's'} past SLA`, overdue: true };
}

const daysBetween = (iso: string, now: number) => Math.max(0, Math.floor((now - new Date(iso).getTime()) / DAY));

/** What the signed-in person should do with THIS claim now, from real data. One pure function: (claim, actor, now) -> card. */
export function nextStep(claim: CardClaim, actor: Actor, now = Date.now()): NextStep {
  const can = (p: string) => actor.permissions.has(p);
  const staff = can('CLAIMS_ALL');
  const money = (n: number | null | undefined) => formatMoney(n, claim.currency);
  const hasReserve = claim.reserveMinor !== null && claim.reserveMinor !== undefined;
  const open = claim.openInfoRequest !== null && claim.openInfoRequest !== undefined;
  const work = availableActions(claim, actor);
  const mayWork = work.length > 0;
  const age = daysBetween(claim.submittedAt, now);

  if (claim.status === 'Rejected') {
    return card({
      chip: staff ? 'Done' : 'Closed',
      instruction: staff ? 'This claim is closed: rejected.' : 'Your claim was not approved.',
      why: claim.decisionReason ?? WHAT_NEXT.Rejected,
    });
  }
  if (claim.status === 'Settled') {
    return card({
      chip: staff ? 'Done' : 'Paid',
      instruction: staff ? 'This claim is settled and closed.' : 'Your claim is paid.',
      why: staff ? 'Nothing more to do on it.' : WHAT_NEXT.Settled,
    });
  }

  if (!staff) {
    switch (claim.status) {
      case 'InfoRequested':
        return card({
          chip: 'Action needed',
          instruction: 'We need one answer from you.',
          why: 'Your claim is paused until you reply. As soon as you do, the review carries on.',
          question: claim.openInfoRequest?.question,
          blockers: [{ label: 'Answer the question', ok: false }],
          primary: can('CLAIMS_RESPOND') ? { kind: 'answer', label: 'Answer now' } : undefined,
        });
      case 'Approved':
        return card({
          chip: 'Approved',
          instruction: `Approved: payment of ${money(claim.approvedAmountMinor)} is being arranged.`,
          why: WHAT_NEXT.Approved,
        });
      default:
        return card({
          chip: 'With us',
          instruction: claim.status === 'Submitted' ? 'We have your claim.' : 'An officer is reviewing your claim.',
          why: WHAT_NEXT[claim.status],
        });
    }
  }

  switch (claim.status) {
    case 'Submitted':
      return mayWork
        ? card({
            chip: age >= SLA_PICKUP_DAYS ? 'Overdue' : 'Ready',
            instruction: 'Pick up this claim.',
            why:
              age >= SLA_PICKUP_DAYS
                ? `Nobody is assigned and it has waited ${age} days (pickup target is ${SLA_PICKUP_DAYS}).`
                : 'Nobody is assigned yet. Picking it up makes you the officer and starts the review.',
            primary: { kind: 'pickup', label: 'Pick up' },
          })
        : card({
            chip: 'Waiting',
            instruction: 'Waiting for an officer to pick it up.',
            why: 'You do not hold the permission to handle claims.',
          });
    case 'UnderReview': {
      if (!mayWork) {
        return card({
          chip: 'Not yours',
          instruction: `Assigned to ${claim.assignedOfficerName ?? 'another officer'}.`,
          why: 'Only they or a manager can act on it.',
        });
      }
      const mine = claim.assignedOfficerId === actor.userId;
      const caption = mine ? undefined : `Acting as manager on ${claim.assignedOfficerName ?? 'the officer'}'s claim`;
      const blockers: Blocker[] = [
        { label: mine ? 'Assigned to you' : `Assigned to ${claim.assignedOfficerName ?? 'an officer'}`, ok: true },
        { label: 'Reserve set', ok: hasReserve },
        { label: 'No open question', ok: !open },
      ];
      const day = slaDayCaption(age, SLA_DECISION_DAYS);
      return hasReserve
        ? card({
            chip: 'Ready',
            instruction: `Decide: approve up to ${money(claim.reserveMinor)}, or reject.`,
            why: 'The reserve is set. Ask the claimant first if you need more information.',
            caption: [day.text, caption].filter(Boolean).join(' · '),
            captionOverdue: day.overdue,
            blockers,
            primary: { kind: 'approve', label: 'Approve' },
            secondary: [
              { kind: 'reject', label: 'Reject' },
              { kind: 'requestInfo', label: 'Request info' },
              { kind: 'reserve', label: 'Change reserve' },
            ],
          })
        : card({
            chip: 'Blocked',
            instruction: 'Set a reserve.',
            why: 'A claim cannot be approved without one, and exposure cannot count it until it is set.',
            caption,
            blockers,
            primary: { kind: 'reserve', label: 'Set reserve' },
            secondary: [
              { kind: 'requestInfo', label: 'Request info' },
              { kind: 'reject', label: 'Reject' },
            ],
          });
    }
    case 'InfoRequested': {
      const asked = claim.openInfoRequest ? daysBetween(claim.openInfoRequest.requestedAt, now) : null;
      return card({
        chip: 'Waiting on claimant',
        instruction:
          asked === null ? 'Waiting for the claimant to answer.' : `Waiting for the claimant: asked ${asked} days ago.`,
        why: 'The review resumes by itself the moment they reply.',
        question: claim.openInfoRequest?.question,
        blockers: [{ label: 'Claimant has answered', ok: false }],
        secondary: mayWork ? [{ kind: 'reserve', label: 'Change reserve' }] : [],
      });
    }
    case 'Approved':
      return can('CLAIMS_SETTLE')
        ? card({
            chip: 'Ready',
            instruction: `Settle ${money(claim.approvedAmountMinor)}.`,
            why: 'It is approved. Settling records the payment as made and closes the claim.',
            primary: { kind: 'settle', label: 'Settle' },
          })
        : card({
            chip: 'Waiting on manager',
            instruction: 'Waiting for a manager to settle it.',
            why: 'Only a manager can record the payment.',
          });
  }
}
