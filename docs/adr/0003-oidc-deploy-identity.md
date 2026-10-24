# ADR-0003: Deploy to Azure with OIDC workload identity federation

- **Status:** Accepted
- **Date:** 2026-10-24

## Context

GitHub Actions needs to deploy to Azure. The traditional approach stores a service principal client secret in GitHub, which is long-lived, must be rotated, and is a high-value target if leaked.

## Options considered

1. **Client secret in GitHub secrets:** simple, but long-lived credentials that need manual rotation.
2. **OIDC workload identity federation:** GitHub issues a short-lived token per job; Azure trusts it only for a specific repository and branch/environment. No secret is stored.

## Decision

Use **OIDC federation**. The Entra ID app registration `gh-meridian-deploy` has:

- **Contributor on `rg-meridian` only** (least privilege: it cannot touch anything else in the subscription, and cannot assign roles).
- Federated credential subject `repo:nestoralfaro@83131937/meridian@1431767514:ref:refs/heads/main` while the repository is private. GitHub uses the *immutable* subject format for this repository (owner and repo numeric IDs embedded), so a deleted-and-recreated repository with the same name cannot inherit the trust.
- Once the repository is public, environment-scoped subjects (`repo:nestoralfaro@83131937/meridian@1431767514:environment:staging` / `...:environment:production`) replace the branch subject, so production deploys require passing the `production` environment's approval gate.

Client, tenant and subscription IDs are stored as GitHub *variables* (they are identifiers, not secrets).

## Consequences

- No Azure credentials exist in GitHub to leak or rotate.
- Workflows need `permissions: id-token: write`.
- Anything requiring role assignments (e.g., granting a managed identity access to Key Vault) cannot be done by the pipeline identity. That's deliberate; such changes are rare and done by an administrator.
