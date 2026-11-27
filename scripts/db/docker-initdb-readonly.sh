#!/bin/sh
# Runs once, on a fresh Postgres volume, from /docker-entrypoint-initdb.d (docker-compose.yml mounts it there).
# Creates the fugue_readonly role before the EF migrations exist; the ALTER DEFAULT PRIVILEGES in the SQL file
# makes every table the migrating owner (POSTGRES_USER) creates later readable. LOCAL development only.
set -eu
psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB" \
  -v ro_password="${FUGUE_READONLY_PASSWORD:-fugue_readonly_dev}" \
  -f /opt/fugue/fugue-readonly-role.sql
