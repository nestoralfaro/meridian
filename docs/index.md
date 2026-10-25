---
title: "Meridian Client Portal: Engineering Docs"
---

# Meridian Client Portal: Engineering Docs

A full-stack demo of a client-facing investing and planning portal (Angular + ASP.NET Core on Azure Container Apps).
Source code: [github.com/nestoralfaro/meridian](https://github.com/nestoralfaro/meridian)

> Fictional demo with synthetic data. Not affiliated with any financial institution; not investment advice.

## Architecture Decision Records

- [ADR-0001: Record architecture decisions](adr/0001-record-architecture-decisions.html)
- [ADR-0002: Host on Azure Container Apps](adr/0002-hosting-azure-container-apps.html)
- [ADR-0003: Deploy with OIDC workload identity federation](adr/0003-oidc-deploy-identity.html)
- [ADR-0004: Serve the SPA and the API from a single origin](adr/0004-single-origin-hosting.html)
- [ADR-0008: Deploy every change as an immutable, verified revision](adr/0008-immutable-revision-deployments.html)

## Runbooks

- [Deploy, promote and roll back](runbooks/deploy-and-rollback.html)
- [Trace a client's request end to end](runbooks/trace-a-request.html)

## Coming soon

API reference, Storybook component library, test & coverage reports, accessibility and performance reports.
