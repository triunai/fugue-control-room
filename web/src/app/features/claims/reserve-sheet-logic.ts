import { ClaimDto, ClaimStatus } from '../../api/models';
import { ApiProblem } from '../../core/http/problem';
import { Actor, availableActions } from './claims-logic';

/** The statuses that count as "open" on the sheet and the calendar: everything not yet rejected or settled. */
export const OPEN_STATUSES: readonly ClaimStatus[] = ['Submitted', 'UnderReview', 'InfoRequested', 'Approved'];

/** Column order of the sheet. Only RESERVE_COL is editable. */
export const SHEET_COLUMNS = [
  'Claim',
  'Market',
  'Currency',
  'Line',
  'Status',
  'Officer',
  'Reserve',
  'Age (days)',
] as const;
export const RESERVE_COL = 6;

/** One sheet row: the claim as loaded plus the reserve in MAJOR units of the claim's own currency (a decimal), or null. */
export interface SheetRow {
  claim: ClaimDto;
  /** Whether this caller may set the reserve on this claim now (the API still decides). */
  editable: boolean;
}

export function toSheetRow(claim: ClaimDto, actor: Actor): SheetRow {
  return { claim, editable: availableActions(claim, actor).some((a) => a.action === 'reserve' && a.enabled) };
}

/** Minor units -> the decimal shown in the cell (250050 -> 2500.5). Exact: minor / 100 has at most two decimals. */
export function minorToCell(minor: number | null | undefined): number | null {
  return minor === null || minor === undefined ? null : minor / 100;
}

/**
 * A cell value -> minor units. `undefined` means "the cell is not a valid reserve" (text, negative, zero, more than two
 * decimals); `null` means the cell is empty. Never rounds: 12.345 is invalid, not 12.35.
 */
export function cellToMinor(cell: unknown): number | null | undefined {
  if (cell === null || cell === undefined || cell === '') return null;
  const n = typeof cell === 'number' ? cell : typeof cell === 'string' ? Number(cell.replace(/,/g, '').trim()) : NaN;
  if (!Number.isFinite(n) || n <= 0) return undefined;
  const minor = Math.round(n * 100);
  if (Math.abs(n * 100 - minor) > 1e-6 || !Number.isSafeInteger(minor)) return undefined;
  return minor;
}

/** One `PUT /claims/{id}/reserve` to send (the If-Match comes from the ETag store), or a row that cannot be sent. */
export interface ReserveCommand {
  kind: 'set';
  id: string;
  number: string;
  amountMinor: number;
}
export interface RowRejection {
  kind: 'rejected';
  id: string;
  number: string;
  message: string;
}

/**
 * The rows whose reserve cell differs from the loaded claim, as commands. One command per changed row.
 * A change on a row the caller may not edit, a cleared cell (the API has no "unset reserve"), or an invalid number is
 * rejected here with a message and never sent.
 */
export function diffReserves(
  rows: readonly SheetRow[],
  cells: readonly unknown[],
): { commands: ReserveCommand[]; rejected: RowRejection[] } {
  const commands: ReserveCommand[] = [];
  const rejected: RowRejection[] = [];
  rows.forEach((row, i) => {
    const { id, number } = row.claim;
    const minor = cellToMinor(cells[i]);
    const before = row.claim.reserveMinor ?? null;
    if (minor === before) return;
    if (minor === undefined) {
      rejected.push({
        kind: 'rejected',
        id,
        number,
        message: 'Not a valid amount: use a positive number with at most two decimals.',
      });
    } else if (minor === null) {
      rejected.push({ kind: 'rejected', id, number, message: 'A reserve cannot be cleared; enter a new amount.' });
    } else if (!row.editable) {
      rejected.push({
        kind: 'rejected',
        id,
        number,
        message: 'You cannot set the reserve on this claim in its current status.',
      });
    } else {
      commands.push({ kind: 'set', id, number, amountMinor: minor });
    }
  });
  return { commands, rejected };
}

export type RowResult =
  | { id: string; number: string; outcome: 'saved'; message: string; claim: ClaimDto }
  | { id: string; number: string; outcome: 'guard'; message: string }
  | { id: string; number: string; outcome: 'stale'; message: string }
  | { id: string; number: string; outcome: 'failed'; message: string };

/** The result line for a saved row. */
export function savedResult(cmd: { id: string; number: string }, claim: ClaimDto): RowResult {
  return { id: cmd.id, number: cmd.number, outcome: 'saved', message: 'Saved', claim };
}

/** Maps a failed command to a per-row result: 409 -> the API's own guard message, 412 -> stale (reload that row), anything else -> failed. */
export function errorResult(cmd: { id: string; number: string }, error: unknown): RowResult {
  const base = { id: cmd.id, number: cmd.number };
  if (error instanceof ApiProblem) {
    if (error.status === 409) return { ...base, outcome: 'guard', message: error.detail ?? error.title };
    if (error.status === 412) {
      return {
        ...base,
        outcome: 'stale',
        message: 'Changed by someone else since you loaded it. Row reloaded; enter your amount again.',
      };
    }
    return { ...base, outcome: 'failed', message: error.detail ?? error.title };
  }
  return { ...base, outcome: 'failed', message: error instanceof Error ? error.message : 'Could not save.' };
}
