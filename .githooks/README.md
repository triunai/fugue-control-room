# Git gates

Small shell scripts that run on `git commit` and `git push`. They keep commit messages readable,
keep secrets out of history, catch code that does not compile, and keep the doc spine's links honest.
No hook manager is needed: plain git runs them.

## Install (once per clone)

```
git config core.hooksPath .githooks
```

Executable bit (one rule): after `git add`, run
`git update-index --chmod=+x .githooks/commit-msg .githooks/pre-commit .githooks/pre-push scripts/spine/*.sh`
on every hook and script. Git for Windows ignores the bit on disk, so git must be told. Check:
`git ls-files -s .githooks scripts/spine` shows `100755` for each. A clone keeps the bit once it is committed as 100755.

Line endings: a hook saved with CRLF line endings fails with a strange `\r` error. The repo's `.gitattributes`
(block `ai-dev-kit:begin`) pins `*.sh` and `.githooks/*` to LF. It must exist BEFORE the scripts are added.
Warnings such as "LF will be replaced by CRLF" on `git add` are harmless.

Do not pipe `git commit` or `git push` output (`| head`, `| tail`). The hooks ignore a broken pipe, but git itself can still misreport.

## The gates

| Hook | Script | Blocks? | What it checks |
|---|---|---|---|
| commit-msg | `.githooks/commit-msg` | yes | `<type>(<scope>)?: <summary>`, 10 to 150 characters, no pasted file list |
| pre-commit | `scripts/spine/secrets-scan.sh` | yes | credential files staged; secret-shaped ADDED lines |
| pre-commit | `scripts/spine/stack-check.sh` | yes | the cheapest compile or type check for the stack of the staged files |
| pre-push | `scripts/spine/ledger-check.sh` | yes | doc-spine links: every entry names a thread, no dangling or duplicate keys |
| pre-push | `scripts/spine/spine-nudge.sh` | never | warns when code ships with no spine update, or a seed file is untracked |

Every failure prints WHAT is wrong, WHERE, and the FIX command. Run any gate by hand:
`bash scripts/spine/ledger-check.sh`, `bash scripts/spine/secrets-scan.sh` (after `git add`),
`bash scripts/spine/spine-nudge.sh`.

Stack-check detects the stack from the staged files: `tsconfig.json` + `.ts` runs `tsc --noEmit` (only if `node_modules/.bin/tsc`
exists); a `.sln`/`.csproj` + `.cs` runs `dotnet build --no-restore -v q` (run `dotnet restore` first); `Cargo.toml` runs
`cargo check`; `go.mod` runs `go vet ./...`; `pyproject.toml` or `requirements*.txt` + `.py` runs `python -m compileall -q`.
No known stack: skipped with one line.

## Limits (read these)

- Client-side only. `git commit --no-verify` and `git push --no-verify` skip every gate. A gate is a
  safety net for honest mistakes, not a wall. For a real wall, repeat the checks in CI.
- Secrets-scan finds common shapes only. It cannot find every secret. If a secret reaches a remote, rotate it.
- Ledger-check reads your working tree, not the pushed commit. Commit your doc edits before you push.
- Stack-check compiles the working tree, not just the staged files.
- A missing tool (no `dotnet`, no `tsc`) is skipped with a note. It is never a failure.
- Windows: a push to a remote under a very deep path can fail with "Filename too long". Fix: `git config core.longpaths true`.

## Secrets-scan exemptions

Edit `is_exempt()` in `scripts/spine/secrets-scan.sh`. Rules: exempt one EXACT file, never a folder
(a folder rule would pass a real secret there forever), and add a dated comment with the reason.
For one harmless line, put the marker `secrets-scan:ignore` on that line instead.

## Re-derive the commit-msg thresholds

The 10 and 150 character limits are a starting point. After about 200 commits, measure your own history:

```
git log --no-merges --format=%s | awk '{print length}' | sort -n | awk '{a[NR]=$1} END {print "n="NR, "median="a[int(NR/2)+1], "p90="a[int(NR*0.9)+1], "p99="a[int(NR*0.99)+1]}'
```

Set `MAX_SUBJECT` in `.githooks/commit-msg` to about the p99 value. The cap should catch pasted junk,
not reject normal commits. Count how many existing subjects a new cap would reject before you change it:

```
git log --no-merges --format=%s | awk -v max=150 'length > max' | wc -l
```

Add types you really use to `TYPES` in the same file.

## Set ratchet cutoffs (adopting on a repo with old docs)

A new repo leaves `scripts/spine/ledger-check.conf` empty and the whole spine is enforced.
An old repo cannot fix years of entries at once. Set cutoffs so only NEW entries are enforced:
older entries are counted, never blocked. The rule: entries at or after a cutoff (`>= cutoff`) are enforced.
So set each numbered cutoff to the highest existing number plus 1, and the date cutoff to tomorrow's date.
Find the newest numbers:

```
grep -m1 -o '^## D-[0-9]*' docs/decisions.md
grep -m1 -o '^### #[0-9]*' docs/state-backlog.md
grep -m1 -o '^## [0-9-]*' docs/project-log.md
grep -o '^### WS-[0-9]*' docs/workstreams.md | tail -1
```

Example: newest decision is D-041, so write `DECISION_CUTOFF=42`. Today is 2026-10-02, so write `LOG_CUTOFF=2026-10-03`.
Lower a cutoff later to bring more history under the rule. Empty means enforce everything.

## Ledger-check findings

1. A log, decision or backlog entry whose first non-blank body line is not `WS: ...`
2. A `WS:` line naming a `WS-NN` that has no `### WS-NN` heading
3. A done thread (heading has the check mark) that no log `WS:` line claims
4. A duplicate `D-NNN`, `#NNN` or `WS-NN` heading
5. A `WS-NN` heading at the wrong level (`#`, `##`, `####`). `##` is still read, but flagged

The last line is `ledger-check: findings=N workstreams=M`. M counts the threads it parsed. If M drops
and you did not delete a thread, the parser went blind: check the heading level first.
Text inside a code fence is ignored, so docs can show example headings safely.

## Optional: lefthook

Not required. If you already use `lefthook`, call the same scripts from `lefthook.yml` and drop the
`core.hooksPath` line. Do not run both, or every gate runs twice.

```yaml
commit-msg:
  commands:
    message-format:
      run: bash .githooks/commit-msg {1}
pre-commit:
  commands:
    secrets-scan:
      run: bash scripts/spine/secrets-scan.sh
    stack-check:
      run: bash scripts/spine/stack-check.sh
pre-push:
  commands:
    ledger-check:
      run: bash scripts/spine/ledger-check.sh
    spine-nudge:
      run: bash scripts/spine/spine-nudge.sh < /dev/null
```
