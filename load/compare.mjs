#!/usr/bin/env node
// Overlays two run summaries (percentile curves, table, p95 over time) into one self-contained HTML page.
// usage: node load/compare.mjs <a.json> <b.json> [out.html]   default out: load/report/compare.html
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname } from 'node:path';
import { renderCompare } from './render.mjs';

const [, , a, b, out = new URL('./report/compare.html', import.meta.url).pathname] = process.argv;
if (!a || !b) { console.error('usage: node load/compare.mjs <a.json> <b.json> [out.html]'); process.exit(2); }
mkdirSync(dirname(out), { recursive: true });
writeFileSync(out, renderCompare(JSON.parse(readFileSync(a, 'utf8')), JSON.parse(readFileSync(b, 'utf8'))));
console.log(`wrote ${out}`);
