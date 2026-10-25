-- Least-privilege grants for the application role. Run once per database, connected as the database owner
-- (Neon: SQL Editor on each branch, as meridiandb_owner). The integration tests execute this exact file
-- against PostgreSQL 17, so the documented setup and the tested setup cannot drift.
--
-- Prerequisite (Neon: run in the SQL Editor, never in the Console "Roles" UI, which grants neon_superuser):
--   CREATE ROLE meridian_app WITH LOGIN PASSWORD '<strong, per-environment password>';
--
-- The application role can read and write rows. It cannot create, alter or drop objects; schema changes
-- run only through migrations, as the owner. Tables that must be append-only (e.g. "AuditEvents")
-- additionally REVOKE UPDATE/DELETE in their migration.

DO $$ BEGIN EXECUTE format('GRANT CONNECT ON DATABASE %I TO meridian_app', current_database()); END $$;
GRANT USAGE ON SCHEMA public TO meridian_app;

-- Objects that already exist.
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO meridian_app;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO meridian_app;

-- Objects that migrations (run as the owner) create later.
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO meridian_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT USAGE, SELECT ON SEQUENCES TO meridian_app;
