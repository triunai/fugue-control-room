#!/bin/sh
# Runs once, on a fresh Postgres volume, from /docker-entrypoint-initdb.d (docker-compose.yml mounts it there).
# Creates the fugue_readonly role before the EF migrations exist; the ALTER DEFAULT PRIVILEGES in the SQL file
# makes every table the migrating owner (POSTGRES_USER) creates later readable. LOCAL development only.
set -eu
: "${FUGUE_READONLY_PASSWORD:?docker-compose.yml sets it}" # secrets-scan:ignore (no value here, a required-variable check)
RO_VAR="ro_password=$FUGUE_READONLY_PASSWORD" # secrets-scan:ignore (value comes from the compose env, local only)
psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB" -v "$RO_VAR" -f /opt/fugue/fugue-readonly-role.sql
