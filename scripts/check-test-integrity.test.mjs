import { test } from "node:test";
import assert from "node:assert/strict";
import { isTestPath, addedSkips, assertionDrop, analyze } from "./check-test-integrity.mjs";

test("isTestPath", () => {
  assert.ok(isTestPath("tests/Fugue.Tests/OrderTests.cs"));
  assert.ok(isTestPath("src/Fugue.Tests/Catalog/ProductTests.cs"));
  assert.ok(isTestPath("Fugue.ArchTests/LayerTests.cs"));
  assert.ok(!isTestPath("src/Fugue.Domain/Order.cs"));
});

test("addedSkips detects Skip= and honours RED:", () => {
  assert.ok(addedSkips("[Fact]\npublic void A(){}", '[Fact(Skip = "later")]\npublic void A(){}'));
  assert.ok(!addedSkips("", '// RED: pending module\n[Fact(Skip = "later")]\npublic void A(){}'));
  assert.ok(!addedSkips('[Fact(Skip = "x")]', '[Fact(Skip = "x")]'));
});

test("assertionDrop counts Assert.* and Should()", () => {
  assert.equal(assertionDrop("Assert.Equal(1,x);\nx.Should().Be(1);", "Assert.Equal(1,x);"), 1);
  assert.equal(assertionDrop("a", "Assert.True(b);"), 0);
});

test("analyze reports deleted test files and ignores non-tests", () => {
  const f = analyze([
    { status: "D", path: "tests/Fugue.Tests/A.cs" },
    { status: "D", path: "src/Fugue.Api/Program.cs" },
  ]);
  assert.equal(f.length, 1);
});
