import type { ImportCheckDto, ImportFileDto } from '../../api/generated/model';
import {
  DraftRow,
  createBody,
  draftsFromFile,
  editValue,
  guessMapping,
  otherErrors,
  rowsToDrafts,
  stateOf,
  summarise,
  toInput,
  withVerdict,
} from './import-logic';

const HEADERS = ['Claimant email', 'Market', 'Line', 'Policy no.', 'Date of incident', 'Where', 'What happened'];
const KEY = 'a'.repeat(64);

const file = (over: Partial<ImportFileDto> = {}): ImportFileDto => ({
  source: 'Spreadsheet',
  fileName: 'claims.csv',
  fileSha256: 'b'.repeat(64),
  drafts: [],
  sheet: {
    headers: HEADERS,
    rows: [
      {
        number: 2,
        key: KEY,
        cells: ['self@fugue.test', 'Singapore', 'Motor', 'POL-SG-1', '20/11/2026', 'Orchard Road', 'Dent.'],
      },
      {
        number: 3,
        key: 'c'.repeat(64),
        cells: ['self2@fugue.test', 'MY', 'Property', 'POL-MY-2', '2026-11-18', 'KL', 'Leak.'],
        imported: { claimId: 'id-1', claimNumber: 'CLM-MY-2026-000001' },
      },
    ],
  },
  ...over,
});

const ready = (key = KEY): ImportCheckDto => ({
  key,
  ready: true,
  market: 'SG',
  line: 'Motor',
  incidentDate: '2026-11-20',
  errors: {},
  claimant: { id: 'u-1', displayName: 'Dev Self-Service One' },
});

describe('import logic', () => {
  it('guesses each column from the header words, one column per field', () => {
    expect(guessMapping(HEADERS)).toEqual({
      claimantEmail: 0,
      market: 1,
      line: 2,
      policyNumber: 3,
      incidentDate: 4,
      incidentLocation: 5,
      incidentDescription: 6,
    });
    const odd = guessMapping(['Email', 'Date', 'Country', 'Narrative']);
    expect(odd.claimantEmail).toBe(0);
    expect(odd.incidentDate).toBe(1);
    expect(odd.market).toBe(2);
    expect(odd.incidentDescription).toBe(3);
    expect(odd.policyNumber).toBe(-1);
  });

  it('turns rows into drafts through the mapping; a row imported before starts excluded and marked', () => {
    const rows = rowsToDrafts(file().sheet!, guessMapping(HEADERS));
    expect(rows[0].item).toBe('Row 2');
    expect(rows[0].values.market).toBe('Singapore');
    expect(rows[0].include).toBe(true);
    expect(stateOf(rows[0])).toBe('unchecked');
    expect(rows[1].include).toBe(false);
    expect(stateOf(rows[1])).toBe('imported');
  });

  it('applies a verdict: normalised values replace the raw text, an edit clears the verdict', () => {
    const [row] = rowsToDrafts(file().sheet!, guessMapping(HEADERS));
    const checked = withVerdict(row, ready());
    expect(checked.values.market).toBe('SG');
    expect(checked.values.incidentDate).toBe('2026-11-20');
    expect(stateOf(checked)).toBe('ready');
    const edited = editValue(checked, 'policyNumber', 'POL-SG-9');
    expect(edited.verdict).toBeNull();
    expect(stateOf(edited)).toBe('unchecked');
  });

  it('a verdict that says the item was imported marks the draft and drops it from the batch', () => {
    const [row] = rowsToDrafts(file().sheet!, guessMapping(HEADERS));
    const done = withVerdict(row, {
      ...ready(),
      ready: false,
      imported: { claimId: 'id-9', claimNumber: 'CLM-SG-2026-000009' },
    });
    expect(stateOf(done)).toBe('imported');
    expect(done.include).toBe(false);
    expect(done.claim?.number).toBe('CLM-SG-2026-000009');
  });

  it('keeps errors on fields the screen does not show', () => {
    const [row] = rowsToDrafts(file().sheet!, guessMapping(HEADERS));
    const bad = withVerdict(row, {
      ...ready(),
      ready: false,
      errors: { policyNumber: ['x'], somethingNew: ['A new rule.'] },
    });
    expect(otherErrors(bad)).toEqual(['A new rule.']);
    expect(stateOf(bad)).toBe('problems');
  });

  it('counts what the Create button will do', () => {
    const rows = rowsToDrafts(file().sheet!, guessMapping(HEADERS));
    const s = summarise([withVerdict(rows[0], ready()), rows[1]]);
    expect(s).toMatchObject({ total: 2, ready: 1, toCreate: 1, imported: 1, problems: 0, unchecked: 0 });
    expect(summarise([{ ...withVerdict(rows[0], ready()), include: false }]).toCreate).toBe(0);
  });

  it('builds the create body from the parse identity and the normalised values, and refuses an unchecked draft', () => {
    const f = file();
    const [row] = rowsToDrafts(f.sheet!, guessMapping(HEADERS));
    expect(() => createBody(f, row)).toThrow();
    const body = createBody(f, withVerdict(row, ready()));
    expect(body).toMatchObject({
      source: 'Spreadsheet',
      key: KEY,
      fileName: 'claims.csv',
      fileSha256: 'b'.repeat(64),
      item: 'Row 2',
      market: 'SG',
      line: 'Motor',
      incidentDate: '2026-11-20',
      policyNumber: 'POL-SG-1',
      claimantEmail: 'self@fugue.test',
      incidentLocation: 'Orchard Road',
    });
  });

  it('email and chat drafts keep their hints and note, and blank fields go to the check as null', () => {
    const [d]: DraftRow[] = draftsFromFile([
      {
        key: KEY,
        item: 'Chat with +65 9123 4567, 7 messages',
        market: 'SG',
        line: null,
        policyNumber: 'POL-SG-300301',
        incidentDate: '2026-11-27',
        incidentDescription: 'Scraped in the carpark.',
        incidentLocation: null,
        claimantEmail: 'self@fugue.test',
        claimantName: '+65 9123 4567',
        note: 'WhatsApp chat export ...',
        hints: ['Market SG from the sender’s calling code +65.'],
      },
    ]);
    expect(d.note).toBe('WhatsApp chat export ...');
    expect(d.hints).toHaveLength(1);
    expect(toInput(d)).toMatchObject({ line: null, incidentLocation: null, market: 'SG' });
  });
});
