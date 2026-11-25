/**
 * commitlint: Conventional Commits plus Fugue scopes.
 * Runs from lefthook commit-msg alongside the kit's .githooks/commit-msg rule
 * (invoked as a script by lefthook; core.hooksPath is NOT set).
 *
 * scope-enum is WARN-only (severity 1). Flip to 2 once the list has settled.
 */
module.exports = {
  extends: ["@commitlint/config-conventional"],
  rules: {
    "type-enum": [
      2,
      "always",
      ["feat", "fix", "docs", "style", "refactor", "perf", "test", "build", "ci", "chore", "revert"],
    ],
    "scope-enum": [
      1,
      "always",
      ["api", "application", "domain", "infra", "identity", "observability", "persistence", "messaging", "tests", "arch", "docs", "ci", "spine", "deps"],
    ],
    "header-max-length": [2, "always", 110],
    "subject-case": [0, "never"],
    "scope-empty": [0, "never"],
  },
};
