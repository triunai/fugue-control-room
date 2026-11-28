#!/usr/bin/env node
// Adds the latency-over-time series to a run's summary JSON and re-renders its HTML.
// usage: node load/report.mjs <summary.json> <k6-points.ndjson>   (load/run.sh calls this)
import { createReadStream, readFileSync, writeFileSync } from 'node:fs';
import { createInterface } from 'node:readline';
import { renderReport } from './render.mjs';

const [, , jsonPath, ndjsonPath] = process.argv;
if (!jsonPath || !ndjsonPath) { console.error('usage: node load/report.mjs <summary.json> <points.ndjson>'); process.exit(2); }
const sum = JSON.parse(readFileSync(jsonPath, 'utf8'));
const rl = createInterface({ input: createReadStream(ndjsonPath) });
const dur = [], fail = [];
for await (const line of rl) {
  if (!line.includes('"Point"') || !line.includes('"phase":"run"')) continue;
  const o = JSON.parse(line);
  const t = Date.parse(o.data.time);
  if (o.metric === 'http_req_duration') dur.push([t, o.data.value]);
  else if (o.metric === 'http_req_failed') fail.push([t, o.data.value]);
}
if (!dur.length) { console.error('no run-phase http_req_duration points found'); process.exit(1); }
const t0 = Math.min(...dur.map((p) => p[0]));
const span = (Math.max(...dur.map((p) => p[0])) - t0) / 1000;
const width = Math.max(1, Math.ceil(span / 60)); // about 60 buckets
const buckets = new Map();
const at = (t) => Math.floor((t - t0) / 1000 / width);
for (const [t, v] of dur) { const b = buckets.get(at(t)) ?? { d: [], f: 0, n: 0 }; b.d.push(v); buckets.set(at(t), b); }
for (const [t, v] of fail) { const b = buckets.get(at(t)); if (b) { b.n += 1; b.f += v; } }
const q = (a, p) => { const s = [...a].sort((x, y) => x - y); return s[Math.min(s.length - 1, Math.ceil(p * s.length) - 1)]; };
sum.timeline = {
  widthSec: width,
  points: [...buckets.entries()].sort((a, b) => a[0] - b[0]).map(([i, b]) => ({
    t: i * width, p50: q(b.d, 0.5), p95: q(b.d, 0.95), p99: q(b.d, 0.99), rps: b.d.length / width, err: b.n ? b.f / b.n : 0,
  })),
};
sum.totals.rps = dur.length / Math.max(1, span); // first to last request, not the whole test (stampede waves idle between)
sum.totals.peakRps = Math.max(...sum.timeline.points.map((p) => p.rps));
writeFileSync(jsonPath, JSON.stringify(sum, null, 2));
writeFileSync(jsonPath.replace(/\.json$/, '.html'), renderReport(sum));
console.log(`timeline: ${sum.timeline.points.length} buckets of ${width} s -> ${jsonPath.replace(/\.json$/, '.html')}`);
