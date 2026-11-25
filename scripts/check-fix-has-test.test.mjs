import { test } from "node:test";
import assert from "node:assert/strict";
import { check } from "./check-fix-has-test.mjs";

const SRC = ["src/Fugue.Api/Program.cs"];
test("fix touching src without tests is rejected", () => assert.equal(check("fix(api): handle null\n", SRC).ok, false));
test("fix touching src and tests passes", () =>
  assert.equal(check("fix(api): handle null\n", [...SRC, "tests/Fugue.Tests/ApiTests.cs"]).ok, true));
test("Tests: none (reason) passes, a short reason does not", () => {
  assert.equal(check("fix(api): x y z\n\nTests: none (config-only change, nothing to execute)\n", SRC).ok, true);
  assert.equal(check("fix(api): x y z\n\nTests: none (n/a)\n", SRC).ok, false);
});
test("fix that only touches docs or scripts passes", () => assert.equal(check("fix(docs): typo\n", ["docs/a.md", "scripts/x.mjs"]).ok, true));
test("non-fix commits are not gated", () => assert.equal(check("feat(api): add x\n", SRC).ok, true));
