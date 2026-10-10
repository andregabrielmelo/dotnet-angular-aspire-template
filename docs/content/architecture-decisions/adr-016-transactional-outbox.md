---
title: "ADR 016: Transactional outbox and inbox, relayed by Hangfire"
weight: 160
---

# ADR 016: Transactional outbox and inbox, relayed by Hangfire

## Status
Accepted. Amends [ADR 012]({{< relref "adr-012-background-jobs-hangfire" >}}): work that must reliably follow a database change is no longer enqueued directly from a use case.

## Context
The welcome email was enqueued in Hangfire right after the new user was saved. Those were two separate writes: if the process stopped between them, the user existed and the email was never enqueued. An hourly job (`enqueue-missed-welcome-emails`) patched the gap by looking for users without one.

Every new "do X after Y is saved" would need the same patch. A transactional outbox removes the gap for all of them: the request to do X is saved in the same transaction as Y.

Separately, the in-process domain events dispatched after `SaveChanges` turned a failing handler into a 500 for a write that had already committed.

## Decision
- **Two kinds of event.** *Domain events* (`IDomainEvent`) stay in-process and best-effort: dispatched after `SaveChanges`, a failure is logged and never fails the request. *Integration events* (`IIntegrationEvent`) are for work that must happen: entities raise them (`RaiseIntegrationEvent`), and they go through the outbox.
- **Outbox in the application database.** `OutboxInterceptor` (a `SavingChanges` interceptor) writes each raised integration event as an `outbox_messages` row in the **same** `SaveChanges` as the entity. Both commit, or neither does.
- **Explicit message contracts.** A row stores a stable key (`user.provisioned.v1`, from `[IntegrationEvent(name, version)]`) and a System.Text.Json payload, never a CLR type name. Only types registered in `AddOutbox(o => o.AddEvent<T>())` are deserialized. An unknown key is dead-lettered unread. A breaking payload change is a new version (a new record); keep the old handler until the old version has drained.
- **Hangfire as the relay, no broker.** `ProcessOutboxJob` is enqueued right after a commit that wrote messages (the fast path), and `OutboxSweepJob` runs every minute (the recovery path). Both call `OutboxProcessor`.
- **Claiming:** batches are claimed with `SELECT ... FOR UPDATE SKIP LOCKED` and a lease (`locked_until_utc`, 5 minutes). Workers never block on each other, and a dead worker's messages become claimable when its lease expires.
- **Retries:** a failed delivery increments `attempts`, records `last_error`, and is rescheduled with exponential back-off (10 s, doubling, capped at 1 hour). After `Outbox:MaxAttempts` (10) it's **dead-lettered**: it stays in the table with the error, is logged at Error, and counted in `outbox.messages.dead_lettered`. `POST /v1/admin/outbox/dead-letters/requeue` (`jobs:manage`) retries them once the cause is fixed.
- **Inbox, per handler.** The relay runs each handler of an event in its own scope and transaction, which also inserts an `inbox_messages` row (message id, handler). A redelivered message skips handlers that already completed. The inbox lives apart from the outbox (`Infrastructure/Inbox/`, `InboxStore`, registered by `AddInbox`): it works on the caller's DbContext, so the row commits in the handler's own transaction, and any future consumer (a broker subscription, a webhook) can use it too.
- **The welcome email is the first consumer.** `User.Create` raises `UserProvisioned`, and `SendWelcomeEmailWhenUserProvisioned` sends it. `EnqueueMissedWelcomeEmailsJob`, `WelcomeEmailJob` and `IBackgroundJobScheduler` are removed.

## Consequences
- **Delivery is at-least-once.** A message is marked processed only after all its handlers succeeded, so a crash means a redelivery. The inbox makes a handler's **database** changes effectively-once, because they commit together with its inbox row. An **external** side effect (an email, an HTTP call) can still repeat if the process dies between the side effect and that commit. Such handlers need their own guard (`User.WelcomeEmailSentAtUtc`) or an idempotency key at the provider.
- **Exclusivity holds only while the lease is valid.** A worker that runs past its lease can overlap with the next claimant. Handlers must tolerate that, which the rule above already requires.
- **Latency:** usually moments, via the fast path. Up to a minute when the fast path is lost (crash, or a write inside an explicit transaction, whose rows aren't visible until commit), plus back-off on failures.
- **Storage grows:** processed rows stay for inspection. A retention job, like the one auditing will have, can delete old processed rows and inbox entries when volume warrants it.
- **Not a message broker.** The outbox delivers within this application. Publishing to other services would add a handler that sends to a broker, keeping the same guarantees up to that send.
- Tests cover every guarantee against real Postgres (`OutboxTests`): atomicity, sweep recovery, concurrent claims, lease expiry, back-off and dead-lettering, unknown types, inbox skips and rollbacks, and welcome-email redelivery.
