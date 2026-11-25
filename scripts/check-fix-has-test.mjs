#!/usr/bin/env node
// commit-msg gate: a `fix(...)` commit whose staged diff touches src/ must also touch tests/,
// or carry a `Tests: none (<reason>)` trailer. Rule from the owner's mobile app repo (its G1 gate).
// Usage (lefthook commit-msg): node scripts/check-fix-has-test.mjs <commit-msg-file>
// The staged file list is read from `git diff --cached --name-only` unless FIX_FILES (newline list) is set (tests).
import { readFileSync } from "node:fs";
import { spawnSync } from "node:child_process";
import path from "node:path";
import { fileURLToPath } from "node:url";

const FIX_RE = /^fix(\([^)]*\))?!?:/;
const NONE_RE = /^Tests:\s*none\s*\((.{10,})\)\s*$/im;

export const touchesSrc = (files) => files.some((f) => f.startsWith("src/") && !/\.(md|txt)$/.test(f));
export const touchesTests = (files) => files.some((f) => f.startsWith("tests/"));

export function check(message, files) {
  const subject = message.split("\n")[0];
  if (!FIX_RE.test(subject)) return { ok: true };
  if (!touchesSrc(files)) return { ok: true };
  if (touchesTests(files)) return { ok: true };
  const body = message.split("\n").filter((l) => !l.startsWith("#")).join("\n");
  if (NONE_RE.test(body)) return { ok: true };
  return {
    ok: false,
    error:
      "a fix(...) commit touches src/ but not tests/. Pin the fix with a regression test in the same commit,\n" +
      "or add a trailer with a real reason (10+ characters):\n\n  Tests: none (<why a test is not possible here>)",
  };
}

function stagedFiles() {
  if (process.env.FIX_FILES !== undefined) return process.env.FIX_FILES.split("\n").map((s) => s.trim()).filter(Boolean);
  const r = spawnSync("git", ["diff", "--cached", "--name-only"], { encoding: "utf8" });
  return r.status === 0 ? r.stdout.split("\n").filter(Boolean) : [];
}

if (process.argv[1] && fileURLToPath(import.meta.url) === path.resolve(process.argv[1])) {
  const file = process.argv[2];
  if (!file) {
    console.error("usage: check-fix-has-test.mjs <commit-msg-file>");
    process.exit(2);
  }
  const res = check(readFileSync(file, "utf8"), stagedFiles());
  if (!res.ok) {
    console.error(`fix-has-test: ${res.error}`);
    process.exit(1);
  }
}
