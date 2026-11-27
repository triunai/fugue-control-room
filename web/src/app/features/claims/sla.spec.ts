import { ClaimDto } from '../../api/models';
import { SLA_DAYS, isOverdue, slaDue } from './sla';
import { calendarEvents, claimsDueOn, dayKey } from './sla-events';
import { STATUS_TONE } from './claims-logic';

const base = { submittedAt: '2026-11-01T08:00:00.000Z', updatedAt: '2026-11-05T08:00:00.000Z' };
const claim = (over: Partial<ClaimDto>): ClaimDto =>
  ({ id: 'c', number: 'CLM-1', ...base, status: 'Submitted', ...over }) as ClaimDto;

describe('slaDue', () => {
  it('Submitted: pickup 2 days after submit', () => {
    expect(slaDue(claim({}))).toEqual({ kind: 'pickup', label: 'Pickup due', due: '2026-11-03T08:00:00.000Z' });
  });
  it('UnderReview: decision 14 days after submit', () => {
    expect(slaDue(claim({ status: 'UnderReview' }))).toMatchObject({
      kind: 'decision',
      due: '2026-11-15T08:00:00.000Z',
    });
  });
  it('InfoRequested: answer 7 days after the question, else after the last update', () => {
    const asked = claim({
      status: 'InfoRequested',
      openInfoRequest: { id: 'q', question: '?', requestedAt: '2026-11-10T00:00:00.000Z' },
    });
    expect(slaDue(asked)).toMatchObject({ kind: 'answer', due: '2026-11-17T00:00:00.000Z' });
    expect(slaDue(claim({ status: 'InfoRequested' }))).toMatchObject({
      kind: 'answer',
      due: '2026-11-12T08:00:00.000Z',
    });
  });
  it('Approved: settlement 7 days after the decision', () => {
    const c = claim({ status: 'Approved', decidedAt: '2026-11-20T00:00:00.000Z' });
    expect(slaDue(c)).toMatchObject({ kind: 'settle', due: '2026-11-27T00:00:00.000Z' });
  });
  it('has nothing due once a claim is closed', () => {
    expect(slaDue(claim({ status: 'Rejected' }))).toBeNull();
    expect(slaDue(claim({ status: 'Settled' }))).toBeNull();
  });
  it('keeps every constant in SLA_DAYS', () => {
    expect(SLA_DAYS).toEqual({ pickup: 2, decision: 14, answer: 7, settle: 7 });
  });
});

describe('isOverdue', () => {
  it('is true only after the due instant', () => {
    const due = '2026-11-03T08:00:00.000Z';
    expect(isOverdue(due, Date.parse('2026-11-03T08:00:01Z'))).toBe(true);
    expect(isOverdue(due, Date.parse('2026-11-03T07:59:59Z'))).toBe(false);
  });
});

describe('calendarEvents', () => {
  it('makes one coloured event per open claim, flags overdue, skips closed claims', () => {
    const now = Date.parse('2026-11-10T00:00:00Z');
    const events = calendarEvents(
      [claim({ id: 'a' }), claim({ id: 'b', status: 'UnderReview' }), claim({ id: 'z', status: 'Settled' })],
      STATUS_TONE,
      now,
    );
    expect(events.map((e) => e.id)).toEqual(['a', 'b']);
    expect(events[0].className).toBe('sla-ev sla-muted sla-kind-pickup sla-overdue');
    expect(events[1].className).toBe('sla-ev sla-acc sla-kind-decision');
    expect(events.map((e) => e.color)).toEqual(['var(--muted)', 'var(--accent)']);
    expect(events[0].title).toBe('CLM-1 · Pickup due');
  });
});

describe('claimsDueOn', () => {
  const claims = [
    claim({ id: 'a', number: 'CLM-A' }), // pickup due 2026-11-03T08:00Z
    claim({ id: 'b', number: 'CLM-B', status: 'UnderReview' }), // decision due 2026-11-15
    claim({ id: 'c', number: 'CLM-C', status: 'Rejected' }), // nothing due
  ];
  const now = Date.parse('2026-11-10T00:00:00Z');

  it('lists the claims due on that local day with what is due', () => {
    const day = dayKey(new Date('2026-11-03T08:00:00.000Z'));
    expect(claimsDueOn(claims, day, now)).toEqual([
      { id: 'a', number: 'CLM-A', status: 'Submitted', what: 'Pickup due', overdue: true },
    ]);
  });

  it('is empty for a day with nothing due ("Nothing due")', () => {
    expect(claimsDueOn(claims, '2026-12-25', now)).toEqual([]);
  });

  it('formats the local day as YYYY-MM-DD', () => {
    expect(dayKey(new Date(2026, 0, 5, 23, 30))).toBe('2026-01-05');
  });
});
