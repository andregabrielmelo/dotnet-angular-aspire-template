---
title: "ADR 003: FastEndpoints + Mediator over Controllers + MediatR"
weight: 30
---

# ADR 003: FastEndpoints + Mediator instead of MVC Controllers + MediatR

## Status
Accepted

## Context
The Web layer needs a way to expose HTTP endpoints, and UseCases needs a way to dispatch CQRS commands/queries to their handlers. The conventional choices are ASP.NET Core MVC Controllers and MediatR.

## Decision
Use [FastEndpoints](https://fast-endpoints.com/) for HTTP endpoints (one class per endpoint, following the REPR pattern: Request-Endpoint-Response) with built-in FluentValidation support, and [Mediator](https://github.com/martinothamar/Mediator) (not MediatR) for in-process CQRS dispatch, using its source-generated `Mediator.SourceGenerator` package.

## Consequences
- One file per endpoint keeps request/response types and handling logic colocated and easy to find, instead of several actions sharing a controller class.
- FastEndpoints' built-in FluentValidation integration means request validation doesn't need a separate MVC filter pipeline.
- Mediator's source-generated dispatch avoids the reflection-based registration and runtime overhead of MediatR, at the cost of a build-time source generator step (`Mediator.SourceGenerator`, referenced as a private analyzer asset in `Web.csproj`).
- Both libraries are smaller and less ubiquitous than Controllers + MediatR, so the learning curve is slightly steeper for contributors coming from a typical ASP.NET Core background.
- Validation still happens in two places by design (FastEndpoints/FluentValidation at the HTTP boundary, domain value objects in Core) - see [Goals & Design Decisions]({{< relref "../design-decisions" >}}#where-to-validate).
