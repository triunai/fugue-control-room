import { ClaimDto, ClaimStatus } from '../../api/models';

/**
 * SLA targets in whole days. The only place these numbers live: change them here and the calendar and its tests follow.
 * The API has no SLA field; this is a console rule, stated in the web README.
 */
export const SLA_DAYS = {
  /** A submitted claim should be picked up by an officer within 2 days of `submittedAt`. */
  pickup: 2,
  /** An open claim should be decided within 14 days of `submittedAt`. */
  decision: 14,
  /** A question put to the claimant should be answered within 7 days of being asked. */
  answer: 7,
  /** An approved claim should be settled within 7 days of the decision. */
  settle: 7,
} as const;

export type SlaKind = 'pickup' | 'decision' | 'answer' | 'settle';

export interface SlaDue {
  kind: SlaKind;
  /** What is due, in words. */
  label: string;
  /** The due instant (ISO 8601). */
  due: string;
}

const DAY_MS = 86_400_000;

function plusDays(iso: string, days: number): string {
  return new Date(new Date(iso).getTime() + days * DAY_MS).toISOString();
}

/**
 * The ONE due-date function. Given a claim it returns what is due next and when, or null when nothing is (Rejected, Settled).
 *  - Submitted:     pickup, `submittedAt` + 2 days.
 *  - UnderReview:   decision, `submittedAt` + 14 days.
 *  - InfoRequested: answer, the question's `requestedAt` + 7 days (falls back to `updatedAt` if the question is not on the claim).
 *  - Approved:      settlement, `decidedAt` + 7 days (falls back to `updatedAt`).
 */
export function slaDue(
  claim: Pick<ClaimDto, 'status' | 'submittedAt' | 'updatedAt' | 'decidedAt' | 'openInfoRequest'>,
): SlaDue | null {
  const status: ClaimStatus = claim.status;
  switch (status) {
    case 'Submitted':
      return { kind: 'pickup', label: 'Pickup due', due: plusDays(claim.submittedAt, SLA_DAYS.pickup) };
    case 'UnderReview':
      return { kind: 'decision', label: 'Decision due', due: plusDays(claim.submittedAt, SLA_DAYS.decision) };
    case 'InfoRequested': {
      const asked = claim.openInfoRequest?.requestedAt ?? claim.updatedAt;
      return { kind: 'answer', label: 'Answer due', due: plusDays(asked, SLA_DAYS.answer) };
    }
    case 'Approved':
      return {
        kind: 'settle',
        label: 'Settlement due',
        due: plusDays(claim.decidedAt ?? claim.updatedAt, SLA_DAYS.settle),
      };
    default:
      return null;
  }
}

/** Overdue when the due instant has passed. */
export function isOverdue(due: string, now = Date.now()): boolean {
  return new Date(due).getTime() < now;
}
