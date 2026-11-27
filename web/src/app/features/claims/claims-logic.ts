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
