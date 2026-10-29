# Sprint 2: review and retrospective

- **Planned window:** 2026-10-29 → 2026-11-12 (milestone [Sprint 2](https://github.com/nestoralfaro/meridian/milestone/3))
- **Actual:** delivered 2026-10-29
- **Sprint goal:** *move money, see holdings, plan for retirement* ✅

## What shipped

| Issue | Outcome | PR |
|---|---|---|
| #60 Dependency advisories | All 9 Dependabot alerts fixed (vite via Angular 22.2.5; js-yaml via an override, with the reason recorded). CI now fails on high-severity npm advisories | #68 |
| #61 Web-first assertions | `eslint-plugin-playwright` on the e2e specs. **The plugin doesn't flag the exact #56 bug** (`expect(await list.count()).toBeGreaterThan(5)`), so a `no-restricted-syntax` rule bans it, checked against that pattern | #69 |
| #62 Positions | Account pages with holdings sorted, filtered and paged on the server; allow-listed sort keys; `aria-sort` headers; ownership as in #19 | #72 |
| #63 Transfers | Review → confirm; idempotency keys; optimistic concurrency on `xmin`; one atomic transaction with an audit event; DB check constraints | #74 |
| #64 Retirement planner | Seeded Monte Carlo built test-first and checked against an independent implementation; fan chart + data table | #75 |
| #65 Live quotes | SignalR over WebSockets; the server values every holding per viewer; silent ticks, announced state changes; lazy-loaded client | #76 |
| #66 Learn center | Articles as reviewed Markdown (Git-based CMS), raw HTML disabled, then sanitized; public pages | #77 |
| #67 Review + promotion | This document, README rewrite, production promotion | this PR |

Also: #59 removed audience-specific wording from the docs, a test persona and an issue.

**Demo:** [production](https://meridian-production.grayhill-d6e042ce.eastus2.azurecontainerapps.io) once promoted (staging until then): sign in as Dana or Raj.

## By the numbers

- **Velocity:** 26 of 26 points (Sprint 1: 38, Sprint 0: 33)
- **Tests:**
  - 196 backend: domain, application, integration, Reqnroll scenarios (real PostgreSQL), architecture
  - 94 Vitest
  - 47 Playwright journeys × 3 browser profiles, every page axe-scanned (140 passed, 1 skipped by design: WebKit Tab navigation)
- **Security:**
  - 0 open Dependabot alerts (9 at the start of the sprint)
  - CodeQL clean
  - every new state-changing endpoint has antiforgery and ownership tests

## Retrospective

**What went well**
- **Tests found real defects before review:**
  - **Planner:** a plan starting at $0 never received contributions, because the depletion check also blocked deposits.
  - **Planner:** my first sanity band was wrong for the model; an independent Python implementation gave 20.7%, and the test now checks agreement within about four standard errors.
  - **Learn:** DI picked a test constructor and passed an *empty* `IEnumerable`, so the API served zero articles; the Reqnroll scenarios caught it.
  - **Transfers:** under load, a same-key retry could lose on the account version before the key's unique index and answer 409 instead of replaying. Money still moved once, but the client would have been told to review again. Fixed with a unit test for that ordering.
- **Mutation-style check:** removing the row-version token makes "concurrent transfers never overdraw" fail, as it should.
- **Money stays on the server**, even for live prices: the browser formats figures, never computes them.
- **Accessibility kept pace with features:**
  - sortable headers with `aria-sort`
  - live regions that announce state, not ticks
  - focus management through multi-step flows
  - a data table behind every chart
  - and, after a manual check at 320px, a phone nav that fits five items

**What didn't go well**
- **Merge mechanics cost time.** `gh pr update-branch --rebase` had GitHub rewrite a commit *unsigned*. The ruleset (signed commits) then blocked that PR and two stacked on it, with every check green, so three PRs were re-opened as signed commits (#70 → #72, #71 → #74, #73 → #75). I first blamed the wrong rule; checking each commit's verification would have found it immediately.
- **Test infrastructure:** concurrency scenarios exhausted the test PostgreSQL's 100 connections (one pool per isolated database), which needed `max_connections=500`.
- **A race showed up only in a heavier local run**, not in CI. Concurrency tests need more pressure than five requests.
- **The phone nav clipped** "Planner" and "Learn" at 320px. axe can't see it, and only a manual screenshot caught it.
- I started transfer work on the wrong branch once; it was moved before any commit.

**Action items**
| Action | Owner | Status |
|---|---|---|
| Update PR branches only by merging `origin/main` locally (signed); never rebase-update on GitHub | dev | ✅ practice adopted |
| When a PR is blocked with green checks, check commit signatures first | dev | ✅ practice adopted |
| Raise parallelism in the concurrency scenarios (5 → 20) and run them repeatedly in CI | dev | ⏳ next sprint |
| e2e check that every nav item is fully visible at 320px | dev | ⏳ next sprint |
| Promote to production at each sprint review | repo owner | ⏳ promotion requested with this review |
| Upgrade the Azure free account before the credit ends (~2026-11-17) | repo owner | ⏳ open |

## Next sprint (proposal)

- The two action items above
- Price alerts (the Worker's first scheduled job)
- Statements: monthly PDF generation for each account
- Accessibility audit with a screen reader (VoiceOver and NVDA) across all flows, recorded in the repo
