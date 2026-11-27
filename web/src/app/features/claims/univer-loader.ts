import { type FUniver, LocaleType, createUniver, mergeLocales } from '@univerjs/presets';
import { UniverSheetsCorePreset } from '@univerjs/preset-sheets-core';
import sheetsCoreEnUS from '@univerjs/preset-sheets-core/locales/en-US';
import { RESERVE_COLUMN_WIDTHS, ASSESSMENT_COLUMN_WIDTHS, sheetRowCount } from './sheet-layout';

/** What the page keeps: the facade to read and write cells, and a dispose that tears the whole instance down. */
export interface UniverHandle {
  dispose(): void;
  univerAPI: FUniver;
}

/** A CSS token's value, so the sheet takes its colours from tokens.css like the rest of the console. */
function token(name: string): string {
  return getComputedStyle(document.documentElement).getPropertyValue(name).trim();
}

/** A Univer instance with the full ribbon (Start / Formulas / View tabs), toolbar, formula bar and context menu. */
function boot(container: HTMLElement) {
  return createUniver({
    locale: LocaleType.EN_US,
    locales: { [LocaleType.EN_US]: mergeLocales(sheetsCoreEnUS) },
    presets: [
      UniverSheetsCorePreset({
        container,
        header: true,
        toolbar: true,
        footer: false,
        contextMenu: true,
      }),
    ],
  });
}

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
  const { univer, univerAPI } = boot(container);

  const cellData: Record<number, Record<number, object>> = {};
  const headBg = token('--surface');
  const text = token('--text');
  const muted = token('--muted');
  const input = token('--sheet-input');
  const put = (r: number, c: number, cell: object) => ((cellData[r] ??= {})[c] = cell);
  header.forEach((h, c) =>
    put(0, c, { v: h, s: { bl: 1, bg: { rgb: headBg }, cl: { rgb: text }, ...(c >= editableCol ? { ht: 3 } : {}) } }),
  );
  body.forEach((row, i) =>
    row.forEach((v, c) => {
      if (v === null) return;
      const reserve = c === editableCol;
      put(i + 1, c, {
        v,
        t: typeof v === 'number' ? 2 : 1,
        s: {
          cl: { rgb: reserve ? text : muted },
          ...(reserve ? { bg: { rgb: input }, n: { pattern: '#,##0.00' } } : {}),
          ...(c >= editableCol ? { ht: 3 } : {}),
        },
      });
    }),
  );

  univerAPI.createWorkbook({
    id: 'reserves',
    name: 'Reserves',
    sheetOrder: ['s1'],
    sheets: {
      s1: {
        id: 's1',
        name: 'Open claims',
        rowCount: sheetRowCount(body.length),
        columnCount: header.length,
        freeze: { xSplit: 1, ySplit: 1, startRow: 1, startColumn: 1 },
        columnData: Object.fromEntries(RESERVE_COLUMN_WIDTHS.map((width, c) => [c, { w: width }])),
        cellData,
      },
    },
  });

  const subs = [
    univerAPI.addEvent(univerAPI.Event.BeforeSheetEditStart, (p: { column: number; row: number; cancel?: boolean }) => {
      if (p.column !== editableCol || p.row === 0) p.cancel = true;
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
  const headBg = token('--surface');
  const input = token('--sheet-input');
  const cellData: Record<number, Record<number, object>> = {
    0: {
      0: { v: 'Item', s: { bl: 1, bg: { rgb: headBg }, cl: { rgb: text } } },
      1: { v: `Amount (${currency})`, s: { bl: 1, bg: { rgb: headBg }, cl: { rgb: text }, ht: 3 } },
    },
  };
  labels.forEach((label, i) => {
    const row = i + 1;
    const total = formulas[row] !== undefined;
    cellData[row] = {
      0: { v: label, s: { cl: { rgb: total ? text : muted }, ...(total ? { bl: 1 } : {}) } },
      1: total
        ? { f: formulas[row], s: { bl: 1, n: { pattern: '#,##0.00' }, cl: { rgb: text }, ht: 3 } }
        : { s: { bg: { rgb: input }, n: { pattern: '#,##0.00' }, cl: { rgb: text }, ht: 3 } },
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
