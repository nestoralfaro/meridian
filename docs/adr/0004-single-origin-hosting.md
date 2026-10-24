# ADR-0004: Serve the SPA and the API from a single origin

- **Status:** Accepted
- **Date:** 2026-10-24

## Context

The Angular SPA and the ASP.NET Core API could be hosted separately (e.g., SPA on a static host or CDN, API on Container Apps) or together. The app handles authenticated financial data, so the session mechanism matters: we want `HttpOnly`, `Secure`, `SameSite=Strict` cookies rather than tokens readable by JavaScript.

## Options considered

1. **Separate origins** (static host + API): independent scaling and a CDN for the SPA, but requires CORS, and cookie auth across origins needs `SameSite=None` (weaker) or tokens in browser storage (exposed to XSS). Two deployables, two bills.
2. **Single origin**: the API host serves the built SPA as static files. No CORS, strict same-site cookies, one deployable and one container.

## Decision

**Single origin.** `Meridian.Api` serves the Angular build from its web root:

- Static assets via the static-files middleware; `index.html` is sent with `Cache-Control: no-cache` so a new deployment is picked up immediately (bundles are content-hashed).
- Client-side routes (`/dashboard`, …) fall back to `index.html`, except paths that look like files (`/missing.js` → 404).
- `/api/**` and `/health/**` are **reserved**: unknown paths under them return a 404 ProblemDetails, never the SPA.
- Locally, `ng serve` proxies `/api` and `/health` to the API (`proxy.conf.json`); the `single-origin` launch profile reproduces production hosting with a real Angular build.

## Consequences

- The SPA can't be put on a CDN independently. Acceptable at this scale, and revisitable (a CDN in front of the single origin also works).
- The container image must include the Angular build (multi-stage Dockerfile, #10).
- Auth can use strict same-site cookies with antiforgery protection (#16, #17).
