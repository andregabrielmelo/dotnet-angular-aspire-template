---
title: "ADR 009: Third-party sign-in brokered by Keycloak"
weight: 90
---

# ADR 009: Third-party sign-in brokered by Keycloak

## Status
Accepted

## Context
Users want to sign in with accounts they already have (Google, GitHub, Microsoft). Each provider speaks OAuth 2.0 / OpenID Connect a little differently and has its own app registration and secrets. We don't want the application, whether the SPA, the backend for frontend or the API, to integrate with each one directly, deal with their tokens, or give each provider's users a different kind of identity.

## Decision
- **Keycloak brokers every third-party provider.** The realm file declares `google`, `github` and `microsoft` identity providers. Their `enabled` flag, client id and client secret come from `${KC_IDP_<ALIAS>_*}` placeholders, which Keycloak resolves from environment variables when it imports the realm.
- **The AppHost switches a provider on only when it is configured:**
  - Add `Parameters:<alias>-client-id` and `Parameters:<alias>-client-secret` to the AppHost's user secrets. The secret is passed as a secret Aspire parameter.
  - Unconfigured providers stay disabled.
  - The backend for frontend is told which providers are enabled (`ExternalIdentityProviders:<alias>:Enabled`).
- **The SPA asks, the backend for frontend decides:**
  - `GET /backend-for-frontend/providers` lists the enabled providers for the "Continue with …" buttons.
  - `GET /backend-for-frontend/login?provider=<alias>` accepts only an enabled alias and sends Keycloak `kc_idp_hint=<alias>`, so the browser goes straight to that provider.
  - The rest of the flow is unchanged: code flow + PKCE to Keycloak, and the same session cookie.
- **The app only ever sees Keycloak identities.** Whoever the upstream provider is, Keycloak issues the tokens, so the Web API, just-in-time provisioning (`sub` → `User.ExternalId`) and authorization work the same. Provider tokens are not stored (`storeToken: false`).
- **Account linking is Keycloak's "first broker login" flow.** `trustEmail` is `false`, so an upstream email that matches an existing account is linked only after the user proves they own that account (re-authentication or an email). A provider can't silently take over an account.

## Consequences
- Adding another provider means one realm entry, one alias in `AppHost/ExternalIdentityProviders.cs`, and one entry in the backend for frontend's `appsettings.json`. No new application code.
- Each provider's OAuth app must allow the redirect URI `http://localhost:8080/realms/apptemplate/broker/<alias>/endpoint` in development (the realm's public URL in other environments).
- The realm, including the providers' enabled flags and credentials, is imported only while Keycloak's data volume is empty. After configuring a provider, delete the volume or set the provider up in the admin console.
- Outside development, set `Keycloak:Authority` on both the backend for frontend and the Web API to the realm's public HTTPS URL. The Aspire service-discovery address is for local use only, and OpenID Connect requires an HTTPS authority.
