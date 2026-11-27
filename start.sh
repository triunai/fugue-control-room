#!/usr/bin/env bash
# One command for a reviewer: needs Docker only (no .NET, Node or Postgres on the machine).
# Creates .env with fresh secrets if it is missing, moves any host port that is already taken,
# builds and starts the stack (Postgres, a one-shot migrate + seed, API, Meilisearch, console),
# waits until the console answers, and prints where to go. Safe to run again.
set -euo pipefail
cd "$(dirname "$0")"

say() { printf '%s\n' "$*"; }

command -v docker >/dev/null 2>&1 || { say "Docker is required: https://docs.docker.com/get-docker/"; exit 1; }
docker info >/dev/null 2>&1 || { say "Docker is installed but not running. Start Docker Desktop (or OrbStack) and run ./start.sh again."; exit 1; }
docker compose version >/dev/null 2>&1 || { say "This needs Docker Compose v2 ('docker compose'). Update Docker Desktop."; exit 1; }

rand_b64() { openssl rand -base64 32 2>/dev/null || head -c 32 /dev/urandom | base64 | tr -d '\n'; }
rand_hex() { openssl rand -hex 16 2>/dev/null || head -c 16 /dev/urandom | od -An -tx1 | tr -d ' \n'; }

# set_env KEY VALUE: replace the line KEY=... in .env, or append it (portable across BSD and GNU sed).
set_env() {
  local key="$1" value="$2"
  if grep -q "^${key}=" .env; then
    grep -v "^${key}=" .env > .env.tmp && printf '%s=%s\n' "$key" "$value" >> .env.tmp && mv .env.tmp .env
  else
    printf '%s=%s\n' "$key" "$value" >> .env
  fi
}
get_env() { grep "^$1=" .env 2>/dev/null | tail -n1 | cut -d= -f2- || true; }

if [ ! -f .env ]; then
  say "Creating .env with fresh development secrets..."
  cp .env.example .env
  pw="$(rand_hex)"
  key="$(rand_b64)"
  meili="$(rand_b64)"
  # Every placeholder line, rewritten without sed so a '/' or '+' in a secret cannot break anything.
  while IFS= read -r line; do
    line="${line//REPLACE_ME/$pw}"
    case "$line" in
      MEILI_MASTER_KEY=*) line="MEILI_MASTER_KEY=$meili" ;;
      *GENERATE_WITH_openssl_rand_base64_32*) line="${line//GENERATE_WITH_openssl_rand_base64_32/$key}" ;;
    esac
    printf '%s\n' "$line"
  done < .env.example > .env
fi

# A host port is busy when something already accepts connections on it.
port_busy() { (exec 3<>"/dev/tcp/127.0.0.1/$1") >/dev/null 2>&1; }
# Containers of THIS stack hold our own ports on a re-run; those are not a clash.
ours_running() { [ -n "$(docker compose ps -q 2>/dev/null)" ]; }

pick_port() { # pick_port VAR default
  local var="$1" def="$2" cur
  cur="$(get_env "$var")"; cur="${cur:-$def}"
  if ! ours_running && port_busy "$cur"; then
    local p=$((cur + 1))
    while port_busy "$p"; do p=$((p + 1)); done
    say "Port $cur is in use on this machine; using $p for $var." >&2
    set_env "$var" "$p"
    cur="$p"
  fi
  printf '%s' "$cur"
}

PG_PORT="$(pick_port POSTGRES_PORT 5432)"
WEB_PORT="$(pick_port WEB_PORT 8080)"
API_PORT="$(pick_port API_PORT 5081)"
MEILI_PORT="$(pick_port MEILI_PORT 7700)"

say "Building and starting the stack (the first build takes a few minutes)..."
docker compose up -d --build

say "Waiting for the console on http://localhost:${WEB_PORT} ..."
for _ in $(seq 1 120); do
  if curl -fsS "http://localhost:${WEB_PORT}/" >/dev/null 2>&1; then
    say ""
    say "Fugue is up."
    say "  Console   http://localhost:${WEB_PORT}"
    say "  API       http://localhost:${API_PORT}/swagger"
    say "  Postgres  localhost:${PG_PORT} (user fugue, database fugue; password in .env)"
    say "  Search    http://localhost:${MEILI_PORT}"
    say ""
    say "Sign in with any account on the sign-in page; the development-only password is Fugue-Dev-Passw0rd!"
    say "  self@fugue.test     claimant        staff@fugue.test   claims officer"
    say "  manager@fugue.test  manager         admin@fugue.test   admin"
    say ""
    say "Stop: docker compose down   (keeps the data; 'down -v' also deletes it)"
    exit 0
  fi
  sleep 3
done

say "The console did not answer within 6 minutes. Status:"
docker compose ps
say "Logs: docker compose logs migrate api web --tail 50"
exit 1
