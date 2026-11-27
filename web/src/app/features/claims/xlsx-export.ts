/** One cell of an exported sheet: text, a number, empty, or a formula with its last computed result. */
export type XlsxCell = string | number | null | { formula: string; result: number };

/**
 * ExcelJS ships as CommonJS. The dev server exposes `Workbook` on the import's namespace, the production bundle only
 * under `default`: destructuring `{ Workbook }` worked in `ng serve` and threw "is not a constructor" in the image.
 */
export function workbookClass<T>(mod: { Workbook?: T; default?: { Workbook?: T } }): T {
  const ctor = mod.Workbook ?? mod.default?.Workbook;
  if (!ctor) throw new Error('exceljs: no Workbook export');
  return ctor;
}

/**
 * Builds a real .xlsx of `grid` and downloads it. ExcelJS is imported here and only here, so it ships in the lazy chunk
 * of the page that calls this, never in the initial bundle. Numbers stay numbers (amounts keep two decimals).
 */
export async function downloadXlsx(filename: string, sheetName: string, grid: readonly XlsxCell[][]): Promise<void> {
  const Workbook = workbookClass(await import('exceljs'));
  const book = new Workbook();
  const sheet = book.addWorksheet(sheetName);
  grid.forEach((row) => sheet.addRow(row.map((c) => c ?? null)));
  sheet.getRow(1).font = { bold: true };
  sheet.columns.forEach((col) => {
    col.width = 22;
  });
  sheet.eachRow((row) =>
    row.eachCell((cell) => {
      const v = cell.value;
      if (typeof v === 'number' || (v && typeof v === 'object' && 'formula' in v)) cell.numFmt = '#,##0.00';
    }),
  );
  const buffer = await book.xlsx.writeBuffer();
  const url = URL.createObjectURL(
    new Blob([buffer], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' }),
  );
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  document.body.appendChild(a);
  a.click();
  a.remove();
  URL.revokeObjectURL(url);
}
