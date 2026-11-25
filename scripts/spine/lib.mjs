// Shared helpers for the doc-lifecycle scripts (project-log is warm, docs/archive/project-log/ is cold, CHANGELOG is forever).
import { readFileSync, readdirSync, existsSync } from "node:fs";
import { execFileSync } from "node:child_process";
import path from "node:path";

export const rd = (root, p) => (existsSync(path.join(root, p)) ? readFileSync(path.join(root, p), "utf8") : "");
export const git = (root, args) => {
  try {
    return execFileSync("git", args, { cwd: root, encoding: "utf8", stdio: ["ignore", "pipe", "ignore"] }).trim();
  } catch {
    return null;
  }
};
/** project-log text plus every archived project-log month (verbatim entries). */
export function allLogText(root) {
  const dir = path.join(root, "docs/archive/project-log");
  const archived = existsSync(dir) ? readdirSync(dir).filter((f) => /^\d{4}-\d{2}\.md$/.test(f)).map((f) => readFileSync(path.join(dir, f), "utf8")) : [];
  return [rd(root, "docs/project-log.md"), ...archived].join("\n");
}
/** Entries = blocks that start at a `## ` heading. */
export function splitEntries(text) {
  const parts = text.split(/^(?=## )/m);
  return { head: parts[0].startsWith("## ") ? "" : parts[0], entries: parts[0].startsWith("## ") ? parts : parts.slice(1) };
}
export const closedIds = (text) => [...text.matchAll(/^Closed:\s*(W-\d+)/gm)].map((m) => m[1]);
