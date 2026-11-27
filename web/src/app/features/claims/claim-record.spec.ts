import { ClaimStatus } from '../../api/models';
import { Actor, claimTrack, moneyTrack, nextStep } from './claims-logic';

const actor = (userId: string | undefined, ...permissions: string[]): Actor => ({
  userId,
  permissions: new Set(permissions),
});
const labels = (t: { steps: { label: string }[] }) => t.steps.map((s) => s.label);
const stateOf = (t: ReturnType<typeof claimTrack>) => t.steps.map((s) => s.state);

describe('claimTrack', () => {
  it('marks done, current and next for each status', () => {
    expect(stateOf(claimTrack('Submitted'))).toEqual(['current', 'next', 'todo', 'todo']);
    expect(stateOf(claimTrack('UnderReview'))).toEqual(['done', 'current', 'next', 'todo']);
    expect(stateOf(claimTrack('Approved'))).toEqual(['done', 'done', 'current', 'next']);
    expect(stateOf(claimTrack('Settled'))).toEqual(['done', 'done', 'done', 'current']);
    expect(claimTrack('UnderReview').position).toBe('2 of 4');
    expect(claimTrack('Settled').position).toBe('4 of 4');
  });

  it('renders InfoRequested as a dot on Under review, not a step', () => {
    const t = claimTrack('InfoRequested');
    expect(labels(t)).toEqual(['Submitted', 'Under review', 'Decided', 'Closed']);
    expect(t.steps[1].state).toBe('current');
    expect(t.steps[1].dot).toBeTruthy();
    expect(t.position).toBe('2 of 4');
  });

  it('ends a rejected track at an error-tone Decided step', () => {
    const t = claimTrack('Rejected');
    expect(labels(t)).toEqual(['Submitted', 'Under review', 'Rejected']);
    expect(t.steps[2]).toMatchObject({ state: 'current', error: true });
    expect(t.position).toBe('3 of 3');
  });

  it('has a track for every status', () => {
    for (const s of Object.values(ClaimStatus)) expect(claimTrack(s).steps.length).toBeGreaterThan(2);
  });
});

describe('moneyTrack', () => {
  it('staff: follows the reserve, the approved amount and settlement', () => {
    expect(moneyTrack({ status: 'UnderReview', reserveMinor: null }, true).steps[0].state).toBe('current');
    const set = moneyTrack({ status: 'UnderReview', reserveMinor: 100 }, true);
    expect(labels(set)).toEqual(['No reserve', 'Reserve set', 'Approved amount', 'Settled']);
    expect(set.position).toBe('2 of 4');
    expect(moneyTrack({ status: 'Approved', reserveMinor: 100, approvedAmountMinor: 90 }, true).position).toBe(
      '3 of 4',
    );
    expect(moneyTrack({ status: 'Settled', reserveMinor: 100, approvedAmountMinor: 90 }, true).position).toBe('4 of 4');
  });

  it('claimant: never shows a reserve step, only "Assessing"', () => {
    const t = moneyTrack({ status: 'UnderReview', reserveMinor: undefined }, false);
    expect(labels(t)).toEqual(['Assessing', 'Approved amount', 'Settled']);
    expect(t.steps[0].state).toBe('current');
    expect(moneyTrack({ status: 'Approved', approvedAmountMinor: 5 }, false).position).toBe('2 of 3');
  });

  it('a rejected claim ends the money track where it stood', () => {
    expect(labels(moneyTrack({ status: 'Rejected', reserveMinor: 5 }, true))).toEqual(['No reserve', 'Reserve set']);
    expect(labels(moneyTrack({ status: 'Rejected' }, false))).toEqual(['Assessing']);
  });
});

describe('nextStep', () => {
  const officer = actor('u1', 'CLAIMS_ALL', 'CLAIMS_HANDLE');
  const manager = actor('u2', 'CLAIMS_ALL', 'CLAIMS_HANDLE', 'CLAIMS_SETTLE');
  const claimant = actor('c1', 'CLAIMS_SUBMIT', 'CLAIMS_RESPOND');
  const base = {
    assignedOfficerId: 'u1',
    assignedOfficerName: 'Dev Staff',
    reserveMinor: null,
    openInfoRequest: null,
    approvedAmountMinor: null,
  };
  const q = { id: 'q', question: '?', requestedAt: '2026-01-01T00:00:00Z' };

  it('officer, Submitted: Ready with Pick up', () => {
    const n = nextStep({ ...base, status: 'Submitted', assignedOfficerId: null }, officer);
    expect(n.chip).toBe('Ready');
    expect(n.primary?.kind).toBe('pickup');
  });

  it('officer, UnderReview without a reserve: Blocked, the checklist says why, primary is Set reserve', () => {
    const n = nextStep({ ...base, status: 'UnderReview' }, officer);
    expect(n.chip).toBe('Blocked');
    expect(n.primary?.kind).toBe('reserve');
    expect(n.blockers.find((b) => b.label === 'Reserve is set')?.ok).toBe(false);
  });

  it('officer, UnderReview with a reserve: Ready with Approve and a green checklist', () => {
    const n = nextStep({ ...base, status: 'UnderReview', reserveMinor: 500 }, officer);
    expect(n.chip).toBe('Ready');
    expect(n.primary?.kind).toBe('approve');
    expect(n.blockers.every((b) => b.ok)).toBe(true);
  });

  it('another officer, UnderReview: Waiting, no button; a manager may still act', () => {
    const other = actor('u9', 'CLAIMS_ALL', 'CLAIMS_HANDLE');
    const n = nextStep({ ...base, status: 'UnderReview', reserveMinor: 5 }, other);
    expect(n.chip).toBe('Waiting');
    expect(n.primary).toBeUndefined();
    expect(nextStep({ ...base, status: 'UnderReview', reserveMinor: 5 }, manager).primary?.kind).toBe('approve');
  });

  it('staff, InfoRequested: Waiting on claimant, no button', () => {
    const n = nextStep({ ...base, status: 'InfoRequested', openInfoRequest: q }, officer);
    expect(n.chip).toBe('Waiting on claimant');
    expect(n.primary).toBeUndefined();
  });

  it('Approved: the manager settles, an officer waits', () => {
    const c = { ...base, status: 'Approved' as const, reserveMinor: 5, approvedAmountMinor: 4 };
    expect(nextStep(c, manager).primary?.kind).toBe('settle');
    const o = nextStep(c, officer);
    expect(o.chip).toBe('Waiting');
    expect(o.primary).toBeUndefined();
  });

  it('Rejected and Settled are Done for everyone', () => {
    for (const status of ['Rejected', 'Settled'] as const) {
      for (const a of [officer, manager, claimant]) {
        const n = nextStep({ ...base, status }, a);
        expect(n.chip).toBe('Done');
        expect(n.primary).toBeUndefined();
      }
    }
  });

  it('claimant: InfoRequested asks for one answer with the Answer action; other statuses just wait', () => {
    const n = nextStep({ ...base, status: 'InfoRequested', openInfoRequest: q }, claimant);
    expect(n.chip).toBe('Ready');
    expect(n.instruction).toMatch(/one answer/i);
    expect(n.primary?.kind).toBe('answer');
    for (const status of ['Submitted', 'UnderReview', 'Approved'] as const) {
      const w = nextStep({ ...base, status }, claimant);
      expect(w.chip).toBe('Waiting');
      expect(w.primary).toBeUndefined();
    }
  });

  it('never mentions the reserve to a claimant', () => {
    for (const status of Object.values(ClaimStatus)) {
      const n = nextStep({ ...base, status, reserveMinor: 5 }, claimant);
      expect(JSON.stringify(n).toLowerCase()).not.toContain('reserve');
    }
  });

  it('covers every status for every role without throwing', () => {
    for (const status of Object.values(ClaimStatus))
      for (const a of [officer, manager, claimant]) expect(nextStep({ ...base, status }, a).instruction).toBeTruthy();
  });
});
