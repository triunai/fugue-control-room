import { test } from "node:test";
import assert from "node:assert/strict";
import { doneThreadsOnBoard, droppedWithoutLog, hotStateFindings } from "./prune-check.mjs";
import { check as adrCheck } from "./adr-check.mjs";
import { findThread, removeThread, buildEntry, prepend } from "./close-workstream.mjs";
import { unlinked, missingAdrs, closedMissingFromChangelog } from "./commit-link-check.mjs";
import { plan } from "./archive-project-log.mjs";

test("prune-check: done thread still on the board is flagged", () => {
  assert.deepEqual(doneThreadsOnBoard("### WS-02 — x — ✅ DONE (d)\n### WS-05 — y — ⏸ PARKED (d)\n"), ["WS-02"]);
});
test("prune-check: a W in the changelog that left the board needs a Closed: line", () => {
  const workstreams = "### WS-05 — y — ⏸ PARKED (d)\nID: W-005\n";
  const changelog = "## 2026-11-25 — W-002 S0\n## 2026-11-25 — W-005 S3\n";
  assert.deepEqual(droppedWithoutLog({ workstreams, projectLog: "", changelog }), ["W-002"]);
  assert.deepEqual(droppedWithoutLog({ workstreams, projectLog: "Closed: W-002 (WS-02) S0\n", changelog }), []);
});
test("prune-check: hot-state sha and size", () => {
  assert.equal(hotStateFindings({ text: "Last green sha: abc1234\n", shaKnown: true, behind: 3 }).length, 0);
  assert.equal(hotStateFindings({ text: "Last green sha: abc1234\n", shaKnown: false, behind: 0 }).length, 1);
  assert.equal(hotStateFindings({ text: "Last green sha: abc1234\n", shaKnown: true, behind: 99 }).length, 1);
  assert.equal(hotStateFindings({ text: "x\n".repeat(60), shaKnown: true, behind: 0 }).length, 2);
});
test("adr-check: unlinked ADR and unresolved promotion", () => {
  const decisions = "## D-001 — a — d\nWS: WS-01\nPromote: yes (ADR-009)\n\n## D-002 — b — d\nPromote: yes\n\nADR-001 is linked\n";
  const f = adrCheck({ decisions, adrFiles: ["ADR-001-x.md", "ADR-002-y.md", "_template.md"] });
  assert.equal(f.length, 3);
  assert.ok(f.some((x) => x.startsWith("ADR-002")) && f.some((x) => x.includes("ADR-009")) && f.some((x) => x.startsWith("D-002")));
});

const board = "# Board\n\n### WS-04 — S2 persistence — ✅ DONE (2026-11-25)\nID: W-004\nStatus: x\n\n### WS-05 — S3 — ⏸ PARKED (2026-11-25)\nID: W-005\nStatus: y\n";

test("close-workstream: finds the thread, removes only its row, builds an entry with commits and links", () => {
  const t = findThread(board, "W-004");
  assert.equal(t.ws, "WS-04");
  assert.equal(t.title, "S2 persistence");
  const after = removeThread(board, t);
  assert.ok(!after.includes("W-004") && after.includes("### WS-05") && after.includes("ID: W-005"));
  const e = buildEntry({ id: "W-004", ws: "WS-04", title: t.title, date: "2026-11-25", outcome: "done", commits: ["abc1234 feat: x"], decisions: "D-004, ADR-006", followups: "" });
  assert.match(e, /^## 2026-11-25 — Closed W-004: S2 persistence\nWS: WS-04\nClosed: W-004 \(WS-04\) S2 persistence\n/);
  assert.match(e, /- abc1234 feat: x\nDecisions: D-004, ADR-006\nFollow-ups: none/);
  assert.equal(findThread(board, "W-999"), null);
});
test("close-workstream: the same entry text is prepended below the header, newest first, never rewriting old entries", () => {
  const e1 = buildEntry({ id: "W-001", ws: "WS-01", title: "a", date: "2026-11-01", outcome: "o", commits: [] });
  const once = prepend("# Log\n\n## 2026-10-01 — old\nWS: (pre-WS)\n", e1);
  assert.ok(once.indexOf("Closed W-001") < once.indexOf("2026-10-01 — old"));
  assert.ok(once.endsWith("WS: (pre-WS)\n"));
});
test("commit-link-check: unlinked commits, missing ADRs, project-log/CHANGELOG parity", () => {
  const commits = [{ sha: "aaaaaaa", refs: [] }, { sha: "bbbbbbb", refs: ["W-005"] }, { sha: "ccccccc", refs: ["W-004"] }];
  const u = unlinked(commits, "- ccccccc listed", new Set(["W-005"]));
  assert.deepEqual(u.map((c) => c.sha), ["aaaaaaa"]);
  assert.deepEqual(missingAdrs("see ADR-006 and ADR-099", ["ADR-006-x.md"]), ["ADR-099"]);
  assert.deepEqual(closedMissingFromChangelog("Closed: W-001 (WS-01) a\nClosed: W-002 (WS-02) b\n", "Closed: W-001 (WS-01) a\n"), ["W-002"]);
});
test("archive-project-log: moves entries older than 30 days or beyond the latest 20, verbatim", () => {
  const mk = (d, n) => `## ${d} — e${n}\nWS: WS-01\n`;
  const fresh = Array.from({ length: 22 }, (_, i) => mk("2026-11-20", i));
  const p = plan([...fresh, mk("2026-09-01", 99)], "2026-11-25");
  assert.equal(p.keep.length, 20);
  assert.equal(p.move.length, 3);
  assert.equal(p.move.at(-1).month, "2026-09");
  assert.equal(p.move.at(-1).text, mk("2026-09-01", 99));
});
