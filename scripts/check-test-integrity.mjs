#!/usr/bin/env node
// check-test-integrity.mjs: ADVISORY pre-commit check over the STAGED diff (C# / xUnit).
// Warns on (a) newly added `Skip =` in [Fact]/[Theory], (b) removed Assert.* / .Should() lines
// in a modified test file, (c) deleted test files under tests/ or *.Tests/. ALWAYS exits 0.
//
// Turns "never fake or game tests to pass" from prose into a check. A deliberate skip
// (e.g. a committed RED test) is exempt when `RED:` appears on the same line or the line above.
//
// Detection approach adapted from mertkayacs/ultramod plugin/hooks/lib/testguard.ts
// (MIT, Copyright (c) 2026 Mert Kaya): isTestPath, added-marker count, assertion drop.
import { spawnSync } from "node:child_process";
import { pathToFileURL } from "node:url";

/** Test paths: anything under a tests/ or test/ dir, a *.Tests / *.ArchTests project dir, or *Tests.cs. */
export function isTestPath(path) {
  const segs = path.replace(/\\/g, "/").split("/").filter((s) => s && s !== ".");
  if (segs.slice(0, -1).some((s) => s === "tests" || s === "test" || /\.(Arch)?Tests?$/.test(s))) return true;
  const base = segs[segs.length - 1] ?? "";
  return /Tests?\.cs$/.test(base);
}

/** Drop lines carrying a RED: marker, and the line below a RED: comment line. */
export function stripRedExempt(text) {
  const out = [];
  let prevRed = false;
  for (const line of text.split("\n")) {
    const red = /\bRED:/.test(line);
    if (!red && !prevRed) out.push(line);
    prevRed = red;
  }
  return out.join("\n");
}

const SKIP_RE = /\b(?:Fact|Theory)\s*\([^)\]]*\bSkip\s*=/g;
const countSkips = (t) => (stripRedExempt(t).match(SKIP_RE) ?? []).length;

/** True when `after` has more `[Fact(Skip=...)]` / `[Theory(Skip=...)]` than `before`. */
export function addedSkips(before, after) {
  return countSkips(after) > countSkips(before);
}

const ASSERT_RE = /\bAssert\.\w+\s*\(|\.Should\s*\(\s*\)/g;
const countAssertions = (t) => (t.match(ASSERT_RE) ?? []).length;

/** How many fewer assertions `after` has than `before`; 0 when none dropped. */
export function assertionDrop(before, after) {
  return Math.max(0, countAssertions(before) - countAssertions(after));
}

/** entries: [{status:'A'|'M'|'D'|'R', path, before, after}] -> findings strings. */
export function analyze(entries) {
  const findings = [];
  for (const e of entries) {
    if (!isTestPath(e.path)) continue;
    if (e.status === "D") {
      findings.push(`deleted test file: ${e.path}`);
      continue;
    }
    const before = e.before ?? "";
    const after = e.after ?? "";
    if (addedSkips(before, after)) findings.push(`new Skip = in [Fact]/[Theory] in ${e.path} (add a "RED:" comment if deliberate)`);
    const drop = assertionDrop(before, after);
    if (drop) findings.push(`${drop} fewer Assert.*/Should() in ${e.path}`);
  }
  return findings;
}

const git = (args) => {
  const r = spawnSync("git", args, { encoding: "utf8", maxBuffer: 64 * 1024 * 1024 });
  return r.status === 0 ? r.stdout : null;
};

function stagedEntries() {
  const out = git(["diff", "--cached", "--name-status", "-M", "--diff-filter=ADMR"]);
  if (!out) return [];
  return out.split("\n").filter(Boolean).map((line) => {
    const [st, p1, p2] = line.split("\t");
    const status = st[0];
    const path = status === "R" ? p2 : p1;
    return {
      status,
      path,
      before: status === "A" ? "" : git(["show", `HEAD:${p1}`]) ?? "",
      after: status === "D" ? "" : git(["show", `:${path}`]) ?? "",
    };
  });
}

if (import.meta.url === pathToFileURL(process.argv[1] ?? "").href) {
  try {
    for (const f of analyze(stagedEntries())) console.warn(`[test-integrity] WARN ${f}`);
  } catch (err) {
    console.warn(`[test-integrity] skipped: ${err?.message ?? err}`);
  }
  process.exit(0);
}
