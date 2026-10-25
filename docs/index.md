---
title: "Meridian Client Portal: Engineering Docs"
---

# Meridian Client Portal: Engineering Docs

A full-stack demo of a client-facing investing and planning portal (Angular + ASP.NET Core on Azure Container Apps).
Source code: [github.com/nestoralfaro/meridian](https://github.com/nestoralfaro/meridian)

> Fictional demo with synthetic data. Not affiliated with any financial institution; not investment advice.

## Reports (latest `main`)

- [End-to-end and accessibility tests (Playwright + axe)](reports/e2e/)
- [Code coverage (backend + frontend)](reports/coverage/)

## Architecture Decision Records

- [ADR-0001: Record architecture decisions](adr/0001-record-architecture-decisions.html)
- [ADR-0002: Host on Azure Container Apps](adr/0002-hosting-azure-container-apps.html)
- [ADR-0003: Deploy with OIDC workload identity federation](adr/0003-oidc-deploy-identity.html)
- [ADR-0004: Serve the SPA and the API from a single origin](adr/0004-single-origin-hosting.html)
- [ADR-0005: PostgreSQL on Neon, through EF Core](adr/0005-postgresql-on-neon.html)
- [ADR-0006: Layered solution structure, enforced by tests](adr/0006-solution-structure.html)
- [ADR-0007: Angular state with signals and resources; NgRx SignalStore deferred](adr/0007-angular-state-with-signals.html)
- [ADR-0008: Deploy every change as an immutable, verified revision](adr/0008-immutable-revision-deployments.html)

## Runbooks

- [Deploy, promote and roll back](runbooks/deploy-and-rollback.html)
- [Trace a client's request end to end](runbooks/trace-a-request.html)

## Coming soon

API reference, Storybook component library, test & coverage reports, accessibility and performance reports.

## How we work

- [Definition of Ready](agile/definition-of-ready.html) · [Definition of Done](agile/definition-of-done.html)
- [Sprint 0 review and retrospective](agile/sprint-0-review.html)
- [Sprint 1 review and retrospective](agile/sprint-1-review.html)
