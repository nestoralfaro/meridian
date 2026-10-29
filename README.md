# Meridian Client Portal

[![ci](https://github.com/nestoralfaro/meridian/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/nestoralfaro/meridian/actions/workflows/ci.yml)
[![CodeQL](https://github.com/nestoralfaro/meridian/actions/workflows/codeql/badge.svg?branch=main)](https://github.com/nestoralfaro/meridian/security/code-scanning)
[![cd](https://github.com/nestoralfaro/meridian/actions/workflows/cd.yml/badge.svg?branch=main)](https://github.com/nestoralfaro/meridian/actions/workflows/cd.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

A full-stack demo of a client-facing investing and planning portal: **Angular** SPA + **ASP.NET Core (.NET 10)** REST API, deployed to **Azure Container Apps** with infrastructure as code, CI/CD, and automated quality gates.

**Try it:** [production](https://meridian-production.grayhill-d6e042ce.eastus2.azurecontainerapps.io) → **Sign in** → *Sign in as Dana* (a new investor) or *Sign in as Raj* (a pre-retiree). No account needed; demo data resets daily.

| What you can do | Under the hood |
|---|---|
| **Dashboard**: total value, day change, allocation chart, recent activity | Exact `decimal` money; allocation percentages always total 100 (largest-remainder); every chart has a data table |
| **Account pages**: holdings sorted, filtered and paged on the server | Allow-listed sort keys; another client's account looks exactly like a missing one, and the attempt is audited |
| **Move money** between your own accounts, with review and confirm | Idempotency keys (retries never move money twice), optimistic concurrency on PostgreSQL `xmin`, one atomic transaction, DB check constraints, audit trail |
| **Live prices** on account pages while you watch | SignalR over WebSockets; the server values every holding, so the browser never multiplies money; ticks are silent for screen readers |
| **Retirement planner**: the chance your savings last to 95, with a fan chart | Seeded Monte Carlo built test-first and checked against an independent implementation |
| **Learn**: short articles, no sign-in needed | Markdown reviewed through pull requests (a Git-based CMS), raw HTML disabled, then sanitized |
| **Register**, sign in, idle timeout warning | Cookie session (no tokens in the browser), CSRF tokens, lockout, rate limiting, enumeration-safe errors |
| **API reference** at [`/docs`](https://meridian-production.grayhill-d6e042ce.eastus2.azurecontainerapps.io/docs) | Committed OpenAPI contract → generated TypeScript types, with drift and breaking-change gates in CI |

**Every change** passes the required checks before merge:
- unit tests, plus Reqnroll acceptance scenarios against real PostgreSQL
- Playwright in three browser profiles, with an axe WCAG 2.2 AA scan on every page
- CodeQL and secret scanning
- API contract checks

It then deploys to staging as an immutable revision, and traffic moves only after smoke tests and the full browser suite pass against that revision. Production is promoted from the same image, with approval. How the work was planned and reviewed: [sprint reviews](#how-we-work), [ADRs](#architecture-decisions).

> **Disclaimer:** Meridian is a fictional demo. All data is synthetic. It is not affiliated with, endorsed by, or representative of any financial institution, and nothing here is investment advice.

## Environments

| Environment | URL | Deployed by |
|---|---|---|
| Staging | https://meridian-staging.whitefield-3aacc510.eastus2.azurecontainerapps.io | [`cd.yml`](.github/workflows/cd.yml) on every merge to `main` (blue/green, smoke-tested) |
| Production | https://meridian-production.grayhill-d6e042ce.eastus2.azurecontainerapps.io | [`cd.yml`](.github/workflows/cd.yml) manual promotion of a staging-verified image, approval-gated |
| Docs | https://nestoralfaro.github.io/meridian/ | [`pages.yml`](.github/workflows/pages.yml) on merge to `main` |

> Both environments scale to zero when idle, so the first request after a quiet period can take a few seconds. Deployment and rollback: [runbook](docs/runbooks/deploy-and-rollback.md).

## Repository layout

```
src/
  Meridian.Domain/          domain model (no framework or project dependencies)
  Meridian.Application/     use cases and ports (depends on Domain only)
  Meridian.Infrastructure/  adapters; only DependencyInjection is public
  Meridian.Api/             ASP.NET Core host and composition root
  Meridian.Worker/          background jobs host; one-off commands (demo-data reset)
  meridian-web/             Angular SPA (zoneless, standalone, strict TypeScript)
tests/
  Meridian.Domain.Tests/          domain rules (Money, valuation, transfers, the planner model), test-first
  Meridian.Application.Tests/     use cases (queries, transfers) against in-memory ports
  Meridian.ArchitectureTests/     layering rules enforced in CI (ArchUnitNET)
  e2e/                            Playwright + axe against the running stack
  Meridian.Api.IntegrationTests/  host tests + Reqnroll acceptance specs, real PostgreSQL via Testcontainers
content/     Learn center articles (Markdown with front matter, embedded at build time)
db/          least-privilege grants for the application role (executed by tests)
openapi/     the committed API contract (OpenAPI 3.1)
.github/     workflows, issue/PR templates, CODEOWNERS, Dependabot
infra/       Bicep: Log Analytics, Application Insights, Container Apps, budget
docs/adr/    Architecture Decision Records
```

## Build and test

**Backend** (.NET 10 SDK, pinned in [`global.json`](global.json)):

```bash
dotnet build              # warnings are errors
dotnet test               # xUnit v3 on Microsoft.Testing.Platform
dotnet run --project src/Meridian.Api
```

**Frontend** (Node 24, pinned in [`.nvmrc`](.nvmrc)):

```bash
cd src/meridian-web
nvm use
npm ci
npm start                 # dev server at http://localhost:4200
npm run lint              # angular-eslint incl. template accessibility rules
npm run format:check      # Prettier
npm run test:ci           # Vitest, single run
npm run build             # production build with bundle budgets
```

## Run locally

**Whole stack in containers** (production-like image + PostgreSQL 17):

```bash
SOURCE_REVISION=$(git rev-parse HEAD) docker compose up --build   # http://localhost:8080
docker compose down -v                                             # stop and delete the local database
```

**Day-to-day development** (hot reload). Start the database and apply migrations, then run the API and the Angular dev server, which proxies `/api` and `/health` to the API:

```bash
docker compose up -d db migrations
dotnet run --project src/Meridian.Api          # http://localhost:5090
(cd src/meridian-web && npm start)            # http://localhost:4200
```

To serve a real Angular build from the API without Docker: `(cd src/meridian-web && npm run build)`, then `dotnet run --project src/Meridian.Api --launch-profile single-origin`.

Endpoints: `/health/live` (process up), `/health/ready` (database reachable **and** schema up to date), `/api/v1/version` (build and commit).

### Sign-in and session security

- **Cookie session, no tokens in the browser**: ASP.NET Core Identity issues an `HttpOnly`, `Secure`, `SameSite=Strict` cookie with a 15-minute sliding idle timeout. The API answers 401/403; it never redirects.
- **CSRF protection**: antiforgery tokens on every state-changing request, using the cookie and header names Angular's `HttpClient` sends automatically (`XSRF-TOKEN` → `X-XSRF-TOKEN`).
- **Demo personas**: "Sign in as Dana / Raj" without credentials. Demo accounts can't change credentials.
- **Registration and password sign-in**: passwords of at least 12 characters that aren't on a common-password list (NIST SP 800-63B), with field-level guidance. **Lockout** after 5 failures for 15 minutes; **one generic message** for every failure (and a dummy hash check for unknown emails), so responses don't reveal which emails are registered; **per-IP rate limiting** on credential endpoints (10/min in production).
- **Append-only audit trail** of registrations, sign-ins, failures, lockouts and sign-outs, correlated with trace ids.
- **Idle timeout**: after 13 minutes without input, a modal warning (native `<dialog>`, focus on "Stay signed in"); at 15 minutes the client is signed out and told why. This matches the server's 15-minute sliding session. Background requests don't count as activity.
- **Data Protection keys** (which sign cookies and tokens) live in PostgreSQL, so sessions survive restarts and work across replicas.
- Identity stays inside Infrastructure behind an Application port; the API only turns a verified user into claims and a cookie.

### Money movement and live prices

- **Transfers** (`POST /api/v1/transfers`):
  - **Idempotency:** an `Idempotency-Key` header is required. A retry returns the original transfer (`Idempotent-Replayed: true`); reusing a key for a different transfer is a 422.
  - **Optimistic concurrency** on PostgreSQL's `xmin`: a stale review is a 409, and concurrent transfers can't lose updates.
  - **One transaction** covers both balances, both activity rows, the transfer and its audit event; check constraints back it up.
  - **Tested under real concurrency:** parallel retries with one key, and parallel transfers that would overdraw.
- **Live prices** (`/hubs/quotes`, SignalR):
  - Prices are a **simulated**, bounded random walk from the last close. Stored balances stay valued at the close; live figures are display-only.
  - The server values each holding with the same domain rules as stored balances, and pushes per-viewer results only to clients watching their **own** account.
  - The feed does no work when nobody is watching.
  - Prices and subscriptions live in memory per replica. A real deployment would take a market-data feed through a backplane (e.g. Azure SignalR Service).

### Demo data

- Two **fictional personas**, Dana Lee (`dana@meridian.test`, new investor) and Raj Mehta (`raj@meridian.test`, pre-retiree), with brokerage, IRA and savings accounts, holdings in fictional securities (`MDTOT`, `MDINT`, `MDBND`, `MDMMF`, `ACME`, `GLBX`) and 90 days of activity.
- Holdings are defined explicitly, so stories can rely on exact figures (Dana: $12,480.55 brokerage, $8,301.20 Roth IRA). The history comes from a **seeded generator**, so the same date always produces the same data.
- When `Demo__Enabled=true` (compose, staging, production), the app **seeds an empty database on startup and resets data older than 24 hours**. It runs whenever the app is awake, so it works with scale-to-zero and needs no external scheduler. Seeding runs as the least-privilege app role, in one transaction under a PostgreSQL advisory lock, so concurrent replicas can't double-seed.
- Reset on demand: Actions → `demo-data` → choose the environment, or locally `dotnet run --project src/Meridian.Worker -- demo-data reset`.

### API contract

- Interactive reference at **`/docs`** (Scalar); OpenAPI document at **`/openapi/v1.json`** ([staging](https://meridian-staging.whitefield-3aacc510.eastus2.azurecontainerapps.io/docs)).
- The contract is committed at [`openapi/meridian-v1.json`](openapi/meridian-v1.json), so API changes are visible in code review:
  - a backend test fails if the API and the committed contract differ;
  - the SPA's TypeScript types are **generated** from it (`src/meridian-web/src/app/core/api`), and CI fails if they drift;
  - **oasdiff** fails a PR that makes a breaking change against `main`. A deliberate, reviewed exception goes in [`openapi/oasdiff-err-ignore.txt`](openapi/oasdiff-err-ignore.txt) **with a written reason**, never through an admin bypass.
- After changing an endpoint:
  ```bash
  MERIDIAN_UPDATE_OPENAPI=1 dotnet test --project tests/Meridian.Api.IntegrationTests -- --filter-class "*OpenApiContractTests"
  (cd src/meridian-web && npm run api:generate)
  ```

### Database and migrations

- **EF Core 10 + Npgsql**; migrations live in `src/Meridian.Infrastructure/Persistence/Migrations`.
  ```bash
  dotnet tool restore
  dotnet ef migrations add <Name> --project src/Meridian.Infrastructure --startup-project src/Meridian.Infrastructure --output-dir Persistence/Migrations
  ```
- **Two roles, least privilege.** CD applies migrations with a self-contained migration bundle as the database **owner** (direct connection) *before* the new revision exists. The running app connects as **`meridian_app`**, which can read and write rows but can't change the schema. Grants: [`db/app-role-grants.sql`](db/app-role-grants.sql), the same file the integration tests execute.
- **Append-only audit trail.** The app role can't `UPDATE`, `DELETE` or `TRUNCATE` `"AuditEvents"`, enforced by the database and covered by tests.
- Migrations must be **backward compatible** (expand/contract): the previous revision keeps serving while they run.

### End-to-end and accessibility tests

[Playwright](tests/e2e) drives the running stack in **Chromium, WebKit and a mobile viewport**, and every visited page is scanned with **axe** for WCAG 2.2 AA. Any serious or critical violation fails CI.

```bash
docker compose up -d --build                    # the stack under test
cd tests/e2e && npm ci && npx playwright install chromium webkit
npm test                                        # BASE_URL=https://<env-url> npm test to run against a deployment
```

Latest reports from `main`: [Playwright](https://nestoralfaro.github.io/meridian/reports/e2e/) · [coverage](https://nestoralfaro.github.io/meridian/reports/coverage/)

### Observability

- **OpenTelemetry** traces, metrics and logs (ASP.NET Core, HttpClient, Npgsql), exported to **Application Insights** via the Azure Monitor distro when `APPLICATIONINSIGHTS_CONNECTION_STRING` is set (Bicep injects it).
- **Every response carries its trace id** (`X-Trace-Id`, and `traceId` in every RFC 9457 ProblemDetails body), so a support ticket leads straight to the request, its database calls and any exception: [runbook](docs/runbooks/trace-a-request.md).
- Unhandled exceptions return a 500 ProblemDetails with no internals; details stay in telemetry.
- Cost guardrails: health probes aren't traced, Npgsql logs warnings only, and nothing logs credentials or bodies (tested).

### Container image

Multi-stage [`Dockerfile`](Dockerfile): Node builds the SPA → .NET SDK publishes the API → **chiseled** ASP.NET runtime (no shell or package manager, non-root user `app`, port 8080). About 134 MB uncompressed and 52 MB to pull; the app itself adds about 2 MB to Microsoft's base image.

## Architecture decisions

- [ADR-0001](docs/adr/0001-record-architecture-decisions.md): Record architecture decisions
- [ADR-0002](docs/adr/0002-hosting-azure-container-apps.md): Host on Azure Container Apps
- [ADR-0003](docs/adr/0003-oidc-deploy-identity.md): Deploy with OIDC workload identity federation
- [ADR-0004](docs/adr/0004-single-origin-hosting.md): Serve the SPA and the API from a single origin
- [ADR-0005](docs/adr/0005-postgresql-on-neon.md): PostgreSQL on Neon, through EF Core
- [ADR-0006](docs/adr/0006-solution-structure.md): Layered solution structure, enforced by tests
- [ADR-0007](docs/adr/0007-angular-state-with-signals.md): Angular state with signals and resources; NgRx SignalStore deferred
- [ADR-0008](docs/adr/0008-immutable-revision-deployments.md): Deploy every change as an immutable, verified revision

## How we work

- [Definition of Ready](docs/agile/definition-of-ready.md) · [Definition of Done](docs/agile/definition-of-done.md)
- [Project board](https://github.com/users/nestoralfaro/projects/3) (two-week sprints, story points)
- Sprint reviews and retrospectives: [Sprint 0](docs/agile/sprint-0-review.md) · [Sprint 1](docs/agile/sprint-1-review.md) · [Sprint 2](docs/agile/sprint-2-review.md)

## License

[MIT](LICENSE)
