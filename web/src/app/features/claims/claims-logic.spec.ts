import { ClaimStatus } from '../../api/models';
import {
  ActionState,
  Actor,
  WHAT_NEXT,
  STATUS_LABEL,
  availableActions,
  currencyOf,
  formatMoney,
  localToday,
  marketFlag,
  parseMoney,
} from './claims-logic';

const actor = (userId: string | undefined, ...permissions: string[]): Actor => ({
  userId,
  permissions: new Set(permissions),
});
const names = (a: ActionState[]) => a.map((x) => x.action);

/** Intl puts a no-break space between the code and the number (so an amount never wraps); compare with a plain one. */
const plain = (s: string) => s.replace(/\u00a0/g, ' ');

describe('formatMoney', () => {
  it('shows minor units with two decimals and the currency code, for every market currency', () => {
    expect(plain(formatMoney(250000, 'SGD'))).toBe('SGD 2,500.00');
    expect(plain(formatMoney(250000, 'MYR'))).toBe('MYR 2,500.00');
    expect(plain(formatMoney(250000, 'THB'))).toBe('THB 2,500.00');
    expect(plain(formatMoney(250000, 'HKD'))).toBe('HKD 2,500.00');
    expect(plain(formatMoney(250000, 'PHP'))).toBe('PHP 2,500.00');
  });

  it('does not use Intl default digits for IDR (which has 0): it keeps the two minor digits the API stores', () => {
    expect(plain(formatMoney(123456789, 'IDR'))).toBe('IDR 1,234,567.89');
  });

  it('formats zero, small and missing amounts', () => {
    expect(plain(formatMoney(0, 'SGD'))).toBe('SGD 0.00');
    expect(plain(formatMoney(5, 'SGD'))).toBe('SGD 0.05');
    expect(plain(formatMoney(null, 'SGD'))).toBe('Not set');
    expect(plain(formatMoney(undefined, 'SGD'))).toBe('Not set');
  });
});

describe('parseMoney', () => {
  it('turns what a person types into minor units', () => {
    expect(parseMoney('2500')).toBe(250000);
    expect(parseMoney('2,500.5')).toBe(250050);
    expect(parseMoney(' 0.05 ')).toBe(5);
  });

  it('refuses zero, negatives, more than two decimals and junk', () => {
    for (const bad of ['', '0', '0.00', '-3', '1.234', 'abc', '1e3', '1.']) expect(parseMoney(bad)).toBeNull();
  });
});

describe('currencyOf', () => {
  it('is fixed per market', () => {
    expect(['SG', 'MY', 'TH', 'HK', 'ID', 'PH'].map(currencyOf)).toEqual(['SGD', 'MYR', 'THB', 'HKD', 'IDR', 'PHP']);
  });
});

describe('availableActions (status x role)', () => {
  const officer = actor('u-officer', 'CLAIMS_ALL', 'CLAIMS_HANDLE');
  const manager = actor('u-manager', 'CLAIMS_ALL', 'CLAIMS_HANDLE', 'CLAIMS_SETTLE');
  const claim = (status: ClaimStatus, extra: { assignedOfficerId?: string; reserveMinor?: number } = {}) => ({
    status,
    assignedOfficerId: extra.assignedOfficerId ?? null,
    reserveMinor: extra.reserveMinor ?? null,
  });

  it('Submitted: only pick up', () => {
    expect(names(availableActions(claim('Submitted'), officer))).toEqual(['pickup']);
  });

  it('UnderReview assigned to me: reserve, ask, approve (disabled until a reserve exists), reject', () => {
    const list = availableActions(claim('UnderReview', { assignedOfficerId: 'u-officer' }), officer);
    expect(names(list)).toEqual(['reserve', 'requestInfo', 'approve', 'reject']);
    expect(list.find((a) => a.action === 'approve')).toMatchObject({ enabled: false, hint: 'Set a reserve first' });
    const withReserve = availableActions(
      claim('UnderReview', { assignedOfficerId: 'u-officer', reserveMinor: 1000 }),
      officer,
    );
    expect(withReserve.find((a) => a.action === 'approve')?.enabled).toBe(true);
  });

  it('UnderReview assigned to someone else: an officer sees nothing, a manager may still work it', () => {
    const other = claim('UnderReview', { assignedOfficerId: 'u-other', reserveMinor: 1 });
    expect(availableActions(other, officer)).toEqual([]);
    expect(names(availableActions(other, manager))).toEqual(['reserve', 'requestInfo', 'approve', 'reject']);
  });

  it('InfoRequested: the reserve can still change, nothing else', () => {
    expect(names(availableActions(claim('InfoRequested', { assignedOfficerId: 'u-officer' }), officer))).toEqual([
      'reserve',
    ]);
  });

  it('Approved: only a settler may settle', () => {
    expect(availableActions(claim('Approved'), officer)).toEqual([]);
    expect(names(availableActions(claim('Approved'), manager))).toEqual(['settle']);
  });

  it('terminal statuses and a claimant (no CLAIMS_HANDLE) get no actions', () => {
    expect(availableActions(claim('Rejected'), manager)).toEqual([]);
    expect(availableActions(claim('Settled'), manager)).toEqual([]);
    const claimant = actor('u-self', 'CLAIMS_SUBMIT', 'CLAIMS_READ', 'CLAIMS_RESPOND');
    for (const s of Object.values(ClaimStatus)) expect(availableActions(claim(s), claimant)).toEqual([]);
  });
});

describe('claimant "what happens next"', () => {
  it('has a plain sentence and a label for every status', () => {
    for (const s of Object.values(ClaimStatus)) {
      expect(WHAT_NEXT[s].length).toBeGreaterThan(20);
      expect(STATUS_LABEL[s]).toBeTruthy();
    }
  });

  it('asks the claimant to answer only when information is requested', () => {
    expect(WHAT_NEXT.InfoRequested).toMatch(/answer the question/i);
    for (const s of Object.values(ClaimStatus).filter((x) => x !== 'InfoRequested'))
      expect(WHAT_NEXT[s]).not.toMatch(/answer the question/i);
  });
});

describe('localToday', () => {
  it('is the local calendar day', () => {
    expect(localToday(new Date(2026, 10, 7, 23, 59))).toBe('2026-11-07');
  });
});

describe('marketFlag', () => {
  it('gives each of the six markets its flag and an unknown code nothing', () => {
    expect(['HK', 'ID', 'MY', 'PH', 'SG', 'TH'].map(marketFlag)).toEqual(['🇭🇰', '🇮🇩', '🇲🇾', '🇵🇭', '🇸🇬', '🇹🇭']);
    expect(marketFlag('XX')).toBe('');
  });
});
