# ADR-0008: Deploy every change as an immutable, verified revision

- **Status:** Accepted
- **Date:** 2026-10-24
- **Supersedes:** the separate `infra.yml` infrastructure workflow

## Context

Azure Container Apps supports multiple active revisions with per-revision URLs and weighted traffic. Two workflows deploying the same Container App (one for infrastructure, one for the application image) would overwrite each other's image and traffic settings. We want zero-downtime deployments, automatic rollback, and a single, auditable path to production.

## Decision

- **One pipeline** (`cd.yml` → reusable `deploy.yml`) deploys both infrastructure (Bicep) and the image, for every change merged to `main`.
- The image is **built once** per commit, tagged with the commit SHA, pushed to GHCR, and given a **signed build-provenance attestation**. Staging and production run the identical image.
- Each deployment creates a **uniquely named revision** (`<app>--<sha7>-<run>-<attempt>`) while Bicep keeps **100% of traffic pinned to the current revision**.
- The new revision is **smoke-tested on its own revision URL** (`scripts/smoke-test.sh`: liveness, readiness/database, commit identity, SPA routes, API 404s). On **staging**, the full **Playwright + axe** suite then runs against that URL over real HTTPS with real data, before any traffic moves. (Added after a demo-data bug passed CI against a fresh database but broke sign-in on staging's existing data.)
- **Pass:** traffic shifts 100% to the new revision; the previous revision stays active (scaled to zero) for instant rollback; older ones are deactivated.
- **Fail:** traffic never moves; the new revision is deactivated; the job summary explains why.
- **Production** is promoted manually (`workflow_dispatch` with a commit SHA) through the `production` environment, which requires approval. Merges don't create a queue of pending production approvals.

## Consequences

- Infrastructure changes get the same verification as code changes.
- Rollback is a traffic switch, not a rebuild (see `docs/runbooks/deploy-and-rollback.md`).
- The GHCR package must be **public** so Container Apps can pull without stored registry credentials.
- Database migrations (#13) slot in before the new revision is deployed, and must stay backward compatible with the previous revision (expand/contract).
