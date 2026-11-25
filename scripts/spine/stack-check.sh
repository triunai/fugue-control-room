#!/usr/bin/env bash
# stack-check: runs the cheapest compile or type check for the stack of the STAGED files.
# It only runs a check when a staged file belongs to that stack. Unknown stack = skip, one line.
#   TypeScript : tsconfig.json + staged .ts/.tsx  -> npx --no-install tsc --noEmit  (only if node_modules/.bin/tsc exists)
#   .NET       : *.sln/*.slnx/*.csproj + staged .cs/.csproj/.sln -> dotnet restore, then dotnet build --no-restore
#                (no solution yet = exit 0 with a note; pass --test to also run dotnet test --no-build)
#   Rust       : Cargo.toml + staged .rs          -> cargo check -q
#   Go         : go.mod + staged .go              -> go vet ./...
#   Python     : pyproject.toml or requirements*.txt + staged .py -> python -m compileall -q <staged files>
# Client-side only: git commit --no-verify skips it. A missing tool is skipped with a note, not failed.

set -u
WITH_TEST=0
[ "${1:-}" = "--test" ] && WITH_TEST=1
cd "$(git rev-parse --show-toplevel 2>/dev/null || pwd)" || exit 0

STAGED=$(git diff --cached --name-only --diff-filter=ACMR)
[ -z "$STAGED" ] && [ "$WITH_TEST" -eq 0 ] && exit 0
RAN=0
FAILED=0

has_staged() { printf '%s\n' "$STAGED" | grep -qE "$1"; }
have() { command -v "$1" >/dev/null 2>&1; }

run_check() {
  # run_check <label> <fix hint> <command...>
  label="$1"; hint="$2"; shift 2
  RAN=1
  echo "stack-check: $label ..."
  if ! "$@"; then
    FAILED=1
    echo ""
    echo "COMMIT REJECTED - $label failed"
    echo "  WHAT:  the staged change does not compile or type-check"
    echo "  WHERE: see the compiler output above"
    echo "  FIX:   fix the errors, git add the files, commit again. To reproduce: $hint"
  fi
}

if [ -f tsconfig.json ] && has_staged '\.(ts|tsx)$'; then
  if [ -x node_modules/.bin/tsc ] || [ -x node_modules/.bin/tsc.cmd ]; then
    run_check "typescript (tsc --noEmit)" "npx tsc --noEmit" npx --no-install tsc --noEmit
  else
    RAN=1; echo "stack-check: typescript skipped - node_modules/.bin/tsc is missing (fix: npm install)"
  fi
fi

# .NET: runs when a .NET file is staged, or always with --test (pre-push). Skips cleanly with no solution.
if [ "$WITH_TEST" -eq 1 ] || has_staged '\.(cs|csproj|sln|slnx|props|targets)$'; then
  TARGET=$(git ls-files '*.sln' '*.slnx' | head -n1)
  [ -z "$TARGET" ] && TARGET=$(git ls-files '*.csproj' | head -n1)
  if [ -z "$TARGET" ]; then
    RAN=1; echo "stack-check: dotnet skipped - no .sln/.slnx/.csproj yet (Phase 1 creates the solution)"
  elif have dotnet; then
    run_check "dotnet restore" "dotnet restore $TARGET" dotnet restore "$TARGET" -v q
    if [ "$FAILED" -eq 0 ]; then
      run_check "dotnet build" "dotnet build $TARGET --no-restore -v q" dotnet build "$TARGET" --no-restore -v q
    fi
    if [ "$FAILED" -eq 0 ] && [ "$WITH_TEST" -eq 1 ]; then
      run_check "dotnet test" "dotnet test $TARGET --no-build -v q" dotnet test "$TARGET" --no-build -v q
    fi
  else
    RAN=1; echo "stack-check: dotnet skipped - dotnet is not on PATH (fix: install the .NET SDK)"
  fi
fi

if [ -f Cargo.toml ] && has_staged '\.rs$'; then
  if have cargo; then run_check "cargo check" "cargo check" cargo check -q
  else RAN=1; echo "stack-check: cargo skipped - cargo is not on PATH (fix: install Rust)"; fi
fi

if [ -f go.mod ] && has_staged '\.go$'; then
  if have go; then run_check "go vet" "go vet ./..." go vet ./...
  else RAN=1; echo "stack-check: go skipped - go is not on PATH (fix: install Go)"; fi
fi

if { [ -f pyproject.toml ] || ls requirements*.txt >/dev/null 2>&1; } && has_staged '\.py$'; then
  PY=""
  have python3 && PY=python3
  [ -z "$PY" ] && have python && PY=python
  if [ -n "$PY" ]; then
    PYFILES=$(printf '%s\n' "$STAGED" | grep -E '\.py$')
    run_check "python compileall" "$PY -m compileall -q <the staged .py files>" sh -c 'printf "%s\n" "$1" | while IFS= read -r f; do "$2" -m compileall -q "$f" || exit 1; done' _ "$PYFILES" "$PY"
  else
    RAN=1; echo "stack-check: python skipped - python is not on PATH (fix: install Python)"
  fi
fi

[ "$RAN" -eq 0 ] && echo "stack-check: no known stack in the staged files - skipped"
[ "$FAILED" -eq 1 ] && exit 1
exit 0
