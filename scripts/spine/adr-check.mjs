#!/usr/bin/env node
// Decision <-> ADR consistency (hygiene.md "Decisions and ADRs"). Exit 1 on findings.
//  1. Every docs/adr/ADR-NNN-*.md is linked from a D entry or index row in docs/decisions.md.
//  2. Every D entry with `Promote: yes (ADR-NNN)` names an ADR file that exists (bare `Promote: yes` is a finding).
import { readFileSync, readdirSync, existsSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

export function check({ decisions, adrFiles }) {
  const findings = [];
  const adrIds = adrFiles.filter((f) => /^ADR-\d+-.*\.md$/.test(f)).map((f) => f.match(/^ADR-\d+/)[0]);
  for (const id of adrIds) if (!new RegExp(`\\b${id}\\b`).test(decisions)) findings.push(`${id} is not linked from any D entry in docs/decisions.md`);
  for (const sec of decisions.split(/^## (?=D-\d+)/m).slice(1)) {
    const d = sec.match(/^D-\d+/)[0];
    const p = sec.match(/^Promote:\s*yes(?:\s*\((ADR-\d+)\))?/im);
    if (!p) continue;
    if (!p[1]) findings.push(`${d} is marked Promote: yes but names no ADR (write the ADR, then \`Promote: yes (ADR-NNN)\`)`);
    else if (!adrIds.includes(p[1])) findings.push(`${d} promotes to ${p[1]}, but no docs/adr/${p[1]}-*.md exists`);
  }
  return findings;
}

if (process.argv[1] && fileURLToPath(import.meta.url) === path.resolve(process.argv[1])) {
  const root = process.cwd();
  const dir = path.join(root, "docs/adr");
  const findings = check({
    decisions: existsSync("docs/decisions.md") ? readFileSync("docs/decisions.md", "utf8") : "",
    adrFiles: existsSync(dir) ? readdirSync(dir) : [],
  });
  for (const f of findings) console.error(`adr-check: ${f}`);
  if (!findings.length) console.log("adr-check: clean");
  process.exit(findings.length ? 1 : 0);
}
