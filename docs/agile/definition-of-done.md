# Definition of Done

An item is **Done** only when all of the following are true. CI enforces the starred (★) items; the rest are checked in review.

**Built right**
- [ ] ★ Builds with **zero warnings** (.NET analyzers as errors; strict TypeScript; angular-eslint incl. template accessibility rules)
- [ ] ★ Formatting clean (`dotnet format`, Prettier)
- [ ] ★ Architecture rules pass (`Meridian.ArchitectureTests`)
- [ ] Tests written **first** where practical (red → green → refactor), and seen failing for the right reason

**Proven to work**
- [ ] ★ Unit, integration and acceptance tests pass, including against real PostgreSQL (Testcontainers)
- [ ] Acceptance criteria automated (Reqnroll scenarios / Playwright journeys)
- [ ] New behaviour covered; coverage doesn't drop without a stated reason
- [ ] Accessibility: keyboard and screen-reader behaviour considered for UI changes; ★ no serious/critical axe violations on any page (Playwright, Chromium + WebKit + mobile)

**Safe to ship**
- [ ] ★ No secrets (gitleaks), CodeQL alerts triaged, dependencies reviewed
- [ ] ★ Container image builds; ★ deployed to **staging** by CD and smoke-tested on its own revision before taking traffic
- [ ] Database changes are migrations, backward compatible with the previous revision (expand/contract)
- [ ] Logs and telemetry: no credentials or personal data; errors carry a trace id

**Communicated**
- [ ] PR follows the template (what / why / how / testing / risk & rollback), titled per Conventional Commits, linked to its issue
- [ ] Docs updated (README, runbooks); an **ADR** for any significant decision
- [ ] Issue closed and the board updated
