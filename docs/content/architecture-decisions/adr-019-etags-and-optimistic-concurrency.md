---
title: "ADR 019: ETags, optimistic concurrency and idempotency keys"
weight: 190
---

# ADR 019: ETags, optimistic concurrency and idempotency keys

## Status
Accepted.

## Context
Two people editing the same user both read it, both change it, and the second save silently overwrites the first ("lost update"). HTTP has the tools to prevent this: an `ETag` on reads and an `If-Match` precondition on writes. Postgres has a per-row version for free: the `xmin` system column changes with every update.

## Decision
- **Version = `xmin`.** `User.Version` (a `uint`) is mapped with Npgsql's `IsRowVersion()`, which Npgsql EF 10 maps to `xmin`: a concurrency token, generated on add and update, with no column of our own. EF scaffolds an `AddColumn` for it that Postgres would reject, so its migration (`MapUserVersionToXmin`) is model-snapshot only. Every `UPDATE users` now carries `WHERE xmin = @loaded`, so **every** user write is protected, not only those that send `If-Match`.
- **ETag:** `GET /v1/users/{id}` and a successful `PUT` return the version as a **quoted, strong** ETag (`"1234"`). It covers **only the user row**, and `UserRecord` contains nothing else. A representation that included related data would need a composite version. The cached user carries the version, and every user write invalidates the cache, the welcome email's included, so the ETag served is current.
- **`If-Match` parsed per RFC 9110** (`Web/Http/EntityTagHeader`, on `Microsoft.Net.Http.Headers.EntityTagHeaderValue`), not compared as a string:
  - `*` matches if the user exists
  - a list matches if any strong tag equals the current version
  - a **weak** tag never matches (`If-Match` uses strong comparison)
  - an unparsable header is **400**
- **Atomic check.** The handler compares the precondition with the version it just loaded, and the `UPDATE ... WHERE xmin = that version` repeats the check inside the database. A write that lands between the two is caught by the second (EF's concurrency exception, which the repository turns into `ConcurrencyConflictException`). The result is the same as applying the `If-Match` version as the original value, without UseCases needing EF Core.
- **Status codes:**

  | Case | Status |
  |---|---|
  | missing user | 404 |
  | `If-Match` sent and not met, up front or by a racing write | **412** |
  | racing write without `If-Match` | **409** |
  | any other unhandled `ConcurrencyConflictException` | 409 (`ConcurrencyConflictExceptionHandler`) |

  412 travels through `Ardalis.Result` as a `Conflict` carrying `ConcurrencyResults.PreconditionFailed`, which `ResultExtensions` maps to 412.

## Consequences
- Clients that want lost-update protection send `If-Match`; those that don't still never silently overwrite a change made after their read, but they get 409 when they race.
- Background writers (profile sync, welcome email) can now fail on a race too. They throw, and Hangfire or the outbox retries them, which reloads fresh data.
- `xmin` is per row and changes on **any** update of it, including columns the API doesn't show (`welcome_email_sent_at_utc`). Such a change makes an ETag stale even though the visible representation didn't change. That's safe (the client rereads), just occasionally unnecessary.
- `ConcurrencyTests` covers every `If-Match` case, plus two forced races (a test interceptor holds both writers until each has loaded the same version): same `If-Match` gives one 200 and one 412; no `If-Match` gives one 200 and one 409.

## Idempotency keys (extension)

### Context
A client that times out on a write can't tell whether it happened. Retrying blindly can apply it twice; not retrying can lose it. An `Idempotency-Key` header lets the client retry safely: the server runs the request once and answers every repeat with the first result.

### Decision: Option A, atomic execute and replay
- **Table** `idempotency_records`: unique (`subject`, `operation`, `key`), plus `fingerprint`, `result_payload` (JSON), `created_at_utc` and `expires_at_utc` (24 hours, `Idempotency:RecordLifetime`). **No status column:** a row becomes visible only once it has committed with the command's changes, so a row always means "done".
- **Opt-in per command:** `IIdempotentCommand` (`IdempotencyKey`, and an `Operation` like `users.update.v1`, never the route). The reference is `PUT /v1/users/{id}`.
- **Fingerprint:** SHA-256 of the operation plus the command's canonical JSON (route values and `If-Match` included, the key excluded). It's the semantic input, not the raw body.
- **`IdempotencyBehavior`** (a Mediator pipeline behavior, so UseCases owns it and nothing in it is HTTP) runs everything in one transaction (`IUnitOfWork`):
  1. **Authorize first**, with `ICommandAuthorizer<T>`, so a repeat from a caller who has since lost access is a 403, never a replay.
  2. **Claim:** insert the record before the handler. A concurrent duplicate's insert **blocks on the unique index** until this transaction ends.
  3. **On a unique violation** (a committed record exists): the same fingerprint replays its result; a different one is **422**.
  4. **Otherwise** run the handler. On success, store the result and commit together with the business changes. On failure, roll back, leaving no record, so the retry runs.

  So the handler runs once per committed key. A blocked duplicate replays if the first committed, or runs if it rolled back: there's no "in progress" 409.
- **The stored result is the use case's `Result`**, not HTTP. A replay goes through the same `ResultExtensions` mapping (and the same ETag), and UseCases stays free of ASP.NET Core.
- **Scope:** keys are per subject, so the same key from another user is unrelated. Anonymous requests and requests without the header run normally.
- **Header:** `Idempotency-Key`, 1 to 64 of `[A-Za-z0-9_-]` (a UUID works); anything else is 400.
- **Cache invalidation waits for the commit** (`IUnitOfWork.AfterCommit`): invalidating inside the transaction would let a concurrent read re-cache the old row.
- **External side effects** still go through the outbox, so they commit atomically with the record or not at all.
- `IdempotencyCleanupJob` (hourly) deletes expired records.

### Consequences
- A command that runs inside the behavior holds a transaction for its whole duration, including the handler. Keep idempotent handlers short; never call slow external services from them (use the outbox).
- Keys expire after 24 hours; after that the same key runs again.
- `IdempotencyTests` (Postgres) covers:
  - an identical replay with the handler run once
  - 422 on a fingerprint mismatch
  - concurrent duplicates: the second waits on the first's open transaction, then replays, and there's one update
  - no record after a handler failure, and the retry runs
  - neither change nor record after a crash between the handler and the commit
  - 403 on replay after the permission is revoked
  - independent subjects
  - malformed keys
  - cleanup

### Removing it
Delete `AddIdempotency` (and `Infrastructure/Idempotency/`, with a migration dropping `idempotency_records`), `UseCases/Idempotency/`, the behavior's entry in `MediatorConfigurations`, `IIdempotentCommand` on `UpdateUserCommand` with its `UpdateUserAuthorizer`, and the header on `UpdateUserRequest`. Keep `IUnitOfWork` if anything else uses it.

