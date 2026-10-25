# Sprint 1: review and retrospective

- **Planned window:** 2026-11-09 → 2026-11-20 (milestone [Sprint 1](https://github.com/nestoralfaro/meridian/milestone/2))
- **Actual:** delivered 2026-10-25, ahead of the planned window
- **Sprint goal:** *identity and the accounts dashboard* ✅

## What shipped

| Issue | Outcome | PR |
|---|---|---|
| #25 Playwright + axe gate | Chromium, WebKit and mobile against the real stack; axe WCAG 2.2 AA on every page; reports on Pages | #41 |
| #23 App shell | Skip link, bottom bar on phones, focus moves to each page's `<h1>`, light/dark tokens, 200% zoom | #43 |
| #27 Spike: state management | ADR-0007: signals + `httpResource`; NgRx SignalStore deferred with revisit triggers | #44 |
| #26 OpenAPI contract | Committed contract, generated TypeScript types, drift checks, oasdiff breaking-change gate, Scalar at `/docs` | #45 |
| #24 Demo data | Domain model (`Money`, portfolio valuation), deterministic personas, in-app daily reset, advisory-locked seeding | #46 |
| #16 Demo sign-in | ASP.NET Core Identity behind an Application port; hardened cookie session; antiforgery; Data Protection keys in PostgreSQL | #48 |
| #17 Email/password | Lockout, enumeration-safe failures, NIST password rules + common-password check, per-IP rate limiting, audit trail | #51 |
| #18 Session lifecycle | Sign-out; idle warning at 13 min (native `<dialog>`), sign-out at 15 with an explanation | #52 |
| #19 Account ownership | Accounts API scoped to the client; another client's account is indistinguishable from a missing one, and audited | #53 |
| #20 Dashboard | Balances, signed day changes, total; loading/error (reference ID)/empty states; per-user data isolation | #54 |
| #21 Allocation chart | Largest-remainder percentages that total exactly 100; donut + accessible table; palette contrast computed in tests | #55 |
| #22 Recent activity | Five newest transactions, full Activity page, empty state | #56 |

Improvements found along the way: #47 (EF Core log volume), #49 (demo-data versioning), #50 (e2e gate in CD), #57 (non-waiting assertion).

**Demo:** [staging](https://meridian-staging.whitefield-3aacc510.eastus2.azurecontainerapps.io): sign in as Dana or Raj, or register.

## By the numbers

- **Velocity:** 38 of 38 points, plus the time-boxed spike (Sprint 0: 33)
- **Tests:** 103 backend (domain, application, integration, 25 Reqnroll scenarios, architecture), 68 Vitest, 35 Playwright journeys × 3 browser profiles; every page axe-scanned
- **Coverage (CI on `main`, generated code excluded):** 84.9% line / 70.4% branch over 1,723 lines. *Sprint 0 reported 90.7% / 91.3%; from this sprint EF migrations and generated OpenAPI types are excluded, because they inflated the figure.*
- **Deployments:** every merge deployed to staging through smoke + full e2e gates; **the e2e gate blocked one deployment** (#56) and traffic never moved
- **Dependencies:** 1 Dependabot PR rejected (#42, `@types/node` major ahead of the runtime) with an ignore rule

## Retrospective

**What went well**
- **Gates caught real problems before users did:**
  - running e2e against staging found a demo-data bug (old data lacked demo users), which led to the e2e gate in CD (#49, #50)
  - oasdiff flagged a contract change, accepted through a reviewed ignore file instead of an admin bypass (#54)
  - the e2e gate blocked a deployment over a flaky assertion (#56 → #57)
- **Security properties are tested, not asserted**: lockout, generic failures, rate limits (429 + Retry-After), CSRF, IDOR (identical 404s), per-user data isolation in the SPA, least-privilege SQL.
- **Accessibility is designed in**: focus management, announced errors, a data table behind every chart, computed palette contrast, axe in both themes.
- Mutation-style checks (removing the advisory lock, the REVOKE, the trace filter, the log level) proved key tests fail when they should.

**What didn't go well**
- **Timing-sensitive tests**, three times: a span-export wait (#53), a navigation race only visible on staging (#50), and a non-waiting `count()` assertion that blocked a deployment (#57).
- **A hand-computed expectation was wrong** (allocation rounding). It was caught by computing the method exactly before implementing, but it shows the risk.
- **Configuration that differs by environment** surprised us twice: `Secure` cookies on the plain-HTTP compose stack, and the rate limiter tripping the parallel e2e suite.
- **The board** was set to Done once before a PR had actually merged (fixed; status now changes only after the issue closes).

**Action items**
| Action | Owner | Status |
|---|---|---|
| Web-first assertions only in Playwright (add `eslint-plugin-playwright` with `prefer-web-first-assertions`) | dev | ⏳ next sprint |
| Compute expected values in tests from the specification (or independently), not by hand | dev | ✅ practice adopted |
| Environment-specific settings are explicit and documented (`Auth:SecureCookies`, `Auth:SignInAttemptsPerMinute`) | dev | ✅ done |
| Promote to production at each sprint review | repo owner | ⏳ promotion requested with this review |
| Upgrade the Azure free account before the credit ends (~2026-11-17) | repo owner | ⏳ open |
| Turn on the board's built-in "closed → Done" workflow | repo owner | ⏳ open |

## Next sprint (proposal)

There's no Sprint 2 backlog yet. Candidates from the product spec:
- **Move money**: transfers between own accounts, with idempotency keys, optimistic concurrency and an audited review/confirm flow
- **Positions view**: server-side paging, sorting and filtering
- **Live quotes over SignalR**
- **Retirement planner**: Monte Carlo, built test-first
- **CMS-backed Learn center**
