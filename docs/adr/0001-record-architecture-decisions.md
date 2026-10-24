# ADR-0001: Record architecture decisions

- **Status:** Accepted
- **Date:** 2026-10-24

## Context

Meridian is built to be reviewed by people who were not present when decisions were made: reviewers, future contributors, and future me. Decisions and the alternatives behind them get lost in chat and commit messages.

## Decision

Record every architecturally significant decision as a short Markdown ADR in `docs/adr/`, using this format: Context → Options considered → Decision → Consequences. ADRs are numbered, immutable once accepted, and superseded (never edited) when a decision changes.

## Consequences

- Reviewers can see *why*, not just *what*.
- Changing direction is explicit: a new ADR supersedes an old one.
- Small overhead per decision (~15 minutes).
