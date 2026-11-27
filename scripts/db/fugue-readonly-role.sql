-- Creates (or refreshes) the fugue_readonly LOGIN role: SELECT only, and read-only transactions even if a client asks for more.
-- For LOCAL development and agent tooling (the postgres-readonly MCP server). It can read every table, including refresh-token digests and
-- idempotency bodies, so never point it at a shared or production database.
-- Idempotent: safe to run again; a re-run also rotates the password. Run it as the database owner (the role that runs the migrations):
--   psql "$OWNER_URI" -v ro_password=REPLACE_ME -f scripts/db/fugue-readonly-role.sql
-- The password comes from the psql variable ro_password; it is never stored in this file.
\set ON_ERROR_STOP on

SELECT format('CREATE ROLE fugue_readonly LOGIN PASSWORD %L', :'ro_password')
WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'fugue_readonly')
\gexec

SELECT format('ALTER ROLE fugue_readonly WITH LOGIN PASSWORD %L', :'ro_password')
\gexec

SELECT format('GRANT CONNECT ON DATABASE %I TO fugue_readonly', current_database())
\gexec

GRANT USAGE ON SCHEMA public TO fugue_readonly;
GRANT SELECT ON ALL TABLES IN SCHEMA public TO fugue_readonly;
GRANT SELECT ON ALL SEQUENCES IN SCHEMA public TO fugue_readonly;

-- Tables the owner creates later (new migrations) are readable too. Applies to objects created by the role running this script.
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT ON TABLES TO fugue_readonly;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT ON SEQUENCES TO fugue_readonly;

ALTER ROLE fugue_readonly SET default_transaction_read_only = on;
