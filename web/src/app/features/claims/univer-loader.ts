import { type FUniver, LocaleType, createUniver, mergeLocales } from '@univerjs/presets';
import { UniverSheetsCorePreset } from '@univerjs/preset-sheets-core';
import sheetsCoreEnUS from '@univerjs/preset-sheets-core/locales/en-US';
import {
  RESERVE_COLUMN_WIDTHS,
  ASSESSMENT_COLUMN_WIDTHS,
  HEADER_ROW_HEIGHT,
  ROW_HEIGHT,
  RESERVES_SHEET,
  SUMMARY_SHEET,
  sheetRowCount,
} from './sheet-layout';

/** What the page keeps: the facade to read and write cells, and a dispose that tears the whole instance down. */
export interface UniverHandle {
  dispose(): void;
  univerAPI: FUniver;
}

/** A CSS token's value, so the sheet takes its colours from tokens.css like the rest of the console. */
function token(name: string): string {
  return getComputedStyle(document.documentElement).getPropertyValue(name).trim();
}

/** Footer for a full workbook (sheet tabs, the selection's count/sum bar, zoom), like a desktop spreadsheet. */
const WORKBOOK_FOOTER = { sheetBar: true, statisticBar: true, menus: false, zoomSlider: true } as const;

/**
 * A Univer instance with the single-row toolbar (no Start / Formulas / View tabs), formula bar and context menu.
 * `footer` adds the sheet tabs; the small assessment sheet inside a claim page leaves it off.
 */
function boot(container: HTMLElement, footer: false | typeof WORKBOOK_FOOTER = false) {
  return createUniver({
    locale: LocaleType.EN_US,
    locales: { [LocaleType.EN_US]: mergeLocales(sheetsCoreEnUS) },
    presets: [
      UniverSheetsCorePreset({
        container,
        header: true,
        toolbar: true,
        ribbonType: 'simple',
        footer,
        contextMenu: true,
      }),
    ],
  });
}

/** Blank columns after the data so the grid fills the frame like a spreadsheet, not a cut-off table. */
const SPARE_COLUMNS = 8;

/** A1 column letter for a 0-based column (A..Z is enough here). */
const col = (c: number) => String.fromCharCode(65 + c);

/** The console's faces on the canvas (the variable fonts are already loaded by the app), vertically centred, a little padding. */
const UI_FONT = 'Inter Variable';
const MONO_FONT = 'JetBrains Mono Variable';
const base = { ff: UI_FONT, fs: 11, vt: 2, pd: { l: 6, r: 6 } };
/** Header band: the accent colour with light text, like the console's own table heads, not a grey row. */
function headStyle(right: boolean) {
  return {
    ...base,
    bl: 1,
    bg: { rgb: token('--accent') },
    cl: { rgb: token('--on-accent') },
    ...(right ? { ht: 3 } : {}),
  };
}
/** Row geometry shared by both sheets. */
const rowGeometry = { defaultRowHeight: ROW_HEIGHT, rowData: { 0: { h: HEADER_ROW_HEIGHT } } };

/**
 * Mounts a Univer workbook into `container` and returns a handle to dispose it. This module (and Univer with it) is only
 * reached through a dynamic import from the reserve sheet page, so it lands in that route's lazy chunks.
 * Only column `editableCol` can be edited: edits that start anywhere else are cancelled.
 */
export function createSheet(
  container: HTMLElement,
  header: readonly string[],
  body: readonly (string | number | null)[][],
  editableCol: number,
  onEdited: () => void,
): UniverHandle {
  const { univer, univerAPI } = boot(container, WORKBOOK_FOOTER);

  const cellData: Record<number, Record<number, object>> = {};
  const text = token('--text');
  const muted = token('--muted');
  const input = token('--sheet-input');
  const put = (r: number, c: number, cell: object) => ((cellData[r] ??= {})[c] = cell);
  header.forEach((h, c) => put(0, c, { v: h, s: headStyle(c >= editableCol) }));
  body.forEach((row, i) =>
    row.forEach((v, c) => {
      if (v === null) return;
      const reserve = c === editableCol;
      put(i + 1, c, {
        v,
        t: typeof v === 'number' ? 2 : 1,
        s: {
          ...base,
          ...(c === 0 ? { ff: MONO_FONT } : {}),
          cl: { rgb: reserve ? text : muted },
          ...(reserve ? { bg: { rgb: input }, n: { pattern: '#,##0.00' } } : {}),
          ...(c >= editableCol ? { ht: 3 } : {}),
        },
      });
    }),
  );

  // Summary sheet: one row per currency with live formulas over the claims sheet, so totals move as reserves are typed.
  // Amounts are only ever summed within one currency (the brief's rule; the claims sheet never adds across currencies).
  const currencyCol = header.indexOf('Currency');
  const last = body.length + 1;
  const ref = (c: number) => `'Open claims'!${col(c)}2:${col(c)}${last}`;
  const currencies = [...new Set(body.map((r) => String(r[currencyCol] ?? '')).filter(Boolean))].sort();
  const sumHead = ['Currency', 'Open claims', 'With a reserve', 'Total reserve'];
  const summary: Record<number, Record<number, object>> = {};
  const sput = (r: number, c: number, cell: object) => ((summary[r] ??= {})[c] = cell);
  sumHead.forEach((h, c) => sput(0, c, { v: h, s: headStyle(c > 0) }));
  currencies.forEach((cur, i) => {
    const r = i + 1;
    const a = `A${r + 1}`;
    sput(r, 0, { v: cur, s: { ...base, bl: 1, cl: { rgb: text } } });
    sput(r, 1, { f: `=COUNTIF(${ref(currencyCol)},${a})`, s: { ...base, cl: { rgb: muted }, ht: 3 } });
    sput(r, 2, {
      f: `=COUNTIFS(${ref(currencyCol)},${a},${ref(editableCol)},"<>")`,
      s: { ...base, cl: { rgb: muted }, ht: 3 },
    });
    sput(r, 3, {
      f: `=SUMIF(${ref(currencyCol)},${a},${ref(editableCol)})`,
      s: { ...base, bl: 1, cl: { rgb: text }, n: { pattern: '#,##0.00' }, ht: 3 },
    });
  });

  univerAPI.createWorkbook({
    id: 'reserves',
    name: 'Reserves',
    sheetOrder: [RESERVES_SHEET, SUMMARY_SHEET],
    sheets: {
      [RESERVES_SHEET]: {
        id: RESERVES_SHEET,
        name: 'Open claims',
        rowCount: sheetRowCount(body.length),
        columnCount: header.length + SPARE_COLUMNS,
        ...rowGeometry,
        freeze: { xSplit: 1, ySplit: 1, startRow: 1, startColumn: 1 },
        columnData: Object.fromEntries(RESERVE_COLUMN_WIDTHS.map((width, c) => [c, { w: width }])),
        cellData,
      },
      [SUMMARY_SHEET]: {
        id: SUMMARY_SHEET,
        name: 'By currency',
        rowCount: Math.max(sheetRowCount(currencies.length), sheetRowCount(body.length)),
        columnCount: sumHead.length + SPARE_COLUMNS,
        ...rowGeometry,
        freeze: { xSplit: 0, ySplit: 1, startRow: 1, startColumn: 0 },
        columnData: { 0: { w: 110 }, 1: { w: 120 }, 2: { w: 130 }, 3: { w: 160 } },
        cellData: summary,
      },
    },
  });

  const subs = [
    // The claims sheet takes input only in the reserve column and the summary is formulas only. A sheet the user adds
    // with "+" is free scratch space: it is never read back or saved.
    univerAPI.addEvent(
      univerAPI.Event.BeforeSheetEditStart,
      (p: { column: number; row: number; cancel?: boolean; worksheet?: { getSheetId(): string } }) => {
        const sheet = p.worksheet?.getSheetId() ?? RESERVES_SHEET;
        if (sheet === SUMMARY_SHEET) p.cancel = true;
        if (sheet === RESERVES_SHEET && (p.column !== editableCol || p.row === 0)) p.cancel = true;
      },
    ),
    univerAPI.addEvent(univerAPI.Event.SheetEditEnded, onEdited),
    univerAPI.addEvent(univerAPI.Event.ClipboardPasted, onEdited),
  ];

  return {
    univerAPI,
    dispose() {
      subs.forEach((s: { dispose(): void }) => s.dispose());
      univerAPI.dispose();
      univer.dispose();
    },
  };
}

/**
 * The claim assessment worksheet: labels in column A, amounts in column B. `formulas` maps a row to its live formula
 * (Gross, Net: Univer computes them); only cells where `editable(row, col)` is true accept edits.
 */
export function createAssessmentSheet(
  container: HTMLElement,
  currency: string,
  labels: readonly string[],
  formulas: Readonly<Record<number, string>>,
  editable: (row: number, col: number) => boolean,
  onEdited: () => void,
): UniverHandle {
  const { univer, univerAPI } = boot(container);
  const text = token('--text');
  const muted = token('--muted');
  const input = token('--sheet-input');
  const cellData: Record<number, Record<number, object>> = {
    0: {
      0: { v: 'Item', s: headStyle(false) },
      1: { v: `Amount (${currency})`, s: headStyle(true) },
    },
  };
  labels.forEach((label, i) => {
    const row = i + 1;
    const total = formulas[row] !== undefined;
    cellData[row] = {
      0: { v: label, s: { ...base, cl: { rgb: total ? text : muted }, ...(total ? { bl: 1 } : {}) } },
      1: total
        ? { f: formulas[row], s: { ...base, bl: 1, n: { pattern: '#,##0.00' }, cl: { rgb: text }, ht: 3 } }
        : { s: { ...base, bg: { rgb: input }, n: { pattern: '#,##0.00' }, cl: { rgb: text }, ht: 3 } },
    };
  });
  univerAPI.createWorkbook({
    id: 'assessment',
    name: 'Assessment',
    sheetOrder: ['s1'],
    sheets: {
      s1: {
        id: 's1',
        name: 'Assessment',
        rowCount: sheetRowCount(labels.length),
        columnCount: 2,
        ...rowGeometry,
        columnData: Object.fromEntries(ASSESSMENT_COLUMN_WIDTHS.map((width, c) => [c, { w: width }])),
        cellData,
      },
    },
  });
  const subs = [
    univerAPI.addEvent(univerAPI.Event.BeforeSheetEditStart, (p: { column: number; row: number; cancel?: boolean }) => {
      if (!editable(p.row, p.column)) p.cancel = true;
    }),
    univerAPI.addEvent(univerAPI.Event.SheetEditEnded, onEdited),
    univerAPI.addEvent(univerAPI.Event.ClipboardPasted, onEdited),
  ];
  return {
    univerAPI,
    dispose() {
      subs.forEach((s: { dispose(): void }) => s.dispose());
      univerAPI.dispose();
      univer.dispose();
    },
  };
}
