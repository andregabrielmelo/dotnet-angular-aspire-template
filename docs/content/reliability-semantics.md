---
title: "Transactions, Events and Side Effects"
weight: 27
---

# Transactions, Events and Side Effects

What happens, and in what order, when a use case changes data and something else should follow. Read this before adding a domain event, an integration event, or a handler with a side effect. The decision itself is [ADR 016]({{< relref "architecture-decisions/adr-016-transactional-outbox" >}}).

## When changes commit

- A use case changes entities through `IRepository<T>`, and each `AddAsync`/`UpdateAsync`/`DeleteAsync` calls `SaveChanges`. With no explicit transaction, **each `SaveChanges` is its own transaction**: it commits when it returns.
- Inside `Database.BeginTransactionAsync()`, nothing is visible to anyone else until `CommitAsync`. A rollback undoes every `SaveChanges` since the transaction began.
- A [request timeout]({{< relref "best-practices#rate-limiting-and-timeouts" >}}) cancels work in progress but never undoes a commit.

## Domain events: in-process, best-effort

- An entity registers one with `RegisterDomainEvent`. `EventDispatchInterceptor` publishes it through Mediator **right after `SaveChanges` succeeds**, in the same request.
- **A failing handler doesn't fail the request.** The change is already saved, so the error is logged and the request carries on. A 500 for a committed write would invite the client to repeat it. (`DomainEventDispatchTests`)
- **Inside an explicit transaction, handlers run before the commit,** so they also run when it later rolls back. (`DomainEventDispatchTests`)
- **Nothing retries them,** and a crash right after the commit loses them.
- So: use domain events only for in-process reactions that are fine to miss, never for email, HTTP calls or anything else that must happen.

## Integration events: the transactional outbox

- An entity raises one with `RaiseIntegrationEvent` (`User.Create` raises `UserProvisioned`). The record is the message contract: primitives only, marked `[IntegrationEvent("name", version)]`, and registered in `AddOutbox(o => o.AddEvent<T>())` (in `InfrastructureServiceExtensions`).
- **Atomic with the change.** The event is written to `outbox_messages` in the same `SaveChanges` as the entity, so a rollback leaves neither.
- **Delivered at least once** by the relay: right after commit (`ProcessOutboxJob`), or within a minute by `OutboxSweepJob` if that never ran.
- **Each handler** (`INotificationHandler<TEvent>`, for example `SendWelcomeEmailWhenUserProvisioned`) runs in its own scope and transaction. Its database changes commit together with an inbox row, so a redelivery skips it once it has completed.
- **A failing handler** gets the message retried with back-off (10 s, then doubling up to 1 hour). After 10 attempts the message is dead-lettered. Throw to fail; return normally when there's nothing to do (for example, the user was deleted meanwhile).

## Duplicates and idempotency

- **Database changes in a handler:** effectively-once, thanks to the inbox.
- **External side effects** (email, HTTP, file storage): can repeat if the process dies after the side effect and before the handler's transaction commits. Guard them, either with state the handler checks first (the welcome email checks and sets `User.WelcomeEmailSentAtUtc`), or with an idempotency key the external system honours.
- **Leases:** a worker that outlives its 5-minute lease can overlap with another worker. That's one more reason handlers must be idempotent.

## Operating it

- Watch `outbox.messages.dead_lettered` and alert on any; `outbox.messages.failed` shows retries in progress.
- Inspect dead letters in `outbox_messages` (`dead_lettered_at_utc IS NOT NULL`, with `last_error`). Once the cause is fixed, `POST /v1/admin/outbox/dead-letters/requeue` (`jobs:manage`) makes them due again.
- **Changing a contract:** add `UserProvisionedV2` with `[IntegrationEvent("user.provisioned", 2)]` and a handler, start raising it, and delete the v1 record and handler only once no v1 message is pending. A key nobody registered is dead-lettered without being read.
- **Tuning:** `Outbox:BatchSize`, `LeaseDuration`, `MaxAttempts`, `FirstRetryDelay` and `MaxRetryDelay`.

## Removing it

The outbox is one registration. To drop it from a project that doesn't need it:
1. Delete the `services.AddOutbox(...)` call in `InfrastructureServiceExtensions` and the `Infrastructure/Outbox/` folder, plus `UseCases/Outbox/` and `Web/Features/OutboxFeatures/`.
2. Remove the `OutboxMessages`/`InboxMessages` sets from `ApplicationDatabaseContext`, and add a migration that drops both tables.
3. Remove `IIntegrationEvent`, `RaiseIntegrationEvent` and the integration event records, and send the welcome email another way, for example by enqueuing a Hangfire job from the handler and accepting the lost-enqueue window ADR 016 describes.
4. Remove the outbox metrics from `ApplicationMetrics` and `OutboxTests`.
