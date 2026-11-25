#!/usr/bin/env node
// Advisory verification (exit 0 unless --strict) that history is linked to the ledgers. Rules: hygiene.md "Doc lifecycle".
//  1. Every commit since the marker is listed (`- <sha> ...`) in a project-log / archive / CHANGELOG entry, or carries
//     `Refs:` to a W still open on docs/workstreams.md. Anything else is reported as "unlinked".
//  2. Every ADR-NNN named in an entry exists in docs/adr/.
//  3. Every `Closed: W-NNN` in the project log (warm or archived) has the same entry in CHANGELOG.md, and no closed W is still on the board.
// Marker: `<!-- commit-link:since=<sha> -->` in CHANGELOG.md (commits after it are checked; absent = whole history).
// `--advance` moves the marker to HEAD, only when the check is clean.
import { readFileSync, writeFileSync, existsSync, readdirSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { rd, git, allLogText, closedIds } from "./lib.mjs";

const MARK = /<!-- commit-link:since=([0-9a-f]{7,40}) -->/;

export function unlinked(commits, ledgerText, openW) {
  return commits.filter((c) => !ledgerText.includes(`- ${c.sha} `) && !c.refs.some((r) => openW.has(r)));
}
export const missingAdrs = (entriesText, adrFiles) =>
  [...new Set([...entriesText.matchAll(/\bADR-\d+\b/g)].map((m) => m[0]))].filter((a) => !adrFiles.some((f) => f.startsWith(`${a}-`))).sort();
export const closedMissingFromChangelog = (logText, changelog) => {
  const have = new Set(closedIds(changelog));
  return closedIds(logText).filter((id) => !have.has(id));
};

export function run(root) {
  const findings = [];
  const changelog = rd(root, "CHANGELOG.md");
  const log = allLogText(root);
  const board = rd(root, "docs/workstreams.md");
  const openW = new Set([...board.matchAll(/^ID:\s*(W-\d+)/gm)].map((m) => m[1]));
  const since = changelog.match(MARK)?.[1];
  const raw = git(root, ["log", "--reverse", "--format=%h%x1f%s%x1f%(trailers:key=Refs,valueonly,separator=%x2C)%x1e", since ? `${since}..HEAD` : "HEAD"]) || "";
  const commits = raw.split("\x1e").map((r) => r.trim().split("\x1f")).filter((p) => p.length === 3)
    .map((p) => ({ sha: p[0], subject: p[1], refs: p[2].split(/[,\s]+/).filter(Boolean) }));
  for (const c of unlinked(commits, log + "\n" + changelog, openW)) findings.push(`unlinked commit ${c.sha} ${c.subject} (not in any entry, no Refs: to an open W)`);
  const adrDir = path.join(root, "docs/adr");
  for (const a of missingAdrs(log + "\n" + changelog, existsSync(adrDir) ? readdirSync(adrDir) : [])) findings.push(`${a} is named in an entry but docs/adr/ has no such file`);
  for (const id of closedMissingFromChangelog(log, changelog)) findings.push(`${id} has a project-log entry but no matching CHANGELOG.md entry (they are appended together)`);
  for (const id of closedIds(log)) if (openW.has(id)) findings.push(`${id} is closed in the project log but still on the board`);
  return findings;
}

if (process.argv[1] && fileURLToPath(import.meta.url) === path.resolve(process.argv[1])) {
  const root = process.cwd();
  const findings = run(root);
  for (const f of findings) console.warn(`commit-link-check: ${f}`);
  if (!findings.length) {
    console.log("commit-link-check: clean");
    if (process.argv.includes("--advance")) {
      const head = git(root, ["rev-parse", "--short=7", "HEAD"]);
      const text = rd(root, "CHANGELOG.md");
      const next = MARK.test(text) ? text.replace(MARK, `<!-- commit-link:since=${head} -->`) : text.replace(/^## /m, `<!-- commit-link:since=${head} -->\n\n## `);
      writeFileSync(path.join(root, "CHANGELOG.md"), next);
    }
  }
  process.exit(findings.length && process.argv.includes("--strict") ? 1 : 0);
}
