# 01 - Local setup

Goal: a working checkout where `dotnet build` and `dotnet test` run. Tested on macOS (Apple Silicon). Time: about 10 minutes.

## 1. Install the .NET 8 SDK into your home directory

```bash
curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
bash /tmp/dotnet-install.sh --version 8.0.428 --install-dir "$HOME/.dotnet"
```
Expect: a line ending in `Installed version is 8.0.428` (or "already installed").

## 2. Put it on your PATH for this shell

Run this at the start of every shell that uses .NET (add it to `~/.zshrc` if you like):
```bash
export DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH DOTNET_CLI_TELEMETRY_OPTOUT=1
```
Check:
```bash
dotnet --version
```
Expect: `8.0.428` when run inside the repo (`global.json` pins it; elsewhere it prints the newest installed SDK).

## 3. Docker runtime (needed from S2 onward, Testcontainers and compose)

Use OrbStack. Install it (`brew install --cask orbstack`) and start it once:
```bash
open -a OrbStack
docker version
```
Expect: both a `Client:` and a `Server:` section. OrbStack keeps `/var/run/docker.sock` in place, so Testcontainers needs no configuration. Not required for S0/S1 tests.

## 4. Clone and install repo tooling (git hooks, commit linting)

```bash
git clone <your-fork-or-copy-url> fugue && cd fugue
npm install
npx lefthook install
```
Expect: npm ends with `added ... packages`; lefthook prints `sync hooks: ✔️ (pre-commit, commit-msg, pre-push)`.

## 5. Build

```bash
dotnet build
```
Expect: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`. Warnings are errors in this repo.

## 6. Test

```bash
dotnet test --no-build
```
Expect: the architecture tests pass; `Fugue.Tests` reports some failures. **Known red tests until the owner implements `ApiControllerBase.MapStatus`** (it throws `NotImplementedException` on purpose):
- `MapStatusTests.MapStatus_returns_the_expected_status` (all 8 `ErrorType` cases)
- tests that assert an HTTP status or error-shape through `MapStatus` (for example `Failure_is_a_problem_json_with_code_type_and_trace_id` and `Validation_failure_carries_an_errors_dictionary`)

Any other red test is a real problem.

## 7. Format and doc checks

```bash
dotnet format --verify-no-changes
bash scripts/spine/ledger-check.sh
```
Expect: no output from format; `ledger-check: findings=0 workstreams=N`.

## Troubleshooting
- `dotnet: command not found`: repeat step 2 in this shell.
- `A compatible .NET SDK was not found`: repeat step 1; `global.json` needs 8.0.4xx.
- A commit is rejected: read the message; the format is `type(scope): summary` (see `commitlint.config.cjs`). Do not use `--no-verify`.
