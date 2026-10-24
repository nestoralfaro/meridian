# Meridian Client Portal

A full-stack demo of a client-facing investing and planning portal: **Angular** SPA + **ASP.NET Core (.NET 10)** REST API, deployed to **Azure Container Apps** with infrastructure as code, CI/CD, and automated quality gates.

> **🚧 Work in progress: Sprint 0 (infrastructure).** The application code has not been scaffolded yet. Follow along on the project board and in [`docs/adr`](docs/adr).

> **Disclaimer:** Meridian is a fictional demo. All data is synthetic. It is not affiliated with, endorsed by, or representative of any financial institution, and nothing here is investment advice.

## Environments

| Environment | URL | Deployed by |
|---|---|---|
| Staging | https://meridian-staging.whitefield-3aacc510.eastus2.azurecontainerapps.io | [`infra.yml`](.github/workflows/infra.yml) on merge to `main` |
| Production | https://meridian-production.grayhill-d6e042ce.eastus2.azurecontainerapps.io | [`infra.yml`](.github/workflows/infra.yml) (manual, approval-gated via the `production` environment) |
| Docs | https://nestoralfaro.github.io/meridian/ | [`pages.yml`](.github/workflows/pages.yml) on merge to `main` |

> Both environments currently serve a placeholder container (scale-to-zero, so the first request may take a few seconds) until the application is scaffolded.

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
  Meridian.Api.IntegrationTests/  in-memory host tests (WebApplicationFactory)
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

## Architecture decisions

- [ADR-0001](docs/adr/0001-record-architecture-decisions.md): Record architecture decisions
- [ADR-0002](docs/adr/0002-hosting-azure-container-apps.md): Host on Azure Container Apps
- [ADR-0003](docs/adr/0003-oidc-deploy-identity.md): Deploy with OIDC workload identity federation

## License

[MIT](LICENSE)
