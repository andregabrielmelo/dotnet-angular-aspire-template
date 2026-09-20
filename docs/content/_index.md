---
title: "AppTemplate docs"
type: "docs"
---

# C# + Angular + Aspire Template

A GitHub template repository for starting new full-stack projects with a Clean Architecture .NET backend, an Angular frontend, and .NET Aspire for local orchestration.

It's not a sample application - it's a skeleton with just enough real code (a `User` feature, wired end-to-end) to show you where things belong. Delete or replace it once you understand the shape of the project.

## Stack

- **.NET 10** backend using **Clean Architecture** (Core / UseCases / Infrastructure / Web)
- **FastEndpoints** + the **REPR pattern** for API endpoints
- **Mediator** for CQRS-style command/query handling
- **EF Core** + **PostgreSQL** for persistence
- **Angular** frontend, hosted and proxied through Aspire
- **.NET Aspire** to orchestrate the API, database, and frontend together in local dev

## Getting Started

Use the **Use this template** button on GitHub, then head to [Getting Started]({{< relref "getting-started" >}}) to rename the solution and run it locally.

See [Goals & Design Decisions]({{< relref "design-decisions" >}}) for why the stack is put together the way it is, [Architecture Decisions]({{< relref "architecture-decisions" >}}) for a record of the specific calls made along the way, [Best Practices]({{< relref "best-practices" >}}) for explicit conventions to follow, and [API Reference]({{< relref "api-reference" >}}) for how the OpenAPI docs work.
