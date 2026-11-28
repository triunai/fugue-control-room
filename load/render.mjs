// Pure string renderers for the load reports: no imports, no Node or k6 APIs, so the same file runs inside k6's
// handleSummary() and in the Node scripts (report.mjs, compare.mjs). Self-contained output: inline CSS and inline SVG only.
// Palette is the console's Vellum tokens (web/src/styles/tokens.css): navy, cream, gold.

const C = {
  bg: '#f3eee3', surface: '#fbf8f1', line: '#ddd4c2', text: '#1e1b16', muted: '#6e675a',
  navy: '#22406b', navySoft: '#7f93b3', gold: '#b69a65', ok: '#33653f', err: '#97302a', warn: '#7a540c',
};
export const PCTS = [['p50', 'p50'], ['p90', 'p90'], ['p95', 'p95'], ['p99', 'p99'], ['max', 'max']];

const esc = (s) => String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
const ms = (v) => (v == null || Number.isNaN(v) ? 'n/a' : v >= 1000 ? `${(v / 1000).toFixed(2)} s` : `${v >= 100 ? v.toFixed(0) : v.toFixed(1)} ms`);
const pct = (v) => (v == null ? 'n/a' : `${(v * 100).toFixed(2)} %`);
const num = (v, d = 1) => (v == null ? 'n/a' : Number(v).toFixed(d));

const CSS = `
:root{color-scheme:light}*{box-sizing:border-box}
body{margin:0;background:${C.bg};color:${C.text};font:15px/1.5 system-ui,-apple-system,"Segoe UI",sans-serif}
header{background:linear-gradient(90deg,#1b3358,#34569a);color:#fff;padding:1.4rem 2rem;border-bottom:3px solid ${C.gold}}
header h1{margin:0;font-size:1.5rem;letter-spacing:.01em}header p{margin:.25rem 0 0;color:rgba(255,255,255,.78);font-size:.9rem}
main{max-width:1100px;margin:0 auto;padding:1.5rem 2rem 3rem}
h2{font-size:1.05rem;color:${C.navy};margin:2rem 0 .6rem;border-bottom:1px solid ${C.line};padding-bottom:.25rem}
.cards{display:grid;grid-template-columns:repeat(auto-fit,minmax(150px,1fr));gap:.7rem}
.card{background:${C.surface};border:1px solid ${C.line};border-radius:8px;padding:.7rem .9rem}
.card b{display:block;font-size:1.35rem;color:${C.navy}}.card span{color:${C.muted};font-size:.8rem}
.card.bad b{color:${C.err}}.card.good b{color:${C.ok}}
table{border-collapse:collapse;width:100%;background:${C.surface};border:1px solid ${C.line};font-size:.88rem}
th,td{padding:.4rem .7rem;text-align:right;border-bottom:1px solid ${C.line}}th:first-child,td:first-child{text-align:left}
th{background:${C.navy};color:#f7f3ea;font-weight:600}
.pass{color:${C.ok};font-weight:700}.fail{color:${C.err};font-weight:700}
.chart{background:${C.surface};border:1px solid ${C.line};border-radius:8px;padding:.6rem;margin:.5rem 0}
.chart h3{margin:.1rem .4rem .3rem;font-size:.9rem;color:${C.navy}}
.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(480px,1fr));gap:.8rem}
svg{width:100%;height:auto;display:block}svg text{font:11px system-ui,sans-serif;fill:${C.muted}}
.note{color:${C.muted};font-size:.82rem}code{background:${C.surface};padding:0 .25rem;border-radius:3px}
.legend span{display:inline-block;margin-right:1rem;font-size:.82rem}.legend i{display:inline-block;width:.8rem;height:.25rem;margin-right:.3rem;vertical-align:middle}
`;

function page(title, sub, body) {
  return `<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>${esc(title)}</title><style>${CSS}</style></head><body>
<header><h1>${esc(title)}</h1><p>${sub}</p></header><main>${body}</main></body></html>`;
}

/** Nice axis maximum and ticks for 0..max. */
function niceAxis(max) {
  const m = max > 0 ? max : 1;
  const mag = 10 ** Math.floor(Math.log10(m));
  const step = [1, 2, 2.5, 5, 10].map((k) => k * mag).find((s) => m / s <= 5) || 10 * mag;
  const top = Math.ceil(m / step) * step;
  const ticks = [];
  for (let v = 0; v <= top + 1e-9; v += step) ticks.push(v);
  return { top, ticks };
}

const msTick = (v) => (v >= 1000 ? `${+(v / 1000).toFixed(2)}s` : `${+v.toFixed(1)}ms`);
const tickLabel = (v) => (v >= 1000 ? `${+(v / 1000).toFixed(2)}s` : `${+v.toFixed(1)}`);

/** Horizontal bars: one per percentile, with optional threshold markers on p95/p99. */
function barsSvg(stats, limits = {}) {
  const rows = PCTS.filter(([k]) => stats[k] != null);
  const { top, ticks } = niceAxis(Math.max(...rows.map(([k]) => stats[k]), ...Object.values(limits)));
  const W = 560, L = 44, R = 70, rowH = 26, H = rows.length * rowH + 30;
  const x = (v) => L + ((W - L - R) * v) / top;
  let s = `<svg viewBox="0 0 ${W} ${H}" role="img" aria-label="latency percentiles">`;
  for (const t of ticks) s += `<line x1="${x(t)}" x2="${x(t)}" y1="4" y2="${H - 20}" stroke="${C.line}"/><text x="${x(t)}" y="${H - 6}" text-anchor="middle">${tickLabel(t)}</text>`;
  rows.forEach(([k, label], i) => {
    const y = 8 + i * rowH, v = stats[k];
    const over = limits[k] != null && v > limits[k];
    s += `<text x="${L - 8}" y="${y + 14}" text-anchor="end">${label}</text>`;
    s += `<rect x="${L}" y="${y}" width="${Math.max(1, x(v) - L)}" height="18" rx="3" fill="${over ? C.err : k === 'max' ? C.gold : C.navy}"/>`;
    s += `<text x="${x(v) + 6}" y="${y + 13}">${ms(v)}</text>`;
    if (limits[k] != null) s += `<line x1="${x(limits[k])}" x2="${x(limits[k])}" y1="${y - 3}" y2="${y + 21}" stroke="${C.warn}" stroke-width="2" stroke-dasharray="3 2"><title>threshold ${ms(limits[k])}</title></line>`;
  });
  return `${s}</svg>`;
}

/** Multi-series line chart. series: [{name,color,dash?,pts:[[x,y]]}]. */
export function linesSvg(series, { xLabel = 'seconds', yLabel = '', yFmt = tickLabel, height = 220, xMax } = {}) {
  const all = series.flatMap((s) => s.pts);
  if (!all.length) return '<p class="note">No time series available for this run.</p>';
  const W = 560, L = 48, R = 14, T = 8, B = 34, H = height;
  const xm = xMax ?? Math.max(...all.map((p) => p[0]), 1);
  const { top, ticks } = niceAxis(Math.max(...all.map((p) => p[1])));
  const x = (v) => L + ((W - L - R) * v) / xm, y = (v) => H - B - ((H - B - T) * v) / top;
  let s = `<svg viewBox="0 0 ${W} ${H}" role="img" aria-label="${esc(yLabel)} over time">`;
  for (const t of ticks) s += `<line x1="${L}" x2="${W - R}" y1="${y(t)}" y2="${y(t)}" stroke="${C.line}"/><text x="${L - 6}" y="${y(t) + 4}" text-anchor="end">${yFmt(t)}</text>`;
  const xt = niceAxis(xm).ticks.filter((t) => t <= xm);
  for (const t of xt) s += `<text x="${x(t)}" y="${H - B + 14}" text-anchor="middle">${tickLabel(t)}</text>`;
  s += `<text x="${(L + W - R) / 2}" y="${H - 4}" text-anchor="middle">${esc(xLabel)}</text>`;
  for (const se of series) {
    if (!se.pts.length) continue;
    const d = se.pts.map((p, i) => `${i ? 'L' : 'M'}${x(p[0]).toFixed(1)},${y(p[1]).toFixed(1)}`).join(' ');
    s += `<path d="${d}" fill="none" stroke="${se.color}" stroke-width="2"${se.dash ? ` stroke-dasharray="${se.dash}"` : ''} stroke-linejoin="round"/>`;
    if (se.pts.length === 1) s += `<circle cx="${x(se.pts[0][0])}" cy="${y(se.pts[0][1])}" r="3" fill="${se.color}"/>`;
  }
  return `${s}</svg>`;
}

const legend = (items) => `<div class="legend">${items.map(([n, c, d]) => `<span><i style="background:${c};${d ? 'opacity:.7' : ''}"></i>${esc(n)}</span>`).join('')}</div>`;

const thresholdRows = (sum) => (sum.thresholds || []).map((t) => `<tr><td>${esc(t.metric)}</td><td>${esc(t.expr)}</td><td class="${t.ok ? 'pass' : 'fail'}">${t.ok ? 'PASS' : 'FAIL'}</td></tr>`).join('');

/** The single-run report. `sum` is the summary JSON the scripts write; sum.timeline may be null. */
export function renderReport(sum) {
  const names = Object.keys(sum.endpoints);
  const failed = (sum.thresholds || []).filter((t) => !t.ok).length;
  const o = sum.overall;
  const card = (v, l, cls = '') => `<div class="card ${cls}"><b>${v}</b><span>${l}</span></div>`;
  let body = `<div class="cards">${card(sum.totals.requests, 'requests')}${card(num(sum.totals.rps), 'requests / s (first to last request)')}${sum.totals.peakRps != null ? card(num(sum.totals.peakRps), `peak requests / s (${sum.timeline.widthSec} s bucket)`) : ''}`
    + `${card(ms(o.p50), 'p50')}${card(ms(o.p95), 'p95', sum.limits?.p95 != null && o.p95 > sum.limits.p95 ? 'bad' : 'good')}`
    + `${card(ms(o.p99), 'p99', sum.limits?.p99 != null && o.p99 > sum.limits.p99 ? 'bad' : 'good')}${card(ms(o.max), 'max')}`
    + `${card(pct(sum.totals.failedRate), 'HTTP failures', sum.totals.failedRate > 0.01 ? 'bad' : 'good')}`
    + `${card(failed ? `${failed} FAILED` : 'all pass', 'thresholds', failed ? 'bad' : 'good')}</div>`;

  body += '<h2>Percentiles per endpoint</h2><table><tr><th>endpoint</th><th>requests</th><th>fail</th>'
    + PCTS.map(([, l]) => `<th>${l}</th>`).join('') + '</tr>';
  for (const n of ['ALL', ...names]) {
    const e = n === 'ALL' ? { ...o, count: sum.totals.requests, failRate: sum.totals.failedRate } : sum.endpoints[n];
    body += `<tr><td>${n === 'ALL' ? '<b>all requests</b>' : esc(n)}</td><td>${e.count ?? ''}</td><td>${pct(e.failRate)}</td>${PCTS.map(([k]) => `<td>${ms(e[k])}</td>`).join('')}</tr>`;
  }
  body += '</table><p class="note">Latency is k6 <code>http_req_duration</code> (send + wait + receive). Percentiles are computed by k6 over every request, not averaged.</p>';

  body += '<h2>Percentile bars</h2><div class="grid">';
  for (const n of ['ALL', ...names]) {
    const e = n === 'ALL' ? o : sum.endpoints[n];
    body += `<div class="chart"><h3>${n === 'ALL' ? 'all requests' : esc(n)}</h3>${barsSvg(e, { p95: sum.limits?.p95, p99: sum.limits?.p99 })}</div>`;
  }
  body += `</div><p class="note">Dashed gold marker: the p95 / p99 threshold (${ms(sum.limits?.p95)} / ${ms(sum.limits?.p99)}). A bar over its marker is red.</p>`;

  body += '<h2>Over time</h2>';
  const tl = sum.timeline;
  if (tl && tl.points.length) {
    const P = tl.points;
    body += `<div class="grid"><div class="chart"><h3>Latency (per ${tl.widthSec} s bucket)</h3>${legend([['p50', C.navySoft], ['p95', C.navy], ['p99', C.gold]])}`
      + `${linesSvg([{ name: 'p50', color: C.navySoft, pts: P.map((p) => [p.t, p.p50]) }, { name: 'p95', color: C.navy, pts: P.map((p) => [p.t, p.p95]) }, { name: 'p99', color: C.gold, pts: P.map((p) => [p.t, p.p99]) }], { xLabel: 'seconds since start', yFmt: msTick })}</div>`
      + `<div class="chart"><h3>Throughput and errors</h3>${legend([['requests / s', C.navy], ['HTTP failure % (same axis: 1 = 1 %)', C.err]])}`
      + `${linesSvg([{ name: 'rps', color: C.navy, pts: P.map((p) => [p.t, p.rps]) }, { name: 'err', color: C.err, pts: P.map((p) => [p.t, p.err * 100]) }], { xLabel: 'seconds since start', yFmt: (v) => tickLabel(v) })}</div></div>`
      + '';
  } else {
    body += '<p class="note">No time series in this file. Run through <code>load/run.sh</code> (it records <code>--out json</code> and adds the timeline); a bare <code>k6 run</code> only yields the aggregate summary.</p>';
  }

  body += `<h2>Thresholds</h2><table><tr><th>metric</th><th>rule</th><th>result</th></tr>${thresholdRows(sum)}</table>`;
  const env = sum.env || {};
  body += `<h2>Run</h2><p class="note">scenario <b>${esc(sum.scenario)}</b>, label <b>${esc(sum.label || '-')}</b>, ${esc(sum.startedAt)}, ${num(sum.durationSec, 0)} s, `
    + `target ${esc(sum.target)}, k6 ${esc(sum.k6Version || '?')}. ${esc(env.note || '')}</p>`;
  return page(`Fugue load: ${sum.scenario}${sum.label ? ` (${sum.label})` : ''}`,
    `${esc(sum.startedAt)} · ${sum.totals.requests} requests · ${failed ? `<b>${failed} threshold(s) failed</b>` : 'all thresholds passed'}`, body);
}

/** Overlay of two runs: percentile curves per endpoint, latency over time, delta table. */
export function renderCompare(a, b) {
  const cA = C.navy, cB = C.gold;
  const la = `A: ${a.label || a.scenario}`, lb = `B: ${b.label || b.scenario}`;
  const names = [...new Set([...Object.keys(a.endpoints), ...Object.keys(b.endpoints)])];
  const curve = (ea, eb) => {
    const mk = (e) => PCTS.map(([k], i) => (e && e[k] != null ? [i, e[k]] : null)).filter(Boolean);
    return linesSvgPct([{ name: la, color: cA, pts: mk(ea) }, { name: lb, color: cB, pts: mk(eb), dash: '5 3' }]);
  };
  const delta = (x, y) => (x == null || y == null || x === 0 ? 'n/a' : `${y - x >= 0 ? '+' : ''}${(((y - x) / x) * 100).toFixed(0)} %`);
  let body = `<div class="legend"><span><i style="background:${cA}"></i>${esc(la)} (${esc(a.scenario)}, ${esc(a.startedAt)})</span><span><i style="background:${cB}"></i>${esc(lb)} (${esc(b.scenario)}, ${esc(b.startedAt)})</span></div>`;
  body += '<h2>Percentile curves per endpoint</h2><p class="note">x: p50, p90, p95, p99, max. y: latency. A lower curve is faster; the gap at p99 is the tail.</p><div class="grid">';
  for (const n of ['ALL', ...names]) {
    const ea = n === 'ALL' ? a.overall : a.endpoints[n], eb = n === 'ALL' ? b.overall : b.endpoints[n];
    body += `<div class="chart"><h3>${n === 'ALL' ? 'all requests' : esc(n)}</h3>${curve(ea, eb)}</div>`;
  }
  body += '</div><h2>Percentile table</h2><table><tr><th>endpoint</th><th>percentile</th><th>A</th><th>B</th><th>change</th></tr>';
  for (const n of ['ALL', ...names]) {
    const ea = n === 'ALL' ? a.overall : a.endpoints[n], eb = n === 'ALL' ? b.overall : b.endpoints[n];
    for (const [k, l] of PCTS.filter(([k]) => k !== 'max')) {
      body += `<tr><td>${n === 'ALL' ? '<b>all requests</b>' : esc(n)}</td><td>${l}</td><td>${ms(ea?.[k])}</td><td>${ms(eb?.[k])}</td><td>${delta(ea?.[k], eb?.[k])}</td></tr>`;
    }
  }
  body += `<tr><td><b>all requests</b></td><td>requests / s</td><td>${num(a.totals.rps)}</td><td>${num(b.totals.rps)}</td><td>${delta(a.totals.rps, b.totals.rps)}</td></tr>`
    + `<tr><td><b>all requests</b></td><td>HTTP failures</td><td>${pct(a.totals.failedRate)}</td><td>${pct(b.totals.failedRate)}</td><td></td></tr></table>`;
  body += '<h2>Latency over time (p95)</h2>';
  if (a.timeline?.points.length && b.timeline?.points.length) {
    body += `<div class="chart">${linesSvg([{ name: la, color: cA, pts: a.timeline.points.map((p) => [p.t, p.p95]) }, { name: lb, color: cB, dash: '5 3', pts: b.timeline.points.map((p) => [p.t, p.p95]) }], { xLabel: 'seconds since start of each run', yFmt: msTick })}</div>`;
  } else body += '<p class="note">One of the runs has no time series (it was not run through <code>load/run.sh</code>).</p>';
  const mismatch = a.scenario !== b.scenario;
  body += `<h2>Read this first</h2><p class="note">${mismatch ? `<b>The two runs are different scenarios (${esc(a.scenario)} vs ${esc(b.scenario)}); the curves show different load shapes, not a before/after of one change.</b> ` : ''}`
    + 'A fair before/after uses the same scenario, the same data and the same limiter settings, with only the change under test different. Both runs here were generated on one machine that also runs the API and the database.</p>';
  return page(`Fugue load: ${esc(la)} vs ${esc(lb)}`, 'Percentile overlay of two runs', body);
}

/** Line chart with the five percentile names on the x axis (category axis). */
function linesSvgPct(series) {
  const all = series.flatMap((s) => s.pts);
  if (!all.length) return '<p class="note">No data.</p>';
  const W = 560, L = 48, R = 14, T = 8, B = 28, H = 210;
  const { top, ticks } = niceAxis(Math.max(...all.map((p) => p[1])));
  const x = (i) => L + ((W - L - R) * i) / (PCTS.length - 1), y = (v) => H - B - ((H - B - T) * v) / top;
  let s = `<svg viewBox="0 0 ${W} ${H}" role="img" aria-label="percentile curves">`;
  for (const t of ticks) s += `<line x1="${L}" x2="${W - R}" y1="${y(t)}" y2="${y(t)}" stroke="${C.line}"/><text x="${L - 6}" y="${y(t) + 4}" text-anchor="end">${msTick(t)}</text>`;
  PCTS.forEach(([, l], i) => { s += `<text x="${x(i)}" y="${H - 8}" text-anchor="middle">${l}</text>`; });
  for (const se of series) {
    const d = se.pts.map((p, i) => `${i ? 'L' : 'M'}${x(p[0]).toFixed(1)},${y(p[1]).toFixed(1)}`).join(' ');
    s += `<path d="${d}" fill="none" stroke="${se.color}" stroke-width="2.5"${se.dash ? ` stroke-dasharray="${se.dash}"` : ''}/>`;
    for (const p of se.pts) s += `<circle cx="${x(p[0])}" cy="${y(p[1])}" r="3.5" fill="${se.color}"><title>${esc(se.name)} ${PCTS[p[0]][1]}: ${ms(p[1])}</title></circle>`;
  }
  return `${s}</svg>`;
}
