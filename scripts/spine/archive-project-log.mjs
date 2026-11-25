#!/usr/bin/env node
// Cold storage for the warm project log. Entries older than 30 days, or beyond the latest 20, are MOVED VERBATIM to
// docs/archive/project-log/YYYY-MM.md and given a line in docs/archive/INDEX.md. CHANGELOG.md is never touched.
// Usage: node scripts/spine/archive-project-log.mjs [--dry-run] [--today YYYY-MM-DD]
import { writeFileSync, mkdirSync, existsSync, readFileSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { rd, splitEntries } from "./lib.mjs";

export const KEEP = 20;
export const MAX_AGE_DAYS = 30;

/** Returns {keep, move:[{month, text}]}; entries are newest first. */
export function plan(entries, today) {
  const t = Date.parse(today);
  const keep = [], move = [];
  entries.forEach((e, i) => {
    const d = e.match(/^## (\d{4}-\d{2}-\d{2})/)?.[1];
    const old = Boolean(d) && (t - Date.parse(d)) / 86400000 > MAX_AGE_DAYS;
    if (i >= KEEP || old) move.push({ month: (d || today).slice(0, 7), text: e });
    else keep.push(e);
  });
  return { keep, move };
}

if (process.argv[1] && fileURLToPath(import.meta.url) === path.resolve(process.argv[1])) {
  const root = process.cwd();
  const arg = (n) => { const i = process.argv.indexOf(`--${n}`); return i >= 0 ? process.argv[i + 1] : undefined; };
  const today = arg("today") || new Date().toISOString().slice(0, 10);
  const { head, entries } = splitEntries(rd(root, "docs/project-log.md"));
  const { keep, move } = plan(entries, today);
  if (!move.length) { console.log("archive-project-log: nothing to move"); process.exit(0); }
  if (process.argv.includes("--dry-run")) { console.log(`would move ${move.length} entr${move.length === 1 ? "y" : "ies"}: ${[...new Set(move.map((m) => m.month))].join(", ")}`); process.exit(0); }
  mkdirSync(path.join(root, "docs/archive/project-log"), { recursive: true });
  for (const month of new Set(move.map((m) => m.month))) {
    const f = path.join(root, "docs/archive/project-log", `${month}.md`);
    const add = move.filter((m) => m.month === month).map((m) => m.text).join("");
    const prev = existsSync(f) ? readFileSync(f, "utf8") : `# Project log archive ${month} (cold, verbatim)\n\n`;
    writeFileSync(f, prev.trimEnd() + "\n\n" + add);
    const idx = path.join(root, "docs/archive/INDEX.md");
    const line = `| \`project-log/${month}.md\` | Project-log entries moved verbatim from the warm log (closed W entries) | looking up what happened in ${month}; grep \`Closed: W-\` first |`;
    if (!rd(root, "docs/archive/INDEX.md").includes(`project-log/${month}.md`)) writeFileSync(idx, rd(root, "docs/archive/INDEX.md").trimEnd() + "\n" + line + "\n");
  }
  writeFileSync(path.join(root, "docs/project-log.md"), head + keep.join(""));
  console.log(`archive-project-log: moved ${move.length}`);
}
