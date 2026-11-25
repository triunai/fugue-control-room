#!/usr/bin/env bash
# secrets-scan: blocks credential files and secret-shaped lines in what is staged.
#   1. PATH  - files that carry credentials by nature (.env, keys, certificates)
#   2. LINES - ADDED lines that look like a password, key, token or connection string
# Only added lines are scanned: deleting a secret is never blocked.
# Client-side only: git commit --no-verify skips it. Rotate anything that ever reached a remote.

set -u

BLOCKED_PATHS='(^|/)\.env($|\.)|\.(pfx|p12|jks|pem|key|keystore)$|(^|/)id_(rsa|dsa|ecdsa|ed25519)$|(^|/)secrets\.json$'
PATH_OK='\.env\.example$|\.env\.template$|\.env\.sample$'
# Matched case-insensitively (grep -i). Intervals need grep -E, so awk is not used to match.
SECRET_PATTERNS='(password|passwd|pwd)[[:space:]]*[=:][[:space:]]*[^[:space:]"'"'"']{6,}|accountkey=|sharedaccesssignature=|-----begin [a-z ]*private key-----|(api[_-]?key|apikey|client[_-]?secret|access[_-]?token)[[:space:]]*[=:][[:space:]]*["'"'"'][^"'"'"']{12,}|akia[0-9a-z]{16}|gh[pousr]_[a-z0-9]{30,}|xox[baprs]-[a-z0-9-]{10,}'
# .NET additions (2026-11-11). Matched case-insensitively.
#   - connection-string Password= (appsettings, code, docs), with or without a leading quote
#   - "SigningKey" / "SecretKey" / "ServiceRoleKey" JSON properties holding a value
#   - Payment-provider live/test secret keys that look real (16+ chars after the prefix)
#   - Hosted-Postgres service-role / any JWT: three base64url segments, first starts eyJ
SECRET_PATTERNS="$SECRET_PATTERNS"'|"?password"?[[:space:]]*[=:][[:space:]]*"?[^[:space:]";]{4,}|;password=[^;[:space:]"]{4,}|"(signingkey|secretkey|servicerolekey|service_role_key)"[[:space:]]*:[[:space:]]*"[^"]{8,}"|sk_(live|test)_[a-z0-9]{16,}|rk_(live|test)_[a-z0-9]{16,}|eyj[a-z0-9_-]{15,}\.eyj[a-z0-9_-]{15,}\.[a-z0-9_-]{10,}'
# Visible placeholders are not secrets. A hit line containing any of these is dropped.
PLACEHOLDER_OK='replace_me|changeme|your[_-]?(password|key|secret)|<[a-z_ -]+>|\$\{[a-z_]+\}|example|xxxxx'

# Files exempt from the LINE scan. EXEMPT BY EXACT FILE, NEVER BY FOLDER: a folder rule would
# pass a real secret there forever. Every entry needs a dated reason. Remove an entry to scan again.
# A single line can also carry the marker secrets-scan:ignore (use for a visible placeholder).
is_exempt() {
  case "$1" in
    # 2026-11-11: this script defines the patterns, so it matches itself.
    scripts/spine/secrets-scan.sh) return 0 ;;
    # 2026-11-11: the hooks README shows example lines such as password = value.
    .githooks/README.md) return 0 ;;
    # 2026-11-11: the env template holds only REPLACE_ME placeholders (connection string cannot carry an inline marker).
    .env.example) return 0 ;;
  esac
  return 1
}

STAGED=$(git diff --cached --name-only --diff-filter=ACMR)
[ -z "$STAGED" ] && exit 0
BAD=0

while IFS= read -r f; do
  [ -z "$f" ] && continue
  printf '%s' "$f" | grep -qE "$PATH_OK" && continue
  if printf '%s' "$f" | grep -qE "$BLOCKED_PATHS"; then
    BAD=$((BAD+1))
    echo ""
    echo "COMMIT REJECTED - a credential file is staged"
    echo "  WHAT:  this file type carries credentials; once committed it stays in history"
    echo "  WHERE: $f"
    if git ls-tree --name-only HEAD -- "$f" 2>/dev/null | grep -qx "$f"; then
      echo "  FIX:   it is already tracked, so .gitignore does not help. Untrack it, then rotate what leaked:"
      echo "         git rm --cached \"$f\" && echo \"$f\" >> .gitignore && git add .gitignore"
    else
      echo "  FIX:   git restore --staged \"$f\" && echo \"$f\" >> .gitignore"
      echo "         (a template with no real values? rename it \"$f.example\" - that name is allowed)"
    fi
  fi
done <<EOF
$STAGED
EOF

while IFS= read -r f; do
  [ -z "$f" ] && continue
  is_exempt "$f" && continue
  [ -f "$f" ] || continue
  HITS=$(git diff --cached -U0 --no-color -- "$f" | tr -d '\r' | awk '
    /^@@/ { inh = 1; match($0, /\+[0-9]+/); ln = substr($0, RSTART + 1, RLENGTH - 1) + 0; next }
    inh && /^\+/ { line = substr($0, 2); if (index(line, "secrets-scan:ignore") == 0) printf "%d\t%s\n", ln, substr(line, 1, 80); ln++ }
  ' | grep -iE "$SECRET_PATTERNS" | grep -viE "$PLACEHOLDER_OK")
  if [ -n "$HITS" ]; then
    BAD=$((BAD+1))
    echo ""
    echo "COMMIT REJECTED - a secret-shaped line is staged"
    echo "  WHAT:  an added line looks like a password, key, token or connection string"
    echo "  WHERE: $f"
    printf '%s\n' "$HITS" | while IFS="$(printf '\t')" read -r no text; do echo "         line $no: $text"; done
    echo "  FIX:   move the value to an environment variable or an untracked .env file, then:"
    echo "         git restore --staged \"$f\"   (edit the file, then git add it again)"
    echo "         A visible placeholder? Write REPLACE_ME, or add the marker secrets-scan:ignore on that line."
  fi
done <<EOF
$STAGED
EOF

if [ "$BAD" -gt 0 ]; then
  echo ""
  echo "secrets-scan: $BAD problem(s). Nothing was committed; your changes are still staged."
  exit 1
fi
exit 0
