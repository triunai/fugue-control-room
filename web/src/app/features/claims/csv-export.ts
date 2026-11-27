import type { XlsxCell } from './xlsx-export';

/** One CSV field: quoted when it holds a comma, quote or line break, quotes doubled (RFC 4180). */
function field(cell: XlsxCell): string {
  const v = cell === null ? '' : typeof cell === 'object' ? String(cell.result) : String(cell);
  return /[",\r\n]/.test(v) ? `"${v.replace(/"/g, '""')}"` : v;
}

/** The grid as CSV text: values only (a formula cell writes its computed result), CRLF line ends. */
export function toCsv(grid: readonly XlsxCell[][]): string {
  return grid.map((row) => row.map(field).join(',')).join('\r\n') + '\r\n';
}

/** Downloads `grid` as a .csv file. A BOM lets Excel read it as UTF-8. */
export function downloadCsv(filename: string, grid: readonly XlsxCell[][]): void {
  const url = URL.createObjectURL(new Blob(['﻿', toCsv(grid)], { type: 'text/csv;charset=utf-8' }));
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  document.body.appendChild(a);
  a.click();
  a.remove();
  URL.revokeObjectURL(url);
}
