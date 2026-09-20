---
title: "Best Practices"
weight: 25
---

# Best Practices

Explicit guidelines for projects built from this template, so every project that starts here ends up consistent. These are conventions to *follow*, not a restatement of the [design decisions]({{< relref "design-decisions" >}}) (why the stack looks the way it does) or the [ADRs]({{< relref "architecture-decisions" >}}) (specific past decisions) - though there's naturally some overlap.

## C# & .NET

- Follow [Microsoft's C# coding conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions) (PascalCase types/public members, `I`-prefixed interfaces, one statement per line). The compiler won't enforce this - `.editorconfig` and `dotnet csharpier` are what actually keep it consistent; don't fight them with inline overrides.
- **Never add `System.ComponentModel.DataAnnotations` attributes (`[Required]`, etc.) to FastEndpoints request DTOs.** FastEndpoints validates through FluentValidation `Validator<T>` classes, not Data Annotations - the attributes silently do nothing. This template shipped with exactly that mistake in every `User` endpoint until it was caught; if you ever see a Data Annotations attribute on a request class next to a `Validator<T>`, delete the attribute.
- Repository + Specification (`IRepository<T>`, `Ardalis.Specification`) is for the **write** path, where domain rules matter. Read-only queries don't need it - project straight to a DTO in a query service (see `ListUsersQueryService`'s raw SQL) instead of loading and mapping full entities. Specifications used purely for lookups (not followed by a mutation) should call `.AsNoTracking()` - see `UserByIdSpecification`/`UserByEmailSpecification`.
- Wrap a primitive in a [Vogen](https://github.com/SteveDunn/Vogen) value object when it has a real invariant to enforce or a real type-confusion risk (IDs, emails). Don't wrap everything - plain DTOs and read-only projections (`UserDto`, `UserRecord`) are supposed to stay plain.
- EF Core: avoid queries in loops (N+1) - eager-load or batch instead. Reach for compiled queries only once a specific query is a measured hot path, not by default.
- Inject `ILogger<T>`, never call the static `Serilog.Log` directly. Use message templates (`"{UserId} created", id`), not string interpolation - it's what makes structured log fields searchable. Configuration belongs in `appsettings.json`'s `Serilog` section (already the case in `LoggerConfigurations.cs` via `ReadFrom.Configuration`), not hardcoded in C#.
- Tests follow Arrange-Act-Assert, one behavior per test. See `AppTemplate.UnitTests` for the current shape.
- **Known gap**: there's no authentication/authorization wired up yet - every backend endpoint is `AllowAnonymous()`, even though the frontend has login/register scaffolding. Don't treat that scaffolding as "auth is handled" - it isn't, yet.

## Angular

- Feature-folder structure (`core/`, `shared/`, `features/<feature>/`) is already the convention here - keep new code inside it rather than growing `components/`/`services/`-by-type folders. See the [official style guide](https://angular.dev/style-guide).
- Hyphenate file names, one concept per file, co-locate a component's `.ts`/`.html`/`.css`/`.spec.ts`.
- Prefer `inject()` at field-initializer time over constructor-parameter injection for new code - it reads better and infers types more reliably.
- For state: local component state first; a signal exposed (read-only, via `computed()`) from a service when a few components need to share it; a route-scoped feature store only once that's not enough; NgRx SignalStore only once a feature genuinely needs a full store. Don't start with a global store.
- Prefer native `[class]`/`[style]` bindings over `NgClass`/`NgStyle`.
- Tests run on Vitest (already the default here) - keep specs next to the file they test, as `app.spec.ts` already does.

## .NET Aspire

- `AppTemplate.ServiceDefaults` (wired into `Web` via `AddServiceDefaults()`) is where cross-cutting concerns (OpenTelemetry, health checks, service discovery, retries) belong - once for the whole app, not duplicated per-service.
- `/health` and `/alive` (from `MapDefaultEndpoints()`) are meant for Development/orchestration, not public exposure - if you ever run this outside Aspire in a way that exposes them, gate them behind network policy or authz.
- The AppHost is a local-orchestration and manifest-generation tool (see [ADR 002]({{< relref "architecture-decisions/adr-002-aspire-orchestration" >}})) - it is not itself a production runtime.

## PostgreSQL & migrations

- `DatabaseConfigurations.ApplyMigrationsOnStartup` (and the automatic migration in `Development`) is a **local/demo convenience, not a production deployment strategy**. Auto-migrating on app startup means a bad migration blocks the app from starting at all, and with more than one replica, every instance races to apply migrations concurrently. For a real deployment, generate a [migration bundle](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying#apply-migrations-at-runtime) (`dotnet ef migrations bundle`) and run it as its own CI/CD step before the new app version starts.
- Index for the query patterns you actually have once the schema grows past the seed `User` table - missing indexes are the most common cause of slow Postgres queries, more often than anything on the EF Core side.
- Don't hold a DB connection open across an `await` that doesn't need it - it ties up a slot in Npgsql's connection pool for no reason.
