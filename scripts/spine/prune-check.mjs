#!/usr/bin/env node
// Advisory doc-lifecycle check (never fails unless --strict). Rules live in hygiene.md ("Doc lifecycle").
//  1. A thread marked done on docs/workstreams.md must be pruned: board = live threads only.
//  2. A W-NNN that appears in a CHANGELOG entry heading but is neither on the board nor named by a
//     `Closed: W-NNN ...` line in docs/project-log.md was dropped without a journal entry.
//  3. docs/hot-state.md: <= 40 lines; its `Last green sha:` must exist in git and be <= MAX_BEHIND commits behind HEAD.
import { readFileSync, existsSync } from "node:fs";
import { execFileSync } from "node:child_process";
import path from "node:path";
import { allLogText } from "./lib.mjs";
import { fileURLToPath } from "node:url";

export const MAX_BEHIND = 25;
export const HOT_STATE_MAX_LINES = 40;

export const doneThreadsOnBoard = (ws) =>
  ws.split("\n").filter((l) => /^### WS-\d+/.test(l) && /✅/.test(l)).map((l) => l.match(/WS-\d+/)[0]);

export function droppedWithoutLog({ workstreams, projectLog, changelog }) {
  const onBoard = new Set([...workstreams.matchAll(/^ID:\s*(W-\d+)/gm)].map((m) => m[1]));
  const closed = new Set([...projectLog.matchAll(/^Closed:\s*(W-\d+)/gm)].map((m) => m[1]));
  const inChangelog = new Set([...changelog.matchAll(/^## .*?(W-\d+)/gm)].map((m) => m[1]));
  return [...inChangelog].filter((id) => !onBoard.has(id) && !closed.has(id)).sort();
}

export function hotStateFindings({ text, shaKnown, behind }) {
  const out = [];
  const n = text.split("\n").length;
  if (n > HOT_STATE_MAX_LINES) out.push(`hot-state.md has ${n} lines (max ${HOT_STATE_MAX_LINES}); it is overwritten at each wrap, not appended to`);
  const m = text.match(/^Last green sha:\s*`?([0-9a-f]{7,40})`?/m);
  if (!m) out.push("hot-state.md has no `Last green sha:` line");
  else if (!shaKnown) out.push(`hot-state last green sha ${m[1]} is not in git log`);
  else if (behind > MAX_BEHIND) out.push(`hot-state last green sha ${m[1]} is ${behind} commits behind HEAD (max ${MAX_BEHIND}); re-verify and overwrite at the next wrap`);
  return out;
}

const read = (root, p) => (existsSync(path.join(root, p)) ? readFileSync(path.join(root, p), "utf8") : "");
const git = (root, args) => {
  try {
    return execFileSync("git", args, { cwd: root, encoding: "utf8", stdio: ["ignore", "pipe", "ignore"] }).trim();
  } catch {
    return null;
  }
};

export function run(root) {
  const findings = [];
  const ws = read(root, "docs/workstreams.md");
  for (const id of doneThreadsOnBoard(ws))
    findings.push(`${id} is done but still on docs/workstreams.md: append its Closed: entry to docs/project-log.md, then delete the thread block`);
  for (const id of droppedWithoutLog({ workstreams: ws, projectLog: allLogText(root), changelog: read(root, "CHANGELOG.md") }))
    findings.push(`${id} left the board but docs/project-log.md has no "Closed: ${id} ..." line`);
  const hot = read(root, "docs/hot-state.md");
  const sha = hot.match(/^Last green sha:\s*`?([0-9a-f]{7,40})`?/m)?.[1];
  const known = sha ? git(root, ["cat-file", "-e", `${sha}^{commit}`]) !== null : false;
  const behind = known ? Number(git(root, ["rev-list", "--count", `${sha}..HEAD`]) ?? 0) : 0;
  findings.push(...hotStateFindings({ text: hot, shaKnown: known, behind }));
  return findings;
}

if (process.argv[1] && fileURLToPath(import.meta.url) === path.resolve(process.argv[1])) {
  const root = process.cwd();
  const findings = run(root);
  for (const f of findings) console.warn(`prune-check: ${f}`);
  if (!findings.length) console.log("prune-check: clean");
  process.exit(findings.length && process.argv.includes("--strict") ? 1 : 0);
}
