import { test } from "node:test";
import assert from "node:assert/strict";
import { mkdtempSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import path from "node:path";
import { isSeam, seamFiles, fingerprint, decide, launchedOn, parsePushRefs, looksLikeError, DAILY_CAP } from "./codex-gate.mjs";

test("seam detection", () => {
  for (const f of [
    "src/Fugue.Infrastructure/Persistence/Migrations/20261125_X.cs",
    "src/Fugue.Api/Fugue.Api.csproj",
    "Directory.Build.props",
    "global.json",
    "src/Fugue.Api/Controllers/ApiControllerBase.cs",
    "src/Fugue.Application/Identity/Tokens.cs",
    "src/Fugue.Api/AuthSetup/Jwt.cs",
    ".claude/settings.json",
    "scripts/check-refs-trailer.mjs",
    "lefthook.yml",
  ])
    assert.ok(isSeam(f), f);
  for (const f of ["src/Fugue.Domain/Customer.cs", "docs/a.md", "scripts/x.test.mjs", "tests/Fugue.Tests/A.cs"]) assert.ok(!isSeam(f), f);
  assert.deepEqual(seamFiles(["b/Directory.Packages.props", "global.json", "docs/x.md"]), ["global.json"]);
});

const dir = mkdtempSync(path.join(tmpdir(), "cg-"));
const put = (h, e) => writeFileSync(path.join(dir, `${h}.json`), JSON.stringify(e));

test("none / launch / dedup by diff hash", () => {
  assert.equal(decide({ files: ["docs/a.md"], hash: "h0", dir, day: "2026-11-25" }), "none");
  assert.equal(decide({ files: ["global.json"], hash: "h1", dir, day: "2026-11-25" }), "launch");
  put("h1", { status: "spawned", at: "2026-11-25T01:00:00Z" });
  assert.equal(decide({ files: ["global.json"], hash: "h1", dir, day: "2026-11-25" }), "dedup");
  put("h2", { status: "skipped", at: "2026-11-25T01:00:00Z" });
  assert.equal(decide({ files: ["global.json"], hash: "h2", dir, day: "2026-11-25" }), "launch", "a skip is retried, not deduped");
});
test("daily cap counts launches of that day only", () => {
  for (let i = 0; i < DAILY_CAP; i++) put(`c${i}`, { status: "reviewed", at: "2026-11-26T01:00:00Z" });
  assert.equal(launchedOn(dir, "2026-11-26"), DAILY_CAP);
  assert.equal(decide({ files: ["global.json"], hash: "new", dir, day: "2026-11-26" }), "cap");
  assert.equal(decide({ files: ["global.json"], hash: "new", dir, day: "2026-11-27" }), "launch");
});
test("fingerprint is stable and diff-sensitive", () => {
  assert.equal(fingerprint(["a"], "d"), fingerprint(["a"], "d"));
  assert.notEqual(fingerprint(["a"], "d"), fingerprint(["a"], "e"));
});
test("push refs parse; error-looking output is not a review", () => {
  assert.equal(parsePushRefs("refs/heads/x abc1234 refs/heads/x 0000000").length, 1);
  assert.ok(looksLikeError("") && looksLikeError("Error: not logged in") && looksLikeError("You hit your usage limit"));
  assert.ok(!looksLikeError("NO FINDINGS."));
});
