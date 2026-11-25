#!/usr/bin/env node
// Advisory, seam-triggered, capped, NON-BLOCKING cross-model review (Codex). Decision D-014.
// Ported from the owner's multi-tenant SaaS repo; reduced to: seam match -> dedupe by diff hash -> daily cap -> detached launch.
//
// Behaviour:
//   * Invoked by lefthook pre-push (git passes "<lref> <lsha> <rref> <rsha>" lines on stdin).
//   * Review scope is the pushed range remote_sha..local_sha (new branch: merge-base with origin's default branch).
//   * If no changed path matches a seam glob: silent. Same seam-diff hash seen before: deduped. 8 launches/day cap.
//   * The review runs in a DETACHED worker (`--worker`), so the push is never delayed. ALWAYS exits 0.
//   * The worker reads Codex OUTPUT (stdout / --output-last-message file), never the exit code (a crashed Codex has exited 0
//     before). Output that is empty or looks like an error is recorded as "failed", not "reviewed".
//   * Findings land in docs/reviews/codex/<date>-<shorthash>.md. Read them; they are advisory, never a verdict.
//
// Invocation (checked with `codex exec review --help`, codex-cli on this machine, NOT yet run end-to-end here):
//   codex exec review --base <base-sha> --title <title> -o <last-message-file> -
//   with review instructions on stdin. It reviews the checked-out HEAD against --base, so the gate only launches when the
//   pushed sha IS the checked-out HEAD.
//
// Flags: --dry-run (decide and print the command; record nothing, launch nothing) · --base=<ref> (explicit base, manual run)
// State: .fugue-local/codex-gate/ (gitignored). Test seams: CODEX_GATE_STATE_DIR.
import { spawn, spawnSync, execFileSync } from "node:child_process";
import { createHash } from "node:crypto";
import { closeSync, existsSync, fstatSync, mkdirSync, openSync, readFileSync, readdirSync, writeFileSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

export const DAILY_CAP = 8;
export const SEAM_GLOBS = [
  "src/Fugue.Infrastructure/Persistence/Migrations/**",
  "**/*.csproj",
  "Directory.*.props",
  "global.json",
  "src/Fugue.Api/Controllers/ApiControllerBase.cs",
  "src/**/Identity/**",
  "src/**/Auth*/**",
  ".claude/**",
  "scripts/**",
  "lefthook.yml",
];
const ZERO = /^0+$/;
const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");

export function globToRegExp(glob) {
  let re = "";
  for (let i = 0; i < glob.length; i++) {
    const c = glob[i];
    if (c === "*") {
      if (glob[i + 1] === "*") {
        if (glob[i + 2] === "/") {
          re += "(?:.*/)?";
          i += 2;
        } else {
          re += ".*";
          i += 1;
        }
      } else re += "[^/]*";
    } else if (c === "?") re += "[^/]";
    else re += c.replace(/[.+^${}()|[\]\\]/g, "\\$&");
  }
  return new RegExp("^" + re + "$");
}
const SEAM_RES = SEAM_GLOBS.map(globToRegExp);
/** A path is a seam when a glob matches. Test files are never seams (a test changing is not a contract change). */
export const isSeam = (f) => !/\.test\.mjs$/.test(f) && SEAM_RES.some((re) => re.test(f));
export const seamFiles = (files) => files.filter(isSeam).sort();
export const fingerprint = (files, diff) => createHash("sha256").update(files.join("\n") + "\n" + diff).digest("hex").slice(0, 16);

// ---- ledger: dedupe + daily cap ------------------------------------------------------------
export function readEntry(dir, hash) {
  try {
    return JSON.parse(readFileSync(path.join(dir, `${hash}.json`), "utf8"));
  } catch {
    return null;
  }
}
export const wasLaunched = (e) => Boolean(e) && e.status !== "skipped" && e.status !== "dry-run";
export function launchedOn(dir, day) {
  if (!existsSync(dir)) return 0;
  return readdirSync(dir)
    .filter((f) => f.endsWith(".json"))
    .map((f) => readEntry(dir, f.slice(0, -5)))
    .filter((e) => wasLaunched(e) && typeof e.at === "string" && e.at.slice(0, 10) === day).length;
}
/** Pure decision: "none" | "dedup" | "cap" | "launch". */
export function decide({ files, hash, dir, day, cap = DAILY_CAP }) {
  if (!seamFiles(files).length) return "none";
  if (wasLaunched(readEntry(dir, hash))) return "dedup";
  if (launchedOn(dir, day) >= cap) return "cap";
  return "launch";
}
function record(dir, hash, data) {
  try {
    mkdirSync(dir, { recursive: true });
    writeFileSync(path.join(dir, `${hash}.json`), JSON.stringify({ ...data, at: new Date().toISOString() }, null, 2) + "\n");
  } catch {
    /* best effort: the ledger must never break a push */
  }
}

// ---- git -------------------------------------------------------------------------------------
const git = (args, cwd = ROOT) => {
  try {
    return execFileSync("git", args, { cwd, encoding: "utf8", stdio: ["ignore", "pipe", "ignore"] }).trim();
  } catch {
    return null;
  }
};
const lines = (s) => (s || "").split("\n").map((l) => l.trim()).filter(Boolean);

export function parsePushRefs(text) {
  return lines(text)
    .map((l) => l.match(/^(\S+) ([0-9a-f]{7,64}) (\S+) ([0-9a-f]{7,64})$/))
    .filter(Boolean)
    .map((m) => ({ localRef: m[1], localSha: m[2], remoteRef: m[3], remoteSha: m[4] }));
}
function baseFor(r) {
  if (!ZERO.test(r.remoteSha) && git(["cat-file", "-e", `${r.remoteSha}^{commit}`]) !== null) return r.remoteSha;
  if (!ZERO.test(r.remoteSha)) return null; // remote moved since our last fetch: cannot see what the push replaces
  for (const cand of ["origin/HEAD", "origin/main", "main"]) {
    const mb = git(["merge-base", r.localSha, cand]);
    if (mb && mb !== r.localSha) return mb;
  }
  return null;
}

function reviewPrompt(files, base, head) {
  return [
    `Adversarial review of commits ${base.slice(0, 12)}..${head.slice(0, 12)} in a .NET 8 / EF Core / Postgres API (Fugue).`,
    `Changed critical-seam files: ${files.join(", ")}.`,
    "Hunt for: migration correctness (Up/Down, data loss, locks), auth or identity gaps, EF/Dapper transaction misuse,",
    "ApiControllerBase contract drift (problem+json, ETag/If-Match), build or hook changes that weaken a gate, secrets exposure.",
    "Report findings with severity and file:line. Be concise. If you find nothing, say exactly: NO FINDINGS.",
  ].join("\n");
}
export const codexArgs = (base, title, outFile) => ["exec", "review", "--base", base, "--title", title, "-o", outFile, "-"];

// ---- gate ------------------------------------------------------------------------------------
export function runGate({ refs, dryRun = false, explicitBase = null, dir, now = new Date(), log = console.log, launch = launchDetached }) {
  const head = git(["rev-parse", "HEAD"]);
  const scopes = explicitBase
    ? [{ base: git(["merge-base", "HEAD", explicitBase]), head, label: `--base=${explicitBase}` }]
    : refs
        .filter((r) => !ZERO.test(r.localSha))
        .map((r) => ({ base: baseFor(r), head: r.localSha, label: r.remoteRef }));
  const out = [];
  for (const s of scopes) {
    if (!s.base) {
      log(`codex-gate: skipped ${s.label}: cannot determine the base (fetch, or review by hand). Push not held.`);
      out.push({ action: "skipped", why: "no base" });
      continue;
    }
    const files = lines(git(["diff", "--name-only", `${s.base}..${s.head}`]));
    const seams = seamFiles(files);
    const diff = git(["diff", `${s.base}..${s.head}`, "--", ...seams]) || "";
    const hash = fingerprint(seams, diff);
    const verdict = decide({ files, hash, dir, day: now.toISOString().slice(0, 10) });
    if (verdict === "none") {
      out.push({ action: "none" });
      continue;
    }
    log(`codex-gate: critical seam touched (${seams.length} file(s)): ${seams.join(", ")}`);
    if (verdict === "dedup") {
      log(`codex-gate: this seam-diff was already sent for review (${hash}); not repeating.`);
      out.push({ action: "dedup", hash });
      continue;
    }
    if (verdict === "cap") {
      log(`codex-gate: daily cap of ${DAILY_CAP} reviews reached; review by hand. Push not held.`);
      out.push({ action: "cap", hash });
      continue;
    }
    if (s.head !== head) {
      log(`codex-gate: skipped ${s.label}: pushed sha is not the checked-out HEAD (the review reads HEAD only).`);
      out.push({ action: "skipped", why: "not HEAD", hash });
      continue;
    }
    const cmd = ["codex", ...codexArgs(s.base, `seam review ${hash}`, "<last-message-file>")];
    if (dryRun) {
      log(`codex-gate: --dry-run: WOULD launch detached: ${cmd.join(" ")}  (stdin: review prompt) -> docs/reviews/codex/${now.toISOString().slice(0, 10)}-${hash.slice(0, 8)}.md`);
      out.push({ action: "dry-run", hash, cmd });
      continue;
    }
    record(dir, hash, { status: "spawned", base: s.base, head: s.head, files: seams });
    try {
      launch({ base: s.base, head: s.head, hash, files: seams, day: now.toISOString().slice(0, 10), dir });
      log(`codex-gate: advisory Codex review launched in the background (${hash}). Findings: docs/reviews/codex/. Push not held.`);
      out.push({ action: "launched", hash });
    } catch (e) {
      record(dir, hash, { status: "skipped", why: `launch failed: ${e.message}` });
      log(`codex-gate: launch failed (${e.message}); review by hand. Push not held.`);
      out.push({ action: "skipped", why: "launch failed", hash });
    }
  }
  return out.length === 1 ? out[0] : { action: out.some((o) => o.action === "launched") ? "launched" : "none", results: out };
}

function launchDetached(job) {
  const logFile = path.join(job.dir, `${job.hash}.log`);
  mkdirSync(job.dir, { recursive: true });
  const fd = openSync(logFile, "a");
  const child = spawn(process.execPath, [fileURLToPath(import.meta.url), "--worker", JSON.stringify(job)], {
    cwd: ROOT,
    detached: true,
    stdio: ["ignore", fd, fd],
  });
  child.on("error", () => {});
  child.unref();
  closeSync(fd);
}

/** Looks like a crash/auth/quota message rather than a review. */
export const looksLikeError = (t) => !t.trim() || /^(error|fatal|panic)\b/i.test(t.trim()) || /(not logged in|quota|rate limit|usage limit)/i.test(t.slice(0, 400));

function worker(job) {
  const outFile = path.join(job.dir, `${job.hash}.last-message.txt`);
  const prompt = reviewPrompt(job.files, job.base, job.head);
  const r = spawnSync("codex", codexArgs(job.base, `seam review ${job.hash}`, outFile), {
    cwd: ROOT,
    input: prompt,
    encoding: "utf8",
    timeout: 20 * 60 * 1000,
  });
  // Read the OUTPUT, never trust the exit code.
  let text = "";
  try {
    text = readFileSync(outFile, "utf8");
  } catch {
    /* fall back to stdout */
  }
  if (!text.trim()) text = r.stdout || "";
  if (looksLikeError(text)) {
    record(job.dir, job.hash, { status: "failed", base: job.base, head: job.head, note: (text || r.stderr || "no output").slice(0, 300) });
    return;
  }
  const doc = path.join(ROOT, "docs/reviews/codex", `${job.day}-${job.hash.slice(0, 8)}.md`);
  mkdirSync(path.dirname(doc), { recursive: true });
  writeFileSync(
    doc,
    `# Codex advisory review ${job.hash}\n\nRange: ${job.base.slice(0, 12)}..${job.head.slice(0, 12)}. Seams: ${job.files.join(", ")}.\nAdvisory only (D-014): verify each finding against the source before acting; pin confirmed catches.\n\n${text.trim()}\n`,
  );
  record(job.dir, job.hash, { status: "reviewed", base: job.base, head: job.head, doc: path.relative(ROOT, doc) });
}

function readStdinIfPiped() {
  try {
    const st = fstatSync(0);
    return st.isFIFO() || st.isFile() ? readFileSync(0, "utf8") : "";
  } catch {
    return "";
  }
}

if (process.argv[1] && fileURLToPath(import.meta.url) === path.resolve(process.argv[1])) {
  try {
    const argv = process.argv.slice(2);
    if (argv[0] === "--worker") worker(JSON.parse(argv[1]));
    else {
      const flag = (n) => argv.find((a) => a.startsWith(`--${n}=`))?.split("=").slice(1).join("=");
      runGate({
        refs: parsePushRefs(readStdinIfPiped()),
        dryRun: argv.includes("--dry-run"),
        explicitBase: flag("base") ?? null,
        dir: process.env.CODEX_GATE_STATE_DIR || path.join(ROOT, ".fugue-local/codex-gate"),
      });
    }
  } catch (e) {
    console.log(`codex-gate: internal error (${e.message}); review by hand. Push not held.`);
  }
  process.exit(0);
}
