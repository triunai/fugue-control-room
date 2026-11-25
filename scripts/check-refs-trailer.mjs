#!/usr/bin/env node
// commit-msg gate: an agent-authored commit must end with a RESOLVABLE `Refs:` git trailer.
// Agent = the message carries a `Co-Authored-By:` trailer naming Claude (or another model). Human commits are exempt.
// Resolvable ids: W-NNN (ID: line in docs/workstreams.md, or `Closed: W-NNN` in project-log / archive / CHANGELOG once closed), D-NNN (## heading in docs/decisions.md),
// ADR-NNN (file docs/adr/ADR-NNN-*.md). Beads ids are local-only and deliberately NOT accepted (D-013).
// Ported from the owner's multi-tenant SaaS repo; adapted to Fugue's paths and without the bead lookup.
// Usage (lefthook commit-msg): node scripts/check-refs-trailer.mjs <commit-msg-file>
// Env (tests): REFS_ROOT = repo root to resolve ids against.
import { readFileSync, existsSync, readdirSync } from "node:fs";
import { spawnSync } from "node:child_process";
import path from "node:path";
import { fileURLToPath } from "node:url";

const AGENT_RE = /(claude|codex|sonnet|opus|haiku|fable)/i;
const FORMAT = `Expected a git trailer in the FINAL paragraph of the commit message:

  Refs: W-008, D-013            (several ids in one trailer are fine; every one must resolve)

Ids: W-NNN workstream (docs/workstreams.md), D-NNN decision (docs/decisions.md), ADR-NNN (docs/adr/).
A "Refs:" line above the trailer block is invisible to git. Trailers go last, e.g.:

  Refs: W-008
  Co-Authored-By: Claude <noreply@anthropic.com>`;

export function parseTrailers(message) {
  const clean = message
    .split("\n")
    .filter((l) => !l.startsWith("#"))
    .join("\n");
  const r = spawnSync("git", ["interpret-trailers", "--parse"], { input: clean, encoding: "utf8" });
  if (r.status !== 0) return [];
  return r.stdout
    .split("\n")
    .filter(Boolean)
    .map((l) => {
      const i = l.indexOf(":");
      return i < 0 ? null : { key: l.slice(0, i).trim(), value: l.slice(i + 1).trim() };
    })
    .filter(Boolean);
}

function readIf(p) {
  try {
    return readFileSync(p, "utf8");
  } catch {
    return "";
  }
}

function logText(root) {
  // warm log + archived months + CHANGELOG (the permanent ledger, so a closed W resolves even if the log was pruned)
  const dir = path.join(root, "docs/archive/project-log");
  const archived = existsSync(dir) ? readdirSync(dir).map((f) => readIf(path.join(dir, f))) : [];
  return [readIf(path.join(root, "docs/project-log.md")), readIf(path.join(root, "CHANGELOG.md")), ...archived].join("\n");
}

export function resolveId(id, root) {
  if (/^W-\d+$/.test(id))
    // live thread on the board, or a pruned one whose journal entry has a `Closed: W-NNN ...` line
    return (
      new RegExp(`^ID:\\s*${id}\\s*$`, "m").test(readIf(path.join(root, "docs/workstreams.md"))) ||
      new RegExp(`^Closed:\\s*${id}\\b`, "m").test(logText(root))
    );
  if (/^D-\d+$/.test(id))
    return new RegExp(`^##\\s+D-0*${Number(id.slice(2))}\\b`, "m").test(readIf(path.join(root, "docs/decisions.md")));
  if (/^ADR-\d+$/.test(id)) {
    const dir = path.join(root, "docs/adr");
    return existsSync(dir) && readdirSync(dir).some((f) => f.startsWith(`${id}-`) && f.endsWith(".md"));
  }
  return false;
}

export function check(message, root) {
  const subject = message.split("\n")[0];
  if (/^(Merge |fixup! |squash! )/.test(subject)) return { ok: true, notes: ["skipped (merge/fixup/squash)"] };
  const trailers = parseTrailers(message);
  const agent = trailers.some((t) => /^co-authored-by$/i.test(t.key) && AGENT_RE.test(t.value.replace(/<.*>/, "")));
  if (!agent) return { ok: true, notes: [] };
  const refs = trailers.filter((t) => /^refs$/i.test(t.key));
  if (!refs.length)
    return { ok: false, errors: ["agent commit has no `Refs:` trailer (a Refs line above the trailer block does not count)."] };
  const ids = refs
    .flatMap((t) => t.value.split(/[,;\s]+/))
    .map((s) => s.replace(/\.$/, ""))
    .filter(Boolean);
  // several ids per trailer (`Refs: W-012, ADR-010, D-022`) and several `Refs:` trailers are both fine; EVERY id must resolve.
  if (!ids.length) return { ok: false, errors: ["`Refs:` trailer has no ids."] };
  const errors = [...new Set(ids)]
    .filter((id) => !resolveId(id, root))
    .map((id) => `Refs id does not resolve (only W-/D-/ADR- ids that exist are accepted): ${id}`);
  return errors.length ? { ok: false, errors } : { ok: true, notes: [] };
}

if (process.argv[1] && fileURLToPath(import.meta.url) === path.resolve(process.argv[1])) {
  const file = process.argv[2];
  if (!file) {
    console.error("usage: check-refs-trailer.mjs <commit-msg-file>");
    process.exit(2);
  }
  const res = check(readFileSync(file, "utf8"), process.env.REFS_ROOT || process.cwd());
  for (const n of res.notes || []) console.warn(`refs-trailer: ${n}`);
  if (!res.ok) {
    for (const e of res.errors) console.error(`refs-trailer: ${e}`);
    console.error(`\n${FORMAT}`);
    process.exit(1);
  }
}
