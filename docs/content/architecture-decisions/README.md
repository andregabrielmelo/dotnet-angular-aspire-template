# Architecture Decision Records (ADR)

## What is an ADR?

An Architecture Decision Record documents a significant architecture decision: the context that led to it, the decision itself, and its consequences. It's a historical record, not a living design doc.

## Why bother

- **Knowledge management** - the reasoning behind a decision outlives the person who made it.
- **Onboarding** - new contributors can see *why* something is the way it is, not just that it is.
- **Avoiding re-litigation** - "why don't we just use X" gets answered by a link instead of a meeting.

## Writing one

1. One ADR per decision. Don't conflate several decisions into one record.
2. Explain the context - what problem or constraint prompted the decision, and what alternatives were considered.
3. State the decision plainly, then its consequences (including the downsides - every decision has trade-offs).
4. ADRs are immutable once accepted. If a decision changes, write a new ADR that supersedes the old one and link back to it - don't edit history.
5. File naming: `adr-NNN-short-kebab-case-title.md`, numbered sequentially.

## Template

```markdown
---
title: "ADR NNN: Short title"
weight: NNN
---

# ADR NNN: Short title

## Status
Proposed | Accepted | Superseded by ADR-XXX

## Context
What problem or constraint led to this decision? What alternatives were considered?

## Decision
What was decided.

## Consequences
What becomes easier or harder as a result. Include the trade-offs, not just the upside.
```

## Further reading

- [Documenting Architecture Decisions (Michael Nygard)](https://cognitect.com/blog/2011/11/15/documenting-architecture-decisions)
- [Markdown Architectural Decision Records (MADR)](https://adr.github.io/madr/)
- [adr.github.io](https://adr.github.io/)
