# Runbook: trace a client's request end to end

Every response carries its W3C trace id: the `X-Trace-Id` header, and `traceId` in every ProblemDetails error body. In Application Insights this is the **`operation_Id`**.

## 1. Get the trace id

From the client or support ticket (the error page and ProblemDetails show it), or reproduce:

```bash
curl -si https://<app-url>/api/v1/does-not-exist | grep -i x-trace-id
```

## 2. Find everything that happened in that request

Azure Portal → `appi-meridian-<env>` → **Logs**, or the CLI (`az extension add -n application-insights`):

```kusto
union requests, dependencies, exceptions, traces
| where operation_Id == "<trace-id>"
| project timestamp, itemType, name, resultCode, duration, message, outerMessage, severityLevel
| order by timestamp asc
```

```bash
az monitor app-insights query -g rg-meridian --app appi-meridian-staging \
  --analytics-query 'union requests, dependencies, exceptions, traces | where operation_Id == "<trace-id>" | order by timestamp asc'
```

`requests` is the HTTP request, `dependencies` are database calls (Npgsql) and outbound HTTP, and `exceptions` holds the full exception and stack trace, which are never returned to the client.

## 3. Common queries

```kusto
// Failed requests in the last hour, by route
requests | where timestamp > ago(1h) and success == false
| summarize count() by name, resultCode | order by count_ desc

// Slowest routes (p95)
requests | where timestamp > ago(24h)
| summarize p95 = percentile(duration, 95), count() by name | order by p95 desc
```

## What is deliberately not collected

- **Health probes** (`/health/*`) aren't traced; they run every few seconds and would consume quota.
- **No request bodies, headers or credentials** are logged (covered by `LogsNeverContainCredentialsOrRequestBodies`).
- Log Analytics ingestion is capped at **0.1 GB/day** per environment (Bicep).
