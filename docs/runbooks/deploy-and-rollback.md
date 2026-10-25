# Runbook: deploy, promote and roll back

## Normal flow

1. Merge a PR to `main`. `cd` builds `ghcr.io/nestoralfaro/meridian:<sha>` and deploys it to **staging** as a verified revision (see ADR-0008).
2. Check the run summary: current revision, new revision, smoke-test results, and the URL now serving the commit.
3. **Promote to production:** Actions → `cd` → *Run workflow* → enter the commit SHA (or leave empty for the head of `main`) → approve the `production` deployment when prompted.

## Check what is running

```bash
APP=meridian-production   # or meridian-staging
curl -s https://$(az containerapp show -g rg-meridian -n $APP --query properties.configuration.ingress.fqdn -o tsv)/api/v1/version
az containerapp revision list -g rg-meridian -n $APP \
  --query "[].{name:name, active:properties.active, traffic:properties.trafficWeight, created:properties.createdTime}" -o table
```

## Redeploy a known build

Actions → `deploy` → *Run workflow* → choose the environment and enter a commit SHA whose image exists in GHCR. It goes through the same smoke test, promotion and (for production) approval as a normal deployment.

## Roll back (seconds, no rebuild)

The previous revision stays active after every deployment. Send traffic back to it:

```bash
az containerapp ingress traffic set -g rg-meridian -n $APP --revision-weight <previous-revision-name>=100
```

If it was deactivated, re-activate it first:

```bash
az containerapp revision activate -g rg-meridian -n $APP --revision <revision-name>
```

Then confirm with `/api/v1/version`, and open an issue with the trace ID or failing smoke-test output.

## A deployment failed

The pipeline already kept traffic on the previous revision and deactivated the new one. Read the failing step in the run summary:

| Symptom | Likely cause | Next step |
|---|---|---|
| `/health/live` never 200 | Container failed to start (bad config, crash) | `az containerapp logs show -g rg-meridian -n $APP --revision <new> --tail 100` |
| `/health/ready` 503 | Database unreachable or wrong `DB_CONNECTION` secret | Check Neon status and the environment secret |
| Commit mismatch | Wrong image tag or caching in front of the revision URL | Compare the image on the revision with the expected SHA |
| Image pull error | GHCR package not public, or tag missing | Package settings → visibility *Public*; confirm the `image` job pushed the tag |
