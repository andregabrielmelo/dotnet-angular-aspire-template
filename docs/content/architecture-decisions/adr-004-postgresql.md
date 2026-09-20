---
title: "ADR 004: PostgreSQL as the database"
weight: 40
---

# ADR 004: PostgreSQL as the database

## Status
Accepted

## Context
The template needs a default relational database. Common options in the .NET ecosystem are SQL Server, SQLite, and PostgreSQL.

## Decision
Use PostgreSQL via `Npgsql.EntityFrameworkCore.PostgreSQL`, with `EFCore.NamingConventions` set to snake_case so the database follows Postgres conventions (`users`, `phone_number_number`, ...) rather than C#'s PascalCase. Aspire provisions it as a container with a persistent data volume for local development (see [ADR 002]({{< relref "adr-002-aspire-orchestration" >}})).

## Consequences
- Free, open-source, and runs identically in a local container, CI, and most managed cloud database offerings - no licensing concerns and no Windows-only tooling dependency.
- Aspire's `AddPostgres` integration is a first-class, well-supported resource, keeping local orchestration simple.
- snake_case naming means raw SQL (see `ListUsersQueryService`, which uses `FromSqlRaw`) must use snake_case column names, not the C# property names - a source of friction if you're used to SQL Server's PascalCase defaults.
- Switching to SQL Server or another provider later means replacing the Npgsql package/`UseNpgsql` call in `AppTemplate.Infrastructure` and regenerating migrations - it's isolated to the Infrastructure layer by design.
