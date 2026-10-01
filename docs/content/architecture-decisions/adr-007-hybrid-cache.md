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
  (30 min overall, 1 min local). Redis keys are prefixed with `InstanceName = "apptemplate:"`.
- **Redis is required** unless `Cache:AllowLocalOnly` is `true`. This is enabled in
  `appsettings.Development.json` and in the functional tests, where HybridCache then runs L1-only.
  Everywhere else, a missing `cache` connection string fails at startup instead of silently
  degrading to per-instance caching.
- **Query handlers do cache-aside, and command handlers invalidate** after the repository call
  succeeds. Endpoints and repositories never touch the cache.
- The cached value is the handler's DTO (`UserDto?`), never an `Ardalis.Result` or an EF entity.
  `null` (not found) is cached too, so commands that can make a missing key valid (Create) must
  invalidate it.
- Lists and paged queries are not cached.

## Consequences
- **Invalidation is not instant across instances.** `RemoveAsync` clears Redis and the calling
  instance's L1 only. Other instances keep serving their L1 copy until `LocalCacheExpiration`
  (1 min) elapses. There is no backplane. This is an architectural limitation: keep L1 short, and
  don't cache data that must be strongly consistent (permissions, balances) without addressing it.
- Replacing Redis (Valkey, SQL Server, none) only touches `Infrastructure/Caching/` and `AppHost.cs`.
- Handlers remain unit-testable with NSubstitute because `ICache` is a plain interface.
- Cached types must round-trip through System.Text.Json, since HybridCache serializes even L1
  entries unless a type is `sealed` and `[ImmutableObject(true)]`.
- Any new write path that creates an entity must invalidate its keys. Otherwise a cached miss causes
  a stale 404, which is especially likely with sequential integer IDs.
- The functional tests exercise L1 only, not Redis itself.
- Per-call expirations and tags (for list invalidation via `RemoveByTagAsync`) are deliberately
  omitted. Add them to `ICache` when a concrete feature needs them. A feature-specific cache
  interface is the next step if a feature's cache usage becomes hard to read.
