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
  const { univer, univerAPI } = createUniver({
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
