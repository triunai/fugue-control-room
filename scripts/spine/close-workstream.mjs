#!/usr/bin/env node
// Closes a workstream: ONE entry is appended (verbatim copy) to docs/project-log.md AND CHANGELOG.md, and the W row is deleted
// from docs/workstreams.md. Prompts nothing. Commits are gathered from git by `Refs: W-NNN` trailers (plus --commits for
// pre-gate history). The entry is NOT generated from git log alone: outcome, decisions and follow-ups are supplied by the lead.
//
//   node scripts/spine/close-workstream.mjs W-004 --outcome "2-5 lines" [--decisions "D-004, ADR-006"] \
//        [--followups "..."] [--commits "sha1,sha2"] [--title "..."] [--date YYYY-MM-DD] [--dry-run]
// Entries are append-only: this script never edits an existing entry. Re-closing a W is refused.
import { readFileSync, writeFileSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { rd, git, allLogText, closedIds } from "./lib.mjs";

export function findThread(board, id) {
  const lines = board.split("\n");
  const idLine = lines.findIndex((l) => new RegExp(`^ID:\\s*${id}\\s*$`).test(l));
  if (idLine < 1 || !/^### WS-\d+/.test(lines[idLine - 1])) return null;
  let end = lines.length;
  for (let i = idLine + 1; i < lines.length; i++) if (/^### /.test(lines[i])) { end = i; break; }
  const heading = lines[idLine - 1];
  return {
    start: idLine - 1,
    end,
    ws: heading.match(/WS-\d+/)[0],
    title: heading.replace(/^###\s+WS-\d+\s+—\s+/, "").replace(/\s+—\s+(?:\S+\s+)?(?:✅|⏸|🟢|⛔)?[^—]*\(\d{4}-\d{2}-\d{2}\)\s*$/, "").replace(/\s+—\s*$/, "").trim(),
  };
}
export const removeThread = (board, t) => {
  const lines = board.split("\n");
  lines.splice(t.start, t.end - t.start);
  return lines.join("\n").replace(/\n{3,}/g, "\n\n");
};

export function buildEntry({ id, ws, title, date, outcome, commits, decisions, followups }) {
  return [
    `## ${date} — Closed ${id}: ${title}`,
    `WS: ${ws}`,
    `Closed: ${id} (${ws}) ${title}`,
    `Outcome: ${outcome}`,
    "Commits:",
    ...(commits.length ? commits.map((c) => `- ${c}`) : ["- (none recorded)"]),
    `Decisions: ${decisions || "none"}`,
    `Follow-ups: ${followups || "none"}`,
    "",
    "",
  ].join("\n");
}

/** Insert `entry` before the first `## ` entry (newest first); `afterHead` keeps the file header. */
export function prepend(text, entry) {
  const i = text.search(/^## /m);
  return i < 0 ? text.trimEnd() + "\n\n" + entry : text.slice(0, i) + entry + text.slice(i);
}

export function commitsFor(root, id, extra = []) {
  const out = git(root, ["log", "--reverse", "--format=%h%x1f%s%x1f%(trailers:key=Refs,valueonly,separator=%x2C)%x1e"]) || "";
  const found = out
    .split("\x1e")
    .map((r) => r.trim().split("\x1f"))
    .filter((p) => p.length === 3 && p[2].split(/[,\s]+/).includes(id))
    .map((p) => `${p[0]} ${p[1]}`);
  const manual = extra.map((sha) => `${git(root, ["rev-parse", "--short=7", sha])} ${git(root, ["log", "-1", "--format=%s", sha])}`);
  return [...manual, ...found.filter((f) => !manual.some((m) => m.split(" ")[0] === f.split(" ")[0]))];
}

/** ADR/D ids cited (in `Refs:`) by the commits of a W, so the entry records what the thread produced. */
export function refIdsFor(root, id, extraShas = []) {
  const out = git(root, ["log", "--format=%h%x1f%(trailers:key=Refs,valueonly,separator=%x2C)%x1e"]) || "";
  const found = new Set();
  for (const rec of out.split("\x1e")) {
    const [sha, refs = ""] = rec.trim().split("\x1f");
    const ids = refs.split(/[,;\s]+/).filter(Boolean);
    if (ids.includes(id) || extraShas.some((x) => sha && (x.startsWith(sha) || sha.startsWith(x)))) ids.filter((r) => /^(D|ADR)-\d+$/.test(r)).forEach((r) => found.add(r));
  }
  return [...found];
}
/** Supplied `--decisions` text first, then any commit-cited D/ADR id not already named in it. */
export function mergeDecisions(supplied, found) {
  const have = new Set((supplied || "").match(/\b(?:D|ADR)-\d+\b/g) || []);
  const extra = found.filter((i) => !have.has(i));
  return [supplied, ...extra].filter(Boolean).join(", ");
}

function main(argv) {
  const root = process.cwd();
  const id = argv[0];
  const flag = (n) => { const i = argv.indexOf(`--${n}`); return i >= 0 ? argv[i + 1] : undefined; };
  if (!/^W-\d+$/.test(id || "") || !flag("outcome")) {
    console.error('usage: close-workstream.mjs W-NNN --outcome "<2-5 lines>" [--decisions ...] [--followups ...] [--commits sha,sha] [--title ...] [--date ...] [--dry-run]');
    process.exit(2);
  }
  const board = rd(root, "docs/workstreams.md");
  if (closedIds(allLogText(root)).includes(id)) { console.error(`${id} already has a Closed: entry; entries are append-only.`); process.exit(1); }
  const t = findThread(board, id);
  if (!t) { console.error(`${id} not found on docs/workstreams.md (needs a '### WS-NN' heading followed by 'ID: ${id}').`); process.exit(1); }
  const extraShas = (flag("commits") || "").split(",").map((s) => s.trim()).filter(Boolean);
  const entry = buildEntry({
    id, ws: t.ws, title: flag("title") || t.title, date: flag("date") || new Date().toISOString().slice(0, 10),
    outcome: flag("outcome"), decisions: mergeDecisions(flag("decisions"), refIdsFor(root, id, extraShas)), followups: flag("followups"),
    commits: commitsFor(root, id, extraShas),
  });
  if (argv.includes("--dry-run")) { console.log(entry); return; }
  writeFileSync(path.join(root, "docs/project-log.md"), prepend(rd(root, "docs/project-log.md"), entry));
  writeFileSync(path.join(root, "CHANGELOG.md"), prepend(rd(root, "CHANGELOG.md"), entry));
  writeFileSync(path.join(root, "docs/workstreams.md"), removeThread(board, t));
  console.log(`closed ${id}: entry added to docs/project-log.md and CHANGELOG.md; row removed from docs/workstreams.md`);
}
if (process.argv[1] && fileURLToPath(import.meta.url) === path.resolve(process.argv[1])) main(process.argv.slice(2));
