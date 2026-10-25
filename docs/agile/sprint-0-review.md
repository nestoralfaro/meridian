# Sprint 0: review and retrospective

- **Planned window:** 2026-10-26 → 2026-11-06 (milestone [Sprint 0](https://github.com/nestoralfaro/meridian/milestone/1))
- **Actual:** scope delivered on 2026-10-24, ahead of the planned window, after the infrastructure groundwork (Azure, OIDC, Neon, environments, rulesets) on the same day
- **Sprint goal:** *a walking skeleton deployed end to end* ✅

## What shipped

| Issue | Outcome | PR |
|---|---|---|
| #7 .NET 10 solution with enforced layering | 5 projects, central package management, warnings as errors, 6 architecture rules | #28 |
| #8 Angular workspace with strict tooling | Angular 22, zoneless, strict TS, angular-eslint (incl. a11y), Vitest | #29 |
| #9 Walking skeleton | API serves the SPA on one origin; liveness/readiness/version; Reqnroll acceptance specs | #32 |
| #10 Containerize | Multi-stage chiseled image, non-root, 139 MB; `docker compose up` | #33 |
| #11 CI | Backend, frontend and container-image jobs in parallel; merged coverage report; required checks | #35 |
| #12 CD | Build once, provenance attestation, blue/green revisions, smoke test, auto-rollback; production promotion with approval | #36, #37 |
| #13 EF Core + migrations | Migration bundle in CD before deploy; least-privilege roles as code; append-only audit trail | #38 |
| #14 Observability | OpenTelemetry → Application Insights; trace id on every response and error | #39 |
| #15 DoR/DoD, ADR-0005/0006, this review | Docs | this PR |

**Demo:** [staging](https://meridian-staging.whitefield-3aacc510.eastus2.azurecontainerapps.io) (the footer shows the build commit), [docs site](https://nestoralfaro.github.io/meridian/), [board](https://github.com/users/nestoralfaro/projects/3).

## By the numbers

- **Velocity:** 33 of 33 planned points delivered (first sprint; it calibrates future planning, it isn't a target)
- **Tests:** 37 backend (architecture, integration, Reqnroll acceptance, database security, observability) + 9 frontend, all green in CI
- **Coverage (CI on `main`):** 90.7% line / 91.3% branch, backend and frontend merged
- **Deployments:** every merge since #36 deployed to staging through a smoke-tested revision; one deliberate **failure drill** confirmed traffic never moves to a broken revision
- **Dependencies:** 1 Dependabot PR verified and merged (#30); 2 rejected with reasons (#31 TypeScript 7 is outside Angular's supported range; #34 Node 25 is non-LTS), and ignore rules added so they don't recur

## Retrospective

**What went well**
- Infrastructure first: from #9 on, every change ran through a real pipeline into Azure.
- Tests found real problems before users could: the Kerberos-probe log noise and per-probe log volume in the container (#33), and an EF Core version conflict (#38).
- The quality gates caught mistakes: CI rejected an unformatted `angular.json` (#35); CodeQL flagged a test pattern that was then triaged and documented (#39).
- Mutation checks (temporarily breaking the code) proved the important tests actually fail when they should.

**What didn't go well**
- **Tests that pass for the wrong reason.** `header?.Value.ShouldBe(...)` silently skips when the header is missing; seven tests passed before any implementation existed (#32). In-memory span exporters also saw spans from parallel tests (#39).
- **A hidden local failure.** Piping a check through `tail` masked its exit code, so an unformatted file was committed (caught before push, #29).
- **Assumptions about tooling.** The test runner (VSTest → Microsoft.Testing.Platform), the bicepparam rules for required parameters, and `dotnet ef` bundle restore each needed a second attempt.
- The board doesn't move issues to Done automatically, so status was updated by hand.

**Action items**
| Action | Owner | Status |
|---|---|---|
| Use `HttpAssertions` helpers that fail on missing headers; scope telemetry assertions to the test's own trace ids | dev | ✅ done (#32, #39) |
| Always check real exit codes before committing; CI is the backstop | dev | ✅ CI now required |
| Turn on the board's built-in workflows (item closed / PR merged → Done) | repo owner | ⏳ open: web UI only |
| Promote to production at each sprint review, not ad hoc | repo owner | ⏳ first promotion awaiting approval |
| Upgrade the Azure free account to pay-as-you-go before the credit expires (~2026-11-17) | repo owner | ⏳ open |

## Next sprint

Sprint 1 (milestone [Sprint 1](https://github.com/nestoralfaro/meridian/milestone/2)): identity and the accounts dashboard. Demo sign-in, registration with lockout and CSRF protection, session timeout, account ownership authorization, dashboard, accessible allocation chart, app shell, seeded demo data, and the Playwright + axe gate.
