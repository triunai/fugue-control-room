#!/usr/bin/env node
// Builds the Changelog page data (kept at the decisions/ path and script name). Builds web/src/app/features/decisions/decisions.data.json from docs/adr/*.md, docs/decisions.md and CHANGELOG.md.
// No dependencies. Deterministic: no timestamps, stable sort. Rows it cannot parse go to stderr and `unparsed` in the result.
import { existsSync, readdirSync, readFileSync, writeFileSync, mkdirSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const OUT = "web/src/app/features/decisions/decisions.data.json";
const DATE = /^\d{4}-\d{2}-\d{2}$/;

/** Plain one-liner from markdown: no links, code ticks or emphasis, whitespace collapsed, cut on a word boundary. */
export function plain(text, max = 220) {
  const s = text
    .replace(/\[([^\]]+)\]\([^)]*\)/g, "$1")
    .replace(/[`*]+/g, "")
    .replace(/\s+/g, " ")
    .trim();
  if (s.length <= max) return s;
  const cut = s.slice(0, max).replace(/\s+\S*$/, "");
  return cut.replace(/[.,;:\s]+$/, "") + "…";
}

/** GitHub heading anchor: lowercase, drop punctuation, each space becomes a hyphen. */
export function slug(heading) {
  return heading
    .toLowerCase()
    .replace(/[^\p{L}\p{N}_\- ]/gu, "")
    .replace(/ /g, "-");
}

/** `## v0.0.2 — 2026-11-27` headings, oldest first. */
export function parseVersions(changelog) {
  const out = [];
  for (const m of changelog.matchAll(/^## (v\d[^\s]*) [—-] (\d{4}-\d{2}-\d{2})\s*$/gm)) out.push({ version: m[1], date: m[2] });
  return out.sort((a, b) => a.date.localeCompare(b.date) || a.version.localeCompare(b.version, undefined, { numeric: true }));
}

/** `Label: text` when the label is a short phrase (no sentence end before the colon); otherwise no label. */
function labelled(line) {
  const m = /^([A-Z][^.:]{0,40}):\s+(.+)$/.exec(line);
  return m ? { label: m[1], text: plain(m[2], 700) } : { label: "", text: plain(line, 700) };
}

/**
 * CHANGELOG.md: the release sections (`## vX — date`, one note per non-empty line) and the dated close-outs
 * (`## date — Closed W-NNN: title` with an `Outcome:` line and a `Commits:` bullet list). Older `## date — S2 —` sections
 * carry no work-item id and are left out.
 */
export function parseChangelog(md) {
  const releases = [];
  const closed = [];
  for (const part of md.split(/^(?=## )/m)) {
    const lines = part.split("\n");
    const head = lines[0].replace(/^## /, "").trim();
    const r = /^(v\d[^\s]*) [—-] (\d{4}-\d{2}-\d{2})$/.exec(head);
    if (r) {
      const notes = lines.slice(1).map((l) => l.trim()).filter(Boolean).map(labelled);
      releases.push({ version: r[1], date: r[2], notes });
      continue;
    }
    const c = /^(\d{4}-\d{2}-\d{2}) [—-] Closed (W-\d+):\s*(.+)$/.exec(head);
    if (!c) continue;
    const outcome = lines.find((l) => l.startsWith("Outcome:"));
    const at = lines.findIndex((l) => l.startsWith("Commits:"));
    let commits = 0;
    if (at >= 0) for (const l of lines.slice(at + 1)) { if (/^- /.test(l)) commits++; else if (l.trim()) break; }
    closed.push({
      id: c[2],
      title: plain(c[3], 300),
      date: c[1],
      outcome: outcome ? plain(outcome.slice(8), 500) : "",
      commits,
    });
  }
  return { releases, closed };
}

/** `D-21` and `D-021` are the same row: three digits. */
const dId = (n) => "D-" + String(n).padStart(3, "0");

export function parseDecisions(md, file = "docs/decisions.md") {
  const entries = [];
  const unparsed = [];
  const seen = new Map();
  const parts = md.split(/^(?=## )/m);
  for (const part of parts) {
    if (!/^## D-\d/.test(part)) continue;
    const lines = part.split("\n");
    const heading = lines[0].replace(/^## /, "").trim();
    const m = /^D-(\d+) [—-] (.+?) [—-] (\d{4}-\d{2}-\d{2})$/.exec(heading);
    if (!m) {
      unparsed.push(`${file}: ${heading}`);
      continue;
    }
    const field = (name) => {
      const l = lines.find((x) => x.startsWith(name + ":"));
      return l ? l.slice(name.length + 1).trim() : "";
    };
    const summary = plain(field("Decision") || field("Context") || "").replace(/^[a-z]/, (c) => c.toUpperCase());
    const promote = /ADR-\d+/.exec(field("Promote"));
    let anchor = slug(`D-${m[1]} — ${m[2]} — ${m[3]}`);
    const n = seen.get(anchor) ?? 0;
    seen.set(anchor, n + 1);
    if (n) anchor += "-" + n;
    entries.push({
      kind: "D",
      id: dId(m[1]),
      title: plain(m[2], 300),
      date: m[3],
      summary,
      status: plain(field("Status"), 120),
      adr: promote ? promote[0] : null,
      path: `${file}#${anchor}`,
    });
  }
  return { entries, unparsed };
}

export function parseAdr(md, file) {
  const h = /^# (ADR-\d+)\s*(?:[:—-])\s*(.+)$/m.exec(md);
  const date = /^- Date:\s*(\S+)/m.exec(md)?.[1] ?? "";
  if (!h || !DATE.test(date)) return { error: `${file}: no "# ADR-NNN: title" heading or "- Date:" line` };
  const status = /^- Status:\s*(.+)$/m.exec(md)?.[1] ?? "";
  const dec = /^## Decision\s*\n+([\s\S]*?)(?=\n## |\s*$)/m.exec(md)?.[1] ?? "";
  const first = dec.split(/\n\s*\n/).find((p) => p.trim()) ?? "";
  return {
    entry: {
      kind: "ADR",
      id: h[1],
      title: plain(h[2], 300),
      date,
      summary: plain(first.replace(/^\s*[*-]\s+/, "")),
      status: plain(status, 120),
      adr: null,
      path: file,
    },
  };
}

/** The version whose release date is the first on or after the entry; none means Unreleased. */
export function versionFor(date, versions) {
  return versions.find((v) => date <= v.date) ?? null;
}

export function generate(root) {
  const read = (p) => readFileSync(path.join(root, p), "utf8");
  const changelog = read("CHANGELOG.md");
  const versions = parseVersions(changelog);
  const log = parseChangelog(changelog);
  const unparsed = [];
  const entries = [];
  const adrDir = path.join(root, "docs/adr");
  for (const f of readdirSync(adrDir).filter((x) => /^ADR-\d+.*\.md$/.test(x)).sort()) {
    const r = parseAdr(readFileSync(path.join(adrDir, f), "utf8"), `docs/adr/${f}`);
    if (r.error) unparsed.push(r.error);
    else entries.push(r.entry);
  }
  const d = parseDecisions(read("docs/decisions.md"));
  entries.push(...d.entries);
  unparsed.push(...d.unparsed);

  const byVersion = new Map();
  for (const e of entries) {
    const key = versionFor(e.date, versions)?.version ?? "Unreleased";
    if (!byVersion.has(key)) byVersion.set(key, []);
    byVersion.get(key).push(e);
  }
  const order = [...versions].reverse().map((v) => v.version);
  const closedFor = (k) => log.closed.filter((c) => (versionFor(c.date, versions)?.version ?? "Unreleased") === k);
  const keys = ["Unreleased", ...order].filter((k) => byVersion.has(k) || closedFor(k).length);
  const rank = (e) => (e.kind === "D" ? "1" : "0") + e.id;
  const groups = keys.map((k) => ({
    version: k,
    date: versions.find((v) => v.version === k)?.date ?? null,
    notes: log.releases.find((r) => r.version === k)?.notes ?? [],
    closed: closedFor(k).sort((a, b) => b.date.localeCompare(a.date) || b.id.localeCompare(a.id, undefined, { numeric: true })),
    entries: (byVersion.get(k) ?? []).sort((a, b) => b.date.localeCompare(a.date) || rank(b).localeCompare(rank(a))),
  }));
  return {
    data: {
      sources: ["docs/adr/ADR-*.md", "docs/decisions.md", "CHANGELOG.md"],
      counts: {
        adrs: entries.filter((e) => e.kind === "ADR").length,
        decisions: d.entries.length,
        releases: log.releases.length,
        closed: log.closed.length,
      },
      groups,
    },
    unparsed,
  };
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
  const { data, unparsed } = generate(root);
  const out = path.join(root, OUT);
  if (!existsSync(path.dirname(out))) mkdirSync(path.dirname(out), { recursive: true });
  writeFileSync(out, JSON.stringify(data, null, 2) + "\n");
  console.log(`changelog: ${data.counts.releases} releases, ${data.counts.closed} closed work items, ${data.counts.adrs} ADRs, ${data.counts.decisions} D-rows -> ${OUT}`);
  for (const u of unparsed) console.error(`UNPARSED ${u}`);
  if (unparsed.length) process.exitCode = 1;
}
