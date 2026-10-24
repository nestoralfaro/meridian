# Contributing

## Workflow

1. Every change starts from an issue (story, bug, or spike) on the project board.
2. Branch from `main`: `feat/<issue>-short-name`, `fix/...`, `chore/...`, `docs/...`.
3. Open a pull request using the template. PR titles follow [Conventional Commits](https://www.conventionalcommits.org/) (`feat(transfers): ...`), enforced in CI; PRs are squash-merged, so the title becomes the commit message.
4. All checks must pass and the PR must be reviewed before merging.

## Definition of Done

- Tests written first (TDD) or acceptance criteria automated (BDD)
- CI green: build, tests, lint, accessibility, security scans
- Docs/ADRs updated when behaviour or architecture changes
- Deployed to staging and verified

## Admin bypass

The repository owner can bypass branch rules **only for emergencies** (e.g., restoring a broken pipeline). Every bypass is explained in the follow-up PR.
