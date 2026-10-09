---
title: "ADR 015: URL-versioned API, mandatory from v1"
weight: 150
---

# ADR 015: URL-versioned API, mandatory from v1

## Status
Accepted. Amends the routes in [ADR 008]({{< relref "adr-008-password-reset-via-keycloak" >}}).

## Context
The API had no version: `GET /users`, `POST /password-reset`. A breaking change to one endpoint would then break every client at once, or force a rename. This template has no external consumers yet, so adding a version now costs one route segment. Adding it after clients exist means a migration. FastEndpoints has built-in versioning, so it needs no extra package.

Versioning is a template-wide rule, not per-feature. Every project created from the template inherits it.

## Decision
**URL path versioning, declared per endpoint.**
- In `UseFastEndpoints`, set `Versioning.Prefix = "v"` and `PrependToRoute = true`.
- Every feature endpoint calls `Version(ApiVersions.V1)` in `Configure()` and is served at `/v1/...`.
- There is **no default version**. An endpoint that forgets `Version()` keeps an unversioned route, and `ApiVersioningTests` fails.
- The OpenAPI document includes endpoints up to `ApiVersions.Latest` (`MaxEndpointVersion`).

**Route topology.**

| Caller | Public route | Reaches | Versioned |
|---|---|---|---|
| Browser → backend for frontend's own session endpoints | `/backend-for-frontend/{login,register,user,logout,providers}` | the backend for frontend | No. This is the session protocol, not the API. |
| Browser → API, through the backend for frontend | `/api/v1/**` | Web `/v1/**` (YARP strips `/api`) | Yes |
| Anonymous password reset | `POST /api/v1/password-reset` (anonymous route; `X-CSRF` still required) | Web `POST /v1/password-reset` | Yes |
| Operations | `/health`, `/alive`, `/hangfire`, `/openapi/*`, `/swagger`, `/scalar` | Web | No |

The OpenAPI document describes only the Web `/v1/**` contract. It contains no backend-for-frontend or operational routes, and `ApiVersioningTests` compares it with the registered endpoints. In the Angular app, feature services build URLs from `API_V1` (`core/api/api-paths.ts`).

**Evolving an endpoint.**
- A **non-breaking** change (a new optional field, a new endpoint) stays in v1.
- A **breaking** change (a removed or renamed field, a changed meaning or status code) gets a new endpoint class that calls `Version(2)`, next to the v1 class. Both run side by side, and v1 stays as it is.
- Bump `ApiVersions.Latest` and add a document for v2 (`SwaggerDocument` with `MinEndpointVersion = 2`). FastEndpoints computes release groups from the endpoint versions that exist in code. They are **not frozen snapshots**: v1's document is "every endpoint at version 1", so a deleted v1 class disappears from it. Keep the v1 class until v1 is retired.
- **Support window:** a superseded version stays available for at least one release after its replacement ships. Mark the old endpoints deprecated with FastEndpoints' `Version(1).DeprecateAt(2)`, which drops them from the v2 document, and note the retirement in the CHANGELOG.

## Consequences
- **Breaking:** every route moved under `/v1`, so clients, `api.http` and tests changed with it.
- A new endpoint needs one more line, which a test enforces.
- URL versioning is visible in logs, caches and the browser, and works through YARP with no header plumbing. It is less purist than media-type versioning, but much simpler for an Angular client and generated types.
