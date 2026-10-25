# Meridian Client Portal

[![ci](https://github.com/nestoralfaro/meridian/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/nestoralfaro/meridian/actions/workflows/ci.yml)
[![CodeQL](https://github.com/nestoralfaro/meridian/actions/workflows/codeql/badge.svg?branch=main)](https://github.com/nestoralfaro/meridian/security/code-scanning)
[![cd](https://github.com/nestoralfaro/meridian/actions/workflows/cd.yml/badge.svg?branch=main)](https://github.com/nestoralfaro/meridian/actions/workflows/cd.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

A full-stack demo of a client-facing investing and planning portal: **Angular** SPA + **ASP.NET Core (.NET 10)** REST API, deployed to **Azure Container Apps** with infrastructure as code, CI/CD, and automated quality gates.

> **🚧 Work in progress: Sprint 0 (infrastructure).** The application code has not been scaffolded yet. Follow along on the project board and in [`docs/adr`](docs/adr).

> **Disclaimer:** Meridian is a fictional demo. All data is synthetic. It is not affiliated with, endorsed by, or representative of any financial institution, and nothing here is investment advice.

## Environments

| Environment | URL | Deployed by |
|---|---|---|
| Staging | https://meridian-staging.whitefield-3aacc510.eastus2.azurecontainerapps.io | [`cd.yml`](.github/workflows/cd.yml) on every merge to `main` (blue/green, smoke-tested) |
| Production | https://meridian-production.grayhill-d6e042ce.eastus2.azurecontainerapps.io | [`cd.yml`](.github/workflows/cd.yml) manual promotion of a staging-verified image, approval-gated |
| Docs | https://nestoralfaro.github.io/meridian/ | [`pages.yml`](.github/workflows/pages.yml) on merge to `main` |

> Both environments scale to zero when idle, so the first request after a quiet period can take a few seconds. Deployment and rollback: [runbook](docs/runbooks/deploy-and-rollback.md).

## Repository layout (so far)

```
src/
  Meridian.Domain/          domain model (no framework or project dependencies)
  Meridian.Application/     use cases and ports (depends on Domain only)
  Meridian.Infrastructure/  adapters; only DependencyInjection is public
  Meridian.Api/             ASP.NET Core host and composition root
  Meridian.Worker/          background jobs host
  meridian-web/             Angular SPA (zoneless, standalone, strict TypeScript)
tests/
  Meridian.ArchitectureTests/     layering rules enforced in CI (ArchUnitNET)
  Meridian.Api.IntegrationTests/  host tests + Reqnroll acceptance specs, real PostgreSQL via Testcontainers
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

**Day-to-day development** (hot reload). Start only the database, then the API and the Angular dev server, which proxies `/api` and `/health` to the API:

```bash
docker compose up -d db
dotnet run --project src/Meridian.Api          # http://localhost:5090
(cd src/meridian-web && npm start)            # http://localhost:4200
```

To serve a real Angular build from the API without Docker: `(cd src/meridian-web && npm run build)`, then `dotnet run --project src/Meridian.Api --launch-profile single-origin`.

Endpoints: `/health/live` (process up), `/health/ready` (database reachable), `/api/v1/version` (build and commit).

### Container image

Multi-stage [`Dockerfile`](Dockerfile): Node builds the SPA → .NET SDK publishes the API → **chiseled** ASP.NET runtime (no shell or package manager, non-root user `app`, port 8080). About 134 MB uncompressed and 52 MB to pull; the app itself adds about 2 MB to Microsoft's base image.

## Architecture decisions

- [ADR-0001](docs/adr/0001-record-architecture-decisions.md): Record architecture decisions
- [ADR-0002](docs/adr/0002-hosting-azure-container-apps.md): Host on Azure Container Apps
- [ADR-0003](docs/adr/0003-oidc-deploy-identity.md): Deploy with OIDC workload identity federation
- [ADR-0004](docs/adr/0004-single-origin-hosting.md): Serve the SPA and the API from a single origin
- [ADR-0008](docs/adr/0008-immutable-revision-deployments.md): Deploy every change as an immutable, verified revision

## License

[MIT](LICENSE)
