---
title: "Deferred work"
weight: 90
---

# Deferred work

Features that were built, or designed, and then deliberately taken out until they're needed. Each entry says what it did, why it's out, and how to bring it back. Check here before building one of these from scratch.

## Idempotency keys for writes

**What it did.** `PUT /v1/users/{id}` accepted an `Idempotency-Key` header. A Mediator pipeline behavior (`IdempotencyBehavior`, for commands marked `IIdempotentCommand`) authorized the caller, claimed a row in an `idempotency_records` table before the handler ran, and committed it together with the handler's changes and the stored `Result` in one transaction (`IUnitOfWork`). A repeat with the same key replayed the stored result, a different body with the same key was 422, a concurrent duplicate waited on the unique index and then replayed, and a recurring job deleted records after 24 hours. Cache invalidation inside that transaction was deferred until commit.

**Why it's out.** No client retries writes yet, and `PUT` is already idempotent by nature, with lost updates covered by ETags and `If-Match` ([ADR 019]({{< relref "architecture-decisions/adr-019-etags-and-optimistic-concurrency" >}})). The feature added a table, a transaction abstraction and a pipeline behavior for a guarantee nothing relied on.

**When to bring it back.** When a non-idempotent write (a `POST` that creates something, a payment, an import) can be retried by a client or a proxy, and a duplicate would do harm.

**How to bring it back.** It was merged in PR #79 and removed by the commit `revert: idempotency keys (#79)` on `develop`. Reverting that commit restores the code, the migration, the tests and the ADR text:

```bash
git log --oneline --grep "revert: idempotency keys" develop
git revert <that commit>
```

Then regenerate the migration if the model has moved on since, and re-check the cache-invalidation timing: `ICacheInvalidator` is called after each save today, so the deferred invalidation (`IUnitOfWork.AfterCommit`) has to come back with it.
