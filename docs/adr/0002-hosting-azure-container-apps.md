# ADR-0002: Host the application on Azure Container Apps

- **Status:** Accepted
- **Date:** 2026-10-24

## Context

The live demo must be publicly reachable, run an ASP.NET Core API that also serves the Angular SPA, support zero-downtime deploys and rollback, and cost close to nothing while idle.

## Options considered

| Option | Monthly cost | Notes |
|---|---|---|
| **Azure Container Apps (Consumption)** | $0 within the monthly free grant; a few USD if kept warm | Managed HTTPS, revisions + traffic splitting, scale to zero, OIDC deploys, Bicep |
| Azure App Service F1 | $0 | 60 CPU-minutes/day, no Always On, no deployment slots |
| Azure Static Web Apps (Free) + Functions | $0 | API must be Azure Functions; ASP.NET Core backend requires the paid plan |
| Render free web service | $0 | Sleeps after ~15 min idle with long cold starts; weaker IaC story |
| Small VPS | ~$4–6 | Always on, but OS patching, backups and TLS are on me |

## Decision

Use **Azure Container Apps** on the Consumption plan, one Container App per environment (`staging`, `production`), each in its own Container Apps environment with its own Log Analytics workspace and Application Insights resource. Images come from public GitHub Container Registry packages (no Azure Container Registry cost). All resources live in the resource group `rg-meridian` and are defined in Bicep (`infra/`).

## Consequences

- **Cold starts:** with `minReplicas: 0` the first request after idle takes several seconds. Mitigation: set `minReplicas = 1` in `production.bicepparam` while the demo is being actively reviewed.
- **Cost guardrails:** Log Analytics daily cap (0.1 GB/day), `maxReplicas: 2`, and a resource-group budget (`infra/budget.bicep`) with email alerts.
- `activeRevisionsMode: Multiple` lets the CD pipeline deploy a new revision at 0% traffic, smoke-test it, then shift traffic (blue/green), with instant rollback to the previous revision.
