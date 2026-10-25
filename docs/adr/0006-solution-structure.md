# ADR-0006: Layered solution structure, enforced by tests

- **Status:** Accepted
- **Date:** 2026-10-24

## Context

The codebase will grow feature by feature (accounts, transfers, planner, content). Without enforcement, layering erodes: controllers reach into EF Core, domain types pick up framework attributes, and changes ripple everywhere.

## Decision

Backend (`src/`), dependencies point inward only:

| Project | Responsibility | May depend on |
|---|---|---|
| `Meridian.Domain` | Entities, value objects (e.g. `Money`), domain rules | nothing (base class library only) |
| `Meridian.Application` | Use cases, ports (interfaces), validation | Domain, DI abstractions |
| `Meridian.Infrastructure` | EF Core, Npgsql, external APIs; **only `DependencyInjection` is public** | Application, Domain |
| `Meridian.Api`, `Meridian.Worker` | Hosts and composition roots (HTTP, background jobs) | Application, Infrastructure (via `AddInfrastructure`) |

**`Meridian.ArchitectureTests`** (ArchUnitNET + reflection) fails the build if a rule is broken: no outward dependencies, Domain references only the BCL, Application only Domain and DI abstractions, and Infrastructure exposes nothing but its composition entry point.

Deliberate exceptions, each documented where it's applied:
- EF Core **migrations** are generated as `public` and are exempt (`Persistence.Migrations` namespace; treated as generated code by `.editorconfig`).
- `InternalsVisibleTo` lets the integration tests apply migrations and query the persistence model directly.

Frontend (`src/meridian-web`): standalone components; `core/` for app-wide services (HTTP, auth, build info), `shared/ui/` for the design system, `pages/` and `features/` for routed, lazy-loaded areas. Selectors use the `mer-` prefix (lint-enforced).

Tests: architecture tests, plus `Meridian.Api.IntegrationTests` (in-memory host, real PostgreSQL, Reqnroll acceptance specs). `Domain.Tests` and `Application.Tests` are added with the first domain code; a project with zero tests fails Microsoft.Testing.Platform runs by design.

## Consequences

- Infrastructure can change (EF provider, external APIs) without touching use cases.
- Violations surface in CI with the offending type named, not in code review by memory.
- The structure costs a little ceremony (more projects, DI registration). Worth it at this size, and it's the shape enterprise .NET teams expect.
