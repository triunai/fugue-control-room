import { ClaimStatus } from '../../api/models';
import { Actor, claimTrack, moneyChip, moneyTrack, nextStep, slaDayCaption, slaDue, stepHelp } from './claims-logic';

const actor = (userId: string | undefined, ...permissions: string[]): Actor => ({
  userId,
  permissions: new Set(permissions),
});
const officer = actor('u1', 'CLAIMS_ALL', 'CLAIMS_HANDLE');
const manager = actor('u2', 'CLAIMS_ALL', 'CLAIMS_HANDLE', 'CLAIMS_SETTLE');
const claimant = actor('c1', 'CLAIMS_SUBMIT', 'CLAIMS_RESPOND');

const NOW = Date.parse('2026-11-27T00:00:00Z');
const ago = (d: number) => new Date(NOW - d * 86_400_000).toISOString();
interface Fx {
  assignedOfficerId: string | null;
  assignedOfficerName: string | null;
  reserveMinor: number | null;
  approvedAmountMinor: number | null;
  openInfoRequest: { id: string; question: string; requestedAt: string } | null;
  submittedAt: string;
  assignedAt: string | null;
  decidedAt: string | null;
  settledAt: string | null;
  currency: string;
  decisionReason: string | null;
}
const base: Fx = {
  assignedOfficerId: 'u1',
  assignedOfficerName: 'Dev Staff',
  reserveMinor: null,
  approvedAmountMinor: null,
  openInfoRequest: null,
  submittedAt: ago(3),
  assignedAt: ago(2),
  decidedAt: null,
  settledAt: null,
  currency: 'SGD',
  decisionReason: null,
};
const labels = (t: { steps: { label: string }[] }) => t.steps.map((s) => s.label);
const checked = (t: { steps: { checked: boolean }[] }) => t.steps.map((s) => s.checked);
const q = { id: 'q', question: 'Photos?', requestedAt: ago(2) };

describe('claimTrack: milestones reached', () => {
  it('Reported, Picked up, Decided, Closed, with Next on the following milestone', () => {
    const sub = claimTrack({ ...base, status: 'Submitted' }, officer);
    expect(labels(sub)).toEqual(['Reported', 'Picked up', 'Decided', 'Closed']);
    expect(sub.position).toBe('1 of 4');
    expect(sub.steps[1]).toMatchObject({ state: 'next', caption: 'you' });
    const ur = claimTrack({ ...base, status: 'UnderReview' }, officer);
    expect(ur.position).toBe('2 of 4');
    expect(checked(ur)).toEqual([true, true, false, false]);
    expect(ur.steps[2].state).toBe('next');
  });

  it('InfoRequested does not move the count: Picked up is flagged warn, "Waiting on claimant"', () => {
    const t = claimTrack({ ...base, status: 'InfoRequested' }, officer);
    expect(t.position).toBe('2 of 4');
    expect(t.steps[1]).toMatchObject({ tone: 'warn', caption: 'Waiting on claimant', state: 'current' });
  });

  it('Approved: 3 of 4, labelled Approved, Closed is Next and the manager owes it', () => {
    const t = claimTrack({ ...base, status: 'Approved' }, officer);
    expect(t.position).toBe('3 of 4');
    expect(t.steps[2].label).toBe('Approved');
    expect(t.steps[3]).toMatchObject({ state: 'next', caption: 'manager' });
    expect(claimTrack({ ...base, status: 'Approved' }, manager).steps[3].caption).toBe('you');
  });

  it('Rejected: 4 of 4, Decided in the error tone labelled Rejected, Closed ticked, no Next', () => {
    const t = claimTrack({ ...base, status: 'Rejected', decidedAt: ago(1) }, officer);
    expect(t.position).toBe('4 of 4');
    expect(t.steps[2]).toMatchObject({ label: 'Rejected', tone: 'error' });
    expect(t.steps[3].checked).toBe(true);
    expect(t.steps.some((s) => s.state === 'next')).toBe(false);
  });

  it('Settled is 4 of 4 and shows dates under ticked steps; the pickup date is staff-only', () => {
    const t = claimTrack({ ...base, status: 'Settled', decidedAt: ago(1), settledAt: ago(0) }, officer);
    expect(t.position).toBe('4 of 4');
    expect(t.steps.map((s) => s.date)).toEqual([ago(3), ago(2), ago(1), ago(0)]);
    expect(claimTrack({ ...base, status: 'UnderReview' }, claimant).steps[1].date).toBeUndefined();
  });

  it('Next is the same milestone for every viewer; only the owner caption differs', () => {
    const next = (a: Actor) => claimTrack({ ...base, status: 'UnderReview' }, a).steps.find((s) => s.state === 'next')!;
    expect(next(officer).label).toBe(next(claimant).label);
    expect(next(officer).caption).toBe('you');
    expect(next(claimant).caption).toBe('claims team');
  });

  it('every status yields a track', () => {
    for (const s of Object.values(ClaimStatus))
      expect(claimTrack({ ...base, status: s }, officer).steps).toHaveLength(4);
  });
});

describe('moneyTrack and moneyChip', () => {
  it('staff: Reserved, Approved, Paid', () => {
    expect(labels(moneyTrack({ ...base, status: 'UnderReview' }, officer))).toEqual(['Reserved', 'Approved', 'Paid']);
    expect(moneyTrack({ ...base, status: 'UnderReview' }, officer).position).toBe('0 of 3');
    expect(moneyTrack({ ...base, status: 'UnderReview', reserveMinor: 5 }, officer).position).toBe('1 of 3');
    expect(moneyTrack({ ...base, status: 'Approved', reserveMinor: 5 }, officer).steps[2]).toMatchObject({
      state: 'next',
      caption: 'manager',
    });
    expect(moneyTrack({ ...base, status: 'Settled', reserveMinor: 5 }, officer).position).toBe('3 of 3');
  });

  it('claimant: Assessed first, and a reserve never moves it', () => {
    const t = moneyTrack({ ...base, status: 'UnderReview', reserveMinor: 5 }, claimant);
    expect(labels(t)).toEqual(['Assessed', 'Approved', 'Paid']);
    expect(t.position).toBe('0 of 3');
  });

  it('rejected: no steps, one muted line', () => {
    expect(moneyTrack({ ...base, status: 'Rejected' }, officer)).toMatchObject({
      steps: [],
      note: 'No payment. Reserve released.',
    });
    expect(moneyTrack({ ...base, status: 'Rejected' }, claimant).note).toBe('No payment');
  });

  it('the staff chip names the money status', () => {
    expect(moneyChip({ ...base, status: 'UnderReview' })).toBe('No reserve');
    expect(moneyChip({ ...base, status: 'UnderReview', reserveMinor: 800000 }).replace(/\u00a0/g, ' ')).toBe(
      'Reserved SGD 8,000.00',
    );
    expect(moneyChip({ ...base, status: 'Approved', approvedAmountMinor: 100 })).toMatch(/^Approved SGD/);
    expect(moneyChip({ ...base, status: 'Settled' })).toBe('Paid');
  });
});

describe('slaDue', () => {
  it('pickup 2 d after submit, decision 14 d after submit, answer 7 d after the question', () => {
    const d = (x: ReturnType<typeof slaDue>) => x && [x.kind, x.due.toISOString().slice(0, 10)];
    expect(d(slaDue({ status: 'Submitted', submittedAt: '2026-11-01T00:00:00Z' }))).toEqual(['pickup', '2026-11-03']);
    expect(d(slaDue({ status: 'UnderReview', submittedAt: '2026-11-01T00:00:00Z' }))).toEqual([
      'decision',
      '2026-11-15',
    ]);
    expect(
      d(
        slaDue({
          status: 'InfoRequested',
          submittedAt: '2026-11-01T00:00:00Z',
          openInfoRequest: { id: 'x', question: '?', requestedAt: '2026-11-10T00:00:00Z' },
        }),
      ),
    ).toEqual(['answer', '2026-11-17']);
    expect(slaDue({ status: 'Approved', submittedAt: '2026-11-01T00:00:00Z' })).toBeNull();
  });
});

describe('nextStep', () => {
  const step = (c: Partial<Fx> & { status: ClaimStatus }, a: Actor) => nextStep({ ...base, ...c }, a, NOW);

  it('Submitted: Ready with Pick up, Overdue past 2 days; the claimant is "With us" with no button', () => {
    expect(step({ status: 'Submitted', assignedOfficerId: null, submittedAt: ago(1) }, officer)).toMatchObject({
      chip: 'Ready',
      primary: { kind: 'pickup' },
    });
    expect(step({ status: 'Submitted', assignedOfficerId: null }, officer).chip).toBe('Overdue');
    const c = step({ status: 'Submitted' }, claimant);
    expect(c.chip).toBe('With us');
    expect(c.primary).toBeUndefined();
  });

  it('UnderReview without a reserve: Blocked, primary Set reserve, secondaries Request info and Reject', () => {
    const n = step({ status: 'UnderReview' }, officer);
    expect(n.chip).toBe('Blocked');
    expect(n.instruction).toBe('You set the reserve, the amount we expect to pay, before this claim can be approved.');
    expect(n.primary?.kind).toBe('reserve');
    expect(n.secondary.map((s) => s.kind)).toEqual(['requestInfo', 'reject']);
    expect(n.blockers.map((b) => b.ok)).toEqual([true, false, true]);
  });

  it('UnderReview with a reserve: Ready, Approve, "Day N of 14"', () => {
    const n = step({ status: 'UnderReview', reserveMinor: 500000 }, officer);
    expect(n.chip).toBe('Ready');
    expect(n.primary?.kind).toBe('approve');
    expect(n.caption).toBe('Day 3 of 14');
    expect(n.blockers.every((b) => b.ok)).toBe(true);
  });

  it('SLA caption: Day N of 14 within the SLA, N days past SLA once over', () => {
    expect(slaDayCaption(14, 14)).toEqual({ text: 'Day 14 of 14', overdue: false });
    expect(slaDayCaption(15, 14)).toEqual({ text: '1 day past SLA', overdue: true });
    expect(slaDayCaption(18, 14)).toEqual({ text: '4 days past SLA', overdue: true });
    const n = step({ status: 'UnderReview', reserveMinor: 500000, submittedAt: '2026-11-09T00:00:00Z' }, officer);
    expect(n.caption).toBe('4 days past SLA');
    expect(n.captionOverdue).toBe(true);
  });

  it('another officer\'s claim: "Not yours", no button at all; a manager acts and is told so', () => {
    const other = actor('u9', 'CLAIMS_ALL', 'CLAIMS_HANDLE');
    const n = step({ status: 'UnderReview', reserveMinor: 5 }, other);
    expect(n.chip).toBe('Not yours');
    expect(n.instruction).toContain('Dev Staff');
    expect(n.primary).toBeUndefined();
    expect(n.secondary).toEqual([]);
    const m = step({ status: 'UnderReview', reserveMinor: 5 }, manager);
    expect(m.primary?.kind).toBe('approve');
    expect(m.caption).toContain("Acting as manager on Dev Staff's claim");
  });

  it('InfoRequested: officer waits (asked N days ago), claimant is asked with the question and an Answer button', () => {
    const o = step({ status: 'InfoRequested', openInfoRequest: q }, officer);
    expect(o.chip).toBe('Waiting on claimant');
    expect(o.instruction).toContain('asked 2 days ago');
    expect(o.primary).toBeUndefined();
    const c = step({ status: 'InfoRequested', openInfoRequest: q }, claimant);
    expect(c.chip).toBe('Action needed');
    expect(c.question).toBe('Photos?');
    expect(c.primary?.kind).toBe('answer');
  });

  it('Approved: manager Ready to settle <amount>, officer waits on the manager, claimant sees the payment amount', () => {
    const c = { status: 'Approved' as const, reserveMinor: 5, approvedAmountMinor: 400000 };
    expect(step(c, manager)).toMatchObject({ chip: 'Ready', primary: { kind: 'settle' } });
    expect(step(c, manager).instruction.replace(/\u00a0/g, ' ')).toBe(
      'You settle this claim: record the payment of SGD 4,000.00 as made, which closes it.',
    );
    const o = step(c, officer);
    expect(o.chip).toBe('Waiting on manager');
    expect(o.primary).toBeUndefined();
    expect(step(c, claimant).instruction.replace(/\u00a0/g, ' ')).toContain(
      'payment of SGD 4,000.00 is being arranged',
    );
  });

  it('Rejected and Settled: Done for staff, Closed / Paid for the claimant, the reason shown', () => {
    expect(step({ status: 'Rejected', decisionReason: 'Pre-dates the policy' }, officer)).toMatchObject({
      chip: 'Done',
      why: 'Pre-dates the policy',
    });
    expect(step({ status: 'Rejected', decisionReason: 'x' }, claimant).chip).toBe('Closed');
    expect(step({ status: 'Settled' }, officer).chip).toBe('Done');
    expect(step({ status: 'Settled' }, claimant).chip).toBe('Paid');
  });

  it('never a disabled primary: a card with nothing to do has no button', () => {
    for (const status of Object.values(ClaimStatus))
      for (const a of [officer, manager, claimant]) {
        const n = step({ status, openInfoRequest: status === 'InfoRequested' ? q : null }, a);
        if (n.chip === 'Done' || n.chip === 'Paid' || n.chip === 'Closed') expect(n.primary).toBeUndefined();
        expect(n.instruction).toBeTruthy();
      }
  });

  it('never mentions the reserve to a claimant', () => {
    for (const status of Object.values(ClaimStatus)) {
      const n = step({ status, reserveMinor: 5, openInfoRequest: status === 'InfoRequested' ? q : null }, claimant);
      expect(JSON.stringify(n).toLowerCase()).not.toContain('reserve');
    }
  });
});

describe('next step copy: one plain sentence per state, who acts, plus the action', () => {
  const o = (c: Partial<Fx> & { status: ClaimStatus }, a: Actor) => nextStep({ ...base, ...c }, a, NOW).instruction.replace(/\u00a0/g, ' ');
  it('officer states', () => {
    expect(o({ status: 'Submitted', assignedOfficerId: null }, officer)).toBe(
      'Nobody has this claim yet: you pick it up, which makes you its officer and starts the review.',
    );
    expect(o({ status: 'Submitted', assignedOfficerId: null }, claimant)).toBe('We have your claim.');
    expect(o({ status: 'UnderReview', reserveMinor: 500000 }, officer)).toBe(
      'You decide this claim: approve it up to SGD 5,000.00, or reject it.',
    );
    expect(o({ status: 'InfoRequested', openInfoRequest: q }, officer)).toBe(
      'The claimant must answer our question, asked 2 days ago; the review resumes by itself when they do.',
    );
  });
  it('an approved claim: the officer waits on a manager', () => {
    const c = { status: 'Approved' as const, reserveMinor: 5, approvedAmountMinor: 400000 };
    expect(o(c, officer)).toBe('A manager will record the payment and close this claim; you have nothing to do.');
  });
});

describe('stepHelp', () => {
  it('explains every milestone on both tracks, never leaking the reserve to the claimant wording', () => {
    for (const l of ['Reported', 'Picked up', 'Decided', 'Approved', 'Rejected', 'Closed', 'Reserved', 'Assessed', 'Paid'])
      expect(stepHelp(l).length).toBeGreaterThan(20);
    expect(stepHelp('Assessed').toLowerCase()).not.toContain('reserve');
    expect(stepHelp('nope')).toBe('nope');
  });
});
