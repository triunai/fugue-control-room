#!/usr/bin/env bash
# Load profile: starts the API on :5431 against the throwaway Postgres (:55531) and Meilisearch (:55532) from the runbook,
# with the sign-in limiter RAISED through environment variables. Never use these values in production.
set -euo pipefail
cd "$(dirname "$0")/.."
export ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5431
export ConnectionStrings__Postgres="Host=localhost;Port=55531;Database=fugue;Username=fugue;Password=pw"
# Throwaway key (base64 of 32 bytes), valid only for this local profile.
export Auth__SigningKey="${Auth__SigningKey:-$(printf 'k6-load-profile-key-0123456789ab' | base64)}"
export Search__Meilisearch__Url=http://localhost:55532 Search__Meilisearch__ApiKey=k6-load-test-master-key-123456
export RateLimiting__Auth__PerIpPermits=100000 RateLimiting__Auth__PerEmailPermits=100000 \
  RateLimiting__Auth__PerEmailConcurrency=1000 RateLimiting__Auth__EmailCeilingPermits=100000
exec dotnet run -c Release --project src/Fugue.Api --no-launch-profile -- --seed
