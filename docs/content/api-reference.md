---
title: "API Reference"
weight: 40
---

# API Reference

The API is documented as an [OpenAPI](https://spec.openapis.org/oas/) document, generated automatically from the FastEndpoints route/DTO/validator metadata - not hand-written. There's nothing to keep in sync manually; add or change an endpoint and the document updates itself.

## Browsing it

All three are wired up in `MiddlewareConfigurations.UseAppMiddleware` and only mapped in the `Development` environment:

- **[Scalar](https://scalar.com/)** at `/scalar` - the recommended interactive reference (try-it-out, code samples).
- **Swagger UI** at `/swagger` - the more traditional alternative, if your workflow already expects it.
- **Raw document** at `/openapi/v1.json` - what both UIs above render; also what you'd feed to a client code generator (e.g. `openapi-generator`, NSwag's own client generator, or Orval for the Angular side) or import into Postman/Insomnia.

## OpenAPI version

Generation goes through [FastEndpoints.Swagger](https://fast-endpoints.com/docs/swagger-support), which is built on [NSwag](https://github.com/RicoSuter/NSwag) - it targets **OpenAPI 3.0.x**. The [OpenAPI Specification](https://spec.openapis.org/oas/) has since moved to 3.1 and [3.2](https://swagger.io/specification/v3.2/), which add things like JSON Schema 2020-12 alignment and webhooks; NSwag doesn't generate those newer versions yet. If you need 3.1+ output specifically, the alternative is .NET's own `Microsoft.AspNetCore.OpenApi` (via `AddOpenApi()`/`MapOpenApi()`), which does target 3.1 - that's a bigger change (it doesn't understand FastEndpoints metadata the same way NSwag's ASP.NET Core integration does) and isn't something this template does by default.

## Document metadata

Title/version/description for the generated document are set in `Program.cs`:

```csharp
.SwaggerDocument(o =>
{
    o.ShortSchemaNames = true;
    o.DocumentSettings = s =>
    {
        s.Title = "AppTemplate API";
        s.Version = "v1";
        s.Description = "REST API for AppTemplate.";
    };
});
```

Update these (via `scripts/rename-template.sh` for the title, directly for the description) as the API grows beyond the `User` reference feature.

## API conventions

- **REPR pattern** - one FastEndpoints class per endpoint (`Web/Features/<Feature>Features/`), each with its own `Validator<T>` in the same file. See [ADR 003]({{< relref "architecture-decisions/adr-003-fastendpoints-mediator" >}}).
- **Errors** - handlers return `Ardalis.Result`; `Web/Extensions/ResultExtensions.cs` maps `Result` statuses (`NotFound`, `Invalid`, etc.) to the matching HTTP status and a [`ProblemDetails`](https://www.rfc-editor.org/rfc/rfc7807) response - not ad-hoc error shapes per endpoint.
- **Versioning** - FastEndpoints supports per-endpoint `Version(n)` and multiple named Swagger documents (`MinEndpointVersion`/`MaxEndpointVersion` in `DocumentOptions`) if you need it later. Not set up yet - there's only ever been a `v1`.
