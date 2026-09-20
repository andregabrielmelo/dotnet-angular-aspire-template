---
title: "ADR 002: .NET Aspire for local orchestration"
weight: 20
---

# ADR 002: .NET Aspire for local orchestration

## Status
Accepted

## Context
Running this project locally means starting three things together: a PostgreSQL instance, the Web API, and the Angular dev server, with the frontend pointed at the API's actual (dynamic) port and the API pointed at the database's connection string. `docker-compose` can run the containers, but it doesn't manage the .NET or npm processes, doesn't wire up service discovery/connection strings automatically, and doesn't give a unified view of logs/traces across all three.

## Decision
Use .NET Aspire's `AppHost` project as the single local entry point:

- `AddPostgres(...).WithDataVolume().WithLifetime(ContainerLifetime.Persistent)` - a containerized, persistent-by-default Postgres instance.
- `AddProject<Projects.AppTemplate_Web>(...)` - the API, wired to the database via `WithReference` + `WaitFor` so it doesn't start before Postgres is ready.
- `AddJavaScriptApp(...).WithNpm(...)` (Aspire's JavaScript app hosting) - runs `npm ci` and `npm start` for the Angular frontend, wired to the API the same way, with `PublishAsDockerFile()` available for containerized deployment later.

Running `dotnet run` on the AppHost project starts all three and opens the Aspire dashboard for logs, traces, and endpoint URLs.

## Consequences
- One command starts the whole stack in a consistent, reproducible order - no manually starting `npm start` in a second terminal.
- Connection strings and service URLs are wired by Aspire, not hardcoded in `appsettings.json`, so they stay correct even when ports change between runs.
- Adds a dependency on the .NET Aspire workload/CLI for local development, and on Docker for the Postgres container.
- Aspire's AppHost is a local/dev orchestration tool, not a production deployment mechanism - production deployment of the API and the built Angular app is a separate concern (the frontend resource's `PublishAsDockerFile()` is a starting point, not a full deployment pipeline).
