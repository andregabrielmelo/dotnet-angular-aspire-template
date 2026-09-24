---
title: "ADR 012: Background jobs with Hangfire"
weight: 120
---

# ADR 012: Background jobs with Hangfire

## Status
Accepted

## Context
Some work shouldn't happen inside an HTTP request:
- it's slow or depends on an unreliable external system (sending email)
- it must be retried on failure
- or it runs on a schedule (keeping user profiles in step with Keycloak, where users edit them)

We want persistent jobs that survive restarts, automatic retries, scheduling, and visibility into what ran and what failed.

## Decision
- **[Hangfire](https://docs.hangfire.io/)** with **Postgres storage** in the application database's own `hangfire` schema (`Hangfire.PostgreSql`). There is no extra infrastructure to run. `BackgroundJobs:*` options (validated at startup) control the schema, worker count, whether this process runs a server, and the sync schedule.
- **Jobs are thin adapters; the logic lives in use cases.** Job classes (`Infrastructure/Jobs`) receive **primitive arguments only** (ids, never entities or DTOs). Those arguments are what Hangfire serializes and stores, and they must stay valid across deployments. Each job resolves its dependencies from a DI scope, dispatches a Mediator command, and **throws on failure so Hangfire retries** with back-off. Use cases enqueue through `IBackgroundJobScheduler` (UseCases), so they never reference Hangfire.
- **Idempotent by design.** Hangfire guarantees *at-least-once* execution, so every job must tolerate running twice:
  - **Welcome email** (fire-and-forget, `emails` queue, 5 retries): enqueued when `/users/me` provisions a new user, so sign-in never waits on SMTP. `User.WelcomeEmailSentAtUtc` is checked before sending and set after, so a retry or duplicate doesn't resend. A crash in the gap between sending and saving can still resend, which is acceptable for a welcome email. A deleted user ends the job without retrying.
  - **User profile sync** (recurring, hourly UTC, `default` queue, `DisableConcurrentExecution`): pages through Keycloak's users as the `apptemplate-user-admin` service account in batches of 100. It copies changed names and emails, never takes an email another user already has, never deletes anything, and invalidates the user caches (ADR 011) when something changed. Registered with a stable id on every startup, so re-registering is harmless.
- **Queues in priority order:** `emails` before `default`.
- **`CancellationToken` parameters** are passed `CancellationToken.None` at enqueue time. Hangfire replaces them with its shutdown token, so jobs stop cleanly on deploys.
- **Dashboard:** `/jobs` on the Web API, **Development only** and **local requests only**. It can delete and re-run jobs, so it isn't exposed elsewhere. The Aspire dashboard links to it.
- **Server placement:** the Web API also runs the Hangfire server by default. Set `BackgroundJobs:RunServer=false` on API instances and run a separate worker host to scale job processing independently.

## Consequences
- Enqueueing isn't transactional with the database write that precedes it: if enqueueing fails after a user was created, that user gets no welcome email. Use an outbox if that must never happen.
- Job storage shares the application's Postgres (separate schema). Heavy job volume would compete with application queries; switch storage or database if it grows.
- A job method's signature is a contract with jobs already stored: renaming or changing parameters of a job that may still be queued breaks it. Add a new method instead and remove the old one once it has drained.
- Hangfire and Hangfire.PostgreSql are LGPL-3.0 (Hangfire also sells commercial licenses). Using them as unmodified packages is fine for most projects; check with your legal team if unsure.
- Hangfire keeps some state (the log provider) in static globals. Functional tests run several hosts at once, so they register a no-op `ILogProvider` (which `AddBackgroundJobs` honours) and in-memory storage per host, and they run job classes directly instead of starting a server.
