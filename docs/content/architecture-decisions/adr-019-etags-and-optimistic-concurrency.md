---
title: "ADR 019: ETags and optimistic concurrency"
weight: 190
---

# ADR 019: ETags and optimistic concurrency

## Status
Accepted. Idempotency keys were added to this ADR and then removed until a client needs them; see [Deferred work]({{< relref "../backlog" >}}).

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
