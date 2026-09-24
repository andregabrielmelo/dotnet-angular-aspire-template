---
title: "ADR 011: HybridCache for data, output caching for shared responses"
weight: 110
---

# ADR 011: HybridCache for data, output caching for shared responses

## Status
Accepted

## Context
Some reads happen far more often than the data changes. The SPA calls `GET /users/me` on every page load, admins page through user lists, and the sign-in page asks for the enabled providers. ASP.NET Core offers several caching tools ([overview](https://learn.microsoft.com/aspnet/core/performance/caching/overview?view=aspnetcore-10.0)). Almost every API endpoint is authenticated and permission-gated, so caching must never serve data to someone who isn't allowed to see it, and must never serve stale data after a write.

## Decision
**HybridCache for application data** (UseCases):
- `GetUserHandler` (by id) and `GetOrCreateCurrentUserHandler` (by the token's `sub`) read through `HybridCache.GetOrCreateAsync`. Concurrent misses share one database call (stampede protection), and "not found" is cached too.
- Entries are `CachedUser`, a record of primitives. It serializes cleanly to the distributed cache and never exposes tracked entities or value objects.
- **L1** is in-process memory (1 minute). **L2** is Redis (10 minutes) when Aspire provides the `cache` connection. The short L1 bounds how long another instance can serve a copy after an invalidation.
- Caching happens *inside* the use case, which runs only after the endpoint's authorization. The cached value is the same for everyone.

**Output caching for whole HTTP responses** that are identical for every allowed caller:
- `GET /users` (the list) uses the `users-list` policy.
  - The built-in default policy never caches requests with an `Authorization` header, which is every API call. `AuthorizedSharedResponsePolicy` opts in explicitly.
  - It only runs because `UseOutputCache` comes **after** `UseAuthorization`, so a cached response is served only to callers who passed `users:read`.
  - It caches only GET/HEAD 200 responses without cookies, varies by query string, and never varies by user.
- `GET /backend-for-frontend/providers` uses the stock default policy: anonymous and identical for everyone.
- With Redis, the API's output cache store is Redis. The backend for frontend's store stays in-memory; it only caches a small public response.

**Invalidation by tag.**
- Every user entry and every cached user list carries the tag `users` (`CacheTags.Users`).
- Creating, updating or deleting a user calls `ICacheInvalidator.InvalidateAsync("users")`. It is defined in UseCases and implemented in Web, where it calls `HybridCache.RemoveByTagAsync` and `IOutputCacheStore.EvictByTagAsync`.
- User writes are rare compared to reads, so invalidating the whole tag is simpler and always correct compared to per-key bookkeeping.

## Consequences
- Never apply `AuthorizedSharedResponsePolicy` to an endpoint whose response depends on the caller (such as `/users/me`). Use HybridCache inside the use case instead, keyed by what the result depends on.
- A new cached read needs a key, the right tag, and an invalidation call in every write that affects it. Forgetting the last step means stale data, so cover it with a functional test like `CachingTests`.
- Without Redis (tests, or running the API alone) both layers are in-memory, which is correct for a single instance. With several instances, run Redis. Even then, another instance's L1 may serve an invalidated entry for up to one minute.
- The list query uses raw SQL that EF InMemory can't run (ADR 006), so the output-cache test replaces it with a counting stub. That stub also shows whether a response came from the cache.
