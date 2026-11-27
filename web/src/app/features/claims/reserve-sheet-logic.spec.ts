import { ClaimDto } from '../../api/models';
import { ApiProblem } from '../../core/http/problem';
import { Actor } from './claims-logic';
import { cellToMinor, diffReserves, errorResult, minorToCell, savedResult, toSheetRow } from './reserve-sheet-logic';

const claim = (over: Partial<ClaimDto> & { id: string }): ClaimDto =>
  ({
    number: `CLM-${over.id}`,
    status: 'UnderReview',
    market: 'SG',
    currency: 'SGD',
    line: 'Motor',
    reserveMinor: 100000,
    assignedOfficerId: 'u1',
    submittedAt: '2026-11-01T00:00:00Z',
    updatedAt: '2026-11-01T00:00:00Z',
    version: 3,
    ...over,
  }) as ClaimDto;

const officer: Actor = { userId: 'u1', permissions: new Set(['CLAIMS_HANDLE']) };
const problem = (status: number, detail: string | null = null) =>
  new ApiProblem(status, 'T', 'c', detail, {}, null, null, null, 'PUT', '/x');

describe('cellToMinor', () => {
  it('reads decimals exactly, never rounding', () => {
    expect(cellToMinor(2500.5)).toBe(250050);
    expect(cellToMinor('1,200.05')).toBe(120005);
    expect(cellToMinor(0.1 + 0.2)).toBe(30); // float noise from a formula is still 0.30
    expect(cellToMinor(12.345)).toBeUndefined();
  });
  it('tells empty from invalid', () => {
    expect(cellToMinor(null)).toBeNull();
    expect(cellToMinor('')).toBeNull();
    for (const bad of [0, -5, 'abc', NaN, {}]) expect(cellToMinor(bad)).toBeUndefined();
  });
  it('round-trips with minorToCell', () => {
    for (const minor of [1, 99, 250050, 1500000000]) expect(cellToMinor(minorToCell(minor))).toBe(minor);
    expect(minorToCell(null)).toBeNull();
  });
});

describe('diffReserves', () => {
  const rows = [
    toSheetRow(claim({ id: 'a', reserveMinor: 100000 }), officer),
    toSheetRow(claim({ id: 'b', reserveMinor: null }), officer),
    toSheetRow(claim({ id: 'c', reserveMinor: 5000 }), officer),
    toSheetRow(claim({ id: 'd', status: 'Submitted', assignedOfficerId: null, reserveMinor: null }), officer),
    toSheetRow(claim({ id: 'e', assignedOfficerId: 'someone-else', reserveMinor: 7000 }), officer),
  ];

  it('makes one command per changed row and none for unchanged rows', () => {
    const { commands, rejected } = diffReserves(rows, [1000, 250.5, 50, null, 70]);
    expect(commands).toEqual([{ kind: 'set', id: 'b', number: 'CLM-b', amountMinor: 25050 }]);
    expect(rejected).toEqual([]);
  });

  it('rejects invalid, cleared and not-editable changes without a command', () => {
    const { commands, rejected } = diffReserves(rows, [1000, null, 12.345, 10, 80]);
    expect(commands).toEqual([]);
    expect(rejected.map((r) => [r.id, r.message.slice(0, 19)])).toEqual([
      ['c', 'Not a valid amount:'],
      ['d', 'You cannot set the '],
      ['e', 'You cannot set the '],
    ]);
  });

  it('never sums or converts across currencies: each command carries its own row amount only', () => {
    const mixed = [
      toSheetRow(claim({ id: 'x', market: 'ID', currency: 'IDR', reserveMinor: 1 }), officer),
      toSheetRow(claim({ id: 'y', market: 'SG', currency: 'SGD', reserveMinor: 1 }), officer),
    ];
    const { commands } = diffReserves(mixed, [15000000, 2]);
    expect(commands.map((c) => c.amountMinor)).toEqual([1500000000, 200]);
  });
});

describe('row results', () => {
  const cmd = { id: 'a', number: 'CLM-a' };
  it('maps a saved claim', () => {
    const c = claim({ id: 'a' });
    expect(savedResult(cmd, c)).toMatchObject({ outcome: 'saved', message: 'Saved', claim: c });
  });
  it('maps 409 to the API guard message', () => {
    expect(errorResult(cmd, problem(409, 'Not assigned to you'))).toMatchObject({
      outcome: 'guard',
      message: 'Not assigned to you',
    });
  });
  it('maps 412 to stale, asking for a reload of that row', () => {
    const r = errorResult(cmd, problem(412));
    expect(r.outcome).toBe('stale');
    expect(r.message).toContain('Row reloaded');
  });
  it('maps anything else to failed', () => {
    expect(errorResult(cmd, problem(500, 'boom'))).toMatchObject({ outcome: 'failed', message: 'boom' });
    expect(errorResult(cmd, new Error('offline'))).toMatchObject({ outcome: 'failed', message: 'offline' });
  });
});
