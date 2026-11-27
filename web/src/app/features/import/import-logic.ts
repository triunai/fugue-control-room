import type {
  CreateImportedClaimRequest,
  ImportCheckDto,
  ImportDraftDto,
  ImportDraftInput,
  ImportFileDto,
  ImportSheetDto,
  ImportSource,
} from '../../api/generated/model';

/** The claim fields a draft carries, as raw text the reviewer can edit (the server normalises and checks them). */
export interface DraftValues {
  claimantEmail: string;
  market: string;
  line: string;
  policyNumber: string;
  incidentDate: string;
  incidentLocation: string;
  incidentDescription: string;
}

export type DraftField = keyof DraftValues;

/** One draft on the review screen. `verdict` is the last server check of exactly these values; null after an edit. */
export interface DraftRow {
  key: string;
  item: string;
  values: DraftValues;
  claimantName: string | null;
  note: string | null;
  hints: string[];
  include: boolean;
  verdict: ImportCheckDto | null;
  /** Set when the item was imported before (from the parse or the check) or created on this screen. */
  claim: { id: string; number: string; now: boolean } | null;
  createError: string | null;
}

export type DraftState = 'created' | 'imported' | 'unchecked' | 'ready' | 'problems';

/** The three file sources and what each one stands in for. Labels say plainly that these are files, not connectors. */
export const SOURCES: readonly {
  id: ImportSource;
  label: string;
  accept: string;
  file: string;
  standsInFor: string;
  howTo: string;
}[] = [
  {
    id: 'Spreadsheet',
    label: 'Spreadsheet',
    accept: '.csv,.xlsx',
    file: '.csv or .xlsx',
    standsInFor: 'a shared claims workbook',
    howTo: 'One claim per row, a header row first. You map the columns before anything is checked.',
  },
  {
    id: 'Email',
    label: 'Shared inbox',
    accept: '.eml,.mbox',
    file: '.eml or .mbox export',
    standsInFor: 'a Microsoft Graph or Gmail inbox connector',
    howTo:
      'Save a message as .eml, or export a folder as .mbox. The sender domain picks the market (Import:MarketByDomain).',
  },
  {
    id: 'WhatsApp',
    label: 'WhatsApp',
    accept: '.txt',
    file: 'chat export .txt',
    standsInFor: 'a WhatsApp Business connector',
    howTo: 'In the chat, More > Export chat > Without media. The transcript is attached to the claim as a note.',
  },
];

export const FIELDS: readonly { id: DraftField; label: string; required: boolean }[] = [
  { id: 'claimantEmail', label: 'Claimant email', required: true },
  { id: 'market', label: 'Market', required: true },
  { id: 'line', label: 'Line of cover', required: true },
  { id: 'policyNumber', label: 'Policy number', required: true },
  { id: 'incidentDate', label: 'Incident date', required: true },
  { id: 'incidentLocation', label: 'Location', required: false },
  { id: 'incidentDescription', label: 'What happened', required: true },
];

/** Header words that suggest a column, most specific first. */
const HEADER_WORDS: Record<DraftField, string[]> = {
  claimantEmail: ['claimant email', 'insured email', 'customer email', 'email', 'e-mail'],
  market: ['market', 'country'],
  line: ['line of cover', 'line of business', 'line', 'cover', 'type'],
  policyNumber: ['policy number', 'policy no', 'policy'],
  incidentDate: ['date of incident', 'incident date', 'date of loss', 'loss date', 'date'],
  incidentLocation: ['location', 'where', 'place', 'address'],
  incidentDescription: ['what happened', 'description', 'details', 'narrative', 'incident'],
};

export type ColumnMapping = Record<DraftField, number>;

/** Guesses which column holds each field from the header words; -1 when none matches. A column is used for one field at most. */
export function guessMapping(headers: readonly string[]): ColumnMapping {
  const lower = headers.map((h) => h.trim().toLowerCase());
  const taken = new Set<number>();
  const mapping = {} as ColumnMapping;
  for (const f of FIELDS) {
    let found = -1;
    for (const word of HEADER_WORDS[f.id]) {
      found = lower.findIndex((h, i) => !taken.has(i) && h.includes(word));
      if (found >= 0) break;
    }
    if (found >= 0) taken.add(found);
    mapping[f.id] = found;
  }
  return mapping;
}

export function emptyValues(): DraftValues {
  return {
    claimantEmail: '',
    market: '',
    line: '',
    policyNumber: '',
    incidentDate: '',
    incidentLocation: '',
    incidentDescription: '',
  };
}

/** Spreadsheet rows become drafts through the mapping; a row imported before starts excluded and marked. */
export function rowsToDrafts(sheet: ImportSheetDto, mapping: ColumnMapping): DraftRow[] {
  return sheet.rows.map((r) => {
    const values = emptyValues();
    for (const f of FIELDS) {
      const i = mapping[f.id];
      values[f.id] = i >= 0 ? (r.cells[i] ?? '').trim() : '';
    }
    return {
      key: r.key,
      item: `Row ${r.number}`,
      values,
      claimantName: null,
      note: null,
      hints: [],
      include: !r.imported,
      verdict: null,
      claim: r.imported ? { id: r.imported.claimId, number: r.imported.claimNumber, now: false } : null,
      createError: null,
    };
  });
}

/** Email and chat drafts as the server read them. */
export function draftsFromFile(drafts: readonly ImportDraftDto[]): DraftRow[] {
  return drafts.map((d) => ({
    key: d.key,
    item: d.item,
    values: {
      claimantEmail: d.claimantEmail ?? '',
      market: d.market ?? '',
      line: d.line ?? '',
      policyNumber: d.policyNumber ?? '',
      incidentDate: d.incidentDate ?? '',
      incidentLocation: d.incidentLocation ?? '',
      incidentDescription: d.incidentDescription ?? '',
    },
    claimantName: d.claimantName ?? null,
    note: d.note ?? null,
    hints: [...d.hints],
    include: !d.imported,
    verdict: null,
    claim: d.imported ? { id: d.imported.claimId, number: d.imported.claimNumber, now: false } : null,
    createError: null,
  }));
}

export function toInput(row: DraftRow): ImportDraftInput {
  const v = row.values;
  return {
    key: row.key,
    claimantEmail: v.claimantEmail.trim() || null,
    market: v.market.trim() || null,
    line: v.line.trim() || null,
    policyNumber: v.policyNumber.trim() || null,
    incidentDate: v.incidentDate.trim() || null,
    incidentLocation: v.incidentLocation.trim() || null,
    incidentDescription: v.incidentDescription.trim() || null,
  };
}

/**
 * Applies a check verdict: an item the server says was imported is marked; the normalised market, line and date replace what the reviewer
 * typed (so "Singapore" becomes SG and 20/11/2026 becomes 2026-11-20) only where they parsed.
 */
export function withVerdict(row: DraftRow, verdict: ImportCheckDto): DraftRow {
  const values = { ...row.values };
  if (verdict.market) values.market = verdict.market;
  if (verdict.line) values.line = verdict.line;
  if (verdict.incidentDate) values.incidentDate = verdict.incidentDate;
  const claim =
    row.claim ??
    (verdict.imported ? { id: verdict.imported.claimId, number: verdict.imported.claimNumber, now: false } : null);
  return { ...row, values, verdict, claim, include: row.include && !claim };
}

export function editValue(row: DraftRow, field: DraftField, value: string): DraftRow {
  return { ...row, values: { ...row.values, [field]: value }, verdict: null, createError: null };
}

export function stateOf(row: DraftRow): DraftState {
  if (row.claim?.now) return 'created';
  if (row.claim) return 'imported';
  if (!row.verdict) return 'unchecked';
  return row.verdict.ready ? 'ready' : 'problems';
}

export function fieldErrors(row: DraftRow, field: DraftField): string[] {
  return row.verdict?.errors?.[field] ?? [];
}

/** Errors on fields the screen does not show (a rule the server added): shown on the draft, never dropped. */
export function otherErrors(row: DraftRow): string[] {
  const shown = new Set<string>(FIELDS.map((f) => f.id));
  return Object.entries(row.verdict?.errors ?? {})
    .filter(([k]) => !shown.has(k))
    .flatMap(([, v]) => v);
}

export interface DraftSummary {
  total: number;
  ready: number;
  toCreate: number;
  problems: number;
  unchecked: number;
  imported: number;
  created: number;
}

export function summarise(rows: readonly DraftRow[]): DraftSummary {
  const s: DraftSummary = {
    total: rows.length,
    ready: 0,
    toCreate: 0,
    problems: 0,
    unchecked: 0,
    imported: 0,
    created: 0,
  };
  for (const r of rows) {
    const st = stateOf(r);
    if (st === 'ready') {
      s.ready++;
      if (r.include) s.toCreate++;
    } else if (st === 'problems') s.problems++;
    else if (st === 'unchecked') s.unchecked++;
    else if (st === 'imported') s.imported++;
    else s.created++;
  }
  return s;
}

/** The create body for a ready draft: the item's identity from the parse, the claim fields as the check normalised them. */
export function createBody(file: ImportFileDto, row: DraftRow): CreateImportedClaimRequest {
  const v = row.values;
  const verdict = row.verdict;
  if (!verdict?.ready || !verdict.market || !verdict.line || !verdict.incidentDate) {
    throw new Error(`Draft ${row.key} is not ready to create.`);
  }
  return {
    source: file.source,
    key: row.key,
    fileName: file.fileName,
    fileSha256: file.fileSha256,
    item: row.item,
    market: verdict.market,
    line: verdict.line,
    policyNumber: v.policyNumber.trim(),
    incidentDate: verdict.incidentDate,
    incidentDescription: v.incidentDescription.trim(),
    incidentLocation: v.incidentLocation.trim() || null,
    claimantEmail: v.claimantEmail.trim(),
    note: row.note,
  };
}
