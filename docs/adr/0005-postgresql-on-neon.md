# ADR-0005: PostgreSQL on Neon, through EF Core

- **Status:** Accepted
- **Date:** 2026-10-24

## Context

The app needs a relational database for accounts, transfers, audit events and content. Constraints: near-zero cost while idle, fast first response after idle (reviewers hit the demo cold), managed backups, and a realistic production setup (roles, migrations, TLS). Many .NET teams standardize on SQL Server, so that familiarity matters too.

## Options considered

| Option | Cost | Cold start | Notes |
|---|---|---|---|
| **Neon PostgreSQL (free plan)** | $0 | compute wakes in well under a second | Branching (production/staging), scale to zero, PostgreSQL 17 |
| Azure SQL Database (free offer) | $0 within the monthly vCore allowance | serverless auto-pause resume can take tens of seconds | SQL Server parity; the allowance runs out if kept warm |
| SQLite in the container | $0 | none | Data is ephemeral per revision; not a realistic production signal |
| Azure Database for PostgreSQL Flexible Server | ~$13+/month (burstable) | always on | Managed by Azure, but paid while idle |

## Decision

**Neon PostgreSQL 17** with one database branch per environment, accessed through **EF Core 10 + Npgsql**.

- Two roles: `meridiandb_owner` runs migrations (CD, direct host); `meridian_app` is what the app connects as (pooled host), with **DML only** (`db/app-role-grants.sql`, executed by the tests).
- Persistence stays behind the Infrastructure layer (ADR-0006), so moving to Azure SQL means swapping the EF provider and regenerating migrations; application and domain code don't change.
- Tests run against **PostgreSQL 17 in Testcontainers**, the same major version as Neon, not an in-memory fake.

## Consequences

- PostgreSQL rather than SQL Server: worth calling out in interviews; the EF Core abstraction keeps the switch cost bounded.
- The Neon project is in AWS `us-east-2` while the app runs in Azure `eastus2`, so each database round trip crosses regions (a few ms). Acceptable for a demo; co-locate if latency matters.
- Readiness probes query the database, which keeps Neon compute awake while app replicas run. With scale-to-zero replicas, that's only while the demo is in use, within the free compute allowance.
- Staging can be refreshed from production with Neon "reset from parent". Fine with synthetic data; never with real client data.
