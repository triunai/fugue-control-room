#!/usr/bin/env bash
# ledger-check: keeps the doc spine honest. Exit 1 on any finding.
# Reads docs/workstreams.md, docs/project-log.md, docs/decisions.md, docs/state-backlog.md.
# Pure text parsing: bash + awk + grep + sed. Runs in Git Bash, macOS bash 3.2, Linux.
#
# Findings (each prints "LEDGER: <what> (<where>) | fix: <command or edit>"):
#   1. a log / decision / backlog entry whose first non-blank body line is not "WS: ..."
#   2. a WS: line naming a WS-NN that has no "### WS-NN" heading
#   3. a done thread (heading has the check mark) that no log "WS:" line claims
#   4. a duplicate D-NNN, #NNN or WS-NN heading
#   5. a heading line "#", "##", "####" + space + WS-NN (wrong level; "##" is still read, but flagged)
# Last line: "ledger-check: findings=N workstreams=M". M is the canary: if it drops
# and you did not delete a thread, the parser went blind.
# Adopting on an old repo? Set cutoffs in scripts/spine/ledger-check.conf (see .githooks/README.md).
# Usage: bash scripts/spine/ledger-check.sh [repo-root]

set -u
SELF_DIR=$(cd "$(dirname "$0")" && pwd)
ROOT="${1:-}"
[ -z "$ROOT" ] && ROOT=$(git rev-parse --show-toplevel 2>/dev/null)
[ -z "$ROOT" ] && ROOT=$(cd "$SELF_DIR/../.." && pwd)
cd "$ROOT" || { echo "LEDGER: cannot enter repo root $ROOT"; exit 1; }

CONF="$SELF_DIR/ledger-check.conf"
conf_get() {
  [ -f "$CONF" ] || return 0
  sed -n "s/^$1=//p" "$CONF" | head -n1 | tr -d "\r\"' "
}
LOG_CUTOFF=$(conf_get LOG_CUTOFF)
DECISION_CUTOFF=$(conf_get DECISION_CUTOFF | tr -cd '0-9')
BACKLOG_CUTOFF=$(conf_get BACKLOG_CUTOFF | tr -cd '0-9')
WS_CUTOFF=$(conf_get WS_CUTOFF | tr -cd '0-9')

WS_FILE=docs/workstreams.md
LOG_FILE=docs/project-log.md
DEC_FILE=docs/decisions.md
BL_FILE=docs/state-backlog.md
CHECK_MARK="✅"
ALL=""

for f in "$WS_FILE" "$LOG_FILE" "$DEC_FILE" "$BL_FILE"; do
  if [ ! -f "$f" ]; then
    ALL="${ALL}LEDGER: $f is missing | fix: create it from the spine templates (kit file 02), then re-run bash scripts/spine/ledger-check.sh
"
  fi
done

# ---- workstreams: headings are exactly "### WS-NN" (two digits or more) ----
WS_OUT=""
if [ -f "$WS_FILE" ]; then
  WS_OUT=$(awk -v cut="$WS_CUTOFF" -v file="$WS_FILE" -v check="$CHECK_MARK" '
    { sub(/\r$/, "") }
    /^```/ { fence = !fence; next }
    fence { next }
    /^#+ WS-[0-9][0-9]+/ {
      match($0, /^#+/); lvl = RLENGTH; hashes = substr($0, 1, lvl)
      match($0, /WS-[0-9][0-9]+/); key = substr($0, RSTART, RLENGTH)
      enf = ((substr(key, 4) + 0) >= (cut + 0))
      if (lvl != 3) {
        if (enf) printf "LEDGER: %s heading uses [%s] instead of three hashes (%s:%d) | fix: edit the line to start with ### %s\n", key, hashes, file, FNR, key
        if (lvl != 2) next
      }
      if (key in seen) {
        if (enf) printf "LEDGER: %s has two headings (%s:%d) | fix: merge the two threads into one block, or renumber the newer one\n", key, file, FNR
        next
      }
      seen[key] = 1
      print "KEY " key
      if (index($0, check) > 0) print "DONE " key
    }
  ' "$WS_FILE")
fi
WSLIST=" $(printf '%s\n' "$WS_OUT" | sed -n 's/^KEY //p' | tr '\n' ' ')"
# Pruned threads: a `Closed: W-NNN (WS-NN) title` line in the project log keeps WS-NN a known key (hygiene.md, Doc lifecycle).
CLOSED_WS=$(sed -n 's/^Closed:.*(\(WS-[0-9][0-9]*\)).*/\1/p' "$LOG_FILE" docs/archive/project-log/*.md 2>/dev/null | tr '\n' ' ')
WSLIST="$WSLIST $CLOSED_WS "
WS_COUNT=$(printf '%s\n' "$WS_OUT" | grep -c '^KEY ')

# ---- log / decisions / backlog: one awk program, three kinds ----
IFS= read -r -d '' ENTRY_AWK <<'AWKEOF'
function resolve(line,   s, k, nk) {
  pending = 0
  if (line !~ /^WS: /) {
    if (penf) printf "LEDGER: %s has no WS: first line (%s) | fix: add the line WS: WS-NN (or WS: (pre-WS)) directly under the heading\n", plabel, pwhere
    return
  }
  if (line ~ /^WS: \(pre-WS\)/) return
  s = line; nk = 0
  while (match(s, /WS-[0-9][0-9]+/)) {
    k = substr(s, RSTART, RLENGTH); s = substr(s, RSTART + RLENGTH); nk++
    if (index(wslist, " " k " ") == 0) {
      printf "LEDGER: %s names %s, which has no ### WS heading (%s) | fix: open the thread with a ### %s heading in docs/workstreams.md, or correct the WS: line\n", plabel, k, pwhere, k
    } else if (kind == "log") {
      print "CLAIM " k
    }
  }
  if (nk == 0 && penf) printf "LEDGER: %s has a WS: line that names no workstream (%s) | fix: write WS: WS-NN, or WS: (pre-WS)\n", plabel, pwhere
}
function begin_entry(label, where, enf) {
  if (pending) resolve("")
  pending = 1; plabel = label; pwhere = where; penf = enf
}
{ sub(/\r$/, "") }
/^```/ { if (pending) resolve($0); fence = !fence; next }
fence { next }
kind == "log" && /^## [0-9][0-9][0-9][0-9]-[0-9][0-9]-[0-9][0-9]/ {
  id = substr($0, 4, 10); seq[id]++
  begin_entry("log entry " id "#" seq[id], file ":" FNR, (cut == "" || id >= cut))
  next
}
kind == "decision" && /^## D-[0-9]+/ {
  match($0, /D-[0-9]+/); num = substr($0, RSTART + 2, RLENGTH - 2) + 0
  lab = sprintf("D-%03d", num); enf = (num >= (cut + 0))
  if (num in seen) {
    if (enf) printf "LEDGER: %s is defined twice (%s:%d) | fix: renumber the newer one; the orchestrator mints numbers\n", lab, file, FNR
  }
  seen[num] = 1
  begin_entry("decision " lab, file ":" FNR, enf)
  next
}
kind == "backlog" && /^#+ #[0-9]+/ {
  match($0, /^#+/); lvl = RLENGTH
  match($0, /#[0-9]+/); num = substr($0, RSTART + 1, RLENGTH - 1) + 0
  lab = sprintf("#%03d", num); enf = (num >= (cut + 0))
  if (lvl != 3) {
    printf "LEDGER: backlog %s heading uses the wrong level (%s:%d) | fix: the only form is ### #NNN — title\n", lab, file, FNR
    next
  }
  if (num in seen) {
    if (enf) printf "LEDGER: backlog %s has two headings (%s:%d) | fix: merge them or renumber the newer one\n", lab, file, FNR
  }
  seen[num] = 1
  begin_entry("backlog " lab, file ":" FNR, enf)
  next
}
pending && $0 !~ /^[ \t]*$/ { resolve($0) }
END { if (pending) resolve("") }
AWKEOF

LOG_OUT=""; DEC_OUT=""; BL_OUT=""
[ -f "$LOG_FILE" ] && LOG_OUT=$(awk -v kind=log -v cut="$LOG_CUTOFF" -v file="$LOG_FILE" -v wslist="$WSLIST" "$ENTRY_AWK" "$LOG_FILE")
[ -f "$DEC_FILE" ] && DEC_OUT=$(awk -v kind=decision -v cut="$DECISION_CUTOFF" -v file="$DEC_FILE" -v wslist="$WSLIST" "$ENTRY_AWK" "$DEC_FILE")
[ -f "$BL_FILE" ] && BL_OUT=$(awk -v kind=backlog -v cut="$BACKLOG_CUTOFF" -v file="$BL_FILE" -v wslist="$WSLIST" "$ENTRY_AWK" "$BL_FILE")

# ---- a done thread must be claimed by a log entry's WS: line ----
for k in $(printf '%s\n' "$WS_OUT" | sed -n 's/^DONE //p'); do
  n=$(printf '%s' "$k" | sed 's/^WS-//')
  [ "$((10#$n))" -ge "$((10#${WS_CUTOFF:-0}))" ] || continue
  if ! printf '%s\n' "$LOG_OUT" | grep -qx "CLAIM $k"; then
    ALL="${ALL}LEDGER: $k is marked done but no project-log entry has a WS: line naming it ($WS_FILE) | fix: add a log entry under $LOG_FILE whose first body line is WS: $k
"
  fi
done

FINDINGS=$(printf '%s\n%s\n%s\n%s\n%s\n' "$ALL" "$WS_OUT" "$LOG_OUT" "$DEC_OUT" "$BL_OUT" | grep '^LEDGER: ')
if [ -n "$FINDINGS" ]; then
  COUNT=$(printf '%s\n' "$FINDINGS" | wc -l | tr -d ' ')
  printf '%s\n' "$FINDINGS"
else
  COUNT=0
fi
echo "ledger-check: findings=$COUNT workstreams=$WS_COUNT"
[ "$COUNT" -gt 0 ] && exit 1
exit 0
