---
title: "ADR 006: xUnit, NSubstitute, and functional tests against real Postgres"
weight: 60
---

# ADR 006: xUnit, NSubstitute, and functional tests against real Postgres

## Status
Accepted. Revised: functional tests originally ran on EF Core's InMemory provider; they now run against a real Postgres (Testcontainers).

## Context
The template needs a default testing setup that new projects inherit without a setup decision of their own, and that gives real confidence in the behavior a feature copies from the `User` reference slice.

The first version ran functional tests on EF Core's InMemory provider, so they needed no Docker and ran the same on Windows, Linux and macOS. That turned out to hide exactly the behavior that matters most:

- InMemory doesn't enforce unique indexes, so the email/external-id uniqueness was untested.
- It doesn't run database defaults (`CreatedAtUtc` uses `now()`), raw SQL (`ListUsersQueryService`'s `FromSqlRaw` had to be stubbed out), or transactions the way Postgres does.
- Replacing the app's `AddDbContext` meant re-registering it by hand, which silently dropped the domain-event `SaveChangesInterceptor`.

Microsoft also [recommends against](https://learn.microsoft.com/en-us/ef/core/testing/choosing-a-testing-strategy#in-memory-as-a-database-fake) InMemory as a database fake.

## Decision
Two test projects, both xUnit:

- **`AppTemplate.UnitTests`** - tests Core (aggregates, Vogen value objects) and UseCases (command/query handlers) in isolation. Handlers are tested against `IRepository<T>` substituted with [NSubstitute](https://nsubstitute.github.io/), not a real database.
- **`AppTemplate.FunctionalTests`** - drives the real HTTP pipeline (FastEndpoints -> Mediator -> EF Core -> Postgres) through `WebApplicationFactory<Program>`. `PostgresTestDatabase` starts one Postgres container per test run with [Testcontainers](https://dotnet.testcontainers.org/) (same image major version as the AppHost), and each `AppTemplateWebApplicationFactory` gets its own database in it, created by running the real migrations. The app's DbContext registration is used as is.

Testcontainers was chosen over `Aspire.Hosting.Testing`, which would start the whole AppHost (Keycloak, Redis, Mailpit, the frontend) for every run - far more than these tests need.

## Consequences
- Functional tests need Docker (or a Docker-compatible engine such as Podman, via `DOCKER_HOST`). Without it they fail fast with a message saying so.
- Every test class that uses the factory is marked `[Trait(TestCategories.Name, TestCategories.RequiresDocker)]`. Tests that don't need the database (e.g. the Keycloak client tests) are not, so `dotnet test --filter "Category!=RequiresDocker"` still runs them anywhere.
- CI runs the full suite only on Linux; hosted Windows and macOS runners can't run Linux containers, so they use that filter (see `backend-build.yml`).
- Tests now catch Postgres-only behavior: unique violations, database defaults, owned types, raw SQL and concurrency (`Data/PostgresPersistenceTests`).
- Hangfire still uses in-memory storage per test host - that swap is independent of EF Core.
- NSubstitute was chosen over Moq for its simpler syntax and permissive license; either works fine here since `IRepository<T>` and friends are plain interfaces.
