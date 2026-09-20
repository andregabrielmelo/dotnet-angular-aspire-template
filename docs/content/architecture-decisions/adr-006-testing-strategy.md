---
title: "ADR 006: xUnit, NSubstitute, and in-memory functional tests"
weight: 60
---

# ADR 006: xUnit, NSubstitute, and in-memory functional tests

## Status
Accepted

## Context
The template needs a default testing setup that new projects inherit without a setup decision of their own, and it needs to run identically on all three OSes in `backend-build.yml` without requiring Docker or a real Postgres instance in CI.

## Decision
Two test projects, both xUnit:

- **`AppTemplate.UnitTests`** - tests Core (aggregates, Vogen value objects) and UseCases (command/query handlers) in isolation. Handlers are tested against `IRepository<T>` substituted with [NSubstitute](https://nsubstitute.github.io/), not a real database.
- **`AppTemplate.FunctionalTests`** - drives the real HTTP pipeline (FastEndpoints -> Mediator -> EF Core) through `WebApplicationFactory<Program>`, with `AppTemplateWebApplicationFactory` swapping the Postgres-backed `DbContext` for EF Core's InMemory provider. This trades some fidelity (InMemory doesn't enforce the same constraints or SQL translation as Postgres) for running everywhere with no external dependencies.

## Consequences
- `dotnet test` runs the same way locally and in CI on Windows, Linux, and macOS - no Docker/Testcontainers dependency for the default setup.
- Swapping a real, already-registered `AddDbContext` call for InMemory requires stripping every `Microsoft.EntityFrameworkCore`-namespaced service the original registration added (see the comment in `AppTemplateWebApplicationFactory`) - removing just `DbContextOptions<T>` leaves internal per-provider registrations behind and both providers end up registered at once.
- InMemory won't catch issues that only show up against real Postgres (case sensitivity, cascade behavior, raw SQL like `ListUsersQueryService`'s `FromSqlRaw`, concurrency). If that fidelity matters for your project, add an `AppTemplate.IntegrationTests` project against a real Postgres via [Testcontainers](https://dotnet.testcontainers.org/) - expect to run it as a separate, `continue-on-error` CI job (or Linux-only) rather than in the existing matrix, since Testcontainers needs a Docker daemon that isn't reliably available on hosted Windows/macOS runners.
- NSubstitute was chosen over Moq for its simpler syntax and permissive license; either works fine here since `IRepository<T>` and friends are plain interfaces.
