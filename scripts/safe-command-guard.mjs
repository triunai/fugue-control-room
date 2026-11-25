#!/usr/bin/env node
// Claude Code PreToolUse(Bash) hook: blocks a small, precise set of catastrophic shell commands.
// Reduced port of the owner's multi-tenant SaaS repo guard (that one is ~900 lines of hardening;
// this keeps the protocol and the rule shape, with rules for a .NET + Postgres repo).
// DESIGN BIAS: a false positive trains people to ignore the guard, so every rule is narrow and
// anything unmatched is silently allowed. It is a safety net, not a sandbox: it reads the command text only.
//
// Protocol (Claude Code hooks): stdin JSON { tool_name, tool_input: { command } }.
// Block = exit 2 with the reason on stderr, plus the structured permissionDecision "deny" on stdout.
// Allow = exit 0, no output. Malformed input, other tools, unreadable stdin = allow.
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import path from "node:path";
import { execFileSync } from "node:child_process";

const norm = (c) => c.replace(/\s+/g, " ").trim();
/** Drop quoted strings so text inside a commit message cannot trigger a rule. */
const stripQuoted = (s) => s.replace(/"[^"]*"|'[^']*'/g, " ");
/** Split into simple shell statements on ; && || | and newlines. */
const statements = (cmd) => cmd.split(/&&|\|\||[;|\n]/).map((s) => norm(stripQuoted(s))).filter(Boolean);

let branchOverride = null;
export const setBranchForTests = (b) => (branchOverride = b);
function currentBranch() {
  if (branchOverride !== null) return branchOverride;
  try {
    return execFileSync("git", ["branch", "--show-current"], { encoding: "utf8", stdio: ["ignore", "pipe", "ignore"] }).trim();
  } catch {
    return "";
  }
}

const SAFE_RM_ROOTS = /^(\.\/)?(bin|obj|node_modules|TestResults|\.fugue-local|[\w./-]*\/(bin|obj))\/?$/;
function rmTargetsAreSafe(stmt) {
  const targets = stmt.replace(/^.*?\brm\s/, "").split(" ").filter((t) => t && !t.startsWith("-"));
  if (!targets.length) return true;
  return targets.every((t) => !t.startsWith("/") && !t.startsWith("~") && !t.includes("..") && t !== "." && t !== "*" && SAFE_RM_ROOTS.test(t));
}

export const RULES = [
  {
    reason: "force push rewrites shared history. Push normally; ask the owner if history truly must change.",
    test: (st) => st.some((s) => /\bgit\b.*\bpush\b/.test(s) && (/\s--force(-with-lease)?(=|\s|$)/.test(s) || /\s-[a-z]*f[a-z]*(\s|$)/.test(s) || /\s\+\S+/.test(s))),
  },
  {
    reason: "--no-verify skips the guardrail hooks. Fix the finding the hook reports instead (CLAUDE.md, Guardrail stack).",
    test: (st) => st.some((s) => /\bgit\b.*\b(commit|push|merge|rebase|cherry-pick|am)\b/.test(s) && (/\s--no-verify(\s|$)/.test(s) || /\s-n(\s|$)/.test(s) && /\bcommit\b/.test(s))),
  },
  {
    reason: "git reset --hard on main destroys unpushed work. Use a branch or `git revert`.",
    test: (st) => st.some((s) => /\bgit\b.*\breset\b.*--hard/.test(s)) && ["main", "master"].includes(currentBranch()),
  },
  {
    reason: "rm -rf outside bin/obj/node_modules/TestResults is blocked. Delete specific files, or ask the owner.",
    test: (st) => st.some((s) => /(^|\s)rm\s/.test(" " + s) && (/\s-\w*r\w*\b/.test(s) || /--recursive\b/.test(s)) && (/\s-\w*f\w*\b/.test(s) || /--force\b/.test(s)) && !rmTargetsAreSafe(s)),
  },
  {
    reason: "piping a download into a shell runs unreviewed code. Download, read, then run.",
    test: (_st, whole) => /\b(curl|wget)\b[^|]*\|\s*(sudo\s+)?(ba|z|da)?sh\b/.test(whole),
  },
  {
    reason: "this prints secrets into the transcript. Check that a variable is set without echoing its value.",
    test: (st) =>
      st.some(
        (s) =>
          /^(cat|less|more|head|tail|bat)\s+\S*\.env(\.(?!example)\S*)?(\s|$)/.test(s) ||
          /^(printenv|env)(\s|$)/.test(s) ||
          /\bdotnet user-secrets list\b/.test(s) ||
          /^echo\s+\$\{?\w*(PASSWORD|SECRET|TOKEN|KEY|CONNECTIONSTRING)\w*\}?/i.test(s),
      ),
  },
  {
    reason: "`dotnet ef database drop` is destructive. Run it with --dry-run first and let the owner run the real one.",
    test: (st) => st.some((s) => /\bdotnet ef database drop\b/.test(s) && !/--dry-run\b/.test(s)),
  },
];

/** Returns the reason string when the command must be blocked, else null. */
export function evaluate(command) {
  if (typeof command !== "string" || !command.trim()) return null;
  const st = statements(command);
  // curl|sh spans a pipe, so it is tested on the whole (de-quoted) command rather than per statement.
  const whole = norm(stripQuoted(command));
  for (const rule of RULES) {
    if (rule.test(st, whole)) return rule.reason;
  }
  return null;
}

function main() {
  let input;
  try {
    input = JSON.parse(readFileSync(0, "utf8"));
  } catch {
    return;
  }
  if (input?.tool_name !== "Bash") return;
  const reason = evaluate(input?.tool_input?.command);
  if (!reason) return;
  const msg = `safe-command-guard: BLOCKED. ${reason}`;
  process.stdout.write(
    JSON.stringify({ hookSpecificOutput: { hookEventName: "PreToolUse", permissionDecision: "deny", permissionDecisionReason: msg } }),
  );
  process.stderr.write(msg + "\n");
  process.exit(2);
}

if (process.argv[1] && fileURLToPath(import.meta.url) === path.resolve(process.argv[1])) main();
