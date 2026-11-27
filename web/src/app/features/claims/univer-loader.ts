import { type FUniver, LocaleType, createUniver, mergeLocales } from '@univerjs/presets';
import { UniverSheetsCorePreset } from '@univerjs/preset-sheets-core';
import sheetsCoreEnUS from '@univerjs/preset-sheets-core/locales/en-US';

/** What the page keeps: the facade to read and write cells, and a dispose that tears the whole instance down. */
export interface UniverHandle {
  dispose(): void;
  univerAPI: FUniver;
}

/** A CSS token's value, so the sheet takes its colours from tokens.css like the rest of the console. */
function token(name: string): string {
  return getComputedStyle(document.documentElement).getPropertyValue(name).trim();
}

/** A bare Univer instance (no toolbar, formula bar or menus) mounted into `container`. */
function boot(container: HTMLElement) {
  return createUniver({
    locale: LocaleType.EN_US,
    locales: { [LocaleType.EN_US]: mergeLocales(sheetsCoreEnUS) },
    presets: [
      UniverSheetsCorePreset({
        container,
        header: false,
        toolbar: false,
        footer: false,
        formulaBar: false,
        contextMenu: false,
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
  const accentSoft = token('--accent-soft');
  const put = (r: number, c: number, cell: object) => ((cellData[r] ??= {})[c] = cell);
  header.forEach((h, c) => put(0, c, { v: h, s: { bl: 1, bg: { rgb: headBg }, cl: { rgb: text } } }));
  body.forEach((row, i) =>
    row.forEach((v, c) => {
      if (v === null) return;
      const reserve = c === editableCol;
      put(i + 1, c, {
        v,
        t: typeof v === 'number' ? 2 : 1,
        s: {
          cl: { rgb: reserve ? text : muted },
          ...(reserve ? { bg: { rgb: accentSoft }, n: { pattern: '#,##0.00' } } : {}),
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
        rowCount: body.length + 4,
        columnCount: header.length,
        freeze: { xSplit: 1, ySplit: 1, startRow: 1, startColumn: 1 },
        columnData: Object.fromEntries([190, 70, 80, 90, 120, 160, 130, 90].map((width, c) => [c, { w: width }])),
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
  const accentSoft = token('--accent-soft');
  const cellData: Record<number, Record<number, object>> = {
    0: {
      0: { v: 'Item', s: { bl: 1, bg: { rgb: headBg }, cl: { rgb: text } } },
      1: { v: `Amount (${currency})`, s: { bl: 1, bg: { rgb: headBg }, cl: { rgb: text } } },
    },
  };
  labels.forEach((label, i) => {
    const row = i + 1;
    const total = formulas[row] !== undefined;
    cellData[row] = {
      0: { v: label, s: { cl: { rgb: total ? text : muted }, ...(total ? { bl: 1 } : {}) } },
      1: total
        ? { f: formulas[row], s: { bl: 1, n: { pattern: '#,##0.00' }, cl: { rgb: text } } }
        : { s: { bg: { rgb: accentSoft }, n: { pattern: '#,##0.00' }, cl: { rgb: text } } },
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
        rowCount: labels.length + 2,
        columnCount: 2,
        columnData: { 0: { w: 260 }, 1: { w: 170 } },
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
