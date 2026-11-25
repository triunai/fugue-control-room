#!/usr/bin/env bash
# spine-nudge: a WARN-only reminder before push. ALWAYS exits 0. It never blocks a push.
#   (a) the push changes code but none of docs/hot-state.md, docs/workstreams.md, docs/project-log.md
#   (b) an untracked seed file exists (docs/NEXT-SESSION-SEED-PROMPT-*.md) - it would be lost
# Silent on lane/* and verify/* branches (short-lived work branches).
# Input: git's pre-push lines on stdin: <local ref> <local sha> <remote ref> <remote sha>.
# Run by hand (no stdin) and it checks the current branch against the trunk.
# A new branch (remote sha is all zeros) is compared with origin/HEAD, else origin/main.

trap 'exit 0' EXIT
cd "$(git rev-parse --show-toplevel 2>/dev/null || pwd)" 2>/dev/null || exit 0

ZERO=0000000000000000000000000000000000000000
REFS=""
[ -t 0 ] || REFS=$(cat)
if [ -z "$REFS" ]; then
  CUR=$(git rev-parse --abbrev-ref HEAD 2>/dev/null)
  REFS="refs/heads/$CUR $(git rev-parse HEAD 2>/dev/null) refs/heads/$CUR $ZERO"
fi

warn() { echo "spine-nudge (warning only): $1" >&2; }

SAW_REAL_BRANCH=0
while read -r lref lsha rref rsha; do
  [ -z "${lref:-}" ] && continue
  case "${lsha:-}" in *[!0]*) ;; *) continue ;; esac        # empty or all zeros = branch delete
  branch=${lref#refs/heads/}
  case "$branch" in lane/*|verify/*) continue ;; esac
  SAW_REAL_BRANCH=1

  case "${rsha:-}" in
    *[!0]*) from="$rsha" ;;
    *)
      base=$(git rev-parse --verify -q origin/HEAD 2>/dev/null)
      [ -z "$base" ] && base=$(git rev-parse --verify -q origin/main 2>/dev/null)
      [ -z "$base" ] && base=$(git rev-parse --verify -q main 2>/dev/null)
      [ -z "$base" ] && continue
      from=$(git merge-base "$base" "$lsha" 2>/dev/null)
      [ -z "$from" ] && continue
      ;;
  esac

  CHANGED=$(git diff --name-only "$from" "$lsha" -- 2>/dev/null) || continue
  [ -z "$CHANGED" ] && continue
  CODE=$(printf '%s\n' "$CHANGED" | grep -vE '^docs/|\.md$|^\.githooks/|^scripts/spine/|^\.github/|^lefthook\.yml$|^\.gitignore$')
  SPINE=$(printf '%s\n' "$CHANGED" | grep -E '^docs/(hot-state|workstreams|project-log)\.md$')
  if [ -n "$CODE" ] && [ -z "$SPINE" ]; then
    warn "branch $branch changes code but none of hot-state.md, workstreams.md, project-log.md. Update the spine, or ignore this if it is a spike."
  fi
done <<EOF
$REFS
EOF

SEEDS=""
[ "$SAW_REAL_BRANCH" -eq 1 ] && SEEDS=$(git ls-files --others --exclude-standard -- 'docs/NEXT-SESSION-SEED-PROMPT-*.md' 2>/dev/null)
if [ -n "$SEEDS" ]; then
  warn "untracked seed file(s) would be lost on another machine: $(printf '%s' "$SEEDS" | tr '\n' ' ') - fix: git add docs/NEXT-SESSION-SEED-PROMPT-*.md && git commit"
fi
exit 0
