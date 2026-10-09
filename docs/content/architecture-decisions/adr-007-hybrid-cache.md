---
title: "ADR 007: HybridCache over Redis, behind a minimal ICache"
weight: 70
---

# ADR 007: HybridCache over Redis, behind a minimal ICache

## Status
Proposed

## Context
The template needs a default caching approach that keeps the layering from ADR 001: UseCases must
not depend on caching technology, and Web is HTTP only. Microsoft's `HybridCache`
(Microsoft.Extensions.Caching.Hybrid) provides an in-process L1 cache, any registered
`IDistributedCache` (here Redis) as L2, stampede protection, and global default entry options.

Because this is a template meant to teach where things belong, the caching example must stay small.
Alternatives considered: injecting `HybridCache` directly into handlers (leaks Microsoft types into
UseCases and is awkward to substitute in unit tests), per-feature interfaces such as `IUserCache`
(too much ceremony for one reference slice), caching inside repositories (hides the cache-aside
flow and mixes two infrastructure concerns), and exposing per-call policies/tags up front (no
concrete need yet).

## Decision
- **AppHost** provisions a Redis resource named `cache`. It knows nothing about HybridCache.
- **UseCases** defines `ICache` with exactly two members: `GetOrCreateAsync(key, factory)` and
  `RemoveAsync(key)`. There are no per-call expiration policies and no tags. Features own their keys
  in a static class (`UserCacheKeys.ById(id)` → `users:id:42`), following
  `{feature}:{resource}:{identifier}`.
- **Infrastructure** implements `ICache` with `HybridCacheService` and sets centralized defaults
  from `CacheOptions`, bound from the `Cache` configuration section (`Expiration`, default 30 min;
  `LocalExpiration`, default 1 min; startup fails if either is non-positive or local is longer).
  Redis keys are prefixed with `InstanceName = "apptemplate:"`, and a `cache` health check is
  registered for `/health` (readiness, not liveness) when Redis is configured.
- **Redis is required** unless `Cache:AllowLocalOnly` is `true`. This is enabled in
  `appsettings.Development.json` and in the functional tests, where HybridCache then runs L1-only.
  Everywhere else, a missing `cache` connection string fails at startup instead of silently
  degrading to per-instance caching.
- **Query handlers do cache-aside, and command handlers invalidate** after the repository call
  succeeds. Endpoints and repositories never touch the cache.
- The cached value is the handler's DTO (`UserDto?`), never an `Ardalis.Result` or an EF entity.
- **Misses are not cached.** A `null` factory result is returned but not stored
  (`HybridCacheService` throws a private exception out of the factory, which HybridCache never
  caches, and catches it outside). Concurrent lookups for the same missing key still share one
  query. This keeps arbitrary ids from filling the cache with nulls and means a create never has
  to invalidate anything.
- **Invalidation is best-effort.** `RemoveAsync` logs a distributed cache failure instead of
  throwing, so a Redis outage can't turn a successful write into a 500. Reads already degrade on
  their own: HybridCache logs L2 read/write failures and falls back to the factory.
- Lists and paged queries are not cached.

## Consequences
- **Invalidation is not instant across instances.** `RemoveAsync` clears Redis and the calling
  instance's L1 only. Other instances keep serving their L1 copy until `LocalCacheExpiration`
  (1 min) elapses. There is no backplane. This is an architectural limitation: keep L1 short, and
  don't cache data that must be strongly consistent (permissions, balances) without addressing it.
- Replacing Redis (Valkey, SQL Server, none) only touches `Infrastructure/Caching/` and `AppHost.cs`.
- Handlers remain unit-testable because `ICache` is a plain interface; unit tests use a small
  in-memory `FakeCache` that follows the same "nulls aren't cached" contract.
- Cached types must round-trip through System.Text.Json, since HybridCache serializes even L1
  entries unless a type is `sealed` and `[ImmutableObject(true)]`.
- Lookups of missing ids always reach the database. If a feature has expensive or heavily
  repeated not-found lookups, cache an explicit "not found" value for it rather than changing
  `ICache`.
- If Redis is unreachable when a write invalidates, the old value can stay in Redis until
  `Expiration` once it recovers (the warning is logged with the key). That is the price of not
  failing the write.
- HybridCache's `UnderlyingDataQueryFailed` EventSource counter also counts cache misses, because
  of how misses are kept out of the cache.
- The functional tests exercise L1 only, not Redis itself. `CacheOutageTests` simulates a failing
  L2 with an `IDistributedCache` that always throws.
- Per-call expirations and tags (for list invalidation via `RemoveByTagAsync`) are deliberately
  omitted. Add them to `ICache` when a concrete feature needs them. A feature-specific cache
  interface is the next step if a feature's cache usage becomes hard to read.

## Future options
These are deliberately not in the template; each is a contained change when a project needs it.

- **Cross-instance L1 invalidation.** Register [FusionCache](https://github.com/ZiggyCreatures/FusionCache)
  with a Redis backplane and expose it as `HybridCache` via `.AsHybridCache()`. `HybridCacheService`
  and `ICache` don't change. A hand-rolled Redis pub/sub channel that calls `RemoveAsync` on every
  instance is the alternative.
- **Caching lists.** Add an optional `tags` parameter to `GetOrCreateAsync` and a
  `RemoveByTagAsync` to `ICache`, tag list entries with e.g. `users`, and have every User command
  invalidate the tag. HybridCache's tag invalidation is logical ("ignore entries created before
  now"), not a physical delete.
- **Output caching.** FastEndpoints supports ASP.NET Core output caching per endpoint
  (`Options(x => x.CacheOutput(...))`, plus `AddOutputCache`/`UseOutputCache` and
  `AddStackExchangeRedisOutputCache` for a shared store). It caches the serialized HTTP response,
  so it suits anonymous, read-heavy endpoints that can tolerate a fixed expiry. It isn't the
  default because it duplicates the application cache, skips authenticated requests by default,
  and eviction needs `IOutputCacheStore.EvictByTagAsync`, an ASP.NET Core type that would pull
  invalidation into Web or need a second abstraction.
