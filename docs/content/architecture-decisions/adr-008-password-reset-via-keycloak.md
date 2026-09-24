---
title: "ADR 008: Password reset through Keycloak's Admin API"
weight: 80
---

# ADR 008: Password reset through Keycloak's Admin API

## Status
Accepted

## Context
Users who forget their password need to reset it. Keycloak owns the credentials ([ADR 007]({{< relref "adr-007-authentication-backend-for-frontend-keycloak" >}})), so the application must never see, store or set a password itself. The reset has to start from the app's own signed-out page, work without revealing which emails have accounts, and be hard to abuse for flooding someone's inbox.

## Decision
- **`POST /password-reset`** on the Web API is anonymous and takes `{ email }`. It always answers **202** for a well-formed email, whether or not an account exists, and is throttled per client IP (5 requests per minute, FastEndpoints `Throttle`).
- The `ForgotPasswordHandler` use case calls `IPasswordResetService`, which is defined in UseCases. The handler maps "no such account" to success.
- **`KeycloakPasswordResetService`** (Infrastructure) calls Keycloak's Admin REST API:
  - It looks the user up by exact email.
  - It calls `execute-actions-email` with `UPDATE_PASSWORD`. Keycloak then emails a time-limited link (30 minutes by default) to its own hosted "set a new password" page, and afterwards redirects to `https://localhost:7100/auth`.
  - It uses a typed `HttpClient`. The `apptemplate-user-admin` service account (realm-management `view-users` + `manage-users`) authenticates with the client credentials grant, and Duende.AccessTokenManagement caches and renews that token.
  - `KeycloakAdminOptions` is bound from `Keycloak:Admin` and checked at startup by a source-generated `[OptionsValidator]`.
- The backend for frontend proxies only `POST /api/password-reset` without a session or access token. The `X-CSRF` header is still required.
- **Mailpit** runs in the AppHost as the development SMTP server that Keycloak sends through. Read the emails in its web UI.
- Keycloak's own login page also shows "Forgot password?" (`resetPasswordAllowed`). That flow uses the same SMTP settings and works alongside the app's page.

## Consequences
- The app never handles a password, and a reset link can't be forged by the app either: Keycloak generates, signs and verifies it.
- A new secret, `keycloak-user-admin-secret`, is needed. A dev default is committed; override it outside local development.
- The realm import only runs when Keycloak's data volume is empty. An existing dev volume created before this change lacks the service account and SMTP settings. Delete the `keycloak` data volume (or add them in the admin console) to pick them up.
- Throttling is per IP as seen in `X-Forwarded-For`, which the backend for frontend sets. The Web API must not be exposed directly, or clients could spoof that header.
- Mailpit is for development only. Configure a real SMTP server in the realm for other environments.
