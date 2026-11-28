import { test } from "node:test";
import assert from "node:assert/strict";
import { mkdtempSync, mkdirSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import path from "node:path";
import { generate, parseAdr, parseChangelog, parseDecisions, parseVersions, slug, versionFor } from "./gen-decisions.mjs";

const DECISIONS = `# Fugue — decisions

## Index

| D-001 | x |

## D-065 — Newest after the release — 2026-11-28
WS: WS-22
Context: why.
Decision: ADR-014. Lanes in worktrees.
Status: accepted
Promote: ADR-014

## D-21 — Two-digit id, older style — 2026-11-25
WS: WS-05
Context: lean port.
Decision: not built in S3: SSO columns.
Status: accepted (revised)
Promote: no

## D-003 — Three-layer correlation — 2026-11-25
Context: only a context line here.
Status: accepted
Promote: yes (ADR-005)

## D-099 — No date in the heading
Decision: x.
`;

const ADR_COLON = `# ADR-001: Shared database with a \`tenant_id\` column

- Status: Superseded by ADR-003
- Date: 2026-11-11

## Context
Long context.

## Decision
One shared Postgres database. Every row carries a tenant_id.

More text.

## Consequences
x
`;

const ADR_DASH = `# ADR-010 — Rich append-only audit log

- Status: accepted
- Date: 2026-11-26

## Decision
* **Table.** \`audit_log\`: UUIDv7 id.
* **Append-only.** A trigger.
`;

const CHANGELOG = `# Changelog

intro

## v0.0.2 — 2026-11-27
Release: the claims platform, frozen for the walkthrough.
No label here, it is a sentence. Second sentence: with a colon.

## 2026-11-27 — Closed W-9: Something shipped
WS: WS-22
Outcome: It landed. Released as v0.0.2.
Commits:
- abc1234 feat(x): a
- def5678 fix(x): b

## 2026-11-25 — S2 — old style, no work item
Text.
`;

test("parseChangelog reads release notes with optional labels and closed work items with commit counts", () => {
  const { releases, closed } = parseChangelog(CHANGELOG);
  assert.equal(releases.length, 1);
  assert.deepEqual(releases[0].notes[0], { label: "Release", text: "the claims platform, frozen for the walkthrough." });
  assert.equal(releases[0].notes[1].label, "");
  assert.equal(closed.length, 1);
  assert.deepEqual(closed[0], { id: "W-9", title: "Something shipped", date: "2026-11-27", outcome: "It landed. Released as v0.0.2.", commits: 2 });
});

test("slug matches GitHub anchors for the D heading shape", () => {
  assert.equal(slug("D-065 — Owner-steered lanes, run — 2026-11-28"), "d-065--owner-steered-lanes-run--2026-11-28");
});

test("parseVersions reads version headings oldest first and assigns by date range", () => {
  const v = parseVersions("## v0.0.2 — 2026-11-27\nx\n## v0.0.1 — 2026-11-20\n## 2026-11-26 — Closed W-1: not a version\n");
  assert.deepEqual(v.map((x) => x.version), ["v0.0.1", "v0.0.2"]);
  assert.equal(versionFor("2026-11-20", v).version, "v0.0.1");
  assert.equal(versionFor("2026-11-21", v).version, "v0.0.2");
  assert.equal(versionFor("2026-11-28", v), null);
});

test("parseDecisions reads three- and two-digit ids, falls back to Context, links the ADR, reports bad headings", () => {
  const { entries, unparsed } = parseDecisions(DECISIONS);
  assert.deepEqual(entries.map((e) => e.id), ["D-065", "D-021", "D-003"]);
  assert.equal(entries[0].summary, "ADR-014. Lanes in worktrees.");
  assert.equal(entries[0].adr, "ADR-014");
  assert.equal(entries[1].summary, "Not built in S3: SSO columns.");
  assert.equal(entries[2].summary, "Only a context line here.");
  assert.equal(entries[2].adr, "ADR-005");
  assert.equal(entries[1].path, "docs/decisions.md#d-21--two-digit-id-older-style--2026-11-25");
  assert.equal(unparsed.length, 1);
  assert.match(unparsed[0], /D-099/);
});

test("parseAdr reads both heading styles and the first Decision paragraph", () => {
  const a = parseAdr(ADR_COLON, "docs/adr/ADR-001-x.md").entry;
  assert.deepEqual([a.id, a.title, a.date, a.status], ["ADR-001", "Shared database with a tenant_id column", "2026-11-11", "Superseded by ADR-003"]);
  assert.equal(a.summary, "One shared Postgres database. Every row carries a tenant_id.");
  const b = parseAdr(ADR_DASH, "docs/adr/ADR-010-x.md").entry;
  assert.deepEqual([b.id, b.title], ["ADR-010", "Rich append-only audit log"]);
  assert.match(b.summary, /^Table\. audit_log: UUIDv7 id\.$/);
  assert.ok(parseAdr("no heading", "f.md").error);
});

test("generate groups newest first, Unreleased on top, skips the template, is deterministic", () => {
  const root = mkdtempSync(path.join(tmpdir(), "decisions-"));
  mkdirSync(path.join(root, "docs/adr"), { recursive: true });
  writeFileSync(path.join(root, "CHANGELOG.md"), CHANGELOG);
  writeFileSync(path.join(root, "docs/decisions.md"), DECISIONS);
  writeFileSync(path.join(root, "docs/adr/ADR-001-x.md"), ADR_COLON);
  writeFileSync(path.join(root, "docs/adr/ADR-010-x.md"), ADR_DASH);
  writeFileSync(path.join(root, "docs/adr/_template.md"), "# ADR-NNN: template\n- Date: YYYY-MM-DD\n");
  const r = generate(root);
  assert.deepEqual(r.data.counts, { adrs: 2, decisions: 3, releases: 1, closed: 1 });
  assert.equal(r.data.groups[1].notes.length, 2);
  assert.deepEqual(r.data.groups[1].closed.map((c) => c.id), ["W-9"]);
  assert.deepEqual(r.data.groups.map((g) => g.version), ["Unreleased", "v0.0.2"]);
  assert.deepEqual(r.data.groups[0].entries.map((e) => e.id), ["D-065"]);
  assert.deepEqual(r.data.groups[1].entries.map((e) => e.id), ["ADR-010", "D-021", "D-003", "ADR-001"]);
  assert.equal(r.unparsed.length, 1);
  assert.deepEqual(generate(root), r);
});

test("generate works when nothing is dated after the newest release (no Unreleased group)", () => {
  const root = mkdtempSync(path.join(tmpdir(), "decisions-"));
  mkdirSync(path.join(root, "docs/adr"), { recursive: true });
  writeFileSync(path.join(root, "CHANGELOG.md"), "# Changelog\n\n## v0.0.9 — 2099-01-01\nRelease: far future.\n\n" + CHANGELOG.replace(/^# Changelog\n/, ""));
  writeFileSync(path.join(root, "docs/decisions.md"), DECISIONS);
  writeFileSync(path.join(root, "docs/adr/ADR-001-x.md"), ADR_COLON);
  const r = generate(root);
  assert.equal(r.data.groups[0].version, "v0.0.9");
  assert.ok(!r.data.groups.some((g) => g.version === "Unreleased"));
});
