import { test } from "node:test";
import assert from "node:assert/strict";
import { mkdtempSync, mkdirSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import path from "node:path";
import { check } from "./check-refs-trailer.mjs";

const root = mkdtempSync(path.join(tmpdir(), "refs-"));
mkdirSync(path.join(root, "docs/adr"), { recursive: true });
writeFileSync(path.join(root, "docs/workstreams.md"), "### WS-08 — H1 — x\nID: W-008\n");
writeFileSync(path.join(root, "docs/project-log.md"), "## 2026-11-25 — x\nWS: WS-02\nClosed: W-002 (WS-02) S0\n");
writeFileSync(path.join(root, "CHANGELOG.md"), "## x\nClosed: W-003 (WS-03) S1\n");
writeFileSync(path.join(root, "docs/decisions.md"), "## D-013 — local beads — 2026-11-25\n");
writeFileSync(path.join(root, "docs/adr/ADR-006-x.md"), "# ADR-006\n");

const AI = "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>";
const msg = (...trailers) => `feat(spine): add a thing\n\nBody text.\n\n${trailers.join("\n")}\n`;

test("agent commit without Refs is rejected", () => assert.equal(check(msg(AI), root).ok, false));
test("agent commit with resolvable W-, D-, ADR- ids passes", () => {
  assert.equal(check(msg("Refs: W-008", AI), root).ok, true);
  assert.equal(check(msg("Refs: D-013, ADR-006", AI), root).ok, true);
});
test("a pruned W resolves through its Closed: line", () => assert.equal(check(msg("Refs: W-002", AI), root).ok, true));
test("unresolvable and bead-style ids are rejected", () => {
  assert.equal(check(msg("Refs: W-999", AI), root).ok, false);
  assert.equal(check(msg("Refs: fg-abc", AI), root).ok, false);
});
test("Refs above the trailer block does not count", () => {
  assert.equal(check(`feat(spine): add a thing\n\nRefs: W-008\n\nProse paragraph.\n\n${AI}\n`, root).ok, false);
});
test("human commits are exempt", () => assert.equal(check("feat(spine): human change\n\nNo trailers.\n", root).ok, true));
test("merge commits are skipped", () => assert.equal(check(`Merge branch 'x'\n\n${AI}\n`, root).ok, true));

test("multi-id Refs: every id must resolve", () => {
  assert.equal(check(msg("Refs: W-008, ADR-006, D-013", AI), root).ok, true);
  assert.equal(check(msg("Refs: W-008 ADR-006; D-013", AI), root).ok, true);
  assert.equal(check(msg("Refs: W-008", "Refs: ADR-006", AI), root).ok, true);
});
test("multi-id Refs: one unknown id rejects the commit and is named", () => {
  const r = check(msg("Refs: W-008, ADR-010, D-013", AI), root);
  assert.equal(r.ok, false);
  assert.match(r.errors.join("\n"), /ADR-010/);
  assert.doesNotMatch(r.errors.join("\n"), /W-008|D-013/);
});
test("a closed W resolves via the project log or CHANGELOG, not just the live board", () => {
  assert.equal(check(msg("Refs: W-002, W-003", AI), root).ok, true);
  assert.equal(check(msg("Refs: W-004", AI), root).ok, false);
});
test("D ids resolve by number, so D-016 matches a legacy `## D-16` heading", () => {
  writeFileSync(path.join(root, "docs/decisions.md"), "## D-013 — a — x\n## D-16 — legacy — x\n");
  assert.equal(check(msg("Refs: D-016, D-013", AI), root).ok, true);
  assert.equal(check(msg("Refs: D-017", AI), root).ok, false);
});
